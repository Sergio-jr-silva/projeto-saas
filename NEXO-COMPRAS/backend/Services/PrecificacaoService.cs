using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Nexo.Api.Data;
using Nexo.Api.DTOs;
using Nexo.Api.Models;
using Nexo.Api.MultiTenancy;

namespace Nexo.Api.Services;

public static class SlugHelper
{
    public static readonly HashSet<string> ReservedSlugs = new(StringComparer.OrdinalIgnoreCase)
    {
        "login", "admin", "planos", "api", "cadastro", "recuperar-senha",
        "cotacao", "portal-servico", "dashboard", "produtos", "fornecedores",
        "cotacoes", "servicos", "pedidos", "relatorios", "configuracoes",
        "usuarios", "filiais", "assinatura", "integracao", "sincronizacao",
        "historico", "comparacao", "economia", "analise-fornecedores",
        "health", "static", "assets", "webhooks", "portal", "auth", "system"
    };

    public static string NormalizeSlug(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;

        var trimmed = input.Trim().Trim('/');
        var normalized = trimmed.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();

        foreach (var c in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(c);
            if (category != UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        }

        var clean = sb.ToString().Normalize(NormalizationForm.FormC).ToLowerInvariant();
        clean = Regex.Replace(clean, @"[\s_]+", "-");
        clean = Regex.Replace(clean, @"[^a-z0-9\-]", "");
        clean = Regex.Replace(clean, @"\-{2,}", "-").Trim('-');

        if (clean.Length > 60)
            clean = clean[..60].Trim('-');

        return clean;
    }

    public static bool IsReservedSlug(string? slug)
    {
        if (string.IsNullOrWhiteSpace(slug)) return false;
        var norm = NormalizeSlug(slug);
        return ReservedSlugs.Contains(norm);
    }

    public static (bool Valido, string SlugNormalizado, string? Erro) ValidateSlug(string? rawSlug)
    {
        var norm = NormalizeSlug(rawSlug);
        if (string.IsNullOrWhiteSpace(norm) || norm.Length < 2)
            return (false, norm, "O slug da empresa deve conter ao menos 2 caracteres alfanuméricos.");

        if (ReservedSlugs.Contains(norm))
            return (false, norm, $"O endereço '/{norm}' é uma rota reservada do sistema e não pode ser utilizado por uma empresa.");

        if (!Regex.IsMatch(norm, @"^[a-z0-9]+(-[a-z0-9]+)*$"))
            return (false, norm, "Slug inválido. Utilize apenas letras minúsculas, números e hífens.");

        return (true, norm, null);
    }
}

public interface IPrecificacaoService
{
    Task<ConfiguracaoPrecificacao> ObterConfiguracaoAtivaAsync();
    Task<List<ConfiguracaoPrecificacao>> ListarVersoesConfiguracaoAsync();
    Task<ConfiguracaoPrecificacao> AtualizarConfiguracaoAsync(AtualizarConfiguracaoPrecificacaoRequest req, string usuarioAdmin);
    Task<SimulacaoPrecificacaoResponse> SimularAsync(SimulacaoPrecificacaoRequest req);
    Task<PrecificacaoTenantDetalheDto> ObterPrecificacaoTenantAsync(Guid tenantId);
    Task<List<PrecificacaoTenantDetalheDto>> ListarPrecificacaoTodosTenantsAsync();
    Task<PrecificacaoTenantDetalheDto> AplicarPrecificacaoTenantAsync(Guid tenantId, AplicarPrecificacaoTenantRequest req, string usuarioAdmin);
    Task<List<HistoricoComercialDto>> ListarHistoricoComercialAsync(Guid tenantId);
}

public class PrecificacaoService(NexoDbContext db, ITenantContext tenantCtx, IAuditService audit) : IPrecificacaoService
{
    public async Task<ConfiguracaoPrecificacao> ObterConfiguracaoAtivaAsync()
    {
        using var _ = tenantCtx.SystemScope();
        var cfg = await db.ConfiguracoesPrecificacao
            .IgnoreQueryFilters()
            .Where(c => c.Ativa)
            .OrderByDescending(c => c.Versao)
            .FirstOrDefaultAsync();

        if (cfg != null) return cfg;

        cfg = new ConfiguracaoPrecificacao
        {
            Versao = 1,
            Ativa = true,
            MensalidadeBase = 50.00m,
            ValorMinimo = 50.00m,
            ValorMaximo = 250.00m,
            AtualizadoPor = "Sistema",
            MotivoAlteracao = "Configuração padrão inicial"
        };
        db.ConfiguracoesPrecificacao.Add(cfg);
        await db.SaveChangesAsync();
        return cfg;
    }

