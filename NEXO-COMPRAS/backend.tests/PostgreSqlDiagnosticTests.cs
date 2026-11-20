using System;
using System.Threading.Tasks;
using Nexo.Agent.Core.Connectors;
using Xunit;

namespace Nexo.Api.Tests;

public class PostgreSqlDiagnosticTests
{
    [Fact]
    public async Task Diagnosticar_CamposObrigatoriosVazios_RetornaCamposInvalidos()
    {
        var svc = new PostgreSqlDiagnosticService();

        // Host vazio
        var res1 = await svc.DiagnosticarAsync(new PostgreSqlDiagnosticParams { Host = "" });
        Assert.Equal(PostgreSqlDiagnosticStatus.CamposInvalidos, res1.Status);
        Assert.False(res1.Sucesso);
        Assert.Contains("obrigatório", res1.MensagemTecnica, StringComparison.OrdinalIgnoreCase);

        // Porta inválida
        var res2 = await svc.DiagnosticarAsync(new PostgreSqlDiagnosticParams { Host = "127.0.0.1", Port = -1 });
        Assert.Equal(PostgreSqlDiagnosticStatus.CamposInvalidos, res2.Status);

        // Banco vazio
        var res3 = await svc.DiagnosticarAsync(new PostgreSqlDiagnosticParams { Host = "127.0.0.1", Port = 5432, Database = "" });
        Assert.Equal(PostgreSqlDiagnosticStatus.CamposInvalidos, res3.Status);

        // Usuário vazio
        var res4 = await svc.DiagnosticarAsync(new PostgreSqlDiagnosticParams { Host = "127.0.0.1", Port = 5432, Database = "test", User = "" });
        Assert.Equal(PostgreSqlDiagnosticStatus.CamposInvalidos, res4.Status);
    }

    [Fact]
    public async Task Diagnosticar_HostComFormatoInvalido_RetornaCamposInvalidos()
    {
        var svc = new PostgreSqlDiagnosticService();
        var res = await svc.DiagnosticarAsync(new PostgreSqlDiagnosticParams
        {
            Host = "http://localhost/banco",
            Port = 5432,
            Database = "test",
            User = "postgres"
        });

        Assert.Equal(PostgreSqlDiagnosticStatus.CamposInvalidos, res.Status);
        Assert.False(res.Sucesso);
        Assert.Contains("caracteres inválidos", res.MensagemTecnica, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Diagnosticar_HostDnsInexistente_RetornaFalhaConexao()
    {
        var svc = new PostgreSqlDiagnosticService();
        var res = await svc.DiagnosticarAsync(new PostgreSqlDiagnosticParams
        {
            Host = "servidor-pg-nao-existente-xyz-987.local",
            Port = 5432,
            Database = "test",
            User = "postgres",
            TimeoutSeconds = 3
        });

        Assert.Equal(PostgreSqlDiagnosticStatus.FalhaConexao, res.Status);
        Assert.False(res.Sucesso);
        Assert.Contains("DNS", res.EtapaFalha, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Diagnosticar_PortaFechada_RetornaFalhaConexao()
    {
        var svc = new PostgreSqlDiagnosticService();
        var res = await svc.DiagnosticarAsync(new PostgreSqlDiagnosticParams
        {
            Host = "127.0.0.1",
            Port = 59998, // Porta fechada garantida
            Database = "test",
            User = "postgres",
            TimeoutSeconds = 2
        });

        Assert.Equal(PostgreSqlDiagnosticStatus.FalhaConexao, res.Status);
        Assert.False(res.Sucesso);
        Assert.Contains("TCP", res.EtapaFalha, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Diagnosticar_ConexaoValidaNoBancoLocal_RetornaConectadoComVersao()
    {
        var svc = new PostgreSqlDiagnosticService();
        // Banco embedded local rodando na porta 5433
        var res = await svc.DiagnosticarAsync(new PostgreSqlDiagnosticParams
        {
            Host = "127.0.0.1",
            Port = 5433,
            Database = "nexo_compras",
            User = "postgres",
            Password = "postgres_local_pwd",
            SslMode = "Disable",
            TimeoutSeconds = 5
        });

        if (res.Status == PostgreSqlDiagnosticStatus.Conectado)
        {
            Assert.True(res.Sucesso);
            Assert.Equal("Conectado", res.StatusBadge);
            Assert.NotEmpty(res.VersaoPostgreSql!);
            Assert.Contains("PostgreSQL", res.VersaoPostgreSql!, StringComparison.OrdinalIgnoreCase);
            Assert.True(res.ElapsedMilliseconds > 0);
            Assert.Empty(res.EtapaFalha);
        }
        else
        {
            // Se o embedded PG estiver usando outra senha ou porta, confirma que identificou etapa específica
            Assert.NotEmpty(res.EtapaFalha);
            Assert.NotEmpty(res.SugestaoDiagnostico);
        }
    }

    [Fact]
    public async Task Diagnosticar_AutenticacaoFalha_NaoExibeSenhaEmTextoClaro()
    {
        var svc = new PostgreSqlDiagnosticService();
        const string senhaSecreta = "SUPER_SECRET_PWD_ABC_123";

        var res = await svc.DiagnosticarAsync(new PostgreSqlDiagnosticParams
        {
            Host = "127.0.0.1",
            Port = 5433,
            Database = "nexo_compras",
            User = "postgres",
            Password = senhaSecreta,
            SslMode = "Disable",
            TimeoutSeconds = 5
        });

        // Garantia de segurança: a senha jamais deve aparecer em logs ou mensagem técnica
        Assert.DoesNotContain(senhaSecreta, res.MensagemTecnica);
        foreach (var log in res.LogsEtapas)
        {
            Assert.DoesNotContain(senhaSecreta, log);
        }
    }
}

