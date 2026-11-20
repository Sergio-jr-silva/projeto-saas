using System.IO;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Nexo.Api.Data;
using Nexo.Api.DTOs;
using Nexo.Api.Services;
using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true, MaxParallelThreads = 1)]

namespace Nexo.Api.Tests;

[CollectionDefinition("DatabaseCollection", DisableParallelization = true)]
public class DatabaseCollectionDefinition { }

[Collection("DatabaseCollection")]
public class ResetOperacionalTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public ResetOperacionalTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ExecutarResetOperacional_LimpaTodosOsDadosDeTeste_EMantemApenasAdministradorGeral()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NexoDbContext>();
        var resetService = scope.ServiceProvider.GetRequiredService<DatabaseResetService>();

        // 1. Executa o reset operacional (com backup prévio automático)
        var report = await resetService.ExecutarResetOperacionalAsync(force: true);

        Assert.True(report.Sucesso, $"Reset falhou: {report.Mensagem}");
        Assert.Equal(DatabaseResetService.MasterEmail, report.UsuarioPreservado);
        Assert.Equal("AdministradorGeral", report.PerfilUsuarioPreservado);
        Assert.False(string.IsNullOrWhiteSpace(report.CaminhoBackupGerado));
        Assert.True(File.Exists(report.CaminhoBackupGerado), "O arquivo de backup pré-limpeza deve existir em disco.");

        // Salva o relatório detalhado para auditoria
        try
        {
            var reportJson = JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(
                @"C:\Users\sergi\.gemini\antigravity\brain\a0a4efb5-f373-4ffc-9d90-fd2b446eb87d\scratch\relatorio_limpeza_executada.json",
                reportJson);
        }
        catch { }

        // 2. Validações diretas no banco de dados
        var usuariosRestantes = await db.Usuarios.IgnoreQueryFilters().ToListAsync();
        Assert.Single(usuariosRestantes);
        var unicoUsuario = usuariosRestantes[0];
        Assert.Equal(DatabaseResetService.MasterEmail, unicoUsuario.Email);
        Assert.Equal("AdministradorGeral", unicoUsuario.Perfil);
        Assert.True(unicoUsuario.Ativo);
        Assert.Null(unicoUsuario.TenantId);

        // 3. Valida que todas as entidades operacionais estão completamente zeradas
        Assert.Equal(0, await db.Tenants.IgnoreQueryFilters().CountAsync());
        Assert.Equal(0, await db.Filiais.IgnoreQueryFilters().CountAsync());
        Assert.Equal(0, await db.Produtos.IgnoreQueryFilters().CountAsync());
        Assert.Equal(0, await db.Fornecedores.IgnoreQueryFilters().CountAsync());
        Assert.Equal(0, await db.Cotacoes.IgnoreQueryFilters().CountAsync());
        Assert.Equal(0, await db.PedidosCompra.IgnoreQueryFilters().CountAsync());
        Assert.Equal(0, await db.CotacoesServico.IgnoreQueryFilters().CountAsync());
        Assert.Equal(0, await db.OrdensServico.IgnoreQueryFilters().CountAsync());
        Assert.Equal(0, await db.EstoquesFilial.IgnoreQueryFilters().CountAsync());
        Assert.Equal(0, await db.Agentes.IgnoreQueryFilters().CountAsync());
        Assert.Equal(0, await db.Sincronizacoes.IgnoreQueryFilters().CountAsync());
        Assert.Equal(0, await db.Assinaturas.IgnoreQueryFilters().CountAsync());
        Assert.Equal(0, await db.Faturas.IgnoreQueryFilters().CountAsync());
        Assert.Equal(0, await db.RefreshTokens.IgnoreQueryFilters().CountAsync());

        // 4. Valida que a estrutura e tabelas globais essenciais continuam intactas
        var planosRestantes = await db.Planos.IgnoreQueryFilters().CountAsync();
        Assert.True(planosRestantes > 0, "A tabela de Planos deve ser preservada.");
        var tabelaPrecosRestante = await db.ConfiguracoesPrecificacao.IgnoreQueryFilters().CountAsync();
        Assert.True(tabelaPrecosRestante > 0, "A tabela de Precificação Dinâmica deve ser preservada.");

        // 5. Valida que o Administrador Geral consegue autenticar
        var client = _factory.CreateClient();
        var loginRes = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email = DatabaseResetService.MasterEmail,
            senha = DatabaseResetService.MasterInitialPassword
        });

        Assert.Equal(HttpStatusCode.OK, loginRes.StatusCode);
        var authResult = await loginRes.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(authResult);
        Assert.Equal(DatabaseResetService.MasterEmail, authResult!.Usuario.Email);
        Assert.Equal("AdministradorGeral", authResult.Usuario.Perfil);
        Assert.NotEmpty(authResult.Token);

        // 6. Valida que o painel do Admin Geral (/api/admin/dashboard) retorna todos os 9 indicadores zerados
        var reqDash = new HttpRequestMessage(HttpMethod.Get, "/api/admin/dashboard");
        reqDash.Headers.Authorization = new AuthenticationHeaderValue("Bearer", authResult.Token);
        var resDash = await client.SendAsync(reqDash);
        Assert.Equal(HttpStatusCode.OK, resDash.StatusCode);
        using var dashDoc = JsonDocument.Parse(await resDash.Content.ReadAsStringAsync());
        Assert.Equal(0, dashDoc.RootElement.GetProperty("totalEmpresas").GetInt32());
        Assert.Equal(0, dashDoc.RootElement.GetProperty("totalFiliais").GetInt32());
        Assert.Equal(0, dashDoc.RootElement.GetProperty("empresasAtivas").GetInt32());
        Assert.Equal(0, dashDoc.RootElement.GetProperty("totalSincronizadores").GetInt32());
        Assert.Equal(0, dashDoc.RootElement.GetProperty("totalProdutos").GetInt32());
        Assert.Equal(0, dashDoc.RootElement.GetProperty("totalFornecedores").GetInt32());
        Assert.Equal(0, dashDoc.RootElement.GetProperty("totalCotacoes").GetInt32());
        Assert.Equal(0, dashDoc.RootElement.GetProperty("totalPedidos").GetInt32());
        Assert.Equal(0, dashDoc.RootElement.GetProperty("totalOrdensServico").GetInt32());

        // 7. Valida que o Admin Geral consegue cadastrar uma nova empresa e gerar ativação do SINc
        var reqCadastro = new HttpRequestMessage(HttpMethod.Post, "/api/admin/empresas/cadastro-completo")
        {
            Content = JsonContent.Create(new CadastroCompletoEmpresaRequest(
                RazaoSocial: "Primeira Empresa Real Validacao LTDA",
                NomeFantasia: "Primeira Empresa Real",
                Cnpj: "12345678000195",
                Email: "contato@primeirareal.com.br",
                Telefone: "(11) 98888-7777",
                PlanoId: null,
                NomeFilial: "Matriz",
                CodigoFilial: "001",
                EmailLogin: "admin@primeirareal.com.br",
                GerarCodigoAtivacao: true,
                Slug: "primeirareal"
            ))
        };
        reqCadastro.Headers.Authorization = new AuthenticationHeaderValue("Bearer", authResult.Token);
        var resCad = await client.SendAsync(reqCadastro);
        Assert.Equal(HttpStatusCode.OK, resCad.StatusCode);
        var cadData = await resCad.Content.ReadFromJsonAsync<CadastroCompletoEmpresaResponse>();
        Assert.NotNull(cadData);
        Assert.Equal("primeirareal", cadData!.Slug);
        Assert.StartsWith("ACTV-", cadData.CodigoAtivacao);

        // 8. Reexecuta a limpeza de forma idempotente para entregar o banco 100% limpo para produção
        var reportFinal = await resetService.ExecutarResetOperacionalAsync(force: true);
        Assert.True(reportFinal.Sucesso);
        Assert.Equal(0, await db.Tenants.IgnoreQueryFilters().CountAsync());
        Assert.Equal(1, await db.Usuarios.IgnoreQueryFilters().CountAsync());
    }
}
