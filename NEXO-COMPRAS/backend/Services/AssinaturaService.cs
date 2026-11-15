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
}

public class AssinaturaService(NexoDbContext db) : IAssinaturaService
{
    public async Task<AssinaturaDetalheDto> ObterAssinaturaAtualAsync(Guid tenantId)
    {
        var tenant = await db.Tenants.Include(t => t.Plano).FirstOrDefaultAsync(t => t.Id == tenantId);
        if (tenant == null) throw new InvalidOperationException("Empresa não encontrada.");

        var assinatura = await db.Assinaturas.Include(a => a.Plano)
            .OrderByDescending(a => a.DataCriacao)
            .FirstOrDefaultAsync(a => a.TenantId == tenantId);

        var plano = assinatura?.Plano ?? tenant.Plano ?? await db.Planos.OrderBy(p => p.Mensalidade).FirstAsync();

        // Se o tenant ainda não tiver assinatura formal, provisiona a assinatura inicial de teste gratuita (Trial)
        if (assinatura == null)
        {
            var diasTeste = plano.DiasTeste > 0 ? plano.DiasTeste : 14;
            assinatura = new Assinatura
            {
                TenantId = tenantId,
                PlanoId = plano.Id,
                Status = "Teste",
                Ciclo = "Mensal",
                Valor = plano.Mensalidade,
                DataInicio = tenant.DataCriacao,
                DataFim = tenant.DataCriacao.AddDays(diasTeste),
                DataRenovacao = tenant.DataCriacao.AddDays(diasTeste),
                FormaPagamento = "Pix"
            };
            db.Assinaturas.Add(assinatura);
            await db.SaveChangesAsync();
        }

        // Calcula limites utilizados
        var totalUsuarios = await db.Usuarios.CountAsync(u => u.TenantId == tenantId && u.Ativo);
        var totalFiliais = await db.Filiais.CountAsync(f => f.TenantId == tenantId && f.Ativa);
        var inicioMes = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var cotacoesMes = await db.Cotacoes.CountAsync(c => c.TenantId == tenantId && c.DataCriacao >= inicioMes);

        int diasRestantes = 0;
        if (assinatura.Status == "Teste" && assinatura.DataFim.HasValue)
        {
            diasRestantes = Math.Max(0, (int)(assinatura.DataFim.Value - DateTime.UtcNow).TotalDays);
        }

        var limitesDto = new LimitesUsoDto(
            UsuariosUsados: totalUsuarios,
            MaxUsuarios: plano.MaxUsuarios,
            FiliaisUsadas: totalFiliais,
            MaxFiliais: plano.MaxFiliais,
            CotacoesMesUsadas: cotacoesMes,
            MaxCotacoesMes: plano.MaxCotacoesMes
        );

        return new AssinaturaDetalheDto(
            Id: assinatura.Id,
            PlanoId: plano.Id,
            PlanoNome: plano.Nome,
            Status: assinatura.Status,
            Ciclo: assinatura.Ciclo,
            Valor: assinatura.Valor,
            DataInicio: assinatura.DataInicio,
            DataFim: assinatura.DataFim,
            DataRenovacao: assinatura.DataRenovacao,
            FormaPagamento: assinatura.FormaPagamento,
            DiasRestantesTeste: diasRestantes,
            Limites: limitesDto
        );
    }

