using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Nexo.Api.Data;
using Nexo.Api.DTOs;
using Nexo.Api.Models;

namespace Nexo.Api.Services;

public interface IAssinaturaService
{
    Task<AssinaturaDetalheDto> ObterAssinaturaAtualAsync(Guid tenantId);
    Task<FaturaDto> RealizarCheckoutAsync(Guid tenantId, CheckoutAssinaturaRequest req);
    Task<bool> CancelarAssinaturaAsync(Guid tenantId, string motivo);
    Task<bool> ReativarAssinaturaAsync(Guid tenantId);
    Task<List<FaturaDto>> ListarFaturasAsync(Guid tenantId);
    Task<FaturaDto?> PagarFaturaAsync(Guid tenantId, Guid faturaId, string metodo);
    Task<bool> ProcessarWebhookAsync(WebhookPagamentoPayload payload);
    Task<(bool Permitido, string Mensagem)> ValidarLimiteAsync(Guid tenantId, string tipoRecurso);
    Task<SolicitacaoAlteracaoPlano> SolicitarAlteracaoPlanoAsync(Guid tenantId, Guid? usuarioId, string usuarioNome, SolicitarAlteracaoPlanoRequest req);
    Task<List<SolicitacaoAlteracaoPlano>> ListarSolicitacoesTenantAsync(Guid tenantId);
    Task<List<HistoricoComercialDto>> ListarHistoricoComercialTenantAsync(Guid tenantId);
}

public class AssinaturaService(NexoDbContext db, IPrecificacaoService precificacaoService) : IAssinaturaService
{
    public async Task<AssinaturaDetalheDto> ObterAssinaturaAtualAsync(Guid tenantId)
    {
        var tenant = await db.Tenants.Include(t => t.Plano).FirstOrDefaultAsync(t => t.Id == tenantId);
        if (tenant == null) throw new InvalidOperationException("Empresa não encontrada.");

        var precificacao = await precificacaoService.ObterPrecificacaoTenantAsync(tenantId);

        var assinatura = await db.Assinaturas.Include(a => a.Plano)
            .OrderByDescending(a => a.DataCriacao)
            .FirstOrDefaultAsync(a => a.TenantId == tenantId);

        var plano = assinatura?.Plano ?? tenant.Plano ?? await db.Planos.OrderBy(p => p.Mensalidade).FirstAsync();

        var mensalidadeVigente = precificacao.MensalidadeFinal > 0 ? precificacao.MensalidadeFinal : 50.00m;

        // Se o tenant ainda não tiver assinatura formal, provisiona a assinatura inicial vinculada à mensalidade dinâmica
        if (assinatura == null)
        {
            var diasTeste = plano.DiasTeste > 0 ? plano.DiasTeste : 14;
            assinatura = new Assinatura
            {
                TenantId = tenantId,
                PlanoId = plano.Id,
                Status = "Teste",
                Ciclo = "Mensal",
                Valor = mensalidadeVigente,
                DataInicio = tenant.DataCriacao,
                DataFim = tenant.DataCriacao.AddDays(diasTeste),
                DataRenovacao = tenant.ProximoVencimento ?? tenant.DataCriacao.AddDays(diasTeste),
                FormaPagamento = "Pix"
            };
            db.Assinaturas.Add(assinatura);
            await db.SaveChangesAsync();
        }

        int diasRestantes = 0;
        if (assinatura.Status == "Teste" && assinatura.DataFim.HasValue)
        {
            diasRestantes = Math.Max(0, (int)(assinatura.DataFim.Value - DateTime.UtcNow).TotalDays);
        }

        var maxUsuarios = tenant.MaxUsuariosContratado > 0 ? tenant.MaxUsuariosContratado : plano.MaxUsuarios;
        var maxFiliais = tenant.MaxFiliaisContratado > 0 ? tenant.MaxFiliaisContratado : plano.MaxFiliais;
        var maxProdutos = tenant.MaxProdutosContratado > 0 ? tenant.MaxProdutosContratado : 10000;
        var maxSinc = tenant.MaxSincronizadoresContratado > 0 ? tenant.MaxSincronizadoresContratado : 3;

        var limitesDto = new LimitesUsoDto(
            UsuariosUsados: precificacao.UsuariosAtivos,
            MaxUsuarios: maxUsuarios,
            FiliaisUsadas: precificacao.FiliaisAtivas,
            MaxFiliais: maxFiliais,
            CotacoesMesUsadas: precificacao.CotacoesNoMes,
            MaxCotacoesMes: plano.MaxCotacoesMes,
            ProdutosUsados: precificacao.ProdutosCadastrados,
            MaxProdutos: maxProdutos,
            SincronizadoresUsados: precificacao.SincronizadoresAtivos,
            MaxSincronizadores: maxSinc
        );

        return new AssinaturaDetalheDto(
            Id: assinatura.Id,
            PlanoId: plano.Id,
            PlanoNome: $"Nexo Compras — Faixa {precificacao.FaixaComercial}",
            Status: assinatura.Status,
            Ciclo: assinatura.Ciclo,
            Valor: mensalidadeVigente,
            DataInicio: assinatura.DataInicio,
            DataFim: assinatura.DataFim,
            DataRenovacao: tenant.ProximoVencimento ?? assinatura.DataRenovacao,
            FormaPagamento: assinatura.FormaPagamento,
            DiasRestantesTeste: diasRestantes,
            Limites: limitesDto,
            Slug: tenant.Slug,
            FaixaComercial: precificacao.FaixaComercial,
            ModuloContratado: precificacao.ModuloContratado,
            ModuloDescricao: precificacao.ModuloDescricao,
            VolumeUtilizacao: precificacao.VolumeUtilizacao,
            MensalidadeCalculada: precificacao.MensalidadeCalculada,
            DescontoComercial: precificacao.Desconto,
            AjusteManual: precificacao.AjusteManual,
            MensalidadeFinal: mensalidadeVigente,
            SituacaoFinanceira: precificacao.SituacaoFinanceira,
            ProximoVencimento: precificacao.ProximoVencimento,
            DetalhePrecificacao: precificacao.DetalheCalculo
        );
    }

