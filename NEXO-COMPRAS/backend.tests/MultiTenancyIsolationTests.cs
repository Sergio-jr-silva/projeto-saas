using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Nexo.Api.DTOs;
using Xunit;

namespace Nexo.Api.Tests;

[Collection("DatabaseCollection")]
public class MultiTenancyIsolationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public MultiTenancyIsolationTests(WebApplicationFactory<Program> factory)
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
            RazaoSocial: $"{prefixo} Comércio e Varejo Ltda",
            NomeFantasia: $"{prefixo} Supermercados",
            Cnpj: cnpjGerado,
            Email: $"contato@{prefixo.ToLower()}.com.br",
            Telefone: "(81) 3333-0000",
            AdminNome: $"Admin {prefixo}",
            AdminEmail: $"admin@{prefixo.ToLower()}{Random.Shared.Next(1000, 9999)}.com.br",
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
    public async Task TenantB_NaoPodeAcessar_NemVisualizar_ProdutosDoTenantA()
    {
        // 1. Cadastra Tenant A e Tenant B
        var (tokenA, tenantIdA) = await RegistrarTenantAsync("MercadoA");
        var (tokenB, tenantIdB) = await RegistrarTenantAsync("MercadoB");

        Assert.NotEqual(tenantIdA, tenantIdB);

        // 2. Tenant A cria um produto exclusivo
        var novoProdutoA = new CreateProdutoRequest(
            CodigoErp: "COD-A-999",
            Descricao: "Arroz Especial Tipo 1 - Exclusivo Tenant A",
            CodigoBarras: "78900000001",
            Unidade: "KG",
            Grupo: "Cereais",
            Categoria: "Alimentos",
            PrecoVenda: 25.50m,
            CustoAtual: 18.00m,
            EstoqueAtual: 150m,
            EstoqueMinimo: 30m,
            FornecedorVinculadoId: null
        );

        var reqCriacaoA = new HttpRequestMessage(HttpMethod.Post, "/api/produtos")
        {
            Content = JsonContent.Create(novoProdutoA)
        };
        reqCriacaoA.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenA);

        var resCriacaoA = await _client.SendAsync(reqCriacaoA);
        Assert.Equal(HttpStatusCode.Created, resCriacaoA.StatusCode);
        var produtoCriado = await resCriacaoA.Content.ReadFromJsonAsync<ProdutoDto>();
        Assert.NotNull(produtoCriado);

        // 3. Tenant B lista todos os produtos
        var reqListagemB = new HttpRequestMessage(HttpMethod.Get, "/api/produtos");
        reqListagemB.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenB);

        var resListagemB = await _client.SendAsync(reqListagemB);
        Assert.Equal(HttpStatusCode.OK, resListagemB.StatusCode);

        var jsonListagemB = await resListagemB.Content.ReadFromJsonAsync<ProdutosListResponse>();
        Assert.NotNull(jsonListagemB);

        // GARANTIA: O produto do Tenant A NÃO PODE aparecer na listagem do Tenant B
        Assert.DoesNotContain(jsonListagemB.Itens, p => p.Id == produtoCriado.Id || p.CodigoErp == "COD-A-999");

        // 4. Tenant B tenta acessar diretamente pelo ID do produto do Tenant A
        var reqGetDiretoB = new HttpRequestMessage(HttpMethod.Get, $"/api/produtos/{produtoCriado.Id}");
        reqGetDiretoB.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenB);

        var resGetDiretoB = await _client.SendAsync(reqGetDiretoB);
        // GARANTIA: Deve retornar 404 (Not Found) devido ao filtro de isolamento multi-tenant
        Assert.Equal(HttpStatusCode.NotFound, resGetDiretoB.StatusCode);
    }

    [Fact]
    public async Task UsuarioDeTenant_NaoPodeAcessar_PainelAdministrativoDoSaaS()
    {
        var (tokenTenant, _) = await RegistrarTenantAsync("LojaVarejo");

        // Tenta acessar rota restrita de PlatformAdmin
        var reqAdmin = new HttpRequestMessage(HttpMethod.Get, "/api/admin/tenants");
        reqAdmin.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenTenant);

        var resAdmin = await _client.SendAsync(reqAdmin);
        Assert.Equal(HttpStatusCode.Forbidden, resAdmin.StatusCode);
    }

    [Fact]
    public async Task PlatformAdmin_ConsegueFazerLogin_E_ConsultarDashboardGlobal()
    {
        await RegistrarTenantAsync("DashboardTestTenant");

        var loginReq = new LoginRequest("juniosergio034@gmail.com", "34777126Pe@");
        var resLogin = await _client.PostAsJsonAsync("/api/auth/login", loginReq);
        Assert.Equal(HttpStatusCode.OK, resLogin.StatusCode);

        var loginData = await resLogin.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(loginData);
        Assert.True(loginData.Usuario.Perfil == "PlatformAdmin" || loginData.Usuario.Perfil == "AdministradorGeral");

        var reqDash = new HttpRequestMessage(HttpMethod.Get, "/api/admin/dashboard");
        reqDash.Headers.Authorization = new AuthenticationHeaderValue("Bearer", loginData.Token);

        var resDash = await _client.SendAsync(reqDash);
        Assert.Equal(HttpStatusCode.OK, resDash.StatusCode);

        var dash = await resDash.Content.ReadFromJsonAsync<AdminDashboardDto>();
        Assert.NotNull(dash);
        Assert.True(dash.TotalTenants >= 1);
    }

    [Fact]
    public async Task TenantB_NaoPodeAcessar_FornecedoresDoTenantA()
    {
        var (tokenA, _) = await RegistrarTenantAsync("MatrizA");
        var (tokenB, _) = await RegistrarTenantAsync("FilialB");

        // Tenant A cria fornecedor
        var reqFornA = new HttpRequestMessage(HttpMethod.Post, "/api/fornecedores")
        {
            Content = JsonContent.Create(new SaveFornecedorRequest(
                RazaoSocial: "Fornecedor Alfa Exclusivo A Ltda",
                NomeFantasia: "Alfa Exclusivo",
                Cnpj: "99888777000166",
                InscricaoEstadual: null,
                Email: "alfa@exclusivoA.com.br",
                Telefone: null,
                Whatsapp: null,
                Contato: null,
                Endereco: null,
                Cidade: "Recife",
                Uf: "PE",
                PrazoEntregaPadrao: 5,
                CondicoesPagamento: null,
                Observacoes: null
            ))
        };
        reqFornA.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenA);
        var resFornA = await _client.SendAsync(reqFornA);
        Assert.Equal(HttpStatusCode.Created, resFornA.StatusCode);
        var fornCriado = await resFornA.Content.ReadFromJsonAsync<FornecedorDto>();
        Assert.NotNull(fornCriado);

        // Tenant B tenta consultar diretamente pelo ID do fornecedor do Tenant A
        var reqGetB = new HttpRequestMessage(HttpMethod.Get, $"/api/fornecedores/{fornCriado.Id}");
        reqGetB.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenB);
        var resGetB = await _client.SendAsync(reqGetB);

        // ISOLAMENTO: Deve retornar 404 (NotFound) para o Tenant B
        Assert.Equal(HttpStatusCode.NotFound, resGetB.StatusCode);
    }

    [Fact]
    public async Task ProcessoAtivacaoAgente_CodigoTemporario_ValidaUsoUnico_EEmitirCredencialExclusiva()
    {
        // 1. Admin faz login
        var loginAdmin = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest("juniosergio034@gmail.com", "34777126Pe@"));
        var adminData = await loginAdmin.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(adminData);

        // 2. Registra Tenant
        var (_, tenantId) = await RegistrarTenantAsync("SupermercadoNordeste");

        // 3. Admin cria instalação de agente para o Tenant com código temporário
        var reqCriarAgente = new HttpRequestMessage(HttpMethod.Post, $"/api/admin/tenants/{tenantId}/agentes")
        {
            Content = JsonContent.Create(new CriarAgenteAdminRequest(
                Nome: "Servidor Firebird - Caixa Central",
                TipoBanco: "Firebird",
                Host: "192.168.1.100",
                Porta: 3050,
                CaminhoOuBanco: @"C:\DATACASH\BM.FDB",
                UsuarioBanco: "SYSDBA",
                SenhaBanco: "masterkey",
                Charset: "WIN1252",
                Dialeto: 3,
                IntervaloEstoqueMin: 5,
                IntervaloProdutosMin: 30,
                IntervaloFornecedoresMin: 30,
                IntervaloHeartbeatSeg: 60
            ))
        };
        reqCriarAgente.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminData.Token);
        var resCriarAgente = await _client.SendAsync(reqCriarAgente);
        var resCriarAgenteContent = await resCriarAgente.Content.ReadAsStringAsync();
        Assert.True(resCriarAgente.IsSuccessStatusCode, $"Falha ao criar agente: {resCriarAgente.StatusCode} - {resCriarAgenteContent}");

        var agenteCriado = await resCriarAgente.Content.ReadFromJsonAsync<CriarAgenteAdminResponse>();
        Assert.NotNull(agenteCriado);
        Assert.StartsWith("ACTV-", agenteCriado.CodigoAtivacao);

        // 4. Agente local ativa com o código temporário recebido
        var reqAtivar = new AtivarAgenteRequest(
            CodigoAtivacao: agenteCriado.CodigoAtivacao,
            NomeMaquina: "SRV-PDV-CENTRAL",
            Ambiente: new { OS = "Windows 11 Pro", Versao = "2.0.0" }
        );

        var resAtivar = await _client.PostAsJsonAsync("/api/agent/v1/activate", reqAtivar);
        Assert.Equal(HttpStatusCode.OK, resAtivar.StatusCode);

        var ativacaoData = await resAtivar.Content.ReadFromJsonAsync<AtivarAgenteResponse>();
        Assert.NotNull(ativacaoData);
        Assert.StartsWith("nxk_", ativacaoData.AgentCredential);
        Assert.Equal("Firebird", ativacaoData.BancoConfig.Tipo);
        Assert.Equal("192.168.1.100", ativacaoData.BancoConfig.Host);
        Assert.Equal(3050, ativacaoData.BancoConfig.Porta);

        // 5. REGRA OBRIGATÓRIA: Código de ativação é de uso único! Segunda tentativa deve ser REJEITADA
        var resAtivarRepetido = await _client.PostAsJsonAsync("/api/agent/v1/activate", reqAtivar);
        Assert.Equal(HttpStatusCode.BadRequest, resAtivarRepetido.StatusCode);

        // 6. Agente utiliza a credencial exclusiva emitida para enviar heartbeat
        var reqHeartbeat = new HttpRequestMessage(HttpMethod.Post, "/api/agent/v1/heartbeat")
        {
            Content = JsonContent.Create(new AgentHeartbeatRequest("2.0.0", "SRV-PDV-CENTRAL", null))
        };
        reqHeartbeat.Headers.Add("X-Agent-Key", ativacaoData.AgentCredential);
        var resHeartbeat = await _client.SendAsync(reqHeartbeat);
        Assert.Equal(HttpStatusCode.OK, resHeartbeat.StatusCode);
    }

    [Fact]
    public async Task AgenteRevogado_BloqueiaAcessoImediatamente()
    {
        // 1. Admin login e criação de tenant e agente
        var loginAdmin = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest("juniosergio034@gmail.com", "34777126Pe@"));
        var adminData = await loginAdmin.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(adminData);

        var (_, tenantId) = await RegistrarTenantAsync("MercadinhoRevogacao");

        var reqCriar = new HttpRequestMessage(HttpMethod.Post, $"/api/admin/tenants/{tenantId}/agentes")
        {
            Content = JsonContent.Create(new CriarAgenteAdminRequest(
                Nome: "Agente Para Revogar",
                TipoBanco: "Firebird",
                Host: "localhost",
                Porta: 3050,
                CaminhoOuBanco: @"C:\BM\BM.FDB",
                UsuarioBanco: "SYSDBA",
                SenhaBanco: null,
                Charset: null,
                Dialeto: null,
                IntervaloEstoqueMin: null,
                IntervaloProdutosMin: null,
                IntervaloFornecedoresMin: null,
                IntervaloHeartbeatSeg: null
            ))
        };
        reqCriar.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminData.Token);
        var resCriar = await _client.SendAsync(reqCriar);
        var agenteCriado = await resCriar.Content.ReadFromJsonAsync<CriarAgenteAdminResponse>();
        Assert.NotNull(agenteCriado);

        // 2. Ativa agente
        var resAtivar = await _client.PostAsJsonAsync("/api/agent/v1/activate", new AtivarAgenteRequest(agenteCriado.CodigoAtivacao, "MAQ-REVOGAR", null));
        var ativacao = await resAtivar.Content.ReadFromJsonAsync<AtivarAgenteResponse>();
        Assert.NotNull(ativacao);

        // 3. Admin revoga o agente
        var reqRevogar = new HttpRequestMessage(HttpMethod.Post, $"/api/admin/agentes/{agenteCriado.AgentId}/revogar");
        reqRevogar.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminData.Token);
        var resRevogar = await _client.SendAsync(reqRevogar);
        Assert.Equal(HttpStatusCode.OK, resRevogar.StatusCode);

        // 4. Tentativa de comunicação do agente com credencial revogada DEVE ser REJEITADA com 401
        var reqHeartbeat = new HttpRequestMessage(HttpMethod.Post, "/api/agent/v1/heartbeat")
        {
            Content = JsonContent.Create(new AgentHeartbeatRequest("2.0.0", "MAQ-REVOGAR", null))
        };
        reqHeartbeat.Headers.Add("X-Agent-Key", ativacao.AgentCredential);
        var resHeartbeat = await _client.SendAsync(reqHeartbeat);
        Assert.Equal(HttpStatusCode.Unauthorized, resHeartbeat.StatusCode);
    }

    [Fact]
    public async Task IntegracaoWeb_LojaInfo_E_GenerateKey_RetornaIdDaLoja_E_AtivacaoComUserId()
    {
        // 1. Cria a loja e obtém token do comprador
        var (token, tenantId) = await RegistrarTenantAsync("SupermercadoWebIntegracao");

        // 2. Consulta /api/integration/loja-info
        var reqLojaInfo = new HttpRequestMessage(HttpMethod.Get, "/api/integration/loja-info");
        reqLojaInfo.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var resLojaInfo = await _client.SendAsync(reqLojaInfo);
        Assert.Equal(HttpStatusCode.OK, resLojaInfo.StatusCode);
        var lojaInfoJson = await resLojaInfo.Content.ReadFromJsonAsync<System.Text.Json.Nodes.JsonObject>();
        Assert.NotNull(lojaInfoJson);
        Assert.Equal(tenantId.ToString(), lojaInfoJson["id"]?.ToString());

        // 3. Clica em "Gerar Nova Chave" no web: POST /api/integration/agents/generate-key
        var reqGenerateKey = new HttpRequestMessage(HttpMethod.Post, "/api/integration/agents/generate-key")
        {
            Content = JsonContent.Create(new { nomeIdentificador = "Servidor Matriz BM.FDB" })
        };
        reqGenerateKey.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var resGenerateKey = await _client.SendAsync(reqGenerateKey);
        Assert.Equal(HttpStatusCode.OK, resGenerateKey.StatusCode);
        var genJson = await resGenerateKey.Content.ReadFromJsonAsync<System.Text.Json.Nodes.JsonObject>();
        Assert.NotNull(genJson);
        Assert.Equal(tenantId.ToString(), genJson["chaveAtivacao"]?.ToString());

        // 4. Se o usuário informar seu próprio userId na ativação, o backend também resolve para a loja correta
        var meRes = await _client.SendAsync(new HttpRequestMessage(HttpMethod.Get, "/api/auth/me")
        {
            Headers = { Authorization = new AuthenticationHeaderValue("Bearer", token) }
        });
        var meJson = await meRes.Content.ReadFromJsonAsync<System.Text.Json.Nodes.JsonObject>();
        var userId = meJson!["usuario"]!["id"]!.ToString();

        var reqAtivarUserId = new AtivarAgenteRequest(
            CodigoAtivacao: userId,
            NomeMaquina: "SRV-TESTE-USERID",
            Ambiente: new { OS = "Windows 11" }
        );
        var resAtivarUserId = await _client.PostAsJsonAsync("/api/agent/v1/activate", reqAtivarUserId);
        Assert.Equal(HttpStatusCode.OK, resAtivarUserId.StatusCode);
        var ativacaoUserId = await resAtivarUserId.Content.ReadFromJsonAsync<AtivarAgenteResponse>();
        Assert.NotNull(ativacaoUserId);
        Assert.Equal(tenantId, ativacaoUserId.TenantId);
    }

    [Fact]
    public async Task AtivacaoAgente_UsandoIdDaLojaDiretamente_IdentificaEmpresa_E_SincronizaEstoque()
    {
        // 1. Cria a loja (Tenant) e obtém o ID da loja (ex: E1531698-D310-4851-B5D5-3DD1026EE767)
        var (token, storeId) = await RegistrarTenantAsync("SupermercadoRecife");

        // 2. O usuário utiliza diretamente o ID DA LOJA como chave/token de ativação no sincronizador
        var reqAtivar = new AtivarAgenteRequest(
            CodigoAtivacao: storeId.ToString(),
            NomeMaquina: "SRV-SUPERMERCADO-RECIFE",
            Ambiente: new { OS = "Windows Server 2022", TipoBanco = "Firebird" }
        );

        var resAtivar = await _client.PostAsJsonAsync("/api/agent/v1/activate", reqAtivar);
        var ativacaoJson = await resAtivar.Content.ReadAsStringAsync();
        Assert.True(resAtivar.IsSuccessStatusCode, $"Falha ao ativar com ID da Loja: {resAtivar.StatusCode} - {ativacaoJson}");

        var ativacao = await resAtivar.Content.ReadFromJsonAsync<AtivarAgenteResponse>();
        Assert.NotNull(ativacao);
        Assert.Equal(storeId, ativacao.TenantId);
        Assert.Contains("SupermercadoRecife", ativacao.Empresa.RazaoSocial);
        Assert.StartsWith("nxk_", ativacao.AgentCredential);

        // 3. O sincronizador transmite o estoque usando a credencial exclusiva emitida
        var reqSyncEstoque = new HttpRequestMessage(HttpMethod.Post, "/api/agent/v1/sync/estoque")
        {
            Content = JsonContent.Create(new SyncBatch<EstoqueSync>(
                Itens: new List<EstoqueSync>
                {
                    new("ARROZ-5KG", 150.5m, 0m),
                    new("FEIJAO-1KG", 85.0m, 0m)
                }
            ))
        };
        reqSyncEstoque.Headers.Add("X-Agent-Key", ativacao.AgentCredential);
        var resSyncEstoque = await _client.SendAsync(reqSyncEstoque);
        Assert.Equal(HttpStatusCode.OK, resSyncEstoque.StatusCode);

        // 4. Também verifica autenticação direta pelo ID DA LOJA (para quem configurou diretamente no appsettings)
        var reqDirectTenantSync = new HttpRequestMessage(HttpMethod.Post, "/api/agent/v1/heartbeat")
        {
            Content = JsonContent.Create(new AgentHeartbeatRequest("2.0.0", "SRV-SUPERMERCADO-RECIFE", null))
        };
        reqDirectTenantSync.Headers.Add("X-Agent-Key", storeId.ToString());
        var resDirect = await _client.SendAsync(reqDirectTenantSync);
        Assert.Equal(HttpStatusCode.OK, resDirect.StatusCode);
    }
}

public record ProdutosListResponse(int Total, int Page, int Size, List<ProdutoDto> Itens);

