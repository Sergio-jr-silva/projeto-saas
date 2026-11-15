using System.Diagnostics;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Nexo.Agent.Core.Connectors;
using Nexo.Agent.Core.Queue;
using Nexo.Agent.Core.State;

namespace Nexo.Agent.Core.Sync;

public class SyncStateFile
{
    public DateTime? UltimoEstoque { get; set; }
    public DateTime? UltimosProdutos { get; set; }
    public DateTime? UltimosFornecedores { get; set; }
    public DateTime? UltimosPrecos { get; set; }
    public Dictionary<string, string> HashesEstoque { get; set; } = new();
    public Dictionary<string, string> HashesProdutos { get; set; } = new();
    public Dictionary<string, string> HashesFornecedores { get; set; } = new();
}

public record SyncStepResult(string Etapa, bool Sucesso, int Itens, string? Erro = null);
public record SyncCycleResult(bool Sucesso, bool Parcial, string Mensagem, List<SyncStepResult> Etapas);

public class IncrementalSyncEngine
{
    private readonly IDatabaseConnector _connector;
    private readonly HttpClient _http;
    private readonly PersistentTaskQueue _queue;
    private readonly AgentStateManager _state;
    private readonly ILogger<IncrementalSyncEngine>? _logger;
    private readonly string _syncStatePath;
    private readonly Guid _tenantId;
    private readonly Guid? _filialId;
    private readonly Guid _agentId;

    public TimeSpan IntervaloEstoque { get; set; } = TimeSpan.FromMinutes(5);
    public TimeSpan IntervaloProdutos { get; set; } = TimeSpan.FromMinutes(30);
    public TimeSpan IntervaloFornecedores { get; set; } = TimeSpan.FromMinutes(30);
    public TimeSpan IntervaloPrecos { get; set; } = TimeSpan.FromMinutes(15);
    public TimeSpan IntervaloHeartbeat { get; set; } = TimeSpan.FromMinutes(1);

    private SyncStateFile _syncState = new();
    private readonly object _stateLock = new();

    public event Action<string, string>? OnLog; // level, message

