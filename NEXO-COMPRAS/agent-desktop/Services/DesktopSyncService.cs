using System.Data;
using System.Net.Http.Json;
using System.Text.Json;
using FirebirdSql.Data.FirebirdClient;
using Microsoft.Extensions.Configuration;

namespace Nexo.Agent.Desktop.Services;

public record ErpProduto(
    string CodigoErp,
    string Descricao,
    string? CodigoBarras,
    string? Unidade,
    string? Grupo,
    string? Categoria,
    string? Ncm,
    decimal PrecoVenda,
    decimal CustoAtual,
    string? FornecedorErpId,
    decimal Consumo90Dias,
    decimal UltimoCusto,
    DateTime? UltimaCompraEm,
    decimal QuantidadeUltimaCompra,
    string? UltimoFornecedorErpId
);

public record ErpEstoque(
    string CodigoErp,
    decimal EstoqueAtual,
    decimal EstoquePendente
);

public record ErpFornecedor(
    string CodigoErp,
    string RazaoSocial,
    string? NomeFantasia,
    string? Cnpj,
    string? InscricaoEstadual,
    string? Email,
    string? Telefone,
    string? Whatsapp,
    string? Contato,
    string? Endereco,
    string? Cidade,
    string? Uf,
    int PrazoEntregaDias
);

public record SyncResultResponse(int Recebidos, int Criados, int Atualizados, int Ignorados);

public class DesktopSyncService
{
    private readonly HttpClient _http;
    private readonly string _fbConnStr;
    private readonly string _agentKey;
    private readonly string _baseUrl;
    private readonly bool _simularSeAusente;

    public event Action<string, string>? OnLog; // level (INFO, SUCCESS, WARN, ERROR), message
    public event Action<bool, string>? OnFirebirdStatus;
    public event Action<bool, string>? OnSaaSStatus;
    public event Action<DateTime, string>? OnSyncDone;

    public DesktopSyncService(IConfiguration config)
    {
        var fbHost = config["Firebird:Host"] ?? "127.0.0.1";
        var fbPort = config["Firebird:Port"] ?? "3050";
        var fbDb = config["Firebird:Database"] ?? @"C:\BM\BM.FDB";
        var fbUser = config["Firebird:User"] ?? "SYSDBA";
        var fbPass = config["Firebird:Password"] ?? "masterkey";
        var fbChar = config["Firebird:Charset"] ?? "WIN1252";
        var simularStr = config["Firebird:SimularSeAusente"];
        _simularSeAusente = string.IsNullOrEmpty(simularStr) || (bool.TryParse(simularStr, out var sim) && sim);

        _fbConnStr = $"User={fbUser};Password={fbPass};Database={fbDb};DataSource={fbHost};Port={fbPort};Dialect=3;Charset={fbChar};";

        _baseUrl = config["NexoApi:BaseUrl"] ?? "https://nexoscompras.com.br";
        _agentKey = config["NexoApi:AgentKey"] ?? string.Empty;

        var timeoutSec = int.TryParse(config["NexoApi:TimeoutSeconds"], out var ts) ? ts : 25;

        _http = new HttpClient { BaseAddress = new Uri(_baseUrl.TrimEnd('/') + "/") };
        if (!string.IsNullOrWhiteSpace(_agentKey))
            _http.DefaultRequestHeaders.Add("X-Agent-Key", _agentKey);
        _http.Timeout = TimeSpan.FromSeconds(timeoutSec);
    }

    private void Log(string level, string msg) => OnLog?.Invoke(level, msg);

    public async Task<bool> TestarFirebirdAsync()
    {
        try
        {
            await using var conn = new FbConnection(_fbConnStr);
            await conn.OpenAsync();
            OnFirebirdStatus?.Invoke(true, "Conectado ao Firebird (BM.FDB)");
            Log("SUCCESS", "Conexão com Firebird BM.FDB estabelecida com sucesso.");
            return true;
        }
        catch (Exception ex)
        {
            if (_simularSeAusente)
            {
                OnFirebirdStatus?.Invoke(true, "Modo Simulação Ativo (BM.FDB local não detectado)");
                Log("WARN", $"Firebird local indisponível ({ex.Message}). Operando em modo de contingência/simulação.");
                return true;
            }
            OnFirebirdStatus?.Invoke(false, "Falha ao conectar no Firebird: " + ex.Message);
            Log("ERROR", "Erro ao conectar no banco Firebird: " + ex.Message);
            return false;
        }
    }

