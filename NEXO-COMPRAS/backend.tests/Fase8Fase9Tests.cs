using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Nexo.Api.DTOs;
using Nexo.Api.Models;
using Xunit;

namespace Nexo.Api.Tests;

public class Fase8Fase9Tests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public Fase8Fase9Tests(WebApplicationFactory<Program> factory)
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
            RazaoSocial: $"{prefixo} Varejo SA",
            NomeFantasia: $"{prefixo} Rede",
            Cnpj: cnpjGerado,
            Email: $"contato@{prefixo.ToLower()}{Random.Shared.Next(100, 999)}.com.br",
            Telefone: "(81) 3456-7890",
            AdminNome: $"Diretor {prefixo}",
            AdminEmail: $"admin@{prefixo.ToLower()}{Random.Shared.Next(10000, 99999)}.com.br",
            AdminSenha: "SenhaForte@2026",
            PlanoId: null
        );

        var response = await _client.PostAsJsonAsync("/api/auth/register-tenant", req);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var loginRes = await response.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(loginRes);
        Assert.NotNull(loginRes.Tenant);

        return (loginRes.Token, loginRes.Tenant.Id);
    }

    [Fact]
    public async Task Fase8_Planos_Assinatura_Checkout_Fatura_E_Webhooks_FuncionamPerfeitamente()
    {
        var (token, tenantId) = await RegistrarTenantAsync("SuperMercadosNorte");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // 1. Listar planos disponíveis
        var resPlanos = await _client.GetAsync("/api/assinaturas/planos");
        Assert.Equal(HttpStatusCode.OK, resPlanos.StatusCode);
        var planos = await resPlanos.Content.ReadFromJsonAsync<List<Plano>>();
        Assert.NotNull(planos);
        Assert.NotEmpty(planos);
        var planoPro = planos.FirstOrDefault(p => p.Nome.Contains("Profissional") || p.Nome.Contains("Pro")) ?? planos.First();

        // 2. Consultar assinatura atual (deve vir em teste de 14 dias ou ativa)
        var resAssinatura = await _client.GetAsync("/api/assinaturas/minha");
        Assert.Equal(HttpStatusCode.OK, resAssinatura.StatusCode);
        var assinatura = await resAssinatura.Content.ReadFromJsonAsync<AssinaturaDetalheDto>();
        Assert.NotNull(assinatura);
        Assert.True(assinatura.DiasRestantesTeste > 0 || assinatura.Status == "Teste" || assinatura.Status == "Ativa");

        // 3. Fazer Checkout para o plano selecionado (Anual com Pix)
        var checkoutReq = new CheckoutAssinaturaRequest(
            PlanoId: planoPro.Id,
            Ciclo: "Anual",
            FormaPagamento: "Pix"
        );
        var resCheckout = await _client.PostAsJsonAsync("/api/assinaturas/checkout", checkoutReq);
        Assert.Equal(HttpStatusCode.OK, resCheckout.StatusCode);
        var faturaCheckout = await resCheckout.Content.ReadFromJsonAsync<FaturaDto>();
        Assert.NotNull(faturaCheckout);
        Assert.True(faturaCheckout.Valor > 0);
        Assert.NotEmpty(faturaCheckout.Numero);

        // 4. Listar faturas
        var resFaturas = await _client.GetAsync("/api/assinaturas/faturas");
        Assert.Equal(HttpStatusCode.OK, resFaturas.StatusCode);
        var faturas = await resFaturas.Content.ReadFromJsonAsync<List<FaturaDto>>();
        Assert.NotNull(faturas);
        Assert.NotEmpty(faturas);

        // 5. Pagar a fatura gerada
        var faturaId = faturaCheckout.Id;
        var resPagar = await _client.PostAsJsonAsync($"/api/assinaturas/faturas/{faturaId}/pagar", new PagarFaturaSimuladaRequest("Pix"));
        Assert.Equal(HttpStatusCode.OK, resPagar.StatusCode);

        // Verifica se a assinatura agora está Ativa
        var resAssinaturaAtiva = await _client.GetAsync("/api/assinaturas/minha");
        var assinaturaAtiva = await resAssinaturaAtiva.Content.ReadFromJsonAsync<AssinaturaDetalheDto>();
        Assert.NotNull(assinaturaAtiva);
        Assert.Equal("Ativa", assinaturaAtiva.Status);

        // 6. Teste de Webhook - Inadimplência
        var webhookInadimplente = new WebhookPagamentoPayload(
            Evento: "fatura.falha",
            TransacaoId: "tx_mock_123",
            FaturaNumero: faturaCheckout.Numero,
            Valor: faturaCheckout.Valor,
            Status: "Falha",
            Timestamp: DateTime.UtcNow
        );
        var resWebhookFalha = await _client.PostAsJsonAsync("/api/webhooks/pagamentos", webhookInadimplente);
        Assert.Equal(HttpStatusCode.OK, resWebhookFalha.StatusCode);

        // Verifica se a assinatura passou para Inadimplente
        var resAssinaturaInad = await _client.GetAsync("/api/assinaturas/minha");
        var assinaturaInad = await resAssinaturaInad.Content.ReadFromJsonAsync<AssinaturaDetalheDto>();
        Assert.NotNull(assinaturaInad);
        Assert.Equal("Inadimplente", assinaturaInad.Status);

        // 7. Teste de Webhook - Pagamento e Reativação
        var webhookPaga = new WebhookPagamentoPayload(
            Evento: "fatura.paga",
            TransacaoId: "tx_mock_456",
            FaturaNumero: faturaCheckout.Numero,
            Valor: faturaCheckout.Valor,
            Status: "Paga",
            Timestamp: DateTime.UtcNow
        );
        var resWebhookPaga = await _client.PostAsJsonAsync("/api/webhooks/pagamentos", webhookPaga);
        Assert.Equal(HttpStatusCode.OK, resWebhookPaga.StatusCode);

        var resAssinaturaReativada = await _client.GetAsync("/api/assinaturas/minha");
        var assinaturaReativada = await resAssinaturaReativada.Content.ReadFromJsonAsync<AssinaturaDetalheDto>();
        Assert.NotNull(assinaturaReativada);
        Assert.Equal("Ativa", assinaturaReativada.Status);
    }

    [Fact]
    public async Task Fase9_MultiplasFiliais_EstoqueIndividual_Transferencias_E_Consolidado()
    {
        var (token, tenantId) = await RegistrarTenantAsync("RedeSuperMix");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // 1. Cadastra Filial 01 (permitida dentro do plano inicial Start que tem MaxFiliais = 1)
        var reqFilial1 = new SalvarFilialRequest(
            Codigo: "FILIAL-01",
            Nome: "Filial Boa Viagem",
            Cnpj: "12345678000299",
            Endereco: "Av. Boa Viagem, 1000",
            Cidade: "Recife",
            Uf: "PE",
            Telefone: "(81) 3000-0002",
            Email: "boaviagem@supermix.com.br",
            Responsavel: "Carlos Gerente",
            AgenteId: null
        );

        var resFilial1 = await _client.PostAsJsonAsync("/api/filiais", reqFilial1);
        Assert.Equal(HttpStatusCode.Created, resFilial1.StatusCode);
        var filial1 = await resFilial1.Content.ReadFromJsonAsync<FilialDto>();
        Assert.NotNull(filial1);
        Assert.Equal("FILIAL-01", filial1.Codigo);

        // Tentativa de cadastrar 2ª filial no plano Start deve ser barrada pelo limite do plano!
        var reqFilialMatriz = new SalvarFilialRequest(
            Codigo: "MATRIZ-01",
            Nome: "Centro de Distribuição Matriz",
            Cnpj: "12345678000199",
            Endereco: "Rodovia BR-101 Sul, Km 20",
            Cidade: "Recife",
            Uf: "PE",
            Telefone: "(81) 3000-0001",
            Email: "matriz@supermix.com.br",
            Responsavel: "Diretoria",
            AgenteId: null
        );
        var resMatrizBloqueada = await _client.PostAsJsonAsync("/api/filiais", reqFilialMatriz);
        Assert.Equal(HttpStatusCode.BadRequest, resMatrizBloqueada.StatusCode);

        // Agora fazemos upgrade para o Plano Profissional (que permite até 3 filiais)
        var resPlanos = await _client.GetAsync("/api/assinaturas/planos");
        var planos = await resPlanos.Content.ReadFromJsonAsync<List<Plano>>();
        var planoPro = planos!.First(p => p.MaxFiliais >= 3);

        var resUpgrade = await _client.PostAsJsonAsync("/api/assinaturas/checkout", new CheckoutAssinaturaRequest(
            PlanoId: planoPro.Id,
            Ciclo: "Mensal",
            FormaPagamento: "Cartao"
        ));
        Assert.Equal(HttpStatusCode.OK, resUpgrade.StatusCode);

        // Agora o cadastro da Matriz deve ser permitido com sucesso!
        var resMatriz = await _client.PostAsJsonAsync("/api/filiais", reqFilialMatriz);
        Assert.Equal(HttpStatusCode.Created, resMatriz.StatusCode);
        var filialMatriz = await resMatriz.Content.ReadFromJsonAsync<FilialDto>();
        Assert.NotNull(filialMatriz);

        // Listar filiais
        var resListaFiliais = await _client.GetAsync("/api/filiais");
        Assert.Equal(HttpStatusCode.OK, resListaFiliais.StatusCode);
        var listaFiliais = await resListaFiliais.Content.ReadFromJsonAsync<List<FilialDto>>();
        Assert.NotNull(listaFiliais);
        Assert.True(listaFiliais.Count >= 2);

        // 2. Criar um produto para teste de estoque entre filiais
        var prodReq = new CreateProdutoRequest(
            CodigoErp: "PROD-MULTI-01",
            Descricao: "Leite Integral UHT 1L",
            CodigoBarras: "7891234567890",
            Unidade: "CX",
            Grupo: "Laticínios",
            Categoria: "Alimentos",
            PrecoVenda: 6.90m,
            CustoAtual: 4.50m,
            EstoqueAtual: 200,
            EstoqueMinimo: 50,
            FornecedorVinculadoId: null
        );
        var resProd = await _client.PostAsJsonAsync("/api/produtos", prodReq);
        Assert.Equal(HttpStatusCode.Created, resProd.StatusCode);
        var prodCriado = await resProd.Content.ReadFromJsonAsync<ProdutoDto>();
        Assert.NotNull(prodCriado);

        // 3. Atualizar estoque individual da Matriz e da Filial 01
        var resEstoqueMatriz = await _client.PostAsJsonAsync($"/api/filiais/{filialMatriz.Id}/estoque", new AtualizarEstoqueFilialRequest(
            new List<AtualizarEstoqueItemInput> { new(prodCriado.Id, 200, 50, 0) }
        ));
        Assert.Equal(HttpStatusCode.OK, resEstoqueMatriz.StatusCode);

        var resAtualizaEstoqueFilial1 = await _client.PostAsJsonAsync($"/api/filiais/{filial1.Id}/estoque", new AtualizarEstoqueFilialRequest(
            new List<AtualizarEstoqueItemInput> { new(prodCriado.Id, 30, 40, 0) }
        ));
        Assert.Equal(HttpStatusCode.OK, resAtualizaEstoqueFilial1.StatusCode);

        // Consultar estoque da Filial 01
        var resEstoqueFilial1 = await _client.GetAsync($"/api/filiais/{filial1.Id}/estoque");
        Assert.Equal(HttpStatusCode.OK, resEstoqueFilial1.StatusCode);
        var itensEstoqueFilial1 = await resEstoqueFilial1.Content.ReadFromJsonAsync<List<EstoqueFilialDto>>();
        Assert.NotNull(itensEstoqueFilial1);
        var itemEstoque = itensEstoqueFilial1.FirstOrDefault(i => i.ProdutoId == prodCriado.Id);
        Assert.NotNull(itemEstoque);
        Assert.Equal(30, itemEstoque.EstoqueAtual);

        // 4. Criar solicitação de transferência da Matriz para a Filial 01
        var reqTransf = new CriarTransferenciaRequest(
            FilialOrigemId: filialMatriz.Id,
            FilialDestinoId: filial1.Id,
            Observacoes: "Reposição de emergência para final de semana",
            Itens: new List<ItemTransferenciaInput>
            {
                new(ProdutoId: prodCriado.Id, Quantidade: 25)
            }
        );
        var resTransf = await _client.PostAsJsonAsync("/api/transferencias", reqTransf);
        Assert.Equal(HttpStatusCode.OK, resTransf.StatusCode);
        var transfCriada = await resTransf.Content.ReadFromJsonAsync<SolicitacaoTransferenciaDto>();
        Assert.NotNull(transfCriada);
        Assert.Equal("Pendente", transfCriada.Status);

        // 5. Concluir a transferência e verificar atualização de estoque
        var resStatus = await _client.PutAsJsonAsync($"/api/transferencias/{transfCriada.Id}/status", new AtualizarStatusTransferenciaRequest(
            NovoStatus: "Concluida",
            Observacoes: "Recebido e conferido pela gerência da Filial 01"
        ));
        Assert.Equal(HttpStatusCode.OK, resStatus.StatusCode);

        // Conferir estoque atualizado na Filial 01 (tinha 30 + 25 = 55)
        var resEstoqueAposTransf = await _client.GetAsync($"/api/filiais/{filial1.Id}/estoque");
        var itensApos = await resEstoqueAposTransf.Content.ReadFromJsonAsync<List<EstoqueFilialDto>>();
        Assert.NotNull(itensApos);
        var itemApos = itensApos.First(i => i.ProdutoId == prodCriado.Id);
        Assert.Equal(55, itemApos.EstoqueAtual);

        // 6. Consultar Relatório Consolidado de Filiais
        var resConsolidado = await _client.GetAsync("/api/filiais/relatorio-consolidado");
        Assert.Equal(HttpStatusCode.OK, resConsolidado.StatusCode);
        var consolidado = await resConsolidado.Content.ReadFromJsonAsync<RelatorioConsolidadoFiliaisDto>();
        Assert.NotNull(consolidado);
        Assert.True(consolidado.TotalFiliais >= 2);
        Assert.NotEmpty(consolidado.Filiais);
    }
}
