using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Nexo.Api.Data;
using Nexo.Api.DTOs;
using Nexo.Api.Models;
using Xunit;

namespace Nexo.Api.Tests;

[Collection("DatabaseCollection")]
public class SaasMultiTenantSlugAndPricingTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public SaasMultiTenantSlugAndPricingTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NexoDbContext>();
        DbInitializer.InitializeAsync(db).GetAwaiter().GetResult();
    }

    private async Task<string> LoginMasterAdminAsync()
    {
        var res = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest("juniosergio034@gmail.com", "34777126Pe@"));
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var data = await res.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(data);
        return data!.Token;
    }

    private async Task<(string Token, Guid TenantId, string Slug)> CriarEmpresaComSlugAsync(string nomeFantasia, string slugBase)
    {
        var sufixo = Random.Shared.Next(1000, 9999);
        var slugUnico = $"{slugBase}-{sufixo}";
        var cnpj = $"{Random.Shared.Next(10, 99):D2}{Random.Shared.Next(100, 999):D3}{Random.Shared.Next(100, 999):D3}0001{Random.Shared.Next(10, 99):D2}";

        var req = new RegisterTenantRequest(
            RazaoSocial: $"{nomeFantasia} {sufixo} LTDA",
            NomeFantasia: nomeFantasia,
            Cnpj: cnpj,
            Email: $"contato@{slugUnico}.com.br",
            Telefone: "(11) 99999-0000",
            AdminNome: $"Gestor {nomeFantasia}",
            AdminEmail: $"admin@{slugUnico}.com.br",
            AdminSenha: "SenhaForte@2026",
            PlanoId: null,
            Uf: "SP",
            Slug: slugUnico
        );

        var res = await _client.PostAsJsonAsync("/api/auth/register-tenant", req);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var login = await res.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(login);
        Assert.NotNull(login!.Tenant);
        Assert.Equal(slugUnico, login.Tenant!.Slug);

        return (login.Token, login.Tenant.Id, slugUnico);
    }

    [Fact]
    public async Task RotasPorSlug_Resolucao_AlemDoPao_PaoNorte_Inexistente_Suspenso_Reservadas()
    {
        var adminToken = await LoginMasterAdminAsync();

        // 1. Cria empresas Além do Pão e Pão Norte com seus slugs
        var (tokenAlem, idAlem, slugAlem) = await CriarEmpresaComSlugAsync("Além do Pão", "alemdopao");
        var (tokenNorte, idNorte, slugNorte) = await CriarEmpresaComSlugAsync("Pão Norte", "paonorte");

        // 2. Resolução pública de /alemdopao-* e /paonorte-*
        var resAlem = await _client.GetAsync($"/api/tenants/resolve/{slugAlem}");
        Assert.Equal(HttpStatusCode.OK, resAlem.StatusCode);
        using var docAlem = JsonDocument.Parse(await resAlem.Content.ReadAsStringAsync());
        Assert.Equal(slugAlem, docAlem.RootElement.GetProperty("slug").GetString());
        Assert.Equal("Além do Pão", docAlem.RootElement.GetProperty("nomeFantasia").GetString());

        var resNorte = await _client.GetAsync($"/api/tenants/resolve/{slugNorte}");
        Assert.Equal(HttpStatusCode.OK, resNorte.StatusCode);

        // 3. Slug inexistente deve retornar 404 SLUG_INEXISTENTE
        var resInexistente = await _client.GetAsync("/api/tenants/resolve/empresa-que-nao-existe-99999");
        Assert.Equal(HttpStatusCode.NotFound, resInexistente.StatusCode);
        using var docInex = JsonDocument.Parse(await resInexistente.Content.ReadAsStringAsync());
        Assert.Equal("SLUG_INEXISTENTE", docInex.RootElement.GetProperty("codigo").GetString());

        // 4. Rotas reservadas (/login, /admin, /planos, /api, /cadastro, /recuperar-senha) devem ser bloqueadas como slug
        foreach (var reservado in new[] { "login", "admin", "planos", "api", "cadastro", "recuperar-senha" })
        {
            var resRes = await _client.GetAsync($"/api/tenants/resolve/{reservado}");
            Assert.Equal(HttpStatusCode.BadRequest, resRes.StatusCode);
        }

        // 5. Suspensão da empresa Pão Norte -> deve retornar 403 EMPRESA_SUSPENSA e bloquear login
        var reqSuspender = new HttpRequestMessage(HttpMethod.Put, $"/api/admin/tenants/{idNorte}/status")
        {
            Content = JsonContent.Create(new UpdateTenantStatusRequest("Suspenso", "Suspensão de teste comercial"))
        };
        reqSuspender.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var resSusp = await _client.SendAsync(reqSuspender);
        Assert.Equal(HttpStatusCode.OK, resSusp.StatusCode);

        var resNorteSuspenso = await _client.GetAsync($"/api/tenants/resolve/{slugNorte}");
        Assert.Equal(HttpStatusCode.Forbidden, resNorteSuspenso.StatusCode);
        using var docSusp = JsonDocument.Parse(await resNorteSuspenso.Content.ReadAsStringAsync());
        Assert.Equal("EMPRESA_SUSPENSA", docSusp.RootElement.GetProperty("codigo").GetString());

        // 6. Alteração controlada do slug pelo Admin Geral
        var novoSlugAlem = $"alemdopao-matriz-{Random.Shared.Next(1000, 9999)}";
        var reqMudarSlug = new HttpRequestMessage(HttpMethod.Put, $"/api/admin/tenants/{idAlem}/slug")
        {
            Content = JsonContent.Create(new UpdateTenantSlugRequest(novoSlugAlem, "Padronização de domínio comercial"))
        };
        reqMudarSlug.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var resMudarSlug = await _client.SendAsync(reqMudarSlug);
        Assert.Equal(HttpStatusCode.OK, resMudarSlug.StatusCode);

        var resNovoSlug = await _client.GetAsync($"/api/tenants/resolve/{novoSlugAlem}");
        Assert.Equal(HttpStatusCode.OK, resNovoSlug.StatusCode);
    }

    [Fact]
    public async Task IsolamentoMultiTenant_DuasEmpresasComDadosSemelhantes_BloqueioTotalCruzado()
    {
        var (tokenA, idA, slugA) = await CriarEmpresaComSlugAsync("Auto Peças A", "autopecas-a");
        var (tokenB, idB, slugB) = await CriarEmpresaComSlugAsync("Auto Peças B", "autopecas-b");

        // Empresa A e Empresa B cadastram produtos com o MESMO código ERP ("PEC-001")
        var reqProdA = new HttpRequestMessage(HttpMethod.Post, "/api/produtos")
        {
            Content = JsonContent.Create(new CreateProdutoRequest("PEC-001", "Pastilha Freio Exclusiva Empresa A", "7890001", "JG", "Freios", "Auto", 120m, 70m, 15m, 5m, null))
        };
        reqProdA.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenA);
        reqProdA.Headers.Add("X-Tenant-Slug", slugA);
        var resProdA = await _client.SendAsync(reqProdA);
        Assert.Equal(HttpStatusCode.Created, resProdA.StatusCode);
        var prodA = await resProdA.Content.ReadFromJsonAsync<ProdutoDto>();
        Assert.NotNull(prodA);

        var reqProdB = new HttpRequestMessage(HttpMethod.Post, "/api/produtos")
        {
            Content = JsonContent.Create(new CreateProdutoRequest("PEC-001", "Pastilha Freio Exclusiva Empresa B", "7890001", "JG", "Freios", "Auto", 135m, 80m, 22m, 5m, null))
        };
        reqProdB.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenB);
        reqProdB.Headers.Add("X-Tenant-Slug", slugB);
        var resProdB = await _client.SendAsync(reqProdB);
        Assert.Equal(HttpStatusCode.Created, resProdB.StatusCode);

        // 1. Tentativa da Empresa B de acessar a rota operando com o X-Tenant-Slug da Empresa A -> 403 Forbidden
        var reqSpoofSlug = new HttpRequestMessage(HttpMethod.Get, "/api/produtos");
        reqSpoofSlug.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenB);
        reqSpoofSlug.Headers.Add("X-Tenant-Slug", slugA);
        var resSpoof = await _client.SendAsync(reqSpoofSlug);
        Assert.Equal(HttpStatusCode.Forbidden, resSpoof.StatusCode);

        // 2. Tentativa da Empresa B de acessar diretamente o ID do produto da Empresa A (IDOR) -> 404 NotFound
        var reqIdor = new HttpRequestMessage(HttpMethod.Get, $"/api/produtos/{prodA!.Id}");
        reqIdor.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenB);
        var resIdor = await _client.SendAsync(reqIdor);
        Assert.Equal(HttpStatusCode.NotFound, resIdor.StatusCode);

        // 3. Tentativa da Empresa A de acessar login na URL da Empresa B -> 403 Forbidden
        var resCrossLogin = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest($"admin@{slugA}.com.br", "SenhaForte@2026", slugB));
        Assert.Equal(HttpStatusCode.Forbidden, resCrossLogin.StatusCode);

        // 4. Tentativa de cliente acessar endpoints exclusivos do Admin Geral -> 403 Forbidden
        var reqAdminPricing = new HttpRequestMessage(HttpMethod.Get, "/api/admin/precificacao/tenants");
        reqAdminPricing.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenA);
        var resAdminPricing = await _client.SendAsync(reqAdminPricing);
        Assert.Equal(HttpStatusCode.Forbidden, resAdminPricing.StatusCode);
    }

    [Fact]
    public async Task PrecificacaoDinamica_TodasAsFaixas_Limites50a250_Descontos_Versionamento_SyncSemPenalidade()
    {
        var adminToken = await LoginMasterAdminAsync();

        async Task<SimulacaoPrecificacaoResponse> SimularAsync(SimulacaoPrecificacaoRequest r)
        {
            var msg = new HttpRequestMessage(HttpMethod.Post, "/api/admin/precificacao/simular")
            {
                Content = JsonContent.Create(r)
            };
            msg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
            var resp = await _client.SendAsync(msg);
            Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
            var dto = await resp.Content.ReadFromJsonAsync<SimulacaoPrecificacaoResponse>();
            Assert.NotNull(dto);
            return dto!;
        }

        // 1. Cenário Base Mínimo: até 1000 prod (+0), 1 filial (+0), 2 usuários (+0), 1 SINc (+0), Baixo (+0), Básico (+0) = R$ 50,00
        var c1 = await SimularAsync(new SimulacaoPrecificacaoRequest(800, 1, 2, 1, "Baixo", "Basico", 0, 0));
        Assert.Equal(50.00m, c1.MensalidadeCalculada);
        Assert.Equal(50.00m, c1.MensalidadeFinal);

        // 2. Cenário Intermediário:
        // Base 50 + Prod 2500 (+15) + Filiais 3 (+20) + Usuários 4 (+10) + SINc 2 (+10) + Médio (+10) + Relatórios (+15) = R$ 130,00
        var c2 = await SimularAsync(new SimulacaoPrecificacaoRequest(2500, 3, 4, 2, "Medio", "RelatoriosAvancados", 0, 0));
        Assert.Equal(15.00m, c2.AdicionalProdutos);
        Assert.Equal(20.00m, c2.AdicionalFiliais);
        Assert.Equal(10.00m, c2.AdicionalUsuarios);
        Assert.Equal(10.00m, c2.AdicionalSincronizadores);
        Assert.Equal(10.00m, c2.AdicionalVolume);
        Assert.Equal(15.00m, c2.AdicionalModulos);
        Assert.Equal(130.00m, c2.MensalidadeCalculada);
        Assert.Equal(130.00m, c2.MensalidadeFinal);

        // 3. Cenário Avançado:
        // Base 50 + Prod 5000 (+30) + Filiais 5 (+40) + Usuários 8 (+20) + SINc 4 (+20) + Alto (+25) + Completo (+30) = R$ 215,00
        var c3 = await SimularAsync(new SimulacaoPrecificacaoRequest(5000, 5, 8, 4, "Alto", "CompletoServicos", 15.00m, 0));
        Assert.Equal(215.00m, c3.MensalidadeCalculada);
        Assert.Equal(200.00m, c3.MensalidadeFinal); // 215 - 15 de desconto = 200

        // 4. Teto Máximo R$ 250,00:
        // Base 50 + Prod 12000 (+45) + Filiais 10 (+60) + Usuários 15 (+30) + SINc 6 (+20) + Alto (+25) + Completo (+30) = 260 -> Teto R$ 250,00
        var cMax = await SimularAsync(new SimulacaoPrecificacaoRequest(12000, 10, 15, 6, "Alto", "CompletoServicos", 0, 50m));
        Assert.Equal(250.00m, cMax.MensalidadeCalculada);
        Assert.Equal(250.00m, cMax.MensalidadeFinal);

        // 5. Piso Mínimo R$ 50,00 (mesmo com desconto maior, nunca cai abaixo de R$ 50,00):
        var cMin = await SimularAsync(new SimulacaoPrecificacaoRequest(500, 1, 1, 1, "Baixo", "Basico", 30.00m, -20.00m));
        Assert.Equal(50.00m, cMin.MensalidadeFinal);

        // 6. Aplicação de precificação, desconto justificado e histórico comercial em uma empresa real
        var (tokenEmp, idEmp, _) = await CriarEmpresaComSlugAsync("Supermercado Preço Justo", "precojusto");

        // Registra 50 sincronizações SEM alterações (Criados = 0, Atualizados = 0) -> NÃO pode penalizar o volume da empresa!
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NexoDbContext>();
            var ag = new Agente { TenantId = idEmp, Nome = "SINc Matriz", Status = "Online" };
            db.Agentes.Add(ag);
            for (int i = 0; i < 50; i++)
            {
                db.Sincronizacoes.Add(new Sincronizacao
                {
                    TenantId = idEmp,
                    AgenteId = ag.Id,
                    Tipo = "Produtos",
                    Recebidos = 1000,
                    Criados = 0,
                    Atualizados = 0,
                    Ignorados = 1000,
                    Status = "Sucesso"
                });
            }
            await db.SaveChangesAsync();
        }

        var reqDet = new HttpRequestMessage(HttpMethod.Get, $"/api/admin/precificacao/tenants/{idEmp}");
        reqDet.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var resDet = await _client.SendAsync(reqDet);
        Assert.Equal(HttpStatusCode.OK, resDet.StatusCode);
        var detDto = await resDet.Content.ReadFromJsonAsync<PrecificacaoTenantDetalheDto>();
        Assert.NotNull(detDto);
        Assert.Equal(0, detDto!.SincronizacoesEfetivasNoMes);
        Assert.Equal(50, detDto.SincronizacoesSemAlteracaoIgnoradas);
        Assert.Equal("Baixo", detDto.VolumeSugerido); // Não foi penalizado pelas 50 sincronizações vazias!

        // Aplica atualização comercial com módulo CompletoServicos (+30) e desconto de R$ 10,00 -> 50 + 30 - 10 = R$ 70,00
        var reqAplicar = new HttpRequestMessage(HttpMethod.Put, $"/api/admin/precificacao/tenants/{idEmp}")
        {
            Content = JsonContent.Create(new AplicarPrecificacaoTenantRequest(
                ModuloContratado: "CompletoServicos",
                VolumeUtilizacao: "Baixo",
                Desconto: 10.00m,
                AjusteManual: 0.00m,
                Justificativa: "Desconto comercial de fidelidade aprovado pela diretoria"
            ))
        };
        reqAplicar.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var resAplicar = await _client.SendAsync(reqAplicar);
        Assert.Equal(HttpStatusCode.OK, resAplicar.StatusCode);
        var aplicadoDto = await resAplicar.Content.ReadFromJsonAsync<PrecificacaoTenantDetalheDto>();
        Assert.NotNull(aplicadoDto);
        Assert.Equal(80.00m, aplicadoDto!.MensalidadeCalculada);
        Assert.Equal(70.00m, aplicadoDto.MensalidadeFinal);

        // Verifica que o histórico comercial registrou a alteração
        var reqHist = new HttpRequestMessage(HttpMethod.Get, $"/api/admin/precificacao/tenants/{idEmp}/historico");
        reqHist.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var resHist = await _client.SendAsync(reqHist);
        Assert.Equal(HttpStatusCode.OK, resHist.StatusCode);
        var histList = await resHist.Content.ReadFromJsonAsync<List<HistoricoComercialDto>>();
        Assert.NotNull(histList);
        Assert.NotEmpty(histList!);
        Assert.Equal(70.00m, histList[0].MensalidadeFinal);

        // Verifica que o painel do cliente (/api/assinaturas/minha) reflete exatamente R$ 70,00
        var reqMinha = new HttpRequestMessage(HttpMethod.Get, "/api/assinaturas/minha");
        reqMinha.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenEmp);
        var resMinha = await _client.SendAsync(reqMinha);
        Assert.Equal(HttpStatusCode.OK, resMinha.StatusCode);
        var minhaDto = await resMinha.Content.ReadFromJsonAsync<AssinaturaDetalheDto>();
        Assert.NotNull(minhaDto);
        Assert.Equal(70.00m, minhaDto!.MensalidadeFinal);
    }
}