    public async Task<FaturaDto> RealizarCheckoutAsync(Guid tenantId, CheckoutAssinaturaRequest req)
    {
        var plano = await db.Planos.FindAsync(req.PlanoId)
                    ?? await db.Planos.Where(p => p.Ativo).OrderBy(p => p.Mensalidade).FirstOrDefaultAsync();
        if (plano == null) throw new ArgumentException("Plano comercial não encontrado.");

        var precificacao = await precificacaoService.ObterPrecificacaoTenantAsync(tenantId);
        var ciclo = req.Ciclo?.Equals("Anual", StringComparison.OrdinalIgnoreCase) == true ? "Anual" : "Mensal";
        var valorMensal = precificacao.MensalidadeFinal > 0 ? precificacao.MensalidadeFinal : plano.Mensalidade;
        var valor = ciclo == "Anual" ? Math.Round(valorMensal * 10m, 2) : valorMensal;

        var assinatura = await db.Assinaturas
            .OrderByDescending(a => a.DataCriacao)
            .FirstOrDefaultAsync(a => a.TenantId == tenantId);

        var agora = DateTime.UtcNow;
        var duracaoDias = ciclo == "Anual" ? 365 : 30;

        if (assinatura == null)
        {
            assinatura = new Assinatura
            {
                TenantId = tenantId,
                PlanoId = plano.Id,
                Status = "Ativa",
                Ciclo = ciclo,
                Valor = valor,
                DataInicio = agora,
                DataFim = agora.AddDays(duracaoDias),
                DataRenovacao = agora.AddDays(duracaoDias),
                FormaPagamento = req.FormaPagamento,
                Gateway = "GatewayDesacoplado",
                GatewayAssinaturaId = $"sub_nexo_{Guid.NewGuid():N}"[..20]
            };
            db.Assinaturas.Add(assinatura);
        }
        else
        {
            assinatura.PlanoId = plano.Id;
            assinatura.Ciclo = ciclo;
            assinatura.Valor = valor;
            assinatura.FormaPagamento = req.FormaPagamento;
            assinatura.DataCancelamento = null;
            assinatura.MotivoCancelamento = string.Empty;
            assinatura.DataRenovacao = agora.AddDays(duracaoDias);
            assinatura.DataFim = agora.AddDays(duracaoDias);
        }

        var tenant = await db.Tenants.FindAsync(tenantId);
        if (tenant != null)
        {
            tenant.PlanoId = plano.Id;
            tenant.Status = "Ativo";
            tenant.DataExpiracao = agora.AddDays(duracaoDias + 5);
        }

        // Cria a fatura pendente de confirmação (sem inventar pagamento confirmado)
        var faturaNumero = $"FAT-{agora:yyyyMMdd}-{Random.Shared.Next(1000, 9999)}";
        var transacaoId = $"trx_{Guid.NewGuid():N}"[..22];
        var fatura = new Fatura
        {
            TenantId = tenantId,
            AssinaturaId = assinatura.Id,
            Numero = faturaNumero,
            Descricao = $"Mensalidade Nexo Compras SaaS — {precificacao.ModuloDescricao} ({ciclo})",
            Valor = valor,
            Status = "Pendente",
            DataEmissao = agora,
            DataVencimento = agora.AddDays(5),
            DataPagamento = null,
            MetodoPagamento = req.FormaPagamento,
            PixQrCode = string.Empty,
            PixCopiaECola = string.Empty,
            UrlBoleto = string.Empty,
            GatewayTransacaoId = transacaoId
        };
        db.Faturas.Add(fatura);

        db.TransacoesPagamento.Add(new TransacaoPagamento
        {
            TenantId = tenantId,
            FaturaId = fatura.Id,
            AssinaturaId = assinatura.Id,
            Gateway = "GatewayDesacoplado",
            GatewayTransacaoId = transacaoId,
            Evento = "fatura.gerada",
            Valor = valor,
            Status = "Pendente",
            MetodoPagamento = req.FormaPagamento,
            PayloadJson = JsonSerializer.Serialize(new { faturaNumero, ciclo, valor, formaPagamento = req.FormaPagamento })
        });

        await db.SaveChangesAsync();
        return ToFaturaDto(fatura);
    }