    public IncrementalSyncEngine(
        IDatabaseConnector connector,
        IConfiguration config,
        PersistentTaskQueue queue,
        AgentStateManager state,
        ILogger<IncrementalSyncEngine>? logger = null)
    {
        _connector = connector;
        _queue = queue;
        _state = state;
        _logger = logger;

        var baseUrl = config["NexoApi:BaseUrl"] ?? "http://localhost:5000";
        var credsManager = new Security.AgentCredentialsManager();
        var stored = credsManager.LoadCredentials();
        var agentKey = stored != null && !string.IsNullOrWhiteSpace(stored.AgentCredential)
            ? stored.AgentCredential
            : (config["NexoApi:AgentKey"] ?? "agt_demo_mercadinho_central_123456");

        if (stored != null)
        {
            _tenantId = stored.TenantId;
            _filialId = stored.FilialId;
            _agentId = stored.AgentId;

            _state.UpdateEmpresa(
                stored.TenantId.ToString(),
                stored.RazaoSocial,
                stored.NomeFantasia,
                stored.Cnpj,
                "Ativo"
            );
        }

        var timeout = int.TryParse(config["NexoApi:TimeoutSeconds"], out var t) ? t : 20;

        _http = new HttpClient { BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/") };
        _http.DefaultRequestHeaders.Add("X-Agent-Key", agentKey);
        _http.Timeout = TimeSpan.FromSeconds(timeout);

        var dataDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data");
        if (!Directory.Exists(dataDir)) Directory.CreateDirectory(dataDir);
        var tenantSuffix = (_tenantId != Guid.Empty) ? $"_{_tenantId:N}" : "";
        _syncStatePath = Path.Combine(dataDir, $"sync_state{tenantSuffix}.json");

        if (_tenantId != Guid.Empty)
        {
            var purged = _queue.PurgeOrArchiveForeignItems(_tenantId, _agentId);
            if (purged > 0)
            {
                Log("WARN", $"Isolamento Multi-Empresa: {purged} tarefas residuais de outra empresa foram removidas da fila offline.");
            }
        }

        LoadSyncState();
    }

    private void Log(string level, string msg)
    {
        _logger?.LogInformation("[{Level}] {Msg}", level, msg);
        _state.AddLog(level, msg);
        OnLog?.Invoke(level, msg);
    }

    public async Task<bool> TestarConexaoBancoAsync(CancellationToken ct = default)
    {
        try
        {
            var ok = await _connector.TestConnectionAsync();
            _state.UpdateBanco(
                tipo: _connector.ConnectorName,
                host: "Configurado",
                porta: 0,
                caminhoOuNome: "Banco ERP",
                status: ok ? "Conectado" : "Falha na conexão"
            );
            Log(ok ? "SUCCESS" : "WARN", $"Teste de conexão com banco de dados ERP ({_connector.ConnectorName}): {(ok ? "Sucesso" : "Falhou")}");
            return ok;
        }
        catch (Exception ex)
        {
            _state.UpdateBanco(
                tipo: _connector.ConnectorName,
                host: "Configurado",
                porta: 0,
                caminhoOuNome: "Banco ERP",
                status: "Erro: " + ex.Message
            );
            Log("ERROR", $"Erro ao testar conexão com banco: {ex.Message}");
            return false;
        }
    }

    public Task<SyncStepResult> SincronizarEstoqueAsync(CancellationToken ct = default) => SincronizarEstoqueIncrementalAsync(ct);
    public Task<SyncStepResult> SincronizarProdutosAsync(CancellationToken ct = default) => SincronizarProdutosIncrementalAsync(ct);
    public Task<SyncStepResult> SincronizarFornecedoresAsync(CancellationToken ct = default) => SincronizarFornecedoresIncrementalAsync(ct);

    public async Task<SyncCycleResult> SincronizarTudoAsync(CancellationToken ct = default)
    {
        Log("INFO", "Verificando estrutura do banco ERP...");
        try
        {
            var mapping = await _connector.DiscoverAndApplySchemaAsync(false, ct);
            if (mapping.Produtos.Valido) Log("SUCCESS", $"Mapeamento de produtos concluído: tabela '{mapping.Produtos.TableName}'.");
            if (mapping.Fornecedores.Valido) Log("SUCCESS", $"Mapeamento de fornecedores concluído: tabela '{mapping.Fornecedores.TableName}'.");
            if (mapping.Estoque.Valido)
            {
                var tabEst = mapping.Estoque.HasDedicatedTable ? mapping.Estoque.TableName : mapping.Produtos.TableName;
                Log("SUCCESS", $"Mapeamento de estoque concluído: tabela '{tabEst}' (campo: '{mapping.Estoque.EstoqueAtualCol}').");
            }
            else
            {
                Log("WARN", $"Estrutura de estoque pendente de validação: {mapping.Estoque.MotivoInvalido}");
            }
        }
        catch (Exception ex)
        {
            Log("WARN", $"Aviso ao verificar estrutura do banco: {ex.Message}");
        }

        var rProd = await SincronizarProdutosIncrementalAsync(ct);
        var rForn = await SincronizarFornecedoresIncrementalAsync(ct);
        var rEst = await SincronizarEstoqueIncrementalAsync(ct);

        var etapas = new List<SyncStepResult> { rProd, rForn, rEst };
        var falhas = etapas.Where(e => !e.Sucesso).ToList();

        if (falhas.Count == 0)
        {
            Log("SUCCESS", "Ciclo de sincronização concluído com sucesso.");
            return new SyncCycleResult(true, false, "Ciclo de sincronização concluído com sucesso.", etapas);
        }
        else if (falhas.Count < etapas.Count)
        {
            var detalhesFalhas = string.Join(" | ", falhas.Select(f => $"{f.Etapa}: {f.Erro}"));
            Log("WARN", $"Ciclo de sincronização concluído parcialmente. Falha na etapa: {detalhesFalhas}");
            return new SyncCycleResult(false, true, $"Sucesso parcial. Falhas: {detalhesFalhas}", etapas);
        }
        else
        {
            Log("ERROR", "Falha geral no ciclo de sincronização: todas as etapas falharam.");
            return new SyncCycleResult(false, false, "Todas as etapas falharam.", etapas);
        }
    }

    public async Task<int> DrenarFilaOfflineAsync(CancellationToken ct = default)
    {
        var inicial = _queue.GetPendingCount();
        await DescarregarFilaOfflineAsync(ct);
        var final = _queue.GetPendingCount();
        return Math.Max(0, inicial - final);
    }

    public async Task<bool> ProcessarCicloHeartbeatAsync(CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var body = new
            {
                Versao = "2.4.0",
                NomeMaquina = Environment.MachineName,
                Ambiente = new
                {
                    OS = Environment.OSVersion.ToString(),
                    Processadores = Environment.ProcessorCount,
                    MemoriaGb = Math.Round(GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / 1024.0 / 1024.0 / 1024.0, 1),
                    DataHora = DateTime.Now.ToString("s")
                }
            };

            var res = await _http.PostAsJsonAsync("api/agent/v1/heartbeat", body, ct);
            sw.Stop();
            _state.UpdateLatencia(sw.ElapsedMilliseconds);

            if (res.IsSuccessStatusCode)
            {
                _state.UpdateStatus("Online");

                var cfg = await res.Content.ReadFromJsonAsync<AgentConfigResponse>(cancellationToken: ct);
                if (cfg != null)
                {
                    if (cfg.IntervaloEstoqueMin > 0) IntervaloEstoque = TimeSpan.FromMinutes(cfg.IntervaloEstoqueMin);
                    if (cfg.IntervaloProdutosMin > 0) IntervaloProdutos = TimeSpan.FromMinutes(cfg.IntervaloProdutosMin);
                    if (cfg.IntervaloFornecedoresMin > 0) IntervaloFornecedores = TimeSpan.FromMinutes(cfg.IntervaloFornecedoresMin);
                    if (cfg.IntervaloHeartbeatSeg > 0) IntervaloHeartbeat = TimeSpan.FromSeconds(cfg.IntervaloHeartbeatSeg);

                    if (!string.IsNullOrEmpty(cfg.TenantRazaoSocial))
                    {
                        _state.UpdateEmpresa(
                            tenantId: "",
                            razaoSocial: cfg.TenantRazaoSocial,
                            nomeFantasia: cfg.TenantNomeFantasia ?? cfg.TenantRazaoSocial,
                            cnpj: cfg.TenantCnpj ?? "",
                            plano: "Ativo"
                        );
                    }
                }

                // Se houver itens na fila offline pendente, tenta descarregar agora que estamos online
                await DescarregarFilaOfflineAsync(ct);
                return true;
            }

            _state.UpdateStatus("Aviso");
            Log("WARN", $"Heartbeat respondeu status HTTP {(int)res.StatusCode} ({res.StatusCode}).");
            return false;
        }
        catch (Exception ex)
        {
            _state.UpdateStatus("Offline");
            Log("WARN", $"Internet ou API SaaS indisponível ({ex.Message}). Operando em modo offline.");
            return false;
        }
    }