    public async Task<List<ConfiguracaoPrecificacao>> ListarVersoesConfiguracaoAsync()
    {
        using var _ = tenantCtx.SystemScope();
        return await db.ConfiguracoesPrecificacao
            .IgnoreQueryFilters()
            .OrderByDescending(c => c.Versao)
            .ToListAsync();
    }

    public async Task<ConfiguracaoPrecificacao> AtualizarConfiguracaoAsync(AtualizarConfiguracaoPrecificacaoRequest req, string usuarioAdmin)
    {
        if (req.ValorMinimo < 50.00m || req.ValorMaximo > 250.00m || req.ValorMinimo > req.ValorMaximo)
            throw new ArgumentException("Os limites da mensalidade devem respeitar o intervalo comercial de R$ 50,00 a R$ 250,00.");

        if (req.MensalidadeBase < req.ValorMinimo || req.MensalidadeBase > req.ValorMaximo)
            throw new ArgumentException("A mensalidade-base deve estar entre o valor mínimo e máximo configurados.");

        using var _ = tenantCtx.SystemScope();
        var todas = await db.ConfiguracoesPrecificacao.IgnoreQueryFilters().ToListAsync();
        var maxVersao = todas.Count > 0 ? todas.Max(c => c.Versao) : 0;

        foreach (var antiga in todas.Where(c => c.Ativa))
        {
            antiga.Ativa = false;
        }

        var nova = new ConfiguracaoPrecificacao
        {
            Versao = maxVersao + 1,
            Ativa = true,
            MensalidadeBase = Math.Round(req.MensalidadeBase, 2),
            ValorMinimo = Math.Round(req.ValorMinimo, 2),
            ValorMaximo = Math.Round(req.ValorMaximo, 2),
            AdicProdFaixa1 = Math.Round(req.AdicProdFaixa1, 2),
            AdicProdFaixa2 = Math.Round(req.AdicProdFaixa2, 2),
            AdicProdFaixa3 = Math.Round(req.AdicProdFaixa3, 2),
            AdicProdFaixa4 = Math.Round(req.AdicProdFaixa4, 2),
            AdicFilialFaixa1 = Math.Round(req.AdicFilialFaixa1, 2),
            AdicFilialFaixa2 = Math.Round(req.AdicFilialFaixa2, 2),
            AdicFilialFaixa3 = Math.Round(req.AdicFilialFaixa3, 2),
            AdicFilialFaixa4 = Math.Round(req.AdicFilialFaixa4, 2),
            AdicUsuarioFaixa1 = Math.Round(req.AdicUsuarioFaixa1, 2),
            AdicUsuarioFaixa2 = Math.Round(req.AdicUsuarioFaixa2, 2),
            AdicUsuarioFaixa3 = Math.Round(req.AdicUsuarioFaixa3, 2),
            AdicUsuarioFaixa4 = Math.Round(req.AdicUsuarioFaixa4, 2),
            AdicSincFaixa1 = Math.Round(req.AdicSincFaixa1, 2),
            AdicSincFaixa2 = Math.Round(req.AdicSincFaixa2, 2),
            AdicSincFaixa3 = Math.Round(req.AdicSincFaixa3, 2),
            AdicVolumeBaixo = Math.Round(req.AdicVolumeBaixo, 2),
            AdicVolumeMedio = Math.Round(req.AdicVolumeMedio, 2),
            AdicVolumeAlto = Math.Round(req.AdicVolumeAlto, 2),
            AdicModuloBasico = Math.Round(req.AdicModuloBasico, 2),
            AdicModuloRelatorios = Math.Round(req.AdicModuloRelatorios, 2),
            AdicModuloCompleto = Math.Round(req.AdicModuloCompleto, 2),
            AtualizadoPor = string.IsNullOrWhiteSpace(usuarioAdmin) ? "AdministradorGeral" : usuarioAdmin,
            MotivoAlteracao = string.IsNullOrWhiteSpace(req.MotivoAlteracao) ? "Atualização de critérios comerciais" : req.MotivoAlteracao.Trim()
        };

        db.ConfiguracoesPrecificacao.Add(nova);
        audit.Add("TabelaPrecificacaoAtualizada", "ConfiguracaoPrecificacao", nova.Versao.ToString(), new { nova.Versao, nova.MensalidadeBase, nova.MotivoAlteracao });
        await db.SaveChangesAsync();
        return nova;
    }