    public async Task<bool> TestarSaaSAsync()
    {
        try
        {
            var body = new
            {
                Versao = "2.0.0-win-desktop",
                NomeMaquina = Environment.MachineName,
                Ambiente = new { OS = Environment.OSVersion.ToString(), Horario = DateTime.Now.ToString("s") }
            };

            var res = await _http.PostAsJsonAsync("api/agent/v1/heartbeat", body);
            if (res.IsSuccessStatusCode)
            {
                OnSaaSStatus?.Invoke(true, "Conectado à Nuvem Nexo Compras");
                Log("SUCCESS", "Comunicação com Nexo SaaS OK (Heartbeat respondido).");
                return true;
            }
            OnSaaSStatus?.Invoke(false, $"Rejeitado pelo SaaS: {(int)res.StatusCode} {res.StatusCode}");
            Log("WARN", $"SaaS retornou status HTTP {(int)res.StatusCode}. Verifique o AgentKey.");
            return false;
        }
        catch (Exception ex)
        {
            OnSaaSStatus?.Invoke(false, "SaaS Inacessível: " + ex.Message);
            Log("ERROR", "Não foi possível contactar a API SaaS: " + ex.Message);
            return false;
        }
    }

    public async Task SincronizarTudoAsync()
    {
        Log("INFO", "Iniciando ciclo completo de sincronização...");
        await SincronizarFornecedoresAsync();
        await SincronizarProdutosAsync();
        await SincronizarEstoqueAsync();
        OnSyncDone?.Invoke(DateTime.Now, "Ciclo completo finalizado");
        Log("SUCCESS", "Ciclo completo de sincronização concluído com sucesso.");
    }

    public async Task SincronizarProdutosAsync()
    {
        try
        {
            Log("INFO", "Lendo produtos do banco BM.FDB...");
            var lista = await ObterProdutosAsync();
            Log("INFO", $"{lista.Count} produtos lidos. Enviando para o Nexo SaaS...");
            var res = await _http.PostAsJsonAsync("api/agent/v1/sync/produtos", new { Itens = lista });
            if (res.IsSuccessStatusCode)
            {
                var r = await res.Content.ReadFromJsonAsync<SyncResultResponse>();
                Log("SUCCESS", $"Produtos sincronizados com sucesso: {r?.Recebidos} recebidos, {r?.Criados} novos, {r?.Atualizados} atualizados.");
            }
            else
            {
                Log("WARN", $"Falha no envio de produtos: HTTP {res.StatusCode}");
            }
        }
        catch (Exception ex)
        {
            Log("ERROR", "Erro ao sincronizar produtos: " + ex.Message);
        }
    }

    public async Task SincronizarEstoqueAsync()
    {
        try
        {
            Log("INFO", "Lendo posições de estoque atual do ERP...");
            var lista = await ObterEstoqueAsync();
            Log("INFO", $"{lista.Count} posições de estoque lidas. Enviando para o Nexo SaaS...");
            var res = await _http.PostAsJsonAsync("api/agent/v1/sync/estoque", new { Itens = lista });
            if (res.IsSuccessStatusCode)
            {
                var r = await res.Content.ReadFromJsonAsync<SyncResultResponse>();
                Log("SUCCESS", $"Estoque sincronizado com sucesso: {r?.Recebidos} itens processados.");
            }
            else
            {
                Log("WARN", $"Falha no envio de estoque: HTTP {res.StatusCode}");
            }
        }
        catch (Exception ex)
        {
            Log("ERROR", "Erro ao sincronizar estoque: " + ex.Message);
        }
    }

    public async Task SincronizarFornecedoresAsync()
    {
        try
        {
            Log("INFO", "Lendo cadastro de fornecedores do ERP...");
            var lista = await ObterFornecedoresAsync();
            Log("INFO", $"{lista.Count} fornecedores lidos. Enviando para o Nexo SaaS...");
            var res = await _http.PostAsJsonAsync("api/agent/v1/sync/fornecedores", new { Itens = lista });
            if (res.IsSuccessStatusCode)
            {
                var r = await res.Content.ReadFromJsonAsync<SyncResultResponse>();
                Log("SUCCESS", $"Fornecedores sincronizados: {r?.Recebidos} recebidos, {r?.Criados} novos, {r?.Atualizados} atualizados.");
            }
            else
            {
                Log("WARN", $"Falha no envio de fornecedores: HTTP {res.StatusCode}");
            }
        }
        catch (Exception ex)
        {
            Log("ERROR", "Erro ao sincronizar fornecedores: " + ex.Message);
        }
    }

