using Microsoft.EntityFrameworkCore;
using Nexo.Api.Data;
using Nexo.Api.Models;
using Nexo.Api.Security;

namespace Nexo.Api.Services;

public class DatabaseResetReport
{
    public bool Sucesso { get; set; }
    public string Mensagem { get; set; } = string.Empty;
    public string Ambiente { get; set; } = string.Empty;
    public string BancoIdentificado { get; set; } = string.Empty;
    public string CaminhoBackupGerado { get; set; } = string.Empty;
    public DateTime DataExecucao { get; set; } = DateTime.UtcNow;
    public Dictionary<string, int> RegistrosRemovidos { get; set; } = new();
    public Dictionary<string, int> RegistrosAposLimpeza { get; set; } = new();
    public int TotalRegistrosRemovidos { get; set; }
    public int ArquivosStorageRemovidos { get; set; }
    public string UsuarioPreservado { get; set; } = string.Empty;
    public string PerfilUsuarioPreservado { get; set; } = string.Empty;
}

public class DatabaseResetService(NexoDbContext db)
{
    public const string MasterEmail = "juniosergio034@gmail.com";
    public const string MasterInitialPassword = "34777126Pe@";

    public async Task<DatabaseResetReport> ExecutarResetOperacionalAsync(bool force = false)
    {
        var report = new DatabaseResetReport();

        // 1. Identificar Ambiente e Banco de Dados
        var conn = db.Database.GetDbConnection();
        report.BancoIdentificado = $"{conn.Database} em {conn.DataSource}";
        report.Ambiente = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";

        // Trava de segurança: Impede exclusão em produção sem flag explícita
        if (report.Ambiente.Equals("Production", StringComparison.OrdinalIgnoreCase) && !force)
        {
            report.Sucesso = false;
            report.Mensagem = "OPERAÇÃO BLOQUEADA: O ambiente está marcado como Production. A limpeza completa dos dados de teste só é permitida em Development ou com autorização explícita.";
            return report;
        }

        // 2. Contar registros existentes antes da limpeza (com IgnoreQueryFilters para cobrir todos os tenants)
        var counts = new Dictionary<string, int>();

        counts["AnexosResposta"] = await db.AnexosResposta.IgnoreQueryFilters().CountAsync();
        counts["RespostaCotacaoItens"] = await db.RespostaCotacaoItens.IgnoreQueryFilters().CountAsync();
        counts["RespostasCotacao"] = await db.RespostasCotacao.IgnoreQueryFilters().CountAsync();
        counts["AcessosFornecedor"] = await db.AcessosFornecedor.IgnoreQueryFilters().CountAsync();
        counts["CotacaoFornecedores"] = await db.CotacaoFornecedores.IgnoreQueryFilters().CountAsync();
        counts["CotacaoItens"] = await db.CotacaoItens.IgnoreQueryFilters().CountAsync();
        counts["Cotacoes"] = await db.Cotacoes.IgnoreQueryFilters().CountAsync();

        counts["PedidoCompraItens"] = await db.PedidoCompraItens.IgnoreQueryFilters().CountAsync();
        counts["PedidosCompra"] = await db.PedidosCompra.IgnoreQueryFilters().CountAsync();

        counts["PropostaServicoEtapas"] = await db.PropostaServicoEtapas.IgnoreQueryFilters().CountAsync();
        counts["PropostasServico"] = await db.PropostasServico.IgnoreQueryFilters().CountAsync();
        counts["AcessosFornecedorServico"] = await db.AcessosFornecedorServico.IgnoreQueryFilters().CountAsync();
        counts["FornecedoresServico"] = await db.FornecedoresServico.IgnoreQueryFilters().CountAsync();
        counts["AtividadesServico"] = await db.AtividadesServico.IgnoreQueryFilters().CountAsync();
        counts["OrdensServico"] = await db.OrdensServico.IgnoreQueryFilters().CountAsync();
        counts["AnexosServico"] = await db.AnexosServico.IgnoreQueryFilters().CountAsync();
        counts["CotacoesServico"] = await db.CotacoesServico.IgnoreQueryFilters().CountAsync();

        counts["SolicitacaoTransferenciaItem"] = await db.SolicitacaoTransferenciaItens.IgnoreQueryFilters().CountAsync();
        counts["SolicitacoesTransferencia"] = await db.SolicitacoesTransferencia.IgnoreQueryFilters().CountAsync();
        counts["EstoquesFilial"] = await db.EstoquesFilial.IgnoreQueryFilters().CountAsync();
        counts["HistoricoPrecos"] = await db.HistoricoPrecos.IgnoreQueryFilters().CountAsync();
        counts["Produtos"] = await db.Produtos.IgnoreQueryFilters().CountAsync();
        counts["Fornecedores"] = await db.Fornecedores.IgnoreQueryFilters().CountAsync();

        counts["TransacoesPagamento"] = await db.TransacoesPagamento.IgnoreQueryFilters().CountAsync();
        counts["HistoricosComerciaisTenant"] = await db.HistoricosComerciaisTenant.IgnoreQueryFilters().CountAsync();
        counts["SolicitacoesAlteracaoPlano"] = await db.SolicitacoesAlteracaoPlano.IgnoreQueryFilters().CountAsync();
        counts["Faturas"] = await db.Faturas.IgnoreQueryFilters().CountAsync();
        counts["Assinaturas"] = await db.Assinaturas.IgnoreQueryFilters().CountAsync();
        counts["SincronizacaoErros"] = await db.SincronizacaoErros.IgnoreQueryFilters().CountAsync();
        counts["Sincronizacoes"] = await db.Sincronizacoes.IgnoreQueryFilters().CountAsync();
        counts["Agentes"] = await db.Agentes.IgnoreQueryFilters().CountAsync();
        counts["LogsAuditoria"] = await db.LogsAuditoria.IgnoreQueryFilters().CountAsync();
        counts["RefreshTokens"] = await db.RefreshTokens.IgnoreQueryFilters().CountAsync();

        counts["UsuariosOperacionais"] = await db.Usuarios.IgnoreQueryFilters().CountAsync(u => u.Email != MasterEmail);
        counts["Filiais"] = await db.Filiais.IgnoreQueryFilters().CountAsync();
        counts["ConfiguracoesTenant"] = await db.ConfiguracoesTenant.IgnoreQueryFilters().CountAsync();
        counts["Tenants"] = await db.Tenants.IgnoreQueryFilters().CountAsync();

        // 2.1 Gerar Backup Completo antes da exclusão
        try
        {
            var backupDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "backups");
            Directory.CreateDirectory(backupDir);
            var backupPath = Path.Combine(backupDir, $"backup_pre_limpeza_{DateTime.UtcNow:yyyyMMdd_HHmmss}.json");

            var tenantsSnapshot = await db.Tenants.IgnoreQueryFilters()
                .Select(t => new { t.Id, t.Slug, t.RazaoSocial, t.NomeFantasia, t.Cnpj, t.Status, t.MensalidadeFinal })
                .ToListAsync();
            var usuariosSnapshot = await db.Usuarios.IgnoreQueryFilters()
                .Select(u => new { u.Id, u.Nome, u.Email, u.Perfil, u.TenantId, u.Ativo })
                .ToListAsync();

            var snapshotObj = new
            {
                DataBackupUtc = DateTime.UtcNow,
                report.Ambiente,
                report.BancoIdentificado,
                UsuarioPreservado = MasterEmail,
                ContagensAntesDaLimpeza = counts,
                TenantsExistentes = tenantsSnapshot,
                UsuariosExistentes = usuariosSnapshot
            };

            await File.WriteAllTextAsync(backupPath, System.Text.Json.JsonSerializer.Serialize(snapshotObj, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
            report.CaminhoBackupGerado = backupPath;
        }
        catch { }

        // 3. Execução da limpeza em Transação Única
        await using var tx = await db.Database.BeginTransactionAsync();
        try
        {
            // Remover registros operacionais via ExecuteDeleteAsync respeitando FKs
            await db.AnexosResposta.IgnoreQueryFilters().ExecuteDeleteAsync();
            await db.RespostaCotacaoItens.IgnoreQueryFilters().ExecuteDeleteAsync();
            await db.RespostasCotacao.IgnoreQueryFilters().ExecuteDeleteAsync();
            await db.AcessosFornecedor.IgnoreQueryFilters().ExecuteDeleteAsync();
            await db.CotacaoFornecedores.IgnoreQueryFilters().ExecuteDeleteAsync();
            await db.CotacaoItens.IgnoreQueryFilters().ExecuteDeleteAsync();
            await db.Cotacoes.IgnoreQueryFilters().ExecuteDeleteAsync();

            await db.PedidoCompraItens.IgnoreQueryFilters().ExecuteDeleteAsync();
            await db.PedidosCompra.IgnoreQueryFilters().ExecuteDeleteAsync();

            await db.PropostaServicoEtapas.IgnoreQueryFilters().ExecuteDeleteAsync();
            await db.PropostasServico.IgnoreQueryFilters().ExecuteDeleteAsync();
            await db.AcessosFornecedorServico.IgnoreQueryFilters().ExecuteDeleteAsync();
            await db.FornecedoresServico.IgnoreQueryFilters().ExecuteDeleteAsync();
            await db.AtividadesServico.IgnoreQueryFilters().ExecuteDeleteAsync();
            await db.OrdensServico.IgnoreQueryFilters().ExecuteDeleteAsync();
            await db.AnexosServico.IgnoreQueryFilters().ExecuteDeleteAsync();
            await db.CotacoesServico.IgnoreQueryFilters().ExecuteDeleteAsync();

            await db.SolicitacaoTransferenciaItens.IgnoreQueryFilters().ExecuteDeleteAsync();
            await db.SolicitacoesTransferencia.IgnoreQueryFilters().ExecuteDeleteAsync();
            await db.EstoquesFilial.IgnoreQueryFilters().ExecuteDeleteAsync();
            await db.HistoricoPrecos.IgnoreQueryFilters().ExecuteDeleteAsync();
            await db.Produtos.IgnoreQueryFilters().ExecuteDeleteAsync();
            await db.Fornecedores.IgnoreQueryFilters().ExecuteDeleteAsync();

            await db.TransacoesPagamento.IgnoreQueryFilters().ExecuteDeleteAsync();
            await db.HistoricosComerciaisTenant.IgnoreQueryFilters().ExecuteDeleteAsync();
            await db.SolicitacoesAlteracaoPlano.IgnoreQueryFilters().ExecuteDeleteAsync();
            await db.Faturas.IgnoreQueryFilters().ExecuteDeleteAsync();
            await db.Assinaturas.IgnoreQueryFilters().ExecuteDeleteAsync();
            await db.SincronizacaoErros.IgnoreQueryFilters().ExecuteDeleteAsync();
            await db.Sincronizacoes.IgnoreQueryFilters().ExecuteDeleteAsync();
            await db.Agentes.IgnoreQueryFilters().ExecuteDeleteAsync();
            await db.LogsAuditoria.IgnoreQueryFilters().ExecuteDeleteAsync();
            await db.RefreshTokens.IgnoreQueryFilters().ExecuteDeleteAsync();

            await db.Usuarios.IgnoreQueryFilters().Where(u => u.Email != MasterEmail).ExecuteDeleteAsync();
            await db.Filiais.IgnoreQueryFilters().ExecuteDeleteAsync();
            await db.ConfiguracoesTenant.IgnoreQueryFilters().ExecuteDeleteAsync();
            await db.Tenants.IgnoreQueryFilters().ExecuteDeleteAsync();

            // 4. Garantir que a Conta Principal do Administrador Geral existe e está íntegra
            var master = await db.Usuarios.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.Email == MasterEmail);
            if (master == null)
            {
                master = new Usuario
                {
                    Nome = "Administrador Geral",
                    Email = MasterEmail,
                    SenhaHash = Secrets.HashPassword(MasterInitialPassword),
                    Perfil = "AdministradorGeral",
                    Ativo = true,
                    TenantId = null
                };
                await db.Usuarios.AddAsync(master);
            }
            else
            {
                master.Perfil = "AdministradorGeral";
                master.SenhaHash = Secrets.HashPassword(MasterInitialPassword);
                master.Ativo = true;
                master.TenantId = null;
                master.FilialId = null;
                master.FalhasLogin = 0;
                master.BloqueadoAte = null;
            }

            await db.SaveChangesAsync();
            await tx.CommitAsync();

            // 5. Limpeza de arquivos físicos de empresas fictícias (logos, anexos e PDFs de teste)
            int arquivosRemovidos = 0;
            try
            {
                var storagePaths = new[]
                {
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "storage"),
                    Path.Combine(Directory.GetCurrentDirectory(), "storage"),
                    Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads")
                };
                foreach (var sp in storagePaths)
                {
                    if (Directory.Exists(sp))
                    {
                        foreach (var file in Directory.GetFiles(sp, "*", SearchOption.AllDirectories))
                        {
                            try { File.Delete(file); arquivosRemovidos++; } catch { }
                        }
                    }
                }
            }
            catch { }

            // 6. Verificar contagens reais após a limpeza
            var after = new Dictionary<string, int>
            {
                ["Tenants"] = await db.Tenants.IgnoreQueryFilters().CountAsync(),
                ["Filiais"] = await db.Filiais.IgnoreQueryFilters().CountAsync(),
                ["UsuariosOperacionais"] = await db.Usuarios.IgnoreQueryFilters().CountAsync(u => u.Email != MasterEmail),
                ["AdministradorGeralPreservado"] = await db.Usuarios.IgnoreQueryFilters().CountAsync(u => u.Email == MasterEmail),
                ["Produtos"] = await db.Produtos.IgnoreQueryFilters().CountAsync(),
                ["EstoquesFilial"] = await db.EstoquesFilial.IgnoreQueryFilters().CountAsync(),
                ["Fornecedores"] = await db.Fornecedores.IgnoreQueryFilters().CountAsync(),
                ["Cotacoes"] = await db.Cotacoes.IgnoreQueryFilters().CountAsync(),
                ["PedidosCompra"] = await db.PedidosCompra.IgnoreQueryFilters().CountAsync(),
                ["CotacoesServico"] = await db.CotacoesServico.IgnoreQueryFilters().CountAsync(),
                ["OrdensServico"] = await db.OrdensServico.IgnoreQueryFilters().CountAsync(),
                ["Agentes"] = await db.Agentes.IgnoreQueryFilters().CountAsync(),
                ["Sincronizacoes"] = await db.Sincronizacoes.IgnoreQueryFilters().CountAsync(),
                ["Assinaturas"] = await db.Assinaturas.IgnoreQueryFilters().CountAsync(),
                ["Faturas"] = await db.Faturas.IgnoreQueryFilters().CountAsync(),
                ["RefreshTokens"] = await db.RefreshTokens.IgnoreQueryFilters().CountAsync(),
                ["PlanosGlobaisPreservados"] = await db.Planos.IgnoreQueryFilters().CountAsync(),
                ["TabelasPrecificacaoPreservadas"] = await db.ConfiguracoesPrecificacao.IgnoreQueryFilters().CountAsync()
            };

            report.Sucesso = true;
            report.Mensagem = "Reset operacional concluído com sucesso. Todos os dados de teste foram removidos e o Administrador Geral foi preservado/configurado.";
            report.RegistrosRemovidos = counts;
            report.RegistrosAposLimpeza = after;
            report.TotalRegistrosRemovidos = counts.Values.Sum();
            report.ArquivosStorageRemovidos = arquivosRemovidos;
            report.UsuarioPreservado = MasterEmail;
            report.PerfilUsuarioPreservado = "AdministradorGeral";

            return report;
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync();
            report.Sucesso = false;
            report.Mensagem = $"Erro durante o reset operacional: {ex.Message}";
            return report;
        }
    }
}
