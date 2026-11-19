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

public record SyncStepResult(string Etapa, bool Sucesso, int Itens, string? Erro = null);
public record SyncCycleResult(bool Sucesso, bool Parcial, string Mensagem, List<SyncStepResult> Etapas);

public class IncrementalSyncEngine
{
    private readonly IDatabaseConnector _connector;
    private readonly HttpClient _http;
    private readonly PersistentTaskQueue _queue;
    private readonly AgentStateManager _state;
    private readonly SQLiteSyncRepository _sqliteRepo;
    private readonly ILogger<IncrementalSyncEngine>? _logger;
    private readonly Guid _tenantId;
    private readonly Guid? _filialId;
    private readonly Guid _agentId;

    public TimeSpan IntervaloEstoque { get; set; } = TimeSpan.FromMinutes(5);
    public TimeSpan IntervaloProdutos { get; set; } = TimeSpan.FromMinutes(30);
    public TimeSpan IntervaloFornecedores { get; set; } = TimeSpan.FromMinutes(30);
    public TimeSpan IntervaloPrecos { get; set; } = TimeSpan.FromMinutes(15);
    public TimeSpan IntervaloHeartbeat { get; set; } = TimeSpan.FromMinutes(1);

    public event Action<string, string>? OnLog; // level, message

    public SQLiteSyncRepository SqliteRepo => _sqliteRepo;
    public Guid TenantId => _tenantId;
    public Guid? FilialId => _filialId;
    public Guid AgentId => _agentId;

