using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Nexo.Api.DTOs;
using Xunit;

namespace Nexo.Api.Tests;

public class RelatoriosAndComparativoTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public RelatoriosAndComparativoTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    private async Task<string> RegistrarTenantAsync(string prefixo)
    {
        var randomNum = Random.Shared.Next(10000000, 99999999);
        var cnpjGerado = $"{randomNum:D8}0001{Random.Shared.Next(10, 99):D2}";

        var req = new RegisterTenantRequest(
            RazaoSocial: $"{prefixo} LTDA",
            NomeFantasia: $"{prefixo} Matriz",
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
        return login.Token;
    }

    [Fact]
    public async Task Relatorio_Endpoints_Devem_Retornar_Sucesso_Com_Estrutura_Correta()
    {
        var token = await RegistrarTenantAsync("AutoPecasSul");

        // 1. Relatório de Compras
        var reqCompras = new HttpRequestMessage(HttpMethod.Get, "/api/relatorios/compras");
        reqCompras.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var resCompras = await _client.SendAsync(reqCompras);
        Assert.Equal(HttpStatusCode.OK, resCompras.StatusCode);
        var jsonCompras = await resCompras.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(jsonCompras.TryGetProperty("kpis", out var kpisCompras));
        Assert.True(kpisCompras.TryGetProperty("totalComprado", out _));

        // 2. Relatório de Economia
        var reqEco = new HttpRequestMessage(HttpMethod.Get, "/api/relatorios/economia");
        reqEco.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var resEco = await _client.SendAsync(reqEco);
        Assert.Equal(HttpStatusCode.OK, resEco.StatusCode);
        var jsonEco = await resEco.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(jsonEco.TryGetProperty("kpis", out var kpisEco));
        Assert.True(kpisEco.TryGetProperty("economiaRealizada", out _));

        // 3. Relatório de Fornecedores
        var reqForn = new HttpRequestMessage(HttpMethod.Get, "/api/relatorios/fornecedores");
        reqForn.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var resForn = await _client.SendAsync(reqForn);
        Assert.Equal(HttpStatusCode.OK, resForn.StatusCode);
        var jsonForn = await resForn.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(jsonForn.TryGetProperty("ranking", out _));

        // 4. Relatório de Produtos
        var reqProd = new HttpRequestMessage(HttpMethod.Get, "/api/relatorios/produtos");
        reqProd.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var resProd = await _client.SendAsync(reqProd);
        Assert.Equal(HttpStatusCode.OK, resProd.StatusCode);
        var jsonProd = await resProd.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(jsonProd.TryGetProperty("produtos", out _));

        // 5. Relatório de Cotações
        var reqCot = new HttpRequestMessage(HttpMethod.Get, "/api/relatorios/cotacoes");
        reqCot.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var resCot = await _client.SendAsync(reqCot);
        Assert.Equal(HttpStatusCode.OK, resCot.StatusCode);
        var jsonCot = await resCot.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(jsonCot.TryGetProperty("kpis", out _));

        // 6. Comparativo de Pedidos no Histórico
        var reqComp = new HttpRequestMessage(HttpMethod.Get, "/api/historico/comparativo-pedidos");
        reqComp.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var resComp = await _client.SendAsync(reqComp);
        Assert.Equal(HttpStatusCode.OK, resComp.StatusCode);
        var jsonComp = await resComp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(jsonComp.TryGetProperty("comparativo", out _));
    }
}
