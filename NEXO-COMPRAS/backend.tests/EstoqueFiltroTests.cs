using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Nexo.Api.Data;
using Nexo.Api.DTOs;
using Nexo.Api.Models;
using Nexo.Api.Services;
using Xunit;

namespace Nexo.Api.Tests;

public class EstoqueFiltroTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public EstoqueFiltroTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NexoDbContext>();
        DbInitializer.InitializeAsync(db).GetAwaiter().GetResult();
    }

    private async Task<(string token, Guid tenantId)> CriarTenantAsync(string prefixo)
    {
        var cnpj = $"{Random.Shared.Next(10, 99):D2}{Random.Shared.Next(100, 999):D3}{Random.Shared.Next(100, 999):D3}0001{Random.Shared.Next(10, 99):D2}";
        var req = new RegisterTenantRequest(
            RazaoSocial: $"{prefixo} Varejo e Logistica Ltda",
            NomeFantasia: $"{prefixo} Estoque",
            Cnpj: cnpj,
            Email: $"contato@{prefixo.ToLower()}{Random.Shared.Next(100, 999)}.com.br",
            Telefone: "(11) 3333-4444",
            AdminNome: $"Admin {prefixo}",
            AdminEmail: $"admin@{prefixo.ToLower()}{Random.Shared.Next(1000, 9999)}.com.br",
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
    public void RegraEstoqueService_Classifica_Corretamente_Cenarios_Obrigatorios()
    {
        var cfg = new ConfiguracaoTenant
        {
            LeadTimePadraoDias = 5,
            DiasSeguranca = 3,
            DiasCobertura = 15
        };

        // Cenário 1: Produto A - Estoque 0 -> Zerado
        var prodA = new Produto { Descricao = "Produto A", EstoqueAtual = 0m, EstoqueMinimo = 10m, EstoqueCritico = 3m, Consumo90Dias = 90m };
        var avalA = EstoqueService.Avaliar(prodA, cfg, null);
        Assert.Equal(NivelEstoque.Zerado, avalA.Nivel);

        // Cenário 2: Produto B - Estoque 1 -> Crítico (dentro do limite crítico configurado = 3)
        var prodB = new Produto { Descricao = "Produto B", EstoqueAtual = 1m, EstoqueMinimo = 10m, EstoqueCritico = 3m, Consumo90Dias = 90m };
        var avalB = EstoqueService.Avaliar(prodB, cfg, null);
        Assert.Equal(NivelEstoque.Critico, avalB.Nivel);

        // Cenário 3: Produto C - Estoque 5 -> Baixo (acima do crítico 3 e <= mínimo 10)
        var prodC = new Produto { Descricao = "Produto C", EstoqueAtual = 5m, EstoqueMinimo = 10m, EstoqueCritico = 3m, Consumo90Dias = 90m };
        var avalC = EstoqueService.Avaliar(prodC, cfg, null);
        Assert.Equal(NivelEstoque.Baixo, avalC.Nivel);

        // Cenário 4: Produto D - Estoque 50 -> Normal (acima do mínimo 10)
        var prodD = new Produto { Descricao = "Produto D", EstoqueAtual = 50m, EstoqueMinimo = 10m, EstoqueCritico = 3m, Consumo90Dias = 90m };
        var avalD = EstoqueService.Avaliar(prodD, cfg, null);
        Assert.Equal(NivelEstoque.Normal, avalD.Nivel);

        // Cenário 5: Produto E - Estoque NULL -> Indeterminado (desconhecido, NÃO pode virar zero nem falso zerado)
        var prodE = new Produto { Descricao = "Produto E", EstoqueAtual = null, EstoqueMinimo = 10m, EstoqueCritico = 3m, Consumo90Dias = 90m };
        var avalE = EstoqueService.Avaliar(prodE, cfg, null);
        Assert.Equal(NivelEstoque.Indeterminado, avalE.Nivel);
    }

    [Fact]
    public async Task ApiProdutos_FiltroEstoqueBaixo_ExibeApenas_Zerado_Critico_E_Baixo_InclusiveZeroReal()
    {
        var (token, tenantId) = await CriarTenantAsync("EmpresaEstoqueTest");

        // Cadastra os 5 produtos na API para a empresa
        var prodsParaCriar = new[]
        {
            new CreateProdutoRequest("PRD-A", "Produto A Zerado", null, "UN", "Geral", "Geral", 10m, 5m, 0m, 10m, null, 3m),
            new CreateProdutoRequest("PRD-B", "Produto B Critico", null, "UN", "Geral", "Geral", 10m, 5m, 1m, 10m, null, 3m),
            new CreateProdutoRequest("PRD-C", "Produto C Baixo", null, "UN", "Geral", "Geral", 10m, 5m, 5m, 10m, null, 3m),
            new CreateProdutoRequest("PRD-D", "Produto D Normal", null, "UN", "Geral", "Geral", 10m, 5m, 50m, 10m, null, 3m),
            new CreateProdutoRequest("PRD-E", "Produto E Indeterminado", null, "UN", "Geral", "Geral", 10m, 5m, null, 10m, null, 3m)
        };

        foreach (var p in prodsParaCriar)
        {
            var reqCriar = new HttpRequestMessage(HttpMethod.Post, "/api/produtos")
            {
                Content = JsonContent.Create(p)
            };
            reqCriar.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var resCriar = await _client.SendAsync(reqCriar);
            Assert.Equal(HttpStatusCode.Created, resCriar.StatusCode);
        }

        // 1. Listagem sem filtro (todos os 5 produtos devem vir)
        var reqTodos = new HttpRequestMessage(HttpMethod.Get, "/api/produtos?page=1&size=50");
        reqTodos.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var resTodos = await _client.SendAsync(reqTodos);
        Assert.Equal(HttpStatusCode.OK, resTodos.StatusCode);
        var dataTodos = await resTodos.Content.ReadFromJsonAsync<ProdutosListResponse>();
        Assert.NotNull(dataTodos);
        Assert.Equal(5, dataTodos.Total);

        // 2. FILTRO ATIVO: "Apenas produtos em nível Baixo, Crítico ou Zerado" (estoqueBaixo=true)
        var reqAlerta = new HttpRequestMessage(HttpMethod.Get, "/api/produtos?estoqueBaixo=true&page=1&size=50");
        reqAlerta.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var resAlerta = await _client.SendAsync(reqAlerta);
        Assert.Equal(HttpStatusCode.OK, resAlerta.StatusCode);
        var dataAlerta = await resAlerta.Content.ReadFromJsonAsync<ProdutosListResponse>();
        Assert.NotNull(dataAlerta);

        // Deve conter exatamente Produto A (Zerado), Produto B (Crítico) e Produto C (Baixo)
        Assert.Equal(3, dataAlerta.Total);
        var codigosAlerta = dataAlerta.Itens.Select(x => x.CodigoErp).ToList();
        Assert.Contains("PRD-A", codigosAlerta);
        Assert.Contains("PRD-B", codigosAlerta);
        Assert.Contains("PRD-C", codigosAlerta);
        Assert.DoesNotContain("PRD-D", codigosAlerta); // Normal NÃO pode aparecer
        Assert.DoesNotContain("PRD-E", codigosAlerta); // Indeterminado (NULL) NÃO pode aparecer como zerado!

        // Valida que o Produto A (com EstoqueAtual == 0) veio expressamente como Zerado
        var itemA = dataAlerta.Itens.First(x => x.CodigoErp == "PRD-A");
        Assert.Equal(0m, itemA.EstoqueAtual);
        Assert.Equal(NivelEstoque.Zerado, itemA.NivelEstoque);

        // 3. Teste de Busca por nome/código COM o filtro ativo
        var reqBuscaComFiltro = new HttpRequestMessage(HttpMethod.Get, "/api/produtos?q=PRD-A&estoqueBaixo=true");
        reqBuscaComFiltro.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var resBusca = await _client.SendAsync(reqBuscaComFiltro);
        var dataBusca = await resBusca.Content.ReadFromJsonAsync<ProdutosListResponse>();
        Assert.NotNull(dataBusca);
        Assert.Single(dataBusca.Itens);
        Assert.Equal("PRD-A", dataBusca.Itens[0].CodigoErp);

        // 4. Teste de Paginação com filtro ativo (page=1, size=2)
        var reqPag = new HttpRequestMessage(HttpMethod.Get, "/api/produtos?estoqueBaixo=true&page=1&size=2");
        reqPag.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var resPag = await _client.SendAsync(reqPag);
        var dataPag = await resPag.Content.ReadFromJsonAsync<ProdutosListResponse>();
        Assert.NotNull(dataPag);
        Assert.Equal(3, dataPag.Total); // total continua 3
        Assert.Equal(2, dataPag.Itens.Count); // página retorna 2
    }

    [Fact]
    public async Task IsolamentoMultiTenant_FiltroEstoqueNaoMisturaDadosDeOutrasEmpresas()
    {
        var (token1, tenant1) = await CriarTenantAsync("SuperLojaAlpha");
        var (token2, tenant2) = await CriarTenantAsync("SuperLojaBeta");

        // Cria produto zerado no Tenant 1
        var reqP1 = new HttpRequestMessage(HttpMethod.Post, "/api/produtos")
        {
            Content = JsonContent.Create(new CreateProdutoRequest("ALPHA-ZERO", "Item Zerado Alpha", null, "UN", "G", "C", 10m, 5m, 0m, 10m, null))
        };
        reqP1.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token1);
        await _client.SendAsync(reqP1);

        // Tenant 2 consulta com filtro de estoque baixo ativo
        var reqBeta = new HttpRequestMessage(HttpMethod.Get, "/api/produtos?estoqueBaixo=true");
        reqBeta.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token2);
        var resBeta = await _client.SendAsync(reqBeta);
        var dataBeta = await resBeta.Content.ReadFromJsonAsync<ProdutosListResponse>();
        Assert.NotNull(dataBeta);

        // Tenant 2 NÃO pode ver o produto zerado do Tenant 1
        Assert.DoesNotContain(dataBeta.Itens, x => x.CodigoErp == "ALPHA-ZERO");
    }
}