    public async Task<SimulacaoPrecificacaoResponse> SimularAsync(SimulacaoPrecificacaoRequest req)
    {
        var cfg = await ObterConfiguracaoAtivaAsync();
        return CalcularComConfiguracao(cfg, req);
    }

    public static SimulacaoPrecificacaoResponse CalcularComConfiguracao(ConfiguracaoPrecificacao cfg, SimulacaoPrecificacaoRequest req)
    {
        var qtdProdutos = Math.Max(0, req.QuantidadeProdutos);
        var qtdFiliais = Math.Max(1, req.QuantidadeFiliais);
        var qtdUsuarios = Math.Max(1, req.QuantidadeUsuarios);
        var qtdSinc = Math.Max(1, req.QuantidadeSincronizadores);

        // 1. Produtos
        decimal adicProdutos;
        string faixaProdutos;
        if (qtdProdutos <= 1000)
        {
            adicProdutos = cfg.AdicProdFaixa1;
            faixaProdutos = "Até 1.000 produtos";
        }
        else if (qtdProdutos <= 3000)
        {
            adicProdutos = cfg.AdicProdFaixa2;
            faixaProdutos = "1.001 a 3.000 produtos";
        }
        else if (qtdProdutos <= 8000)
        {
            adicProdutos = cfg.AdicProdFaixa3;
            faixaProdutos = "3.001 a 8.000 produtos";
        }
        else
        {
            adicProdutos = cfg.AdicProdFaixa4;
            faixaProdutos = "Acima de 8.000 produtos";
        }

        // 2. Filiais
        decimal adicFiliais;
        string faixaFiliais;
        if (qtdFiliais <= 1)
        {
            adicFiliais = cfg.AdicFilialFaixa1;
            faixaFiliais = "1 filial";
        }
        else if (qtdFiliais <= 3)
        {
            adicFiliais = cfg.AdicFilialFaixa2;
            faixaFiliais = "2 a 3 filiais";
        }
        else if (qtdFiliais <= 6)
        {
            adicFiliais = cfg.AdicFilialFaixa3;
            faixaFiliais = "4 a 6 filiais";
        }
        else
        {
            adicFiliais = cfg.AdicFilialFaixa4;
            faixaFiliais = "Acima de 6 filiais";
        }

        // 3. Usuários
        decimal adicUsuarios;
        string faixaUsuarios;
        if (qtdUsuarios <= 2)
        {
            adicUsuarios = cfg.AdicUsuarioFaixa1;
            faixaUsuarios = "1 a 2 usuários";
        }
        else if (qtdUsuarios <= 5)
        {
            adicUsuarios = cfg.AdicUsuarioFaixa2;
            faixaUsuarios = "3 a 5 usuários";
        }
        else if (qtdUsuarios <= 10)
        {
            adicUsuarios = cfg.AdicUsuarioFaixa3;
            faixaUsuarios = "6 a 10 usuários";
        }
        else
        {
            adicUsuarios = cfg.AdicUsuarioFaixa4;
            faixaUsuarios = "Acima de 10 usuários";
        }

        // 4. Sincronizadores SINc
        decimal adicSinc;
        string faixaSinc;
        if (qtdSinc <= 1)
        {
            adicSinc = cfg.AdicSincFaixa1;
            faixaSinc = "1 sincronizador SINc";
        }
        else if (qtdSinc <= 3)
        {
            adicSinc = cfg.AdicSincFaixa2;
            faixaSinc = "2 a 3 sincronizadores SINc";
        }
        else
        {
            adicSinc = cfg.AdicSincFaixa3;
            faixaSinc = "4 ou mais sincronizadores SINc";
        }

        // 5. Volume de utilização
        var volNorm = (req.VolumeUtilizacao ?? "Baixo").Trim();
        decimal adicVolume;
        string faixaVolume;
        if (volNorm.Equals("Alto", StringComparison.OrdinalIgnoreCase))
        {
            adicVolume = cfg.AdicVolumeAlto;
            faixaVolume = "Alto";
        }
        else if (volNorm.Equals("Medio", StringComparison.OrdinalIgnoreCase) || volNorm.Equals("Médio", StringComparison.OrdinalIgnoreCase))
        {
            adicVolume = cfg.AdicVolumeMedio;
            faixaVolume = "Médio";
        }
        else
        {
            adicVolume = cfg.AdicVolumeBaixo;
            faixaVolume = "Baixo";
        }

        // 6. Módulos contratados
        var modNorm = (req.ModuloContratado ?? "Basico").Trim();
        decimal adicModulos;
        string faixaModulos;
        if (modNorm.Equals("CompletoServicos", StringComparison.OrdinalIgnoreCase) || modNorm.Contains("Servicos", StringComparison.OrdinalIgnoreCase) || modNorm.Contains("Serviços", StringComparison.OrdinalIgnoreCase))
        {
            adicModulos = cfg.AdicModuloCompleto;
            faixaModulos = "Compras, relatórios e serviços";
            modNorm = "CompletoServicos";
        }
        else if (modNorm.Equals("RelatoriosAvancados", StringComparison.OrdinalIgnoreCase) || modNorm.Contains("Relatorios", StringComparison.OrdinalIgnoreCase) || modNorm.Contains("Relatórios", StringComparison.OrdinalIgnoreCase))
        {
            adicModulos = cfg.AdicModuloRelatorios;
            faixaModulos = "Compras e relatórios avançados";
            modNorm = "RelatoriosAvancados";
        }
        else
        {
            adicModulos = cfg.AdicModuloBasico;
            faixaModulos = "Compras e cotações básicas";
            modNorm = "Basico";
        }

        var somaAdicionais = Math.Round(adicProdutos + adicFiliais + adicUsuarios + adicSinc + adicVolume + adicModulos, 2);
        var subtotalBruto = Math.Round(cfg.MensalidadeBase + somaAdicionais, 2);
        var mensalidadeCalculada = Math.Clamp(subtotalBruto, cfg.ValorMinimo, cfg.ValorMaximo);

        var desconto = Math.Max(0m, Math.Round(req.Desconto, 2));
        var ajusteManual = Math.Round(req.AjusteManual, 2);

        var liquido = mensalidadeCalculada - desconto + ajusteManual;
        var mensalidadeFinal = Math.Clamp(Math.Round(liquido, 2), cfg.ValorMinimo, cfg.ValorMaximo);

        string faixaComercial = mensalidadeFinal switch
        {
            <= 75m => "Essencial",
            <= 135m => "Comercial",
            <= 195m => "Corporativo",
            _ => "Enterprise"
        };

        return new SimulacaoPrecificacaoResponse(
            VersaoTabela: cfg.Versao,
            ValorBase: cfg.MensalidadeBase,
            ValorMinimo: cfg.ValorMinimo,
            ValorMaximo: cfg.ValorMaximo,
            AdicionalProdutos: adicProdutos,
            FaixaProdutos: faixaProdutos,
            AdicionalFiliais: adicFiliais,
            FaixaFiliais: faixaFiliais,
            AdicionalUsuarios: adicUsuarios,
            FaixaUsuarios: faixaUsuarios,
            AdicionalSincronizadores: adicSinc,
            FaixaSincronizadores: faixaSinc,
            AdicionalVolume: adicVolume,
            FaixaVolume: faixaVolume,
            AdicionalModulos: adicModulos,
            FaixaModulos: faixaModulos,
            ModuloCodigo: modNorm,
            SomaAdicionais: somaAdicionais,
            SubtotalBruto: subtotalBruto,
            MensalidadeCalculada: mensalidadeCalculada,
            Desconto: desconto,
            AjusteManual: ajusteManual,
            MensalidadeFinal: mensalidadeFinal,
            FaixaComercial: faixaComercial
        );
    }