    public async Task<bool> CancelarAssinaturaAsync(Guid tenantId, string motivo)
    {
        var assinatura = await db.Assinaturas
            .OrderByDescending(a => a.DataCriacao)
            .FirstOrDefaultAsync(a => a.TenantId == tenantId);

        if (assinatura == null) return false;

        assinatura.Status = "Cancelada";
        assinatura.DataCancelamento = DateTime.UtcNow;
        assinatura.MotivoCancelamento = string.IsNullOrWhiteSpace(motivo) ? "Cancelado pelo cliente no painel." : motivo.Trim();

        await db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> ReativarAssinaturaAsync(Guid tenantId)
    {
        var assinatura = await db.Assinaturas
            .OrderByDescending(a => a.DataCriacao)
            .FirstOrDefaultAsync(a => a.TenantId == tenantId);

        if (assinatura == null) return false;

        assinatura.Status = "Ativa";
        assinatura.DataCancelamento = null;
        assinatura.MotivoCancelamento = string.Empty;
        assinatura.DataRenovacao = DateTime.UtcNow.AddDays(30);

        var tenant = await db.Tenants.FindAsync(tenantId);
        if (tenant != null)
        {
            tenant.Status = "Ativo";
            tenant.SituacaoFinanceira = "Regular";
        }

        await db.SaveChangesAsync();
        return true;
    }

    public async Task<List<FaturaDto>> ListarFaturasAsync(Guid tenantId)
    {
        var faturas = await db.Faturas
            .Where(f => f.TenantId == tenantId)
            .OrderByDescending(f => f.DataEmissao)
            .ToListAsync();

        return faturas.Select(ToFaturaDto).ToList();
    }

    public async Task<FaturaDto?> PagarFaturaAsync(Guid tenantId, Guid faturaId, string metodo)
    {
        var fatura = await db.Faturas.FirstOrDefaultAsync(f => f.TenantId == tenantId && f.Id == faturaId);
        if (fatura == null) return null;

        fatura.Status = "Paga";
        fatura.DataPagamento = DateTime.UtcNow;
        if (!string.IsNullOrWhiteSpace(metodo)) fatura.MetodoPagamento = metodo;

        var assinatura = await db.Assinaturas.FirstOrDefaultAsync(a => a.TenantId == tenantId && a.Id == fatura.AssinaturaId);
        if (assinatura != null)
        {
            assinatura.Status = "Ativa";
            var dias = assinatura.Ciclo == "Anual" ? 365 : 30;
            assinatura.DataRenovacao = DateTime.UtcNow.AddDays(dias);
            assinatura.DataFim = DateTime.UtcNow.AddDays(dias);
        }

        var tenant = await db.Tenants.FindAsync(tenantId);
        if (tenant != null)
        {
            tenant.Status = "Ativo";
            tenant.SituacaoFinanceira = "Regular";
            tenant.ProximoVencimento = DateTime.UtcNow.AddDays(30);
            tenant.DataExpiracao = DateTime.UtcNow.AddDays(35);
        }

        db.TransacoesPagamento.Add(new TransacaoPagamento
        {
            TenantId = tenantId,
            FaturaId = fatura.Id,
            AssinaturaId = fatura.AssinaturaId,
            Gateway = "ConfirmacaoAutorizada",
            GatewayTransacaoId = fatura.GatewayTransacaoId,
            Evento = "fatura.paga",
            Valor = fatura.Valor,
            Status = "Paga",
            MetodoPagamento = fatura.MetodoPagamento,
            ConfirmadoEm = DateTime.UtcNow
        });

        await db.SaveChangesAsync();
        return ToFaturaDto(fatura);
    }

    public async Task<bool> ProcessarWebhookAsync(WebhookPagamentoPayload payload)
    {
        if (string.IsNullOrWhiteSpace(payload.FaturaNumero) && string.IsNullOrWhiteSpace(payload.TransacaoId))
            return false;

        var fatura = await db.Faturas.IgnoreQueryFilters()
            .FirstOrDefaultAsync(f => f.Numero == payload.FaturaNumero || (!string.IsNullOrEmpty(payload.TransacaoId) && f.GatewayTransacaoId == payload.TransacaoId));
        if (fatura == null) return false;

        var agora = payload.Timestamp != default ? payload.Timestamp : DateTime.UtcNow;

        if (payload.Evento.Equals("fatura.paga", StringComparison.OrdinalIgnoreCase))
        {
            fatura.Status = "Paga";
            fatura.DataPagamento = agora;

            var assinatura = await db.Assinaturas.IgnoreQueryFilters().FirstOrDefaultAsync(a => a.Id == fatura.AssinaturaId);
            if (assinatura != null)
            {
                assinatura.Status = "Ativa";
                var dias = assinatura.Ciclo == "Anual" ? 365 : 30;
                assinatura.DataRenovacao = DateTime.UtcNow.AddDays(dias);
            }

            var tenant = await db.Tenants.IgnoreQueryFilters().FirstOrDefaultAsync(t => t.Id == fatura.TenantId);
            if (tenant != null)
            {
                tenant.Status = "Ativo";
                tenant.SituacaoFinanceira = "Regular";
                tenant.ProximoVencimento = DateTime.UtcNow.AddDays(30);
            }
        }
        else if (payload.Evento.Equals("fatura.vencida", StringComparison.OrdinalIgnoreCase) || payload.Evento.Equals("fatura.falha", StringComparison.OrdinalIgnoreCase))
        {
            fatura.Status = "Vencida";

            var assinatura = await db.Assinaturas.IgnoreQueryFilters().FirstOrDefaultAsync(a => a.Id == fatura.AssinaturaId);
            if (assinatura != null) assinatura.Status = "Inadimplente";

            var tenant = await db.Tenants.IgnoreQueryFilters().FirstOrDefaultAsync(t => t.Id == fatura.TenantId);
            if (tenant != null)
            {
                tenant.SituacaoFinanceira = "Inadimplente";
                // Suspensão controlada por inadimplência caso ultrapassada tolerância
                if (DateTime.UtcNow > fatura.DataVencimento.AddDays(5))
                {
                    tenant.Status = "Suspenso";
                }
            }
        }
        else if (payload.Evento.Equals("assinatura.cancelada", StringComparison.OrdinalIgnoreCase))
        {
            var assinatura = await db.Assinaturas.IgnoreQueryFilters().FirstOrDefaultAsync(a => a.Id == fatura.AssinaturaId);
            if (assinatura != null)
            {
                assinatura.Status = "Cancelada";
                assinatura.DataCancelamento = DateTime.UtcNow;
            }
        }

        db.TransacoesPagamento.Add(new TransacaoPagamento
        {
            TenantId = fatura.TenantId,
            FaturaId = fatura.Id,
            AssinaturaId = fatura.AssinaturaId,
            Gateway = "WebhookExterno",
            GatewayTransacaoId = payload.TransacaoId ?? fatura.GatewayTransacaoId,
            Evento = payload.Evento,
            Valor = payload.Valor > 0 ? payload.Valor : fatura.Valor,
            Status = fatura.Status,
            MetodoPagamento = fatura.MetodoPagamento,
            PayloadJson = JsonSerializer.Serialize(payload),
            ConfirmadoEm = fatura.Status == "Paga" ? agora : null
        });

        await db.SaveChangesAsync();
        return true;
    }

    public async Task<(bool Permitido, string Mensagem)> ValidarLimiteAsync(Guid tenantId, string tipoRecurso)
    {
        var tenant = await db.Tenants.Include(t => t.Plano).FirstOrDefaultAsync(t => t.Id == tenantId);
        if (tenant == null) return (false, "Empresa não encontrada.");

        var plano = tenant.Plano ?? await db.Planos.OrderBy(p => p.Mensalidade).FirstAsync();
        var maxUsuarios = tenant.MaxUsuariosContratado > 0 ? tenant.MaxUsuariosContratado : plano.MaxUsuarios;
        var maxFiliais = tenant.MaxFiliaisContratado > 0 ? tenant.MaxFiliaisContratado : plano.MaxFiliais;

        if (tipoRecurso.Equals("Usuario", StringComparison.OrdinalIgnoreCase))
        {
            var total = await db.Usuarios.CountAsync(u => u.TenantId == tenantId && u.Ativo);
            if (total >= maxUsuarios)
                return (false, $"Limite de usuários contratado atingido ({total}/{maxUsuarios}). Solicite ampliação ao Administrador Geral.");
        }
        else if (tipoRecurso.Equals("Filial", StringComparison.OrdinalIgnoreCase))
        {
            var total = await db.Filiais.CountAsync(f => f.TenantId == tenantId && f.Ativa);
            if (total >= maxFiliais)
                return (false, $"Limite de filiais contratado atingido ({total}/{maxFiliais}). Solicite ampliação ao Administrador Geral.");
        }
        else if (tipoRecurso.Equals("Cotacao", StringComparison.OrdinalIgnoreCase))
        {
            var inicioMes = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var total = await db.Cotacoes.CountAsync(c => c.TenantId == tenantId && c.DataCriacao >= inicioMes);
            if (total >= plano.MaxCotacoesMes)
                return (false, $"Limite mensal de cotações atingido ({total}/{plano.MaxCotacoesMes}).");
        }

        return (true, string.Empty);
    }

    public async Task<SolicitacaoAlteracaoPlano> SolicitarAlteracaoPlanoAsync(Guid tenantId, Guid? usuarioId, string usuarioNome, SolicitarAlteracaoPlanoRequest req)
    {
        var tenant = await db.Tenants.FindAsync(tenantId)
            ?? throw new InvalidOperationException("Empresa não encontrada.");

        var solicitacao = new SolicitacaoAlteracaoPlano
        {
            TenantId = tenantId,
            SolicitadoPorUsuarioId = usuarioId,
            SolicitadoPorNome = string.IsNullOrWhiteSpace(usuarioNome) ? "Administrador da Empresa" : usuarioNome,
            ModuloAtual = tenant.ModuloContratado,
            ModuloSolicitado = string.IsNullOrWhiteSpace(req.ModuloSolicitado) ? "CompletoServicos" : req.ModuloSolicitado.Trim(),
            ObservacoesCliente = req.ObservacoesCliente?.Trim() ?? string.Empty,
            Status = "Pendente"
        };

        db.SolicitacoesAlteracaoPlano.Add(solicitacao);
        db.HistoricosComerciaisTenant.Add(new HistoricoComercialTenant
        {
            TenantId = tenantId,
            TipoAlteracao = "SolicitacaoCliente",
            MensalidadeAnterior = tenant.MensalidadeFinal,
            MensalidadeCalculada = tenant.MensalidadeCalculada,
            DescontoAplicado = tenant.DescontoComercial,
            AjusteManual = tenant.AjusteManual,
            MensalidadeFinal = tenant.MensalidadeFinal,
            ModuloContratado = tenant.ModuloContratado,
            VolumeUtilizacao = tenant.VolumeUtilizacao,
            Justificativa = $"Cliente solicitou alteração de módulos para '{solicitacao.ModuloSolicitado}'. Obs: {solicitacao.ObservacoesCliente}",
            AlteradoPor = solicitacao.SolicitadoPorNome,
            NotificadoCliente = true
        });

        await db.SaveChangesAsync();
        return solicitacao;
    }

    public async Task<List<SolicitacaoAlteracaoPlano>> ListarSolicitacoesTenantAsync(Guid tenantId)
    {
        return await db.SolicitacoesAlteracaoPlano
            .Where(s => s.TenantId == tenantId)
            .OrderByDescending(s => s.DataCriacao)
            .ToListAsync();
    }

    public async Task<List<HistoricoComercialDto>> ListarHistoricoComercialTenantAsync(Guid tenantId)
    {
        return await precificacaoService.ListarHistoricoComercialAsync(tenantId);
    }

    private static FaturaDto ToFaturaDto(Fatura f) => new(
        f.Id,
        f.Numero,
        f.Descricao,
        f.Valor,
        f.Status,
        f.DataEmissao,
        f.DataVencimento,
        f.DataPagamento,
        f.MetodoPagamento,
        f.PixQrCode,
        f.PixCopiaECola,
        f.UrlBoleto,
        f.ComprovanteUrl
    );
}