    public async Task<FaturaDto> RealizarCheckoutAsync(Guid tenantId, CheckoutAssinaturaRequest req)
    {
        var plano = await db.Planos.FindAsync(req.PlanoId);
        if (plano == null || !plano.Ativo) throw new ArgumentException("Plano selecionado inválido ou inativo.");

        var ciclo = req.Ciclo?.Equals("Anual", StringComparison.OrdinalIgnoreCase) == true ? "Anual" : "Mensal";
        var valor = ciclo == "Anual" 
            ? (plano.PrecoAnual > 0 ? plano.PrecoAnual : plano.Mensalidade * 10m)
            : plano.Mensalidade;

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
                GatewayAssinaturaId = $"sub_asaas_{Guid.NewGuid():N}"[..18]
            };
            db.Assinaturas.Add(assinatura);
        }
        else
        {
            assinatura.PlanoId = plano.Id;
            assinatura.Ciclo = ciclo;
            assinatura.Valor = valor;
            assinatura.FormaPagamento = req.FormaPagamento;
            assinatura.Status = "Ativa";
            assinatura.DataCancelamento = null;
            assinatura.MotivoCancelamento = string.Empty;
            assinatura.DataRenovacao = agora.AddDays(duracaoDias);
            assinatura.DataFim = agora.AddDays(duracaoDias);
        }

        // Atualiza tenant com plano e reativa caso estivesse bloqueado
        var tenant = await db.Tenants.FindAsync(tenantId);
        if (tenant != null)
        {
            tenant.PlanoId = plano.Id;
            tenant.Status = "Ativo";
            tenant.DataExpiracao = agora.AddDays(duracaoDias + 5);
        }

        // Cria a fatura do checkout
        var faturaNumero = $"FAT-{agora:yyyyMMdd}-{Random.Shared.Next(1000, 9999)}";
        var fatura = new Fatura
        {
            TenantId = tenantId,
            AssinaturaId = assinatura.Id,
            Numero = faturaNumero,
            Descricao = $"Assinatura Nexo Compras SaaS — Plano {plano.Nome} ({ciclo})",
            Valor = valor,
            Status = req.FormaPagamento.Equals("Cartao", StringComparison.OrdinalIgnoreCase) ? "Paga" : "Pendente",
            DataEmissao = agora,
            DataVencimento = agora.AddDays(3),
            DataPagamento = req.FormaPagamento.Equals("Cartao", StringComparison.OrdinalIgnoreCase) ? agora : null,
            MetodoPagamento = req.FormaPagamento,
            PixQrCode = "https://api.qrserver.com/v1/create-qr-code/?size=250x250&data=00020126580014br.gov.bcb.pix0136nexo-compras-pix-pagamento-2026520400005303986540" + valor.ToString("F2").Replace(",", "."),
            PixCopiaECola = $"00020126580014br.gov.bcb.pix0136nexocompras-pix-{faturaNumero.ToLower()}520400005303986540{valor:F2}5802BR5925NEXO COMPRAS SAAS LTDA6009SAO PAULO62070503***6304",
            UrlBoleto = $"https://app.nexocompras.com.br/faturas/{faturaNumero}/boleto.pdf",
            GatewayTransacaoId = $"trans_{Guid.NewGuid():N}"[..20]
        };
        db.Faturas.Add(fatura);

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
        if (tenant != null) tenant.Status = "Ativo";

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
            tenant.DataExpiracao = DateTime.UtcNow.AddDays(35);
        }

        await db.SaveChangesAsync();
        return ToFaturaDto(fatura);
    }

    public async Task<bool> ProcessarWebhookAsync(WebhookPagamentoPayload payload)
    {
        if (string.IsNullOrWhiteSpace(payload.FaturaNumero)) return false;

        var fatura = await db.Faturas.FirstOrDefaultAsync(f => f.Numero == payload.FaturaNumero || f.GatewayTransacaoId == payload.TransacaoId);
        if (fatura == null) return false;

        if (payload.Evento.Equals("fatura.paga", StringComparison.OrdinalIgnoreCase))
        {
            fatura.Status = "Paga";
            fatura.DataPagamento = payload.Timestamp != default ? payload.Timestamp : DateTime.UtcNow;

            var assinatura = await db.Assinaturas.FirstOrDefaultAsync(a => a.Id == fatura.AssinaturaId);
            if (assinatura != null)
            {
                assinatura.Status = "Ativa";
                var dias = assinatura.Ciclo == "Anual" ? 365 : 30;
                assinatura.DataRenovacao = DateTime.UtcNow.AddDays(dias);
            }

            var tenant = await db.Tenants.FindAsync(fatura.TenantId);
            if (tenant != null) tenant.Status = "Ativo";
        }
        else if (payload.Evento.Equals("fatura.vencida", StringComparison.OrdinalIgnoreCase) || payload.Evento.Equals("fatura.falha", StringComparison.OrdinalIgnoreCase))
        {
            fatura.Status = "Vencida";

            var assinatura = await db.Assinaturas.FirstOrDefaultAsync(a => a.Id == fatura.AssinaturaId);
            if (assinatura != null) assinatura.Status = "Inadimplente";

            // Se vencida e ultrapassou tolerância de 5 dias, suspende o acesso da empresa
            if (DateTime.UtcNow > fatura.DataVencimento.AddDays(5))
            {
                var tenant = await db.Tenants.FindAsync(fatura.TenantId);
                if (tenant != null) tenant.Status = "Bloqueado";
            }
        }
        else if (payload.Evento.Equals("assinatura.cancelada", StringComparison.OrdinalIgnoreCase))
        {
            var assinatura = await db.Assinaturas.FirstOrDefaultAsync(a => a.Id == fatura.AssinaturaId);
            if (assinatura != null)
            {
                assinatura.Status = "Cancelada";
                assinatura.DataCancelamento = DateTime.UtcNow;
            }
        }

        await db.SaveChangesAsync();
        return true;
    }

    public async Task<(bool Permitido, string Mensagem)> ValidarLimiteAsync(Guid tenantId, string tipoRecurso)
    {
        var tenant = await db.Tenants.Include(t => t.Plano).FirstOrDefaultAsync(t => t.Id == tenantId);
        if (tenant == null) return (false, "Empresa não encontrada.");

        var plano = tenant.Plano ?? await db.Planos.OrderBy(p => p.Mensalidade).FirstAsync();

        if (tipoRecurso.Equals("Usuario", StringComparison.OrdinalIgnoreCase))
        {
            var total = await db.Usuarios.CountAsync(u => u.TenantId == tenantId && u.Ativo);
            if (total >= plano.MaxUsuarios)
                return (false, $"Limite de usuários do Plano {plano.Nome} atingido ({total}/{plano.MaxUsuarios}). Faça upgrade de plano para adicionar mais usuários.");
        }
        else if (tipoRecurso.Equals("Filial", StringComparison.OrdinalIgnoreCase))
        {
            var total = await db.Filiais.CountAsync(f => f.TenantId == tenantId && f.Ativa);
            if (total >= plano.MaxFiliais)
                return (false, $"Limite de filiais do Plano {plano.Nome} atingido ({total}/{plano.MaxFiliais}). Faça upgrade de plano para cadastrar novas filiais.");
        }
        else if (tipoRecurso.Equals("Cotacao", StringComparison.OrdinalIgnoreCase))
        {
            var inicioMes = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var total = await db.Cotacoes.CountAsync(c => c.TenantId == tenantId && c.DataCriacao >= inicioMes);
            if (total >= plano.MaxCotacoesMes)
                return (false, $"Limite mensal de cotações do Plano {plano.Nome} atingido ({total}/{plano.MaxCotacoesMes}). Faça upgrade de plano para continuar cotando este mês.");
        }

        return (true, string.Empty);
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