    public async Task<PrecificacaoTenantDetalheDto> ObterPrecificacaoTenantAsync(Guid tenantId)
    {
        using var _ = tenantCtx.SystemScope();
        var cfg = await ObterConfiguracaoAtivaAsync();
        var t = await db.Tenants.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.Id == tenantId)
            ?? throw new InvalidOperationException("Empresa não encontrada.");

        return await ConstruirDetalheTenantAsync(t, cfg);
    }

    public async Task<List<PrecificacaoTenantDetalheDto>> ListarPrecificacaoTodosTenantsAsync()
    {
        using var _ = tenantCtx.SystemScope();
        var cfg = await ObterConfiguracaoAtivaAsync();
        var tenants = await db.Tenants.IgnoreQueryFilters().OrderBy(t => t.NomeFantasia).ToListAsync();

        var lista = new List<PrecificacaoTenantDetalheDto>(tenants.Count);
        foreach (var t in tenants)
        {
            lista.Add(await ConstruirDetalheTenantAsync(t, cfg));
        }
        return lista;
    }

    private async Task<PrecificacaoTenantDetalheDto> ConstruirDetalheTenantAsync(Tenant t, ConfiguracaoPrecificacao cfg)
    {
        var inicioMes = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        var totalProdutos = await db.Produtos.IgnoreQueryFilters().CountAsync(p => p.TenantId == t.Id && p.Ativo);
        var totalFiliais = Math.Max(1, await db.Filiais.IgnoreQueryFilters().CountAsync(f => f.TenantId == t.Id && f.Ativa));
        var totalUsuarios = Math.Max(1, await db.Usuarios.IgnoreQueryFilters().CountAsync(u => u.TenantId == t.Id && u.Ativo));
        var totalSinc = Math.Max(1, await db.Agentes.IgnoreQueryFilters().CountAsync(a => a.TenantId == t.Id && !a.Revogado));

        var cotacoesProdutosMes = await db.Cotacoes.IgnoreQueryFilters().CountAsync(c => c.TenantId == t.Id && c.DataCriacao >= inicioMes);
        var cotacoesServicosMes = await db.CotacoesServico.IgnoreQueryFilters().CountAsync(c => c.TenantId == t.Id && c.DataCriacao >= inicioMes);
        var pedidosMes = await db.PedidosCompra.IgnoreQueryFilters().CountAsync(p => p.TenantId == t.Id && p.DataCriacao >= inicioMes);
        var ordensServicoMes = await db.OrdensServico.IgnoreQueryFilters().CountAsync(o => o.TenantId == t.Id && o.DataCriacao >= inicioMes);

        // REGRA CRÍTICA: Não penalizar o cliente por sincronizações sem alterações no ERP (Criados + Atualizados == 0)
        var sincsMes = await db.Sincronizacoes.IgnoreQueryFilters()
            .Where(s => s.TenantId == t.Id && s.DataCriacao >= inicioMes)
            .Select(s => new { s.Recebidos, s.Criados, s.Atualizados })
            .ToListAsync();

        var sincsComAlteracao = sincsMes.Count(s => (s.Criados + s.Atualizados) > 0);
        var sincsSemAlteracao = sincsMes.Count(s => (s.Criados + s.Atualizados) == 0);
        var registrosEfetivosProcessados = sincsMes.Sum(s => s.Criados + s.Atualizados);

        var totalCotacoesMes = cotacoesProdutosMes + cotacoesServicosMes;
        var totalPedidosMes = pedidosMes + ordensServicoMes;

        // Classifica volume sugerido com base na atividade real efetiva (sem contar syncs vazios)
        var scoreAtividade = (totalCotacoesMes * 3) + (totalPedidosMes * 4) + sincsComAlteracao + (registrosEfetivosProcessados / 500);
        var volumeSugerido = scoreAtividade switch
        {
            <= 30 => "Baixo",
            <= 100 => "Medio",
            _ => "Alto"
        };

        var volumeAplicado = !string.IsNullOrWhiteSpace(t.VolumeUtilizacao) ? t.VolumeUtilizacao : volumeSugerido;
        var moduloAplicado = !string.IsNullOrWhiteSpace(t.ModuloContratado) ? t.ModuloContratado : "Basico";

        var sim = CalcularComConfiguracao(cfg, new SimulacaoPrecificacaoRequest(
            QuantidadeProdutos: totalProdutos,
            QuantidadeFiliais: totalFiliais,
            QuantidadeUsuarios: totalUsuarios,
            QuantidadeSincronizadores: totalSinc,
            VolumeUtilizacao: volumeAplicado,
            ModuloContratado: moduloAplicado,
            Desconto: t.DescontoComercial,
            AjusteManual: t.AjusteManual
        ));

        return new PrecificacaoTenantDetalheDto(
            TenantId: t.Id,
            RazaoSocial: t.RazaoSocial,
            NomeFantasia: t.NomeFantasia,
            Slug: t.Slug,
            Cnpj: t.Cnpj,
            Status: t.Status,
            ProdutosCadastrados: totalProdutos,
            FiliaisAtivas: totalFiliais,
            UsuariosAtivos: totalUsuarios,
            SincronizadoresAtivos: totalSinc,
            CotacoesNoMes: totalCotacoesMes,
            PedidosNoMes: totalPedidosMes,
            SincronizacoesEfetivasNoMes: sincsComAlteracao,
            SincronizacoesSemAlteracaoIgnoradas: sincsSemAlteracao,
            RegistrosProcessadosNoMes: registrosEfetivosProcessados,
            VolumeUtilizacao: volumeAplicado,
            VolumeSugerido: volumeSugerido,
            ModuloContratado: sim.ModuloCodigo,
            ModuloDescricao: sim.FaixaModulos,
            MensalidadeCalculada: sim.MensalidadeCalculada,
            Desconto: t.DescontoComercial,
            AjusteManual: t.AjusteManual,
            MensalidadeFinal: t.MensalidadeFinal > 0 ? Math.Clamp(t.MensalidadeFinal, cfg.ValorMinimo, cfg.ValorMaximo) : sim.MensalidadeFinal,
            MensalidadeSimuladaAtual: sim.MensalidadeFinal,
            FaixaComercial: sim.FaixaComercial,
            JustificativaPreco: t.JustificativaPreco,
            DataInicioCobranca: t.DataInicioCobranca ?? t.DataCriacao,
            ProximoVencimento: t.ProximoVencimento ?? DateTime.UtcNow.AddDays(15),
            SituacaoFinanceira: string.IsNullOrWhiteSpace(t.SituacaoFinanceira) ? "Regular" : t.SituacaoFinanceira,
            MaxUsuariosContratado: t.MaxUsuariosContratado,
            MaxFiliaisContratado: t.MaxFiliaisContratado,
            MaxProdutosContratado: t.MaxProdutosContratado,
            MaxSincronizadoresContratado: t.MaxSincronizadoresContratado,
            DetalheCalculo: sim
        );
    }