    public IncrementalSyncEngine(
        IDatabaseConnector connector,
        IConfiguration config,
        PersistentTaskQueue queue,
        AgentStateManager state,
        ILogger<IncrementalSyncEngine>? logger = null,
        SQLiteSyncRepository? sqliteRepo = null,
        HttpClient? httpClient = null)
    {
        _connector = connector;
        _queue = queue;
        _state = state;
        _logger = logger;
        _sqliteRepo = sqliteRepo ?? new SQLiteSyncRepository();

        var baseUrl = config["NexoApi:BaseUrl"] ?? "https://nexoscompras.com.br";
        var credsManager = new Security.AgentCredentialsManager();
        var stored = credsManager.LoadCredentials();
        var agentKey = stored != null && !string.IsNullOrWhiteSpace(stored.AgentCredential)
            ? stored.AgentCredential
            : (config["NexoApi:AgentKey"] ?? string.Empty);

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
                "Ativo",
                stored.FilialId?.ToString(),
                stored.FilialNome,
                stored.FilialCodigo
            );
        }
        else if (Guid.TryParse(config["TenantId"], out var cfgTenant))
        {
            _tenantId = cfgTenant;
            if (Guid.TryParse(config["FilialId"], out var cfgFilial))
                _filialId = cfgFilial;
        }

        var timeout = int.TryParse(config["NexoApi:TimeoutSeconds"], out var t) ? t : 20;

        if (httpClient != null)
        {
            _http = httpClient;
        }
        else
        {
            _http = new HttpClient { BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/") };
            if (!string.IsNullOrWhiteSpace(agentKey))
                _http.DefaultRequestHeaders.Add("X-Agent-Key", agentKey);
            _http.Timeout = TimeSpan.FromSeconds(timeout);
        }

        if (_tenantId != Guid.Empty)
        {
            var purged = _queue.PurgeOrArchiveForeignItems(_tenantId, _agentId);
            if (purged > 0)
            {
                Log("WARN", $"Isolamento Multi-Empresa: {purged} tarefas residuais de outra empresa foram removidas da fila offline.");
            }
        }

        AtualizarSituacaoCargaInicialNoEstado();
    }

    private void Log(string level, string msg)
    {
        _logger?.LogInformation("[{Level}] {Msg}", level, msg);
        _state.AddLog(level, msg);
        OnLog?.Invoke(level, msg);
    }

    public bool IsInitialSyncCompleted(string entityType = "Produtos") =>
        _sqliteRepo.IsInitialSyncCompleted(_tenantId, _filialId, entityType);

    private void AtualizarSituacaoCargaInicialNoEstado()
    {
        var prodInit = _sqliteRepo.IsInitialSyncCompleted(_tenantId, _filialId, "Produtos");
        var summary = _sqliteRepo.GetSyncSummary(_tenantId, _filialId, "Produtos");
        _state.CurrentState.CargaInicialConcluida = prodInit;
        _state.CurrentState.SituacaoCargaInicial = prodInit ? "Carga inicial concluída" : "Pendente de carga inicial";
        _state.CurrentState.UltimosProdutosEnviados = summary.LastCount;
        _state.CurrentState.UltimosProdutosNovos = summary.NewCount;
        _state.CurrentState.UltimosProdutosModificados = summary.ModCount;
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
                Versao = "2.6.0",
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

    #region Sincronização Inteligente de Produtos

    public async Task<SyncStepResult> SincronizarProdutosIncrementalAsync(CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var isInitial = !_sqliteRepo.IsInitialSyncCompleted(_tenantId, _filialId, "Produtos");

            if (isInitial)
            {
                Log("INFO", "Iniciando CARGA INICIAL completa de produtos do ERP...");
                _state.UpdateStatus("Carga Inicial");
            }
            else
            {
                Log("INFO", "Verificando alterações incrementais nos produtos do ERP...");
                _state.UpdateStatus("Sincronizando");
            }

            // Lê todos os produtos do ERP
            var produtos = (await _connector.GetProductsAsync(null, ct))
                .Where(p => !string.IsNullOrWhiteSpace(p.CodigoErp) && !string.IsNullOrWhiteSpace(p.Descricao))
                .ToList();

            if (produtos.Count == 0)
            {
                Log("INFO", "Nenhum produto válido encontrado no banco ERP.");
                _state.RegisterSyncDone("Produtos", 0, sw.ElapsedMilliseconds, "Sucesso", "0 produtos encontrados", DateTime.UtcNow.Add(IntervaloProdutos));
                _state.UpdateStatus("Online");
                return new SyncStepResult("Produtos", true, 0);
            }

            // Se for a primeira sincronização: Carga Inicial Completa
            if (isInitial)
            {
                Log("INFO", $"Carga inicial: {produtos.Count} produtos identificados no ERP para importação.");
                const int chunkSize = 1000;
                var totalEnviados = 0;
                var sucessoGeral = true;

                for (int i = 0; i < produtos.Count; i += chunkSize)
                {
                    var chunk = produtos.Skip(i).Take(chunkSize).ToList();
                    var batch = new SyncBatch<ErpProdutoDto>(Guid.NewGuid(), DateTime.UtcNow, chunk, IsDelta: false, TenantId: _tenantId, BranchId: _filialId);

                    var ok = await TentarEnviarAoSaasAsync("api/agent/v1/sync/produtos", "produtos", batch, ct);
                    if (ok)
                    {
                        // REGRA CRÍTICA: confirma no SQLite local somente após sucesso confirmado da API
                        _sqliteRepo.ConfirmSyncedFingerprints(
                            _tenantId, _filialId, "Produtos",
                            chunk.Select(p => (p.CodigoErp, ComputeProdutoFingerprint(p)))
                        );
                        totalEnviados += chunk.Count;
                    }
                    else
                    {
                        sucessoGeral = false;
                        _queue.Enqueue("produtos", batch, tenantId: _tenantId, branchId: _filialId, agentId: _agentId);
                    }
                }
                sw.Stop();

                if (sucessoGeral)
                {
                    _sqliteRepo.MarkInitialSyncCompleted(_tenantId, _filialId, "Produtos", totalEnviados, _connector.ConnectorName);
                    AtualizarSituacaoCargaInicialNoEstado();

                    Log("SUCCESS", $"Carga inicial concluída com sucesso: {totalEnviados} produtos sincronizados e persistidos no SQLite.");
                    _state.RegisterSyncDone("Produtos", totalEnviados, sw.ElapsedMilliseconds, "Sucesso", $"Carga inicial: {totalEnviados} produtos", DateTime.UtcNow.Add(IntervaloProdutos));
                    _state.UpdateStatus("Online");
                    return new SyncStepResult("Produtos", true, totalEnviados);
                }
                else
                {
                    _state.UpdateFilaOffline(_queue.GetPendingCount());
                    Log("WARN", $"Carga inicial parcial: {totalEnviados} enviados à API, restante salvo na fila offline.");
                    _state.RegisterSyncDone("Produtos", totalEnviados, sw.ElapsedMilliseconds, "Offline (Enfileirado)", "Salvo na fila local", DateTime.UtcNow.Add(IntervaloProdutos));
                    _state.UpdateStatus("Online");
                    return new SyncStepResult("Produtos", totalEnviados > 0, totalEnviados, "Envio parcial para a API");
                }
            }
            else
            {
                // SINCRONIZAÇÕES SUBSEQUENTES: Detecção Incremental Estrita por Fingerprint SHA-256
                var storedFingerprints = _sqliteRepo.GetStoredFingerprints(_tenantId, _filialId, "Produtos");

                var novos = new List<ErpProdutoDto>();
                var modificados = new List<ErpProdutoDto>();

                foreach (var p in produtos)
                {
                    var currentHash = ComputeProdutoFingerprint(p);

                    if (!storedFingerprints.TryGetValue(p.CodigoErp, out var oldHash))
                    {
                        novos.Add(p);
                    }
                    else if (oldHash != currentHash)
                    {
                        modificados.Add(p);
                    }
                }

                var alterados = novos.Concat(modificados).ToList();

                if (alterados.Count == 0)
                {
                    Log("INFO", "Catálogo de produtos verificado: Nenhuma alteração pendente (0 novos, 0 modificados). Nenhum produto reenviado.");
                    _sqliteRepo.UpdateSyncStats(_tenantId, _filialId, "Produtos", 0, 0, 0);
                    AtualizarSituacaoCargaInicialNoEstado();

                    _state.RegisterSyncDone("Produtos", 0, sw.ElapsedMilliseconds, "Sucesso", "Nenhuma alteração detectada (0 enviados)", DateTime.UtcNow.Add(IntervaloProdutos));
                    _state.UpdateStatus("Online");
                    return new SyncStepResult("Produtos", true, 0);
                }

                Log("INFO", $"Detectados {novos.Count} produto(s) novo(s) e {modificados.Count} modificado(s). Enviando {alterados.Count} para a nuvem...");

                const int chunkSize = 1000;
                var totalEnviados = 0;
                var sucessoGeral = true;

                for (int i = 0; i < alterados.Count; i += chunkSize)
                {
                    var chunk = alterados.Skip(i).Take(chunkSize).ToList();
                    var batch = new SyncBatch<ErpProdutoDto>(Guid.NewGuid(), DateTime.UtcNow, chunk, IsDelta: true, TenantId: _tenantId, BranchId: _filialId);

                    var ok = await TentarEnviarAoSaasAsync("api/agent/v1/sync/produtos", "produtos", batch, ct);
                    if (ok)
                    {
                        // REGRA CRÍTICA: Atualiza o fingerprint local somente após a API confirmar
                        _sqliteRepo.ConfirmSyncedFingerprints(
                            _tenantId, _filialId, "Produtos",
                            chunk.Select(p => (p.CodigoErp, ComputeProdutoFingerprint(p)))
                        );
                        totalEnviados += chunk.Count;
                    }
                    else
                    {
                        sucessoGeral = false;
                        _queue.Enqueue("produtos", batch, tenantId: _tenantId, branchId: _filialId, agentId: _agentId);
                    }
                }
                sw.Stop();

                _sqliteRepo.UpdateSyncStats(_tenantId, _filialId, "Produtos", totalEnviados, novos.Count, modificados.Count);
                AtualizarSituacaoCargaInicialNoEstado();

                if (sucessoGeral)
                {
                    Log("SUCCESS", $"Sincronização incremental concluída: {totalEnviados} produto(s) enviado(s) ({novos.Count} novos, {modificados.Count} modificados).");
                    _state.RegisterSyncDone("Produtos", totalEnviados, sw.ElapsedMilliseconds, "Sucesso", $"{totalEnviados} enviados ({novos.Count} novos, {modificados.Count} modificados)", DateTime.UtcNow.Add(IntervaloProdutos));
                    _state.UpdateStatus("Online");
                    return new SyncStepResult("Produtos", true, totalEnviados);
                }
                else
                {
                    _state.UpdateFilaOffline(_queue.GetPendingCount());
                    Log("WARN", $"Envio parcial incremental: {totalEnviados} enviados, restante enfileirado offline.");
                    _state.RegisterSyncDone("Produtos", totalEnviados, sw.ElapsedMilliseconds, "Offline (Enfileirado)", "Salvo na fila local", DateTime.UtcNow.Add(IntervaloProdutos));
                    _state.UpdateStatus("Online");
                    return new SyncStepResult("Produtos", totalEnviados > 0, totalEnviados, "Envio parcial incremental");
                }
            }
        }
        catch (Exception ex)
        {
            Log("ERROR", "Erro no ciclo de produtos: " + ex.Message);
            _state.RegisterSyncDone("Produtos", 0, sw.ElapsedMilliseconds, "Falha", ex.Message, DateTime.UtcNow.Add(IntervaloProdutos));
            _state.UpdateStatus("Online");
            return new SyncStepResult("Produtos", false, 0, ex.Message);
        }
    }

    #endregion

    #region Sincronização Inteligente de Estoque

    public async Task<SyncStepResult> SincronizarEstoqueIncrementalAsync(CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var isInitial = !_sqliteRepo.IsInitialSyncCompleted(_tenantId, _filialId, "Estoque");

            Log("INFO", isInitial ? "Iniciando carga inicial de posições de estoque..." : "Verificando alterações de estoque...");

            var estoques = (await _connector.GetStockAsync(ct))
                .Where(e => !string.IsNullOrWhiteSpace(e.CodigoErp))
                .ToList();

            if (estoques.Count == 0)
            {
                Log("INFO", "Nenhum registro de estoque retornado pelo banco ERP.");
                _state.RegisterSyncDone("Estoque", 0, sw.ElapsedMilliseconds, "Sucesso", "0 registros", DateTime.UtcNow.Add(IntervaloEstoque));
                return new SyncStepResult("Estoque", true, 0);
            }

            var storedFingerprints = _sqliteRepo.GetStoredFingerprints(_tenantId, _filialId, "Estoque");
            var alterados = new List<ErpEstoqueDto>();

            foreach (var item in estoques)
            {
                var hash = ComputeEstoqueFingerprint(item);
                if (!storedFingerprints.TryGetValue(item.CodigoErp, out var oldHash) || oldHash != hash)
                {
                    alterados.Add(item);
                }
            }

            if (alterados.Count == 0)
            {
                Log("INFO", "Estoque verificado: Nenhuma alteração detectada (0 posições enviadas).");
                _sqliteRepo.UpdateSyncStats(_tenantId, _filialId, "Estoque", 0, 0, 0);
                _state.RegisterSyncDone("Estoque", 0, sw.ElapsedMilliseconds, "Sucesso", "Nenhuma alteração detectada", DateTime.UtcNow.Add(IntervaloEstoque));
                return new SyncStepResult("Estoque", true, 0);
            }

            Log("INFO", $"{alterados.Count} posições de estoque alteradas detectadas. Enviando...");
            const int chunkSize = 1000;
            var totalEnviados = 0;
            var sucessoGeral = true;

            for (int i = 0; i < alterados.Count; i += chunkSize)
            {
                var chunk = alterados.Skip(i).Take(chunkSize).ToList();
                var batch = new SyncBatch<ErpEstoqueDto>(Guid.NewGuid(), DateTime.UtcNow, chunk, IsDelta: !isInitial, TenantId: _tenantId, BranchId: _filialId);

                var ok = await TentarEnviarAoSaasAsync("api/agent/v1/sync/estoque", "estoque", batch, ct);
                if (ok)
                {
                    _sqliteRepo.ConfirmSyncedFingerprints(
                        _tenantId, _filialId, "Estoque",
                        chunk.Select(e => (e.CodigoErp, ComputeEstoqueFingerprint(e)))
                    );
                    totalEnviados += chunk.Count;
                }
                else
                {
                    sucessoGeral = false;
                    _queue.Enqueue("estoque", batch, tenantId: _tenantId, branchId: _filialId, agentId: _agentId);
                }
            }
            sw.Stop();

            _sqliteRepo.UpdateSyncStats(_tenantId, _filialId, "Estoque", totalEnviados, alterados.Count, 0);
            if (isInitial && sucessoGeral)
            {
                _sqliteRepo.MarkInitialSyncCompleted(_tenantId, _filialId, "Estoque", totalEnviados, _connector.ConnectorName);
            }

            if (sucessoGeral)
            {
                Log("SUCCESS", $"{alterados.Count} posições de estoque sincronizadas com o Nexo Compras.");
                _state.RegisterSyncDone("Estoque", alterados.Count, sw.ElapsedMilliseconds, "Sucesso", $"{alterados.Count} posições atualizadas", DateTime.UtcNow.Add(IntervaloEstoque));
                return new SyncStepResult("Estoque", true, alterados.Count);
            }
            else
            {
                _state.UpdateFilaOffline(_queue.GetPendingCount());
                Log("WARN", $"Envio parcial de estoque: {totalEnviados} enviados, restante enfileirado offline.");
                _state.RegisterSyncDone("Estoque", totalEnviados, sw.ElapsedMilliseconds, "Offline (Enfileirado)", "Salvo na fila local", DateTime.UtcNow.Add(IntervaloEstoque));
                return new SyncStepResult("Estoque", totalEnviados > 0, totalEnviados, "Envio parcial de estoque");
            }
        }
        catch (Exception ex)
        {
            Log("ERROR", "Erro no ciclo de estoque: " + ex.Message);
            _state.RegisterSyncDone("Estoque", 0, sw.ElapsedMilliseconds, "Falha", ex.Message, DateTime.UtcNow.Add(IntervaloEstoque));
            return new SyncStepResult("Estoque", false, 0, ex.Message);
        }
    }

    #endregion

    #region Sincronização Inteligente de Fornecedores

    public async Task<SyncStepResult> SincronizarFornecedoresIncrementalAsync(CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var isInitial = !_sqliteRepo.IsInitialSyncCompleted(_tenantId, _filialId, "Fornecedores");

            Log("INFO", isInitial ? "Iniciando carga inicial de fornecedores do ERP..." : "Verificando alterações de fornecedores...");

            var fornecedores = (await _connector.GetSuppliersAsync(null, ct))
                .Where(f => !string.IsNullOrWhiteSpace(f.CodigoErp) && !string.IsNullOrWhiteSpace(f.RazaoSocial))
                .ToList();

            if (fornecedores.Count == 0)
            {
                Log("INFO", "Nenhum fornecedor retornado pelo banco ERP.");
                _state.RegisterSyncDone("Fornecedores", 0, sw.ElapsedMilliseconds, "Sucesso", "0 fornecedores", DateTime.UtcNow.Add(IntervaloFornecedores));
                return new SyncStepResult("Fornecedores", true, 0);
            }

            var storedFingerprints = _sqliteRepo.GetStoredFingerprints(_tenantId, _filialId, "Fornecedores");
            var alterados = new List<ErpFornecedorDto>();

            foreach (var f in fornecedores)
            {
                var hash = ComputeFornecedorFingerprint(f);
                if (!storedFingerprints.TryGetValue(f.CodigoErp, out var oldHash) || oldHash != hash)
                {
                    alterados.Add(f);
                }
            }

            if (alterados.Count == 0)
            {
                Log("INFO", "Cadastro de fornecedores verificado: Nenhuma alteração pendente (0 enviados).");
                _sqliteRepo.UpdateSyncStats(_tenantId, _filialId, "Fornecedores", 0, 0, 0);
                _state.RegisterSyncDone("Fornecedores", 0, sw.ElapsedMilliseconds, "Sucesso", "Nenhuma alteração detectada", DateTime.UtcNow.Add(IntervaloFornecedores));
                return new SyncStepResult("Fornecedores", true, 0);
            }

            Log("INFO", $"{alterados.Count} fornecedores novos ou alterados detectados. Enviando...");
            var batch = new SyncBatch<ErpFornecedorDto>(Guid.NewGuid(), DateTime.UtcNow, alterados, IsDelta: !isInitial, TenantId: _tenantId, BranchId: _filialId);

            var ok = await TentarEnviarAoSaasAsync("api/agent/v1/sync/fornecedores", "fornecedores", batch, ct);
            sw.Stop();

            if (ok)
            {
                _sqliteRepo.ConfirmSyncedFingerprints(
                    _tenantId, _filialId, "Fornecedores",
                    alterados.Select(f => (f.CodigoErp, ComputeFornecedorFingerprint(f)))
                );
                _sqliteRepo.UpdateSyncStats(_tenantId, _filialId, "Fornecedores", alterados.Count, alterados.Count, 0);

                if (isInitial)
                {
                    _sqliteRepo.MarkInitialSyncCompleted(_tenantId, _filialId, "Fornecedores", alterados.Count, _connector.ConnectorName);
                }

                Log("SUCCESS", $"{alterados.Count} fornecedores sincronizados com o Nexo Compras.");
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

    #endregion

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
                    break;
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

    #region Algoritmos de Fingerprint / Checksum Normalizados (SHA-256)

    public static string ComputeProdutoFingerprint(ErpProdutoDto p)
    {
        var cod = (p.CodigoErp ?? "").Trim().ToUpperInvariant();
        var desc = (p.Descricao ?? "").Trim();
        var un = string.IsNullOrWhiteSpace(p.Unidade) ? "UN" : p.Unidade.Trim().ToUpperInvariant();
        var barr = (p.CodigoBarras ?? "").Trim();
        var ncm = (p.Ncm ?? "").Trim();
        var grupo = (p.Grupo ?? "").Trim().ToUpperInvariant();
        var cat = (p.Categoria ?? "").Trim().ToUpperInvariant();
        var forn = (p.FornecedorErpId ?? "").Trim().ToUpperInvariant();
        var pv = p.PrecoVenda.ToString("F4", System.Globalization.CultureInfo.InvariantCulture);
        var ca = p.CustoAtual.ToString("F4", System.Globalization.CultureInfo.InvariantCulture);
        var em = p.EstoqueMinimo.ToString("F4", System.Globalization.CultureInfo.InvariantCulture);
        var atv = p.Ativo ? "1" : "0";
        var dt = p.UltimaAtualizacao.HasValue ? p.UltimaAtualizacao.Value.ToString("o") : "";

        var raw = $"{cod}|{desc}|{un}|{barr}|{ncm}|{grupo}|{cat}|{forn}|{pv}|{ca}|{em}|{atv}|{dt}";
        using var sha = SHA256.Create();
        return Convert.ToHexString(sha.ComputeHash(Encoding.UTF8.GetBytes(raw))).ToLowerInvariant();
    }

    public static string ComputeFornecedorFingerprint(ErpFornecedorDto f)
    {
        var cod = (f.CodigoErp ?? "").Trim().ToUpperInvariant();
        var raz = (f.RazaoSocial ?? "").Trim().ToUpperInvariant();
        var fan = (f.NomeFantasia ?? "").Trim().ToUpperInvariant();
        var cnpj = new string((f.Cnpj ?? "").Where(char.IsDigit).ToArray());
        var tel = new string((f.Telefone ?? "").Where(char.IsDigit).ToArray());
        var email = (f.Email ?? "").Trim().ToLowerInvariant();
        var end = (f.Endereco ?? "").Trim().ToUpperInvariant();
        var cidade = (f.Cidade ?? "").Trim().ToUpperInvariant();
        var uf = (f.Uf ?? "").Trim().ToUpperInvariant();
        var atv = f.Ativo ? "1" : "0";

        var raw = $"{cod}|{raz}|{fan}|{cnpj}|{tel}|{email}|{end}|{cidade}|{uf}|{atv}";
        using var sha = SHA256.Create();
        return Convert.ToHexString(sha.ComputeHash(Encoding.UTF8.GetBytes(raw))).ToLowerInvariant();
    }

    public static string ComputeEstoqueFingerprint(ErpEstoqueDto e)
    {
        var cod = (e.CodigoErp ?? "").Trim().ToUpperInvariant();
        var ea = e.EstoqueAtual.HasValue ? e.EstoqueAtual.Value.ToString("F4", System.Globalization.CultureInfo.InvariantCulture) : "NULL";
        var ep = e.EstoquePendente.ToString("F4", System.Globalization.CultureInfo.InvariantCulture);
        var raw = $"{cod}|{ea}|{ep}";
        using var sha = SHA256.Create();
        return Convert.ToHexString(sha.ComputeHash(Encoding.UTF8.GetBytes(raw))).ToLowerInvariant();
    }

    #endregion
}