    public async Task<SyncStepResult> SincronizarEstoqueIncrementalAsync(CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            Log("INFO", "Iniciando verificação de estoque...");

            if (_connector.CurrentMapping != null && !_connector.CurrentMapping.Estoque.Valido)
            {
                Log("ERROR", "Falha na identificação do campo de quantidade.");
                Log("INFO", "Entidade: Estoque.");
                Log("INFO", $"Banco: {_connector.DatabaseType}.");
                Log("INFO", $"Tabela analisada: {_connector.CurrentMapping.Estoque.TableName}.");
                Log("INFO", "Campo esperado: quantidade_estoque.");
                Log("ERROR", $"Nenhum campo compatível foi identificado: {_connector.CurrentMapping.Estoque.MotivoInvalido}");

                _state.RegisterSyncDone("Estoque", 0, sw.ElapsedMilliseconds, "Falha", _connector.CurrentMapping.Estoque.MotivoInvalido ?? "Campo não identificado", DateTime.UtcNow.Add(IntervaloEstoque));
                return new SyncStepResult("Estoque", false, 0, _connector.CurrentMapping.Estoque.MotivoInvalido);
            }

            var estoques = (await _connector.GetStockAsync(ct)).ToList();
            if (estoques.Count == 0)
            {
                Log("INFO", "Nenhum registro de estoque retornado pelo banco ERP.");
                _state.RegisterSyncDone("Estoque", 0, sw.ElapsedMilliseconds, "Sucesso", "0 registros", DateTime.UtcNow.Add(IntervaloEstoque));
                return new SyncStepResult("Estoque", true, 0);
            }

            // Filtra somente registros alterados usando checksum
            var alterados = new List<ErpEstoqueDto>();
            lock (_stateLock)
            {
                foreach (var item in estoques)
                {
                    var hash = ComputeEstoqueHash(item);
                    if (!_syncState.HashesEstoque.TryGetValue(item.CodigoErp, out var oldHash) || oldHash != hash)
                    {
                        alterados.Add(item);
                        _syncState.HashesEstoque[item.CodigoErp] = hash;
                    }
                }
            }

            if (alterados.Count == 0)
            {
                Log("INFO", "Estoque sem alterações detectadas desde a última sincronização.");
                _state.RegisterSyncDone("Estoque", 0, sw.ElapsedMilliseconds, "Sucesso", "Nenhuma alteração detectada", DateTime.UtcNow.Add(IntervaloEstoque));
                return new SyncStepResult("Estoque", true, 0);
            }

            Log("INFO", $"{alterados.Count} posições de estoque alteradas detectadas. Enviando...");
            const int chunkSizeEstoque = 1000;
            var totalEnviadosEstoque = 0;
            var sucessoGeralEstoque = true;

            for (int i = 0; i < alterados.Count; i += chunkSizeEstoque)
            {
                var chunk = alterados.Skip(i).Take(chunkSizeEstoque).ToList();
                var batch = new SyncBatch<ErpEstoqueDto>(Guid.NewGuid(), DateTime.UtcNow, chunk, IsDelta: true, TenantId: _tenantId, BranchId: _filialId);
                var ok = await TentarEnviarAoSaasAsync("api/agent/v1/sync/estoque", "estoque", batch, ct);
                if (ok)
                {
                    totalEnviadosEstoque += chunk.Count;
                }
                else
                {
                    sucessoGeralEstoque = false;
                    _queue.Enqueue("estoque", batch, tenantId: _tenantId, branchId: _filialId, agentId: _agentId);
                }
            }
            sw.Stop();

            if (sucessoGeralEstoque)
            {
                lock (_stateLock)
                {
                    _syncState.UltimoEstoque = DateTime.UtcNow;
                    SaveSyncState();
                }
                Log("SUCCESS", $"{alterados.Count} posições de estoque sincronizadas com o Nexo SaaS.");
                _state.RegisterSyncDone("Estoque", alterados.Count, sw.ElapsedMilliseconds, "Sucesso", $"{alterados.Count} posições atualizadas", DateTime.UtcNow.Add(IntervaloEstoque));
                return new SyncStepResult("Estoque", true, alterados.Count);
            }
            else
            {
                _state.UpdateFilaOffline(_queue.GetPendingCount());
                Log("WARN", $"Envio parcial de estoque: {totalEnviadosEstoque} enviados, restante enfileirado para envio offline.");
                _state.RegisterSyncDone("Estoque", totalEnviadosEstoque, sw.ElapsedMilliseconds, "Offline (Enfileirado)", "Salvo na fila local", DateTime.UtcNow.Add(IntervaloEstoque));
                return new SyncStepResult("Estoque", totalEnviadosEstoque > 0, totalEnviadosEstoque, "Envio parcial para o SaaS");
            }
        }
        catch (Exception ex)
        {
            Log("ERROR", "Erro no ciclo de estoque: " + ex.Message);
            _state.RegisterSyncDone("Estoque", 0, sw.ElapsedMilliseconds, "Falha", ex.Message, DateTime.UtcNow.Add(IntervaloEstoque));
            return new SyncStepResult("Estoque", false, 0, ex.Message);
        }
    }

    public async Task<SyncStepResult> SincronizarProdutosIncrementalAsync(CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            Log("INFO", "Iniciando leitura de produtos do ERP...");
            var produtos = (await _connector.GetProductsAsync(_syncState.UltimosProdutos, ct)).ToList();
            if (produtos.Count == 0)
            {
                Log("INFO", "Nenhum produto novo ou alterado no ERP.");
                _state.RegisterSyncDone("Produtos", 0, sw.ElapsedMilliseconds, "Sucesso", "0 produtos", DateTime.UtcNow.Add(IntervaloProdutos));
                return new SyncStepResult("Produtos", true, 0);
            }

            var alterados = new List<ErpProdutoDto>();
            lock (_stateLock)
            {
                foreach (var p in produtos)
                {
                    var hash = ComputeProdutoHash(p);
                    if (!_syncState.HashesProdutos.TryGetValue(p.CodigoErp, out var oldHash) || oldHash != hash)
                    {
                        alterados.Add(p);
                        _syncState.HashesProdutos[p.CodigoErp] = hash;
                    }
                }
            }

            if (alterados.Count == 0)
            {
                Log("INFO", "Catálogo de produtos sem alterações.");
                _state.RegisterSyncDone("Produtos", 0, sw.ElapsedMilliseconds, "Sucesso", "Nenhuma alteração detectada", DateTime.UtcNow.Add(IntervaloProdutos));
                return new SyncStepResult("Produtos", true, 0);
            }

            Log("INFO", $"{alterados.Count} produtos novos ou alterados detectados. Enviando...");
            const int chunkSizeProdutos = 1000;
            var totalEnviadosProdutos = 0;
            var sucessoGeralProdutos = true;

            for (int i = 0; i < alterados.Count; i += chunkSizeProdutos)
            {
                var chunk = alterados.Skip(i).Take(chunkSizeProdutos).ToList();
                var batch = new SyncBatch<ErpProdutoDto>(Guid.NewGuid(), DateTime.UtcNow, chunk, IsDelta: true, TenantId: _tenantId, BranchId: _filialId);
                var ok = await TentarEnviarAoSaasAsync("api/agent/v1/sync/produtos", "produtos", batch, ct);
                if (ok)
                {
                    totalEnviadosProdutos += chunk.Count;
                }
                else
                {
                    sucessoGeralProdutos = false;
                    _queue.Enqueue("produtos", batch, tenantId: _tenantId, branchId: _filialId, agentId: _agentId);
                }
            }
            sw.Stop();

            if (sucessoGeralProdutos)
            {
                lock (_stateLock)
                {
                    _syncState.UltimosProdutos = DateTime.UtcNow;
                    SaveSyncState();
                }
                Log("SUCCESS", $"{alterados.Count} produtos sincronizados com o Nexo SaaS.");
                _state.RegisterSyncDone("Produtos", alterados.Count, sw.ElapsedMilliseconds, "Sucesso", $"{alterados.Count} produtos sincronizados", DateTime.UtcNow.Add(IntervaloProdutos));
                return new SyncStepResult("Produtos", true, alterados.Count);
            }
            else
            {
                _state.UpdateFilaOffline(_queue.GetPendingCount());
                Log("WARN", $"Envio parcial de produtos: {totalEnviadosProdutos} enviados, restante enfileirado para envio offline.");
                _state.RegisterSyncDone("Produtos", totalEnviadosProdutos, sw.ElapsedMilliseconds, "Offline (Enfileirado)", "Salvo na fila local", DateTime.UtcNow.Add(IntervaloProdutos));
                return new SyncStepResult("Produtos", totalEnviadosProdutos > 0, totalEnviadosProdutos, "Envio parcial para o SaaS");
            }
        }
        catch (Exception ex)
        {
            Log("ERROR", "Erro no ciclo de produtos: " + ex.Message);
            _state.RegisterSyncDone("Produtos", 0, sw.ElapsedMilliseconds, "Falha", ex.Message, DateTime.UtcNow.Add(IntervaloProdutos));
            return new SyncStepResult("Produtos", false, 0, ex.Message);
        }
    }

    public async Task<SyncStepResult> SincronizarFornecedoresIncrementalAsync(CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            Log("INFO", "Iniciando leitura de fornecedores do ERP...");
            var fornecedores = (await _connector.GetSuppliersAsync(_syncState.UltimosFornecedores, ct)).ToList();
            if (fornecedores.Count == 0)
            {
                Log("INFO", "Nenhum fornecedor novo ou alterado no ERP.");
                _state.RegisterSyncDone("Fornecedores", 0, sw.ElapsedMilliseconds, "Sucesso", "0 fornecedores", DateTime.UtcNow.Add(IntervaloFornecedores));
                return new SyncStepResult("Fornecedores", true, 0);
            }

            var alterados = new List<ErpFornecedorDto>();
            lock (_stateLock)
            {
                foreach (var f in fornecedores)
                {
                    var hash = ComputeFornecedorHash(f);
                    if (!_syncState.HashesFornecedores.TryGetValue(f.CodigoErp, out var oldHash) || oldHash != hash)
                    {
                        alterados.Add(f);
                        _syncState.HashesFornecedores[f.CodigoErp] = hash;
                    }
                }
            }

            if (alterados.Count == 0)
            {
                Log("INFO", "Cadastro de fornecedores sem alterações.");
                _state.RegisterSyncDone("Fornecedores", 0, sw.ElapsedMilliseconds, "Sucesso", "Nenhuma alteração detectada", DateTime.UtcNow.Add(IntervaloFornecedores));
                return new SyncStepResult("Fornecedores", true, 0);
            }

            Log("INFO", $"{alterados.Count} fornecedores novos ou alterados detectados. Enviando...");
            var batch = new SyncBatch<ErpFornecedorDto>(Guid.NewGuid(), DateTime.UtcNow, alterados, IsDelta: true, TenantId: _tenantId, BranchId: _filialId);

            var sucesso = await TentarEnviarAoSaasAsync("api/agent/v1/sync/fornecedores", "fornecedores", batch, ct);
            sw.Stop();

            if (sucesso)
            {
                lock (_stateLock)
                {
                    _syncState.UltimosFornecedores = DateTime.UtcNow;
                    SaveSyncState();
                }
                Log("SUCCESS", $"{alterados.Count} fornecedores sincronizados com o Nexo SaaS.");
                _state.RegisterSyncDone("Fornecedores", alterados.Count, sw.ElapsedMilliseconds, "Sucesso", $"{alterados.Count} fornecedores sincronizados", DateTime.UtcNow.Add(IntervaloFornecedores));
                return new SyncStepResult("Fornecedores", true, alterados.Count);
            }
            else
            {
                _queue.Enqueue("fornecedores", batch, tenantId: _tenantId, branchId: _filialId, agentId: _agentId);
                _state.UpdateFilaOffline(_queue.GetPendingCount());
                Log("WARN", $"Envio de fornecedores enfileirado para envio offline posterior ({alterados.Count} itens).");
                _state.RegisterSyncDone("Fornecedores", alterados.Count, sw.ElapsedMilliseconds, "Offline (Enfileirado)", "Salvo na fila local", DateTime.UtcNow.Add(IntervaloFornecedores));
                return new SyncStepResult("Fornecedores", true, alterados.Count);
            }
        }
        catch (Exception ex)
        {
            Log("ERROR", "Erro no ciclo de fornecedores: " + ex.Message);
            _state.RegisterSyncDone("Fornecedores", 0, sw.ElapsedMilliseconds, "Falha", ex.Message, DateTime.UtcNow.Add(IntervaloFornecedores));
            return new SyncStepResult("Fornecedores", false, 0, ex.Message);
        }
    }

    public async Task DescarregarFilaOfflineAsync(CancellationToken ct = default)
    {
        var pendentes = _queue.GetPendingItems(filterTenantId: _tenantId, filterAgentId: _agentId);
        if (pendentes.Count == 0) return;

        Log("INFO", $"Retomando sincronização offline: {pendentes.Count} tarefa(s) pendente(s) na fila...");

        foreach (var item in pendentes)
        {
            try
            {
                var endpoint = item.TaskType switch
                {
                    "estoque" => "api/agent/v1/sync/estoque",
                    "produtos" => "api/agent/v1/sync/produtos",
                    "fornecedores" => "api/agent/v1/sync/fornecedores",
                    _ => null
                };

                if (endpoint == null)
                {
                    _queue.MarkCompleted(item.OperationId);
                    continue;
                }

                using var content = new StringContent(item.PayloadJson, Encoding.UTF8, "application/json");
                var res = await _http.PostAsync(endpoint, content, ct);

                if (res.IsSuccessStatusCode)
                {
                    _queue.MarkCompleted(item.OperationId);
                    Log("SUCCESS", $"Tarefa offline [{item.TaskType}] enviada e confirmada pela API SaaS (Op: {item.OperationId}).");
                }
                else
                {
                    _queue.MarkFailed(item.OperationId, $"HTTP {res.StatusCode}");
                    break; // Interrompe para preservar a ordem FIFO
                }
            }
            catch (Exception ex)
            {
                _queue.MarkFailed(item.OperationId, ex.Message);
                Log("WARN", $"Falha ao descarregar item da fila offline: {ex.Message}");
                break;
            }
        }

        _state.UpdateFilaOffline(_queue.GetPendingCount());
    }

    private async Task<bool> TentarEnviarAoSaasAsync(string endpoint, string taskType, object payload, CancellationToken ct)
    {
        try
        {
            var res = await _http.PostAsJsonAsync(endpoint, payload, ct);
            if (!res.IsSuccessStatusCode)
            {
                var body = await res.Content.ReadAsStringAsync(ct);
                Log("WARN", $"Envio para {endpoint} falhou: HTTP {(int)res.StatusCode} - {body}");
                return false;
            }
            return true;
        }
        catch (Exception ex)
        {
            Log("ERROR", $"Falha de conexão com API Nexo ({endpoint}): {ex.Message}");
            return false;
        }
    }

    private static string ComputeProdutoHash(ErpProdutoDto p)
    {
        var cod = (p.CodigoErp ?? "").Trim().ToUpperInvariant();
        var desc = (p.Descricao ?? "").Trim().ToUpperInvariant();
        var un = (p.Unidade ?? "").Trim().ToUpperInvariant();
        var barr = (p.CodigoBarras ?? "").Trim();
        var ncm = (p.Ncm ?? "").Trim();
        var grupo = (p.Grupo ?? "").Trim().ToUpperInvariant();
        var forn = (p.FornecedorErpId ?? "").Trim().ToUpperInvariant();
        var pv = p.PrecoVenda.ToString("F4", System.Globalization.CultureInfo.InvariantCulture);
        var ca = p.CustoAtual.ToString("F4", System.Globalization.CultureInfo.InvariantCulture);
        var em = p.EstoqueMinimo.ToString("F4", System.Globalization.CultureInfo.InvariantCulture);
        var atv = p.Ativo ? "1" : "0";

        var raw = $"{cod}|{desc}|{un}|{barr}|{ncm}|{grupo}|{forn}|{pv}|{ca}|{em}|{atv}";
        return ComputeHash(raw);
    }

    private static string ComputeFornecedorHash(ErpFornecedorDto f)
    {
        var cod = (f.CodigoErp ?? "").Trim().ToUpperInvariant();
        var raz = (f.RazaoSocial ?? "").Trim().ToUpperInvariant();
        var fan = (f.NomeFantasia ?? "").Trim().ToUpperInvariant();
        var cnpj = new string((f.Cnpj ?? "").Where(char.IsDigit).ToArray());
        var tel = new string((f.Telefone ?? "").Where(char.IsDigit).ToArray());
        var email = (f.Email ?? "").Trim().ToLowerInvariant();
        var cidade = (f.Cidade ?? "").Trim().ToUpperInvariant();
        var uf = (f.Uf ?? "").Trim().ToUpperInvariant();
        var atv = f.Ativo ? "1" : "0";

        var raw = $"{cod}|{raz}|{fan}|{cnpj}|{tel}|{email}|{cidade}|{uf}|{atv}";
        return ComputeHash(raw);
    }

    private static string ComputeEstoqueHash(ErpEstoqueDto e)
    {
        var cod = (e.CodigoErp ?? "").Trim().ToUpperInvariant();
        var ea = e.EstoqueAtual?.ToString("F4", System.Globalization.CultureInfo.InvariantCulture) ?? "NULL";
        var ep = e.EstoquePendente.ToString("F4", System.Globalization.CultureInfo.InvariantCulture);
        var raw = $"{cod}|{ea}|{ep}";
        return ComputeHash(raw);
    }

    private static string ComputeHash(string input)
    {
        using var md5 = MD5.Create();
        var bytes = md5.ComputeHash(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes);
    }

    private void LoadSyncState()
    {
        try
        {
            if (File.Exists(_syncStatePath))
            {
                var json = File.ReadAllText(_syncStatePath);
                _syncState = JsonSerializer.Deserialize<SyncStateFile>(json) ?? new SyncStateFile();
            }
        }
        catch { _syncState = new SyncStateFile(); }
    }

    private void SaveSyncState()
    {
        try
        {
            var json = JsonSerializer.Serialize(_syncState, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_syncStatePath, json);
        }
        catch { }
    }
}