    public async Task<PrecificacaoTenantDetalheDto> AplicarPrecificacaoTenantAsync(Guid tenantId, AplicarPrecificacaoTenantRequest req, string usuarioAdmin)
    {
        using var _ = tenantCtx.SystemScope();
        var cfg = await ObterConfiguracaoAtivaAsync();
        var t = await db.Tenants.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.Id == tenantId)
            ?? throw new InvalidOperationException("Empresa não encontrada.");

        if ((req.Desconto > 0 || req.AjusteManual != 0) && string.IsNullOrWhiteSpace(req.Justificativa))
            throw new ArgumentException("É obrigatório informar uma justificativa comercial para aplicar descontos ou ajustes manuais.");

        var anterior = t.MensalidadeFinal;

        if (!string.IsNullOrWhiteSpace(req.ModuloContratado))
            t.ModuloContratado = req.ModuloContratado.Trim();

        if (!string.IsNullOrWhiteSpace(req.VolumeUtilizacao))
            t.VolumeUtilizacao = req.VolumeUtilizacao.Trim();

        t.DescontoComercial = Math.Max(0m, Math.Round(req.Desconto, 2));
        t.AjusteManual = Math.Round(req.AjusteManual, 2);

        if (req.DataInicioCobranca.HasValue)
            t.DataInicioCobranca = req.DataInicioCobranca.Value;
        else if (!t.DataInicioCobranca.HasValue)
            t.DataInicioCobranca = DateTime.UtcNow;

