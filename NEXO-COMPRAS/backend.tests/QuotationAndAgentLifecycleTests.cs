using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Nexo.Api.DTOs;
using Nexo.Api.Services;
using Xunit;

namespace Nexo.Api.Tests;

public class QuotationAndAgentLifecycleTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public QuotationAndAgentLifecycleTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<Nexo.Api.Data.NexoDbContext>();
        Nexo.Api.Data.DbInitializer.InitializeAsync(db).GetAwaiter().GetResult();
    }

    private async Task<(string token, Guid tenantId)> RegistrarTenantAsync(string prefixo)
    {
        var cnpjGerado = $"{Random.Shared.Next(10, 99):D2}{Random.Shared.Next(100, 999):D3}{Random.Shared.Next(100, 999):D3}0001{Random.Shared.Next(10, 99):D2}";
        var req = new RegisterTenantRequest(
            RazaoSocial: $"{prefixo} Comércio Varejista Ltda",
            NomeFantasia: $"{prefixo} Supermercados",
            Cnpj: cnpjGerado,
            Email: $"contato@{prefixo.ToLower()}{Random.Shared.Next(100, 999)}.com.br",
            Telefone: "(81) 3456-0000",
            AdminNome: $"Gerente {prefixo}",
            AdminEmail: $"gerente@{prefixo.ToLower()}{Random.Shared.Next(1000, 9999)}.com.br",
            AdminSenha: "SenhaForte@2026",
            PlanoId: null
        );

        var res = await _client.PostAsJsonAsync("/api/auth/register-tenant", req);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var login = await res.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(login);
        return (login.Token, login.Tenant!.Id);
    }

    [Fact]
    public async Task FluxoCompleto_Cotacao_Propostas_Comparativo_E_Aprovacao()
    {
        // 1. Cria Tenant e obtém token
        var (token, tenantId) = await RegistrarTenantAsync("RedeCompras");

        // 2. Cria 2 fornecedores
        var reqForn1 = new HttpRequestMessage(HttpMethod.Post, "/api/fornecedores")
        {
            Content = JsonContent.Create(new SaveFornecedorRequest(
                RazaoSocial: "Distribuidora Alfa Ltda",
                NomeFantasia: "Alfa Distribuição",
                Cnpj: "11222333000188",
                InscricaoEstadual: "00112233",
                Email: "comercial@alfa.com.br",
                Telefone: "(81) 3333-1111",
                Whatsapp: "(81) 98888-1111",
                Contato: "Carlos",
                Endereco: "Rua das Indústrias, 100",
                Cidade: "Recife",
                Uf: "PE",
                PrazoEntregaPadrao: 5,
                CondicoesPagamento: "28 dias",
                Observacoes: "Fornecedor homologado"
            ))
        };
        reqForn1.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var resForn1 = await _client.SendAsync(reqForn1);
        Assert.Equal(HttpStatusCode.Created, resForn1.StatusCode);
        var forn1 = await resForn1.Content.ReadFromJsonAsync<FornecedorDto>();
        Assert.NotNull(forn1);

        var reqForn2 = new HttpRequestMessage(HttpMethod.Post, "/api/fornecedores")
        {
            Content = JsonContent.Create(new SaveFornecedorRequest(
                RazaoSocial: "Comercial Beta S/A",
                NomeFantasia: "Beta Alimentos",
                Cnpj: "44555666000199",
                InscricaoEstadual: "00445566",
                Email: "vendas@beta.com.br",
                Telefone: "(81) 3333-2222",
                Whatsapp: "(81) 98888-2222",
                Contato: "Ana",
                Endereco: "Av. Comercial, 200",
                Cidade: "Recife",
                Uf: "PE",
                PrazoEntregaPadrao: 7,
                CondicoesPagamento: "30 dias",
                Observacoes: "Fornecedor secundário"
            ))
        };
        reqForn2.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var resForn2 = await _client.SendAsync(reqForn2);
        Assert.Equal(HttpStatusCode.Created, resForn2.StatusCode);
        var forn2 = await resForn2.Content.ReadFromJsonAsync<FornecedorDto>();
        Assert.NotNull(forn2);

        // 3. Cadastra 2 produtos
        var reqProd1 = new HttpRequestMessage(HttpMethod.Post, "/api/produtos")
        {
            Content = JsonContent.Create(new CreateProdutoRequest(
                CodigoErp: "P-101",
                Descricao: "Arroz Parboilizado 5kg",
                CodigoBarras: "789111111101",
                Unidade: "FD",
                Grupo: "Alimentos",
                Categoria: "Grãos",
                PrecoVenda: 30.00m,
                CustoAtual: 22.00m,
                EstoqueAtual: 10m,
                EstoqueMinimo: 50m,
                FornecedorVinculadoId: forn1.Id
            ))
        };
        reqProd1.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var resProd1 = await _client.SendAsync(reqProd1);
        Assert.Equal(HttpStatusCode.Created, resProd1.StatusCode);
        var prod1 = await resProd1.Content.ReadFromJsonAsync<ProdutoDto>();
        Assert.NotNull(prod1);

        var reqProd2 = new HttpRequestMessage(HttpMethod.Post, "/api/produtos")
        {
            Content = JsonContent.Create(new CreateProdutoRequest(
                CodigoErp: "P-102",
                Descricao: "Óleo Composto 900ml",
                CodigoBarras: "789111111102",
                Unidade: "CX",
                Grupo: "Alimentos",
                Categoria: "Óleos",
                PrecoVenda: 9.00m,
                CustoAtual: 6.50m,
                EstoqueAtual: 5m,
                EstoqueMinimo: 40m,
                FornecedorVinculadoId: forn2.Id
            ))
        };
        reqProd2.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var resProd2 = await _client.SendAsync(reqProd2);
        Assert.Equal(HttpStatusCode.Created, resProd2.StatusCode);
        var prod2 = await resProd2.Content.ReadFromJsonAsync<ProdutoDto>();
        Assert.NotNull(prod2);

        // 4. Cria uma cotação para ambos os fornecedores
        var reqCotacao = new HttpRequestMessage(HttpMethod.Post, "/api/cotacoes")
        {
            Content = JsonContent.Create(new CriarCotacaoRequest(
                Titulo: "Reposição Semanal Grãos e Óleos",
                Observacoes: "Cotação urgente",
                PrazoRespostaDias: 3,
                Itens:
                [
                    new(prod1.Id, 100m),
                    new(prod2.Id, 50m)
                ],
                FornecedorIds: [forn1.Id, forn2.Id]
            ))
        };
        reqCotacao.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var resCotacao = await _client.SendAsync(reqCotacao);
        Assert.Equal(HttpStatusCode.OK, resCotacao.StatusCode);
        var cotacaoCriada = await resCotacao.Content.ReadFromJsonAsync<CotacaoCriadaDto>();
        Assert.NotNull(cotacaoCriada);
        Assert.Equal(2, cotacaoCriada.Acessos.Count);

        var acessoForn1 = cotacaoCriada.Acessos.First(f => f.FornecedorId == forn1.Id);
        var acessoForn2 = cotacaoCriada.Acessos.First(f => f.FornecedorId == forn2.Id);

        var tokenPortal1 = acessoForn1.Link.Split('/').Last();
        var tokenPortal2 = acessoForn2.Link.Split('/').Last();

        // 5. Fornecedor 1 acessa o portal com Token + Código e envia proposta
        var loginPortal1 = await _client.PostAsJsonAsync($"/api/portal/cotacao/{tokenPortal1}/acessar", new AcessarPortalRequest(acessoForn1.Codigo));
        Assert.Equal(HttpStatusCode.OK, loginPortal1.StatusCode);
        var sessao1 = await loginPortal1.Content.ReadFromJsonAsync<AcessarPortalResponse>();
        Assert.NotNull(sessao1);

        // Obtém detalhes dos itens do portal
        var reqDetalhesPortal1 = new HttpRequestMessage(HttpMethod.Get, "/api/portal/cotacao");
        reqDetalhesPortal1.Headers.Authorization = new AuthenticationHeaderValue("Bearer", sessao1.SessaoToken);
        var resDetalhes1 = await _client.SendAsync(reqDetalhesPortal1);
        Assert.Equal(HttpStatusCode.OK, resDetalhes1.StatusCode);
        var portalDados1 = await resDetalhes1.Content.ReadFromJsonAsync<PortalCotacaoDto>();
        Assert.NotNull(portalDados1);
        Assert.Equal(2, portalDados1.Itens.Count);

        var item1Id = portalDados1.Itens[0].Id;
        var item2Id = portalDados1.Itens[1].Id;

        // Envia proposta do Fornecedor 1:
        // P1: R$ 20.00 unitário (abaixo do custo de 22.00)
        // P2: R$ 6.80 unitário
        var reqEnvio1 = new HttpRequestMessage(HttpMethod.Post, "/api/portal/cotacao/resposta/enviar")
        {
            Content = JsonContent.Create(new SalvarRespostaRequest(
                Frete: 50.00m,
                OutrasDespesas: 0m,
                PrazoEntregaDias: 4,
                CondicaoPagamento: "30 dias direto",
                ValidadeProposta: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(15)),
                ContatoNome: "Carlos",
                Observacoes: "Pronta entrega",
                Itens:
                [
                    new(item1Id, true, 100m, 20.00m, 0m, "000", "5102", 12m, 0m, 0m, 0m, 0m, "Sem IPI"),
                    new(item2Id, true, 50m, 6.80m, 0m, "000", "5102", 12m, 0m, 0m, 0m, 0m, "Preço à vista")
                ]
            ))
        };
        reqEnvio1.Headers.Authorization = new AuthenticationHeaderValue("Bearer", sessao1.SessaoToken);
        var resEnvio1 = await _client.SendAsync(reqEnvio1);
        Assert.Equal(HttpStatusCode.OK, resEnvio1.StatusCode);

        // 6. Fornecedor 2 acessa o portal e envia proposta:
        // P1: R$ 21.50
        // P2: R$ 5.90 (mais barato que o Fornecedor 1)
        var loginPortal2 = await _client.PostAsJsonAsync($"/api/portal/cotacao/{tokenPortal2}/acessar", new AcessarPortalRequest(acessoForn2.Codigo));
        Assert.Equal(HttpStatusCode.OK, loginPortal2.StatusCode);
        var sessao2 = await loginPortal2.Content.ReadFromJsonAsync<AcessarPortalResponse>();
        Assert.NotNull(sessao2);

        var reqEnvio2 = new HttpRequestMessage(HttpMethod.Post, "/api/portal/cotacao/resposta/enviar")
        {
            Content = JsonContent.Create(new SalvarRespostaRequest(
                Frete: 0m,
                OutrasDespesas: 0m,
                PrazoEntregaDias: 6,
                CondicaoPagamento: "28/45 dias",
                ValidadeProposta: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(15)),
                ContatoNome: "Ana",
                Observacoes: "Frete grátis",
                Itens:
                [
                    new(item1Id, true, 100m, 21.50m, 0m, "000", "5102", 12m, 0m, 0m, 0m, 0m, null),
                    new(item2Id, true, 50m, 5.90m, 0m, "000", "5102", 12m, 0m, 0m, 0m, 0m, null)
                ]
            ))
        };
        reqEnvio2.Headers.Authorization = new AuthenticationHeaderValue("Bearer", sessao2.SessaoToken);
        var resEnvio2 = await _client.SendAsync(reqEnvio2);
        Assert.Equal(HttpStatusCode.OK, resEnvio2.StatusCode);

        // 7. O Comprador consulta a Comparação Inteligente (Fase 5)
        var reqComp = new HttpRequestMessage(HttpMethod.Get, $"/api/cotacoes/{cotacaoCriada.Id}/comparacao");
        reqComp.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var resComp = await _client.SendAsync(reqComp);
        Assert.Equal(HttpStatusCode.OK, resComp.StatusCode);

        var comparativo = await resComp.Content.ReadFromJsonAsync<ComparacaoDto>();
        Assert.NotNull(comparativo);
        Assert.True(comparativo.TemPropostas);
        Assert.Equal(2, comparativo.Fornecedores.Count);
        Assert.Equal(2, comparativo.Itens.Count);

        // 8. O Comprador aprova a cotação dividindo por melhor item ou selecionando um fornecedor
        var escolhas = new Dictionary<Guid, Guid>
        {
            [item1Id] = acessoForn1.CotacaoFornecedorId,
            [item2Id] = acessoForn2.CotacaoFornecedorId
        };

        var reqAprov = new HttpRequestMessage(HttpMethod.Post, $"/api/cotacoes/{cotacaoCriada.Id}/aprovar")
        {
            Content = JsonContent.Create(new AprovarCotacaoRequest(
                Escolhas: escolhas,
                Observacao: "Aprovado com a melhor combinação de custos por item."
            ))
        };
        reqAprov.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var resAprov = await _client.SendAsync(reqAprov);
        Assert.Equal(HttpStatusCode.OK, resAprov.StatusCode);

        // 13. [FASE 6] Verifica que os Pedidos de Compra foram gerados automaticamente
        var reqListPedidos = new HttpRequestMessage(HttpMethod.Get, "/api/pedidos");
        reqListPedidos.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var resListPedidos = await _client.SendAsync(reqListPedidos);
        Assert.Equal(HttpStatusCode.OK, resListPedidos.StatusCode);
        var pedidos = await resListPedidos.Content.ReadFromJsonAsync<List<System.Text.Json.JsonElement>>();
        Assert.NotNull(pedidos);
        Assert.NotEmpty(pedidos);

        var primeiroPedidoId = pedidos[0].GetProperty("id").GetString()!;

        // 14. [FASE 6] Consulta detalhe do pedido e itens
        var reqDetPedido = new HttpRequestMessage(HttpMethod.Get, $"/api/pedidos/{primeiroPedidoId}");
        reqDetPedido.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var resDetPedido = await _client.SendAsync(reqDetPedido);
        Assert.Equal(HttpStatusCode.OK, resDetPedido.StatusCode);

        // 15. [FASE 6] Avança o status do pedido na linha do tempo
        var reqStatus = new HttpRequestMessage(HttpMethod.Put, $"/api/pedidos/{primeiroPedidoId}/status")
        {
            Content = JsonContent.Create(new { NovoStatus = "Confirmado", Descricao = "Fornecedor confirmou o pedido via WhatsApp" })
        };
        reqStatus.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var resStatus = await _client.SendAsync(reqStatus);
        Assert.Equal(HttpStatusCode.OK, resStatus.StatusCode);

        // 16. [FASE 6] Verifica visualização de impressão (PDF/HTML)
        var reqPrint = new HttpRequestMessage(HttpMethod.Get, $"/api/pedidos/{primeiroPedidoId}/imprimir");
        reqPrint.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var resPrint = await _client.SendAsync(reqPrint);
        Assert.Equal(HttpStatusCode.OK, resPrint.StatusCode);
        var htmlPrint = await resPrint.Content.ReadAsStringAsync();
        Assert.Contains("Pedido de Compra", htmlPrint);

        // 17. [FASE 6] Consulta histórico de compras e indicadores
        var reqHist = new HttpRequestMessage(HttpMethod.Get, "/api/historico/compras");
        reqHist.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var resHist = await _client.SendAsync(reqHist);
        Assert.Equal(HttpStatusCode.OK, resHist.StatusCode);
    }

    [Fact]
    public async Task Agente_ConsegueAutenticar_E_SincronizarDados()
    {
        var (token, tenantId) = await RegistrarTenantAsync("RedeSuperAgente");

        // 1. Gera Chave para o Agente Local
        var reqGenKey = new HttpRequestMessage(HttpMethod.Post, "/api/integration/agents")
        {
            Content = JsonContent.Create(new GenerateAgentKeyRequest("Agente Servidor Loja 01"))
        };
        reqGenKey.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var resGenKey = await _client.SendAsync(reqGenKey);
        Assert.Equal(HttpStatusCode.OK, resGenKey.StatusCode);
        var agentKeyInfo = await resGenKey.Content.ReadFromJsonAsync<AgentKeyResponse>();
        Assert.NotNull(agentKeyInfo);

        // 2. Agente envia Heartbeat usando header X-Agent-Key
        var reqHeartbeat = new HttpRequestMessage(HttpMethod.Post, "/api/agent/v1/heartbeat")
        {
            Content = JsonContent.Create(new AgentHeartbeatRequest(
                Versao: "2.0.0-win",
                NomeMaquina: "SRV-LOJA-MATRIZ",
                Ambiente: null
            ))
        };
        reqHeartbeat.Headers.Add("X-Agent-Key", agentKeyInfo.ChaveAtivacao);
        var resHeartbeat = await _client.SendAsync(reqHeartbeat);
        Assert.Equal(HttpStatusCode.OK, resHeartbeat.StatusCode);

        // 3. Agente sincroniza produtos
        var reqSyncProd = new HttpRequestMessage(HttpMethod.Post, "/api/agent/v1/sync/produtos")
        {
            Content = JsonContent.Create(new SyncBatch<ProdutoSync>(
            [
                new(
                    CodigoErp: "FB-PROD-99",
                    Descricao: "Biscoito Cream Cracker 400g",
                    CodigoBarras: "7899990001",
                    Unidade: "PCT",
                    Grupo: "Biscoitos",
                    Categoria: "Mercearia",
                    Ncm: "1905.90.90",
                    PrecoVenda: 5.50m,
                    CustoAtual: 3.80m,
                    FornecedorErpId: null,
                    Consumo90Dias: 120m,
                    UltimoCusto: 3.75m,
                    UltimaCompraEm: DateTime.UtcNow.AddDays(-10),
                    QuantidadeUltimaCompra: 50m,
                    UltimoFornecedorErpId: null
                )
            ]))
        };
        reqSyncProd.Headers.Add("X-Agent-Key", agentKeyInfo.ChaveAtivacao);
        var resSyncProd = await _client.SendAsync(reqSyncProd);
        Assert.Equal(HttpStatusCode.OK, resSyncProd.StatusCode);
        var syncResult = await resSyncProd.Content.ReadFromJsonAsync<SyncResult>();
        Assert.NotNull(syncResult);
        Assert.Equal(1, syncResult.Criados);
    }
}