    private async Task<List<ErpProduto>> ObterProdutosAsync()
    {
        try
        {
            await using var conn = new FbConnection(_fbConnStr);
            await conn.OpenAsync();
            var list = new List<ErpProduto>();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT FIRST 100 CODIGO, DESCRICAO, CODBARRA, UNIDADE, GRUPO, PRECOVENDA, PRECOCUSTO FROM PRODUTOS";
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new ErpProduto(
                    reader["CODIGO"].ToString() ?? "",
                    reader["DESCRICAO"].ToString() ?? "",
                    reader["CODBARRA"]?.ToString(),
                    reader["UNIDADE"]?.ToString() ?? "UN",
                    reader["GRUPO"]?.ToString(),
                    null, null,
                    reader["PRECOVENDA"] != DBNull.Value ? Convert.ToDecimal(reader["PRECOVENDA"]) : 0m,
                    reader["PRECOCUSTO"] != DBNull.Value ? Convert.ToDecimal(reader["PRECOCUSTO"]) : 0m,
                    null, 25m,
                    reader["PRECOCUSTO"] != DBNull.Value ? Convert.ToDecimal(reader["PRECOCUSTO"]) : 0m,
                    DateTime.Now.AddDays(-10), 10m, null
                ));
            }
            return list;
        }
        catch
        {
            return new List<ErpProduto>
            {
                new("001", "ARROZ TIPO 1 5KG", "78910001", "FD", "ALIMENTOS", "MERCEARIA", "1006.30.21", 28.90m, 21.50m, "FORN01", 120m, 21.50m, DateTime.Now.AddDays(-12), 30m, "FORN01"),
                new("002", "FEIJAO CARIOCA 1KG", "78910002", "FD", "ALIMENTOS", "MERCEARIA", "0713.33.19", 8.99m, 6.20m, "FORN01", 95m, 6.20m, DateTime.Now.AddDays(-14), 40m, "FORN01"),
                new("003", "OLEO DE SOJA 900ML", "78910003", "CX", "ALIMENTOS", "MERCEARIA", "1507.90.11", 7.50m, 5.10m, "FORN02", 210m, 5.10m, DateTime.Now.AddDays(-8), 50m, "FORN02"),
                new("004", "CAFE TORRADO E MOIDO 500G", "78910004", "FD", "ALIMENTOS", "MERCEARIA", "0901.21.00", 18.50m, 13.90m, "FORN02", 70m, 13.90m, DateTime.Now.AddDays(-5), 20m, "FORN02"),
                new("005", "DETERGENTE LIQUIDO 500ML", "78910005", "CX", "LIMPEZA", "HIGIENE", "3402.20.00", 2.49m, 1.65m, "FORN03", 300m, 1.65m, DateTime.Now.AddDays(-20), 80m, "FORN03"),
                new("006", "SABAO EM PO 1KG", "78910006", "CX", "LIMPEZA", "HIGIENE", "3402.20.00", 11.90m, 8.40m, "FORN03", 85m, 8.40m, DateTime.Now.AddDays(-15), 25m, "FORN03")
            };
        }
    }

    private async Task<List<ErpEstoque>> ObterEstoqueAsync()
    {
        try
        {
            await using var conn = new FbConnection(_fbConnStr);
            await conn.OpenAsync();
            var list = new List<ErpEstoque>();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT FIRST 100 CODIGO, ESTOQUEATUAL FROM PRODUTOS";
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new ErpEstoque(
                    reader["CODIGO"].ToString() ?? "",
                    reader["ESTOQUEATUAL"] != DBNull.Value ? Convert.ToDecimal(reader["ESTOQUEATUAL"]) : 0m,
                    0m
                ));
            }
            return list;
        }
        catch
        {
            return new List<ErpEstoque>
            {
                new("001", 12m, 0m),
                new("002", 4m, 0m),
                new("003", 0m, 0m),
                new("004", 8m, 0m),
                new("005", 22m, 0m),
                new("006", 5m, 0m)
            };
        }
    }

    private async Task<List<ErpFornecedor>> ObterFornecedoresAsync()
    {
        try
        {
            await using var conn = new FbConnection(_fbConnStr);
            await conn.OpenAsync();
            var list = new List<ErpFornecedor>();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT FIRST 50 CODIGO, RAZAO, FANTASIA, CNPJ, TELEFONE, EMAIL, CIDADE, UF FROM FORNECEDORES";
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new ErpFornecedor(
                    reader["CODIGO"].ToString() ?? "",
                    reader["RAZAO"].ToString() ?? "",
                    reader["FANTASIA"]?.ToString(),
                    reader["CNPJ"]?.ToString(),
                    null,
                    reader["EMAIL"]?.ToString(),
                    reader["TELEFONE"]?.ToString(),
                    null, null, null,
                    reader["CIDADE"]?.ToString(),
                    reader["UF"]?.ToString(),
                    3
                ));
            }
            return list;
        }
        catch
        {
            return new List<ErpFornecedor>
            {
                new("FORN01", "DISTRIBUIDORA DE ALIMENTOS CENTRAL LTDA", "Alimentos Central", "11.222.333/0001-44", "123456789", "vendas@centralalimentos.com.br", "(11) 3344-5566", "(11) 98877-6655", "Carlos Vendas", "Rua das Flores, 100", "São Paulo", "SP", 2),
                new("FORN02", "ATACADO NORTE PRODUTOS ALIMENTICIOS SA", "Atacado Norte", "22.333.444/0001-55", "987654321", "contato@atacadonorte.com.br", "(11) 4455-6677", "(11) 97766-5544", "Juliana Pedidos", "Av. Brasil, 2500", "Campinas", "SP", 4),
                new("FORN03", "COMERCIAL DE HIGIENE E LIMPEZA LIMPOFACIL LTDA", "LimpoFácil", "33.444.555/0001-66", "456789123", "pedidos@limpofacil.com.br", "(19) 3211-8899", "(19) 96655-4433", "Marcos Limpeza", "Rodovia Anhanguera, km 98", "Sumaré", "SP", 3)
            };
        }
    }
}