        if (req.ProximoVencimento.HasValue)
            t.ProximoVencimento = req.ProximoVencimento.Value;
        else if (!t.ProximoVencimento.HasValue)
            t.ProximoVencimento = DateTime.UtcNow.AddDays(30);

        if (!string.IsNullOrWhiteSpace(req.SituacaoFinanceira))
            t.SituacaoFinanceira = req.SituacaoFinanceira.Trim();

        if (req.MaxUsuariosContratado.HasValue && req.MaxUsuariosContratado.Value > 0)
            t.MaxUsuariosContratado = req.MaxUsuariosContratado.Value;
        if (req.MaxFiliaisContratado.HasValue && req.MaxFiliaisContratado.Value > 0)
            t.MaxFiliaisContratado = req.MaxFiliaisContratado.Value;
        if (req.MaxProdutosContratado.HasValue && req.MaxProdutosContratado.Value > 0)
            t.MaxProdutosContratado = req.MaxProdutosContratado.Value;
        if (req.MaxSincronizadoresContratado.HasValue && req.MaxSincronizadoresContratado.Value > 0)
            t.MaxSincronizadoresContratado = req.MaxSincronizadoresContratado.Value;

        var detalhePrevio = await ConstruirDetalheTenantAsync(t, cfg);
        t.MensalidadeCalculada = detalhePrevio.DetalheCalculo.MensalidadeCalculada;
        t.MensalidadeFinal = detalhePrevio.DetalheCalculo.MensalidadeFinal;
        t.JustificativaPreco = req.Justificativa?.Trim() ?? "Atualização comercial pelo Administrador Geral";

