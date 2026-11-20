using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Nexo.Agent.Core;
using Nexo.Agent.Core.Connectors;
using Nexo.Agent.Core.Queue;
using Nexo.Agent.Core.Schema;
using Nexo.Agent.Core.State;
using Nexo.Agent.Core.Sync;
using Xunit;

namespace Nexo.Api.Tests;

[Collection("DatabaseCollection")]
public class SincronizacaoIncrementalRealTests : IDisposable
{
    private readonly string _tempDir;

    public SincronizacaoIncrementalRealTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "sinc_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, true);
        }
        catch { }
    }

    private class MockConnector : IDatabaseConnector
    {
        public string ConnectorName => "MockConnector";
        public string DatabaseType => "Memory";
        public string DatabasePathOrName => "MemoryDb";
        public ResolvedTableMapping CurrentMapping => new ResolvedTableMapping();

        public List<ErpProdutoDto> Produtos { get; set; } = new();
        public List<ErpFornecedorDto> Fornecedores { get; set; } = new();
        public List<ErpEstoqueDto> Estoques { get; set; } = new();
        public List<ErpPrecoDto> Precos { get; set; } = new();

        public Task<bool> TestConnectionAsync(CancellationToken ct = default) => Task.FromResult(true);
        public Task<ResolvedTableMapping> DiscoverAndApplySchemaAsync(bool forceRefresh = false, CancellationToken ct = default) => Task.FromResult(CurrentMapping);
        public Task<IEnumerable<ErpProdutoDto>> GetProductsAsync(DateTime? since = null, CancellationToken ct = default) => Task.FromResult<IEnumerable<ErpProdutoDto>>(Produtos);
        public Task<IEnumerable<ErpFornecedorDto>> GetSuppliersAsync(DateTime? since = null, CancellationToken ct = default) => Task.FromResult<IEnumerable<ErpFornecedorDto>>(Fornecedores);
        public Task<IEnumerable<ErpEstoqueDto>> GetStockAsync(CancellationToken ct = default) => Task.FromResult<IEnumerable<ErpEstoqueDto>>(Estoques);
        public Task<IEnumerable<ErpPrecoDto>> GetPricesAsync(DateTime? since = null, CancellationToken ct = default) => Task.FromResult<IEnumerable<ErpPrecoDto>>(Precos);
    }

    private class MockHttpHandler : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = new();
        public List<int> SentProductBatchSizes { get; } = new();
        public bool SimulateFailure { get; set; } = false;
        public string LastBody { get; set; } = "";

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);

            if (SimulateFailure)
            {
                return new HttpResponseMessage(HttpStatusCode.InternalServerError)
                {
                    Content = new StringContent("{\"error\":\"Internal Server Error\"}")
                };
            }

            if (request.Content != null)
            {
                var body = await request.Content.ReadAsStringAsync(cancellationToken);
                LastBody = body;
                try
                {
                    using var doc = JsonDocument.Parse(body);
                    foreach (var prop in doc.RootElement.EnumerateObject())
                    {
                        if ((prop.Name.Equals("itens", StringComparison.OrdinalIgnoreCase) || prop.Name.Equals("items", StringComparison.OrdinalIgnoreCase))
                            && prop.Value.ValueKind == JsonValueKind.Array)
                        {
                            SentProductBatchSizes.Add(prop.Value.GetArrayLength());
                            break;
                        }
                    }
                }
                catch { }
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"success\":true,\"received\":1}")
            };
        }
    }

    private static ErpProdutoDto CriarProduto(string codigoErp, string descricao, decimal precoVenda = 10.0m, decimal custoAtual = 5.0m, string? ncm = null)
    {
        return new ErpProdutoDto(
            CodigoErp: codigoErp,
            Descricao: descricao,
            CodigoBarras: "789" + codigoErp.PadLeft(10, '0'),
            Unidade: "UN",
            Grupo: "GERAL",
            Categoria: "PADRAO",
            Ncm: ncm ?? "12345678",
            PrecoVenda: precoVenda,
            CustoAtual: custoAtual,
            FornecedorErpId: null,
            Consumo90Dias: 0,
            UltimoCusto: custoAtual,
            UltimaCompraEm: null,
            QuantidadeUltimaCompra: 0,
            UltimoFornecedorErpId: null,
            EstoqueMinimo: 5,
            UltimaAtualizacao: DateTime.UtcNow,
            Ativo: true
        );
    }

    private List<ErpProdutoDto> GerarProdutos(int count, string prefix = "PRD")
    {
        var list = new List<ErpProdutoDto>(count);
        for (int i = 1; i <= count; i++)
        {
            list.Add(CriarProduto(
                $"{prefix}-{i:D6}",
                $"PRODUTO TESTE {prefix} {i}",
                precoVenda: 10.50m + (i * 0.1m),
                custoAtual: 5.25m + (i * 0.05m)
            ));
        }
        return list;
    }

    [Fact]
    public async Task CicloCompleto_1000Produtos_Incremental_Reinicio_IsolamentoMultiTenant()
    {
        var tenantA = Guid.NewGuid();
        var filialA = Guid.NewGuid();
        var dbPath = Path.Combine(_tempDir, "sinc_local.db");
        var queuePath = Path.Combine(_tempDir, "offline_queue.json");
        var statePath = Path.Combine(_tempDir, "agent_state.json");

        var sqliteRepo = new SQLiteSyncRepository(dbPath);
        var queue = new PersistentTaskQueue(queuePath);
        var state = new AgentStateManager(statePath);

        var configDict = new Dictionary<string, string?>
        {
            ["NexoApi:BaseUrl"] = "http://localhost:5000",
            ["TenantId"] = tenantA.ToString(),
            ["FilialId"] = filialA.ToString(),
            ["Sync:IntervaloProdutosMinutos"] = "30"
        };
        var config = new ConfigurationBuilder().AddInMemoryCollection(configDict).Build();

        var connector = new MockConnector();
        var httpHandler = new MockHttpHandler();
        var httpClient = new HttpClient(httpHandler) { BaseAddress = new Uri("http://localhost:5000/") };

        var engine = new IncrementalSyncEngine(connector, config, queue, state, sqliteRepo: sqliteRepo, httpClient: httpClient);

        // =========================================================================
        // PASSO 1: PRIMEIRA SINCRONIZAÇÃO — ENVIAR 1.000 PRODUTOS (CARGA INICIAL)
        // =========================================================================
        connector.Produtos = GerarProdutos(1000, "PRD");

        Assert.False(engine.IsInitialSyncCompleted("Produtos"), "Inicialmente a carga inicial deve ser falsa");

        var sync1 = await engine.SincronizarProdutosIncrementalAsync(CancellationToken.None);

        Assert.True(sync1.Sucesso, "Primeira sincronização deve ter sucesso");
        Assert.Equal(1000, sync1.Itens);
        Assert.Equal(1000, state.CurrentState.UltimosProdutosNovos);
        Assert.Equal(0, state.CurrentState.UltimosProdutosModificados);
        Assert.Equal(1000, state.CurrentState.UltimosProdutosEnviados);
        Assert.True(engine.IsInitialSyncCompleted("Produtos"), "Carga inicial deve ser marcada como concluída");
        Assert.True(state.CurrentState.CargaInicialConcluida, "Estado deve registrar CargaInicialConcluida = true");
        Assert.Equal("Carga inicial concluída", state.CurrentState.SituacaoCargaInicial);

        // Verificar total enviado para API HTTP
        var totalEnviadosHttp1 = httpHandler.SentProductBatchSizes.Sum();
        Assert.Equal(1000, totalEnviadosHttp1);

        // =========================================================================
        // PASSO 2: SEGUNDA SINCRONIZAÇÃO IMEDIATA (SEM ALTERAÇÕES) — 0 ENVIADOS
        // =========================================================================
        httpHandler.SentProductBatchSizes.Clear();
        httpHandler.Requests.Clear();

        var sync2 = await engine.SincronizarProdutosIncrementalAsync(CancellationToken.None);

        Assert.True(sync2.Sucesso);
        Assert.Equal(0, sync2.Itens);
        Assert.Equal(0, state.CurrentState.UltimosProdutosNovos);
        Assert.Equal(0, state.CurrentState.UltimosProdutosModificados);
        Assert.Equal(0, state.CurrentState.UltimosProdutosEnviados);
        Assert.Empty(httpHandler.SentProductBatchSizes); // Nenhuma chamada de API para produtos deve ser feita!

        // =========================================================================
        // PASSO 3: MODIFICAÇÃO DE EXATAMENTE 1 PRODUTO — EXATAMENTE 1 ENVIADO
        // =========================================================================
        httpHandler.SentProductBatchSizes.Clear();
        connector.Produtos[42] = connector.Produtos[42] with { PrecoVenda = 99.90m }; // Altera preço do produto 43

        var sync3 = await engine.SincronizarProdutosIncrementalAsync(CancellationToken.None);

        Assert.True(sync3.Sucesso);
        Assert.Equal(1, sync3.Itens);
        Assert.Equal(0, state.CurrentState.UltimosProdutosNovos);
        Assert.Equal(1, state.CurrentState.UltimosProdutosModificados);
        Assert.Equal(1, state.CurrentState.UltimosProdutosEnviados);
        Assert.Single(httpHandler.SentProductBatchSizes);
        Assert.Equal(1, httpHandler.SentProductBatchSizes[0]);

        // =========================================================================
        // PASSO 4: ADIÇÃO DE EXATAMENTE 1 NOVO PRODUTO — EXATAMENTE 1 ENVIADO
        // =========================================================================
        httpHandler.SentProductBatchSizes.Clear();
        connector.Produtos.Add(CriarProduto("PRD-NOVO-01", "PRODUTO ADICIONADO", precoVenda: 15.00m));

        var sync4 = await engine.SincronizarProdutosIncrementalAsync(CancellationToken.None);

        Assert.True(sync4.Sucesso);
        Assert.Equal(1, sync4.Itens);
        Assert.Equal(1, state.CurrentState.UltimosProdutosNovos);
        Assert.Equal(0, state.CurrentState.UltimosProdutosModificados);
        Assert.Equal(1, state.CurrentState.UltimosProdutosEnviados);
        Assert.Single(httpHandler.SentProductBatchSizes);
        Assert.Equal(1, httpHandler.SentProductBatchSizes[0]);

        // =========================================================================
        // PASSO 5: MODIFICAÇÃO DE 10 PRODUTOS EM LOTE — EXATAMENTE 10 ENVIADOS
        // =========================================================================
        httpHandler.SentProductBatchSizes.Clear();
        for (int i = 100; i < 110; i++)
        {
            connector.Produtos[i] = connector.Produtos[i] with { Descricao = $"PRODUTO MODIFICADO LOTE {i}" };
        }

        var sync5 = await engine.SincronizarProdutosIncrementalAsync(CancellationToken.None);

        Assert.True(sync5.Sucesso);
        Assert.Equal(10, sync5.Itens);
        Assert.Equal(0, state.CurrentState.UltimosProdutosNovos);
        Assert.Equal(10, state.CurrentState.UltimosProdutosModificados);
        Assert.Equal(10, state.CurrentState.UltimosProdutosEnviados);
        Assert.Equal(10, httpHandler.SentProductBatchSizes.Sum());

        // =========================================================================
        // PASSO 6: ADIÇÃO DE 5 PRODUTOS E MODIFICAÇÃO DE 3 — EXATAMENTE 8 ENVIADOS
        // =========================================================================
        httpHandler.SentProductBatchSizes.Clear();
        for (int i = 1; i <= 5; i++)
        {
            connector.Produtos.Add(CriarProduto($"PRD-EXTRA-{i}", $"PRODUTO EXTRA {i}", precoVenda: 25.00m));
        }
        connector.Produtos[200] = connector.Produtos[200] with { PrecoVenda = 55.00m };
        connector.Produtos[201] = connector.Produtos[201] with { PrecoVenda = 66.00m };
        connector.Produtos[202] = connector.Produtos[202] with { PrecoVenda = 77.00m };

        var sync6 = await engine.SincronizarProdutosIncrementalAsync(CancellationToken.None);

        Assert.True(sync6.Sucesso);
        Assert.Equal(8, sync6.Itens);
        Assert.Equal(5, state.CurrentState.UltimosProdutosNovos);
        Assert.Equal(3, state.CurrentState.UltimosProdutosModificados);
        Assert.Equal(8, state.CurrentState.UltimosProdutosEnviados);
        Assert.Equal(8, httpHandler.SentProductBatchSizes.Sum());

        // =========================================================================
        // PASSO 7: REINICIALIZAÇÃO DO SINC — LEITURA DO SQLITE E 0 ENVIADOS
        // =========================================================================
        httpHandler.SentProductBatchSizes.Clear();

        // Simula reinicialização do aplicativo criando nova instância apontando para mesmo SQLite
        var restartedSqliteRepo = new SQLiteSyncRepository(dbPath);
        var restartedState = new AgentStateManager(statePath);
        var restartedQueue = new PersistentTaskQueue(queuePath);

        var restartedEngine = new IncrementalSyncEngine(
            connector, config, restartedQueue, restartedState, sqliteRepo: restartedSqliteRepo, httpClient: httpClient);

        Assert.True(restartedEngine.IsInitialSyncCompleted("Produtos"), "Após reiniciar, o SQLite deve manter Carga Inicial Concluída");

        var syncAfterRestart = await restartedEngine.SincronizarProdutosIncrementalAsync(CancellationToken.None);

        Assert.True(syncAfterRestart.Sucesso);
        Assert.Equal(0, syncAfterRestart.Itens);
        Assert.Equal(0, restartedState.CurrentState.UltimosProdutosNovos);
        Assert.Equal(0, restartedState.CurrentState.UltimosProdutosModificados);
        Assert.Equal(0, restartedState.CurrentState.UltimosProdutosEnviados);
        Assert.Empty(httpHandler.SentProductBatchSizes); // 0 chamadas de API!

        // =========================================================================
        // PASSO 8: REGRA DE CONFIRMAÇÃO — FALHA NA API NÃO ATUALIZA SQLITE
        // =========================================================================
        httpHandler.SentProductBatchSizes.Clear();
        connector.Produtos[300] = connector.Produtos[300] with { PrecoVenda = 1234.56m }; // Modifica 1 produto

        // Simula queda da API (HTTP 500)
        httpHandler.SimulateFailure = true;

        var syncFail = await restartedEngine.SincronizarProdutosIncrementalAsync(CancellationToken.None);
        Assert.False(syncFail.Sucesso, "Sincronização deve registrar falha ao receber HTTP 500");

        // Restaura conexão da API
        httpHandler.SimulateFailure = false;
        httpHandler.SentProductBatchSizes.Clear();

        // Como falhou antes, o produto modificado ainda não deve ter sido gravado no SQLite e deve ser reenviado!
        var syncRetry = await restartedEngine.SincronizarProdutosIncrementalAsync(CancellationToken.None);
        Assert.True(syncRetry.Sucesso);
        Assert.Equal(1, syncRetry.Itens);
        Assert.Equal(1, httpHandler.SentProductBatchSizes.Sum());

        // =========================================================================
        // PASSO 9: ISOLAMENTO MULTI-TENANT
        // =========================================================================
        var tenantB = Guid.NewGuid();
        var filialB = Guid.NewGuid();
        var configDictB = new Dictionary<string, string?>
        {
            ["NexoApi:BaseUrl"] = "http://localhost:5000",
            ["TenantId"] = tenantB.ToString(),
            ["FilialId"] = filialB.ToString(),
            ["Sync:IntervaloProdutosMinutos"] = "30"
        };
        var configB = new ConfigurationBuilder().AddInMemoryCollection(configDictB).Build();

        var connectorB = new MockConnector();
        // Tenant B possui um produto com o MESMO código de Tenant A ("PRD-000001")
        connectorB.Produtos = new List<ErpProdutoDto>
        {
            CriarProduto("PRD-000001", "PRODUTO DO TENANT B TOTALMENTE DIFERENTE", precoVenda: 999.00m)
        };

        var httpHandlerB = new MockHttpHandler();
        var httpClientB = new HttpClient(httpHandlerB) { BaseAddress = new Uri("http://localhost:5000/") };
        var engineB = new IncrementalSyncEngine(
            connectorB, configB, restartedQueue, restartedState, sqliteRepo: restartedSqliteRepo, httpClient: httpClientB);

        // Tenant B ainda não realizou carga inicial
        Assert.False(engineB.IsInitialSyncCompleted("Produtos"), "Tenant B deve estar com Carga Inicial pendente");

        var syncB1 = await engineB.SincronizarProdutosIncrementalAsync(CancellationToken.None);
        Assert.True(syncB1.Sucesso);
        Assert.Equal(1, syncB1.Itens);
        Assert.True(engineB.IsInitialSyncCompleted("Produtos"), "Tenant B concluiu carga inicial");

        // Agora verifica que Tenant A não foi corrompido pela carga do Tenant B
        httpHandler.SentProductBatchSizes.Clear();
        var syncACheck = await restartedEngine.SincronizarProdutosIncrementalAsync(CancellationToken.None);
        Assert.True(syncACheck.Sucesso);
        Assert.Equal(0, syncACheck.Itens); // Tenant A continua com 0 pendências
        Assert.Empty(httpHandler.SentProductBatchSizes);
    }
}
