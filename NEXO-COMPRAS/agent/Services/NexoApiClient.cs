using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Nexo.Agent.Services;

public record AgentConfig(
    int IntervaloEstoqueMin,
    int IntervaloProdutosMin,
    int IntervaloFornecedoresMin,
    int IntervaloHeartbeatSeg
);

public record SyncResultResponse(int Recebidos, int Criados, int Atualizados, int Ignorados);

public class NexoApiClient
{
    private readonly HttpClient _http;
    private readonly string _agentKey;
    private readonly ILogger<NexoApiClient> _logger;
    private const string VersaoAgente = "2.0.0-win";

    public NexoApiClient(HttpClient http, IConfiguration config, ILogger<NexoApiClient> logger)
    {
        _http = http;
        _logger = logger;
        _agentKey = config["NexoApi:AgentKey"] ?? "";
        var baseUrl = config["NexoApi:BaseUrl"] ?? "http://localhost:5000";
        _http.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
        _http.DefaultRequestHeaders.Add("X-Agent-Key", _agentKey);
        _http.Timeout = TimeSpan.FromSeconds(config.GetValue<int>("NexoApi:TimeoutSeconds", 30));
    }

    public async Task<AgentConfig?> EnviarHeartbeatAsync(CancellationToken ct = default)
    {
        try
        {
            var body = new
            {
                Versao = VersaoAgente,
                NomeMaquina = Environment.MachineName,
                Ambiente = new
                {
                    OS = Environment.OSVersion.ToString(),
                    ProcessadorCount = Environment.ProcessorCount,
                    MemoriaGb = Math.Round(GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / 1024.0 / 1024.0 / 1024.0, 1),
                    DotNet = Environment.Version.ToString(),
                    HorarioLocal = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                }
            };

            var res = await _http.PostAsJsonAsync("api/agent/v1/heartbeat", body, ct);
            if (res.IsSuccessStatusCode)
            {
                var cfg = await res.Content.ReadFromJsonAsync<AgentConfig>(cancellationToken: ct);
                return cfg;
            }

            _logger.LogWarning("Heartbeat rejeitado pela API SaaS. Status: {Status}", res.StatusCode);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Não foi possível enviar heartbeat ao SaaS (API inacessível no momento).");
            return null;
        }
    }

    public async Task<SyncResultResponse?> SincronizarProdutosAsync(List<ErpProduto> produtos, CancellationToken ct = default)
    {
        try
        {
            var res = await _http.PostAsJsonAsync("api/agent/v1/sync/produtos", new { Itens = produtos }, ct);
            if (res.IsSuccessStatusCode)
            {
                return await res.Content.ReadFromJsonAsync<SyncResultResponse>(cancellationToken: ct);
            }
            _logger.LogWarning("Sincronização de produtos falhou. Status: {Status}", res.StatusCode);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Erro de rede ao sincronizar produtos com SaaS.");
            return null;
        }
    }

    public async Task<SyncResultResponse?> SincronizarFornecedoresAsync(List<ErpFornecedor> fornecedores, CancellationToken ct = default)
    {
        try
        {
            var res = await _http.PostAsJsonAsync("api/agent/v1/sync/fornecedores", new { Itens = fornecedores }, ct);
            if (res.IsSuccessStatusCode)
            {
                return await res.Content.ReadFromJsonAsync<SyncResultResponse>(cancellationToken: ct);
            }
            _logger.LogWarning("Sincronização de fornecedores falhou. Status: {Status}", res.StatusCode);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Erro de rede ao sincronizar fornecedores com SaaS.");
            return null;
        }
    }

    public async Task<SyncResultResponse?> SincronizarEstoqueAsync(List<ErpEstoque> estoque, CancellationToken ct = default)
    {
        try
        {
            var res = await _http.PostAsJsonAsync("api/agent/v1/sync/estoque", new { Itens = estoque }, ct);
            if (res.IsSuccessStatusCode)
            {
                return await res.Content.ReadFromJsonAsync<SyncResultResponse>(cancellationToken: ct);
            }
            _logger.LogWarning("Sincronização de estoque falhou. Status: {Status}", res.StatusCode);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Erro de rede ao sincronizar estoque com SaaS.");
            return null;
        }
    }

    public async Task ReportarErroAsync(string origem, string mensagem, string detalhe, CancellationToken ct = default)
    {
        try
        {
            var body = new { Origem = origem, Mensagem = mensagem, Detalhe = detalhe };
            await _http.PostAsJsonAsync("api/agent/v1/erro", body, ct);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Não foi possível enviar log de erro ao SaaS.");
        }
    }
}