        // Sincroniza valor com a Assinatura ativa da empresa
        var assinatura = await db.Assinaturas.IgnoreQueryFilters()
            .OrderByDescending(a => a.DataCriacao)
            .FirstOrDefaultAsync(a => a.TenantId == t.Id);

        if (assinatura != null)
        {
            assinatura.Valor = t.MensalidadeFinal;
            if (t.ProximoVencimento.HasValue)
                assinatura.DataRenovacao = t.ProximoVencimento.Value;
        }

        // Registra no Histórico Comercial e Auditoria
        var hist = new HistoricoComercialTenant
        {
            TenantId = t.Id,
            TipoAlteracao = "Precificacao",
            MensalidadeAnterior = anterior,
            MensalidadeCalculada = t.MensalidadeCalculada,
            DescontoAplicado = t.DescontoComercial,
            AjusteManual = t.AjusteManual,
            MensalidadeFinal = t.MensalidadeFinal,
            ModuloContratado = t.ModuloContratado,
            VolumeUtilizacao = t.VolumeUtilizacao,
            Justificativa = t.JustificativaPreco,
            AlteradoPor = string.IsNullOrWhiteSpace(usuarioAdmin) ? "AdministradorGeral" : usuarioAdmin,
            NotificadoCliente = true,
            VersaoTabelaPrecos = cfg.Versao,
            SnapshotConsumoJson = JsonSerializer.Serialize(new
            {
                detalhePrevio.ProdutosCadastrados,
                detalhePrevio.FiliaisAtivas,
                detalhePrevio.UsuariosAtivos,
                detalhePrevio.SincronizadoresAtivos,
                detalhePrevio.CotacoesNoMes,
                detalhePrevio.PedidosNoMes
            })
        };
        db.HistoricosComerciaisTenant.Add(hist);

        audit.Add("PrecificacaoTenantAtualizada", "Tenant", t.Id.ToString(), new
        {
            de = anterior,
            calculada = t.MensalidadeCalculada,
            desconto = t.DescontoComercial,
            ajuste = t.AjusteManual,
            final = t.MensalidadeFinal,
            justificativa = t.JustificativaPreco
        }, t.Id);

        await db.SaveChangesAsync();
        return await ConstruirDetalheTenantAsync(t, cfg);
    }

    public async Task<List<HistoricoComercialDto>> ListarHistoricoComercialAsync(Guid tenantId)
    {
        using var _ = tenantCtx.SystemScope();
        return await db.HistoricosComerciaisTenant.IgnoreQueryFilters()
            .Where(h => h.TenantId == tenantId)
            .OrderByDescending(h => h.DataCriacao)
            .Select(h => new HistoricoComercialDto(
                h.Id,
                h.TenantId,
                h.TipoAlteracao,
                h.MensalidadeAnterior,
                h.MensalidadeCalculada,
                h.DescontoAplicado,
                h.AjusteManual,
                h.MensalidadeFinal,
                h.ModuloContratado,
                h.VolumeUtilizacao,
                h.Justificativa,
                h.AlteradoPor,
                h.NotificadoCliente,
                h.VersaoTabelaPrecos,
                h.DataCriacao
            ))
            .ToListAsync();
    }
}
