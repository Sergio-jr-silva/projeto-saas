using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nexo.Api.Data;
using Nexo.Api.Models;
using Nexo.Api.MultiTenancy;
using Nexo.Api.Services;

namespace Nexo.Api.Controllers;

[ApiController]
[Route("api/relatorios")]
[Authorize]
public class RelatoriosController : ControllerBase
{
    private readonly NexoDbContext _db;
    private readonly ITenantContext _tenant;

    public RelatoriosController(NexoDbContext db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    /// <summary>
    /// Relatório 1: Compras Realizadas com KPIs, agrupamentos e lista de pedidos.
    /// </summary>
    [HttpGet("compras")]
    public async Task<IActionResult> RelatorioCompras(
        [FromQuery] DateTime? dataInicio,
        [FromQuery] DateTime? dataFim,
        [FromQuery] Guid? fornecedorId,
        [FromQuery] Guid? filialId)
    {
        var q = _db.PedidosCompra
            .Include(p => p.Fornecedor)
            .Include(p => p.Cotacao)
            .Include(p => p.Itens)
            .Where(p => p.Status != "Cancelado")
            .AsNoTracking()
            .AsQueryable();

        if (dataInicio.HasValue)
            q = q.Where(p => p.DataCriacao >= dataInicio.Value.Date);

        if (dataFim.HasValue)
            q = q.Where(p => p.DataCriacao <= dataFim.Value.Date.AddDays(1).AddTicks(-1));

        if (fornecedorId.HasValue)
            q = q.Where(p => p.FornecedorId == fornecedorId.Value);

        var pedidos = await q.OrderByDescending(p => p.DataCriacao).ToListAsync();

        var totalComprado = pedidos.Sum(p => p.ValorTotal);
        var totalPedidos = pedidos.Count;
        var ticketMedio = totalPedidos > 0 ? Math.Round(totalComprado / totalPedidos, 2) : 0m;
        var totalItens = pedidos.Sum(p => p.Itens.Count);

        // Agrupamento por fornecedor
        var porFornecedor = pedidos
            .GroupBy(p => p.Fornecedor != null ? (string.IsNullOrEmpty(p.Fornecedor.NomeFantasia) ? p.Fornecedor.RazaoSocial : p.Fornecedor.NomeFantasia) : "Não especificado")
            .Select(g => new
            {
                Fornecedor = g.Key,
                Total = g.Sum(x => x.ValorTotal),
                QtdPedidos = g.Count(),
                Percentual = totalComprado > 0 ? Math.Round((g.Sum(x => x.ValorTotal) / totalComprado) * 100, 2) : 0m
            })
            .OrderByDescending(x => x.Total)
            .ToList();

        // Agrupamento mensal / temporal
        var porPeriodo = pedidos
            .GroupBy(p => p.DataCriacao.ToString("yyyy-MM"))
            .Select(g => new
            {
                Periodo = g.Key,
                Total = g.Sum(x => x.ValorTotal),
                QtdPedidos = g.Count()
            })
            .OrderBy(x => x.Periodo)
            .ToList();

        // Agrupamento por status
        var porStatus = pedidos
            .GroupBy(p => p.Status)
            .Select(g => new
            {
                Status = g.Key,
                Quantidade = g.Count(),
                Total = g.Sum(x => x.ValorTotal)
            })
            .ToList();

        var itensFormatados = pedidos.Select(p => new
        {
            p.Id,
            p.Numero,
            DataCriacao = p.DataCriacao.ToString("yyyy-MM-dd HH:mm"),
            FornecedorNome = p.Fornecedor != null ? (string.IsNullOrEmpty(p.Fornecedor.NomeFantasia) ? p.Fornecedor.RazaoSocial : p.Fornecedor.NomeFantasia) : "",
            FornecedorCnpj = p.Fornecedor?.Cnpj ?? "",
            p.CondicaoPagamento,
            p.PrazoEntregaDias,
            p.Subtotal,
            p.Desconto,
            p.Frete,
            p.OutrasDespesas,
            p.ImpostosTotal,
            p.ValorTotal,
            p.Status,
            CotacaoNumero = p.Cotacao?.Numero ?? "-",
            QtdItens = p.Itens.Count
        }).ToList();

        return Ok(new
        {
            kpis = new
            {
                totalComprado,
                totalPedidos,
                ticketMedio,
                totalItens
            },
            porFornecedor,
            porPeriodo,
            porStatus,
            pedidos = itensFormatados
        });
    }

    /// <summary>
    /// Relatório 2: Economia Realizada vs Potencial.
    /// </summary>
    [HttpGet("economia")]
    public async Task<IActionResult> RelatorioEconomia(
        [FromQuery] DateTime? dataInicio,
        [FromQuery] DateTime? dataFim)
    {
        var q = _db.Cotacoes
            .Include(c => c.Itens)
            .Include(c => c.Fornecedores).ThenInclude(f => f.Fornecedor)
            .Where(c => c.Status == "Aprovada")
            .AsNoTracking()
            .AsQueryable();

        if (dataInicio.HasValue)
            q = q.Where(c => c.DataCriacao >= dataInicio.Value.Date);

        if (dataFim.HasValue)
            q = q.Where(c => c.DataCriacao <= dataFim.Value.Date.AddDays(1).AddTicks(-1));

        var cotacoes = await q.OrderByDescending(c => c.DataCriacao).ToListAsync();

        var economiaRealizada = cotacoes.Sum(c => c.EconomiaRealizada);
        var economiaPotencial = cotacoes.Sum(c => c.EconomiaPotencial);
        var totalAprovado = cotacoes.Sum(c => c.TotalAprovado);
        var baseComparativa = totalAprovado + economiaRealizada;
        var percentualEconomia = baseComparativa > 0 ? Math.Round((economiaRealizada / baseComparativa) * 100, 2) : 0m;

        var detalhes = cotacoes.Select(c =>
        {
            var totalRef = c.TotalAprovado + c.EconomiaRealizada;
            var perc = totalRef > 0 ? Math.Round((c.EconomiaRealizada / totalRef) * 100, 2) : 0m;
            var fornecedoresVencedores = c.Fornecedores
                .Where(f => f.Status == "Aprovada" && f.Fornecedor != null)
                .Select(f => string.IsNullOrEmpty(f.Fornecedor!.NomeFantasia) ? f.Fornecedor.RazaoSocial : f.Fornecedor.NomeFantasia)
                .Distinct()
                .ToList();

            return new
            {
                c.Id,
                c.Numero,
                c.Titulo,
                Data = c.DataCriacao.ToString("yyyy-MM-dd"),
                c.TotalAprovado,
                TotalReferencia = totalRef,
                c.EconomiaRealizada,
                c.EconomiaPotencial,
                PercentualEconomia = perc,
                ItensQtd = c.Itens.Count,
                Fornecedores = string.Join(", ", fornecedoresVencedores)
            };
        }).ToList();

        return Ok(new
        {
            kpis = new
            {
                economiaRealizada,
                economiaPotencial,
                totalAprovado,
                percentualEconomia,
                totalCotacoesAprovadas = cotacoes.Count
            },
            detalhes
        });
    }

    /// <summary>
    /// Relatório 3: Desempenho e Ranking de Fornecedores.
    /// </summary>
    [HttpGet("fornecedores")]
    public async Task<IActionResult> RelatorioFornecedores(
        [FromQuery] DateTime? dataInicio,
        [FromQuery] DateTime? dataFim)
    {
        var fornecedores = await _db.Fornecedores
            .Where(f => f.Ativo)
            .AsNoTracking()
            .ToListAsync();

        var qPedidos = _db.PedidosCompra
            .Include(p => p.Fornecedor)
            .Where(p => p.Status != "Cancelado")
            .AsNoTracking()
            .AsQueryable();

        var qCotForn = _db.CotacaoFornecedores
            .Include(cf => cf.Resposta)
            .Include(cf => cf.Fornecedor)
            .AsNoTracking()
            .AsQueryable();

        if (dataInicio.HasValue)
        {
            qPedidos = qPedidos.Where(p => p.DataCriacao >= dataInicio.Value.Date);
            qCotForn = qCotForn.Where(cf => cf.DataCriacao >= dataInicio.Value.Date);
        }

        if (dataFim.HasValue)
        {
            qPedidos = qPedidos.Where(p => p.DataCriacao <= dataFim.Value.Date.AddDays(1).AddTicks(-1));
            qCotForn = qCotForn.Where(cf => cf.DataCriacao <= dataFim.Value.Date.AddDays(1).AddTicks(-1));
        }

        var pedidos = await qPedidos.ToListAsync();
        var convites = await qCotForn.ToListAsync();

        var ranking = fornecedores.Select(f =>
        {
            var pedidosForn = pedidos.Where(p => p.FornecedorId == f.Id).ToList();
            var convitesForn = convites.Where(cf => cf.FornecedorId == f.Id).ToList();
            var respostasForn = convitesForn.Count(cf => cf.Resposta is { Status: "Enviada" });
            var taxaParticipacao = convitesForn.Count > 0 ? Math.Round(((decimal)respostasForn / convitesForn.Count) * 100, 1) : 0m;
            var totalVendido = pedidosForn.Sum(p => p.ValorTotal);
            var prazoMedio = pedidosForn.Count > 0 ? (int)Math.Round(pedidosForn.Average(p => p.PrazoEntregaDias)) : f.PrazoEntregaPadrao;

            return new
            {
                f.Id,
                Nome = string.IsNullOrEmpty(f.NomeFantasia) ? f.RazaoSocial : f.NomeFantasia,
                RazaoSocial = f.RazaoSocial,
                Cnpj = f.Cnpj,
                f.Cidade,
                f.Uf,
                TotalVendido = totalVendido,
                QtdPedidos = pedidosForn.Count,
                CotacoesConvidadas = convitesForn.Count,
                PropostasEnviadas = respostasForn,
                TaxaParticipacao = taxaParticipacao,
                PrazoMedioDias = prazoMedio,
                CondicaoPagamentoPadrao = f.CondicoesPagamento ?? "A combinar"
            };
        })
        .OrderByDescending(x => x.TotalVendido)
        .ThenByDescending(x => x.PropostasEnviadas)
        .ToList();

        var totalFinanceiro = ranking.Sum(r => r.TotalVendido);

        return Ok(new
        {
            totalFornecedores = fornecedores.Count,
            fornecedoresAtivosComprados = ranking.Count(r => r.QtdPedidos > 0),
            totalGeralComprado = totalFinanceiro,
            ranking
        });
    }

    /// <summary>
    /// Relatório 4: Análise de Produtos Mais Comprados / Cotados e Variação de Preços.
    /// </summary>
    [HttpGet("produtos")]
    public async Task<IActionResult> RelatorioProdutos([FromQuery] string? busca)
    {
        var q = _db.Produtos
            .Include(p => p.FornecedorVinculado)
            .Where(p => p.Ativo)
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(busca))
        {
            var b = busca.Trim().ToLower();
            q = q.Where(p => p.Descricao.ToLower().Contains(b) || p.CodigoErp.ToLower().Contains(b));
        }

        var produtos = await q.ToListAsync();
        var pedidosItens = await _db.PedidoCompraItens.AsNoTracking().ToListAsync();
        var histPrecos = await _db.HistoricoPrecos.AsNoTracking().ToListAsync();

        var relatorio = produtos.Select(p =>
        {
            var itensP = pedidosItens.Where(i => i.ProdutoId == p.Id).ToList();
            var totalQtdComprada = itensP.Sum(i => i.Quantidade);
            var totalGasto = itensP.Sum(i => i.Subtotal);
            var precosHistoricos = histPrecos.Where(h => h.ProdutoId == p.Id).OrderBy(h => h.Data).Select(h => h.Valor).ToList();

            decimal menorPreco = precosHistoricos.Count > 0 ? precosHistoricos.Min() : (p.UltimoCusto > 0 ? p.UltimoCusto : p.CustoAtual);
            decimal maiorPreco = precosHistoricos.Count > 0 ? precosHistoricos.Max() : (p.UltimoCusto > 0 ? p.UltimoCusto : p.CustoAtual);
            decimal variacaoPerc = menorPreco > 0 ? Math.Round(((maiorPreco - menorPreco) / menorPreco) * 100, 2) : 0m;

            return new
            {
                p.Id,
                Codigo = p.CodigoErp,
                p.Descricao,
                p.Unidade,
                p.Grupo,
                p.EstoqueAtual,
                p.Consumo90Dias,
                CustoAtual = p.CustoAtual,
                UltimoCusto = p.UltimoCusto,
                PrecoVenda = p.PrecoVenda,
                TotalQtdComprada = totalQtdComprada,
                TotalGasto = totalGasto,
                QtdPedidos = itensP.Count,
                MenorPrecoRegistrado = menorPreco,
                MaiorPrecoRegistrado = maiorPreco,
                VariacaoPrecoPerc = variacaoPerc,
                FornecedorPrincipal = p.FornecedorVinculado != null ? (string.IsNullOrEmpty(p.FornecedorVinculado.NomeFantasia) ? p.FornecedorVinculado.RazaoSocial : p.FornecedorVinculado.NomeFantasia) : "-"
            };
        })
        .OrderByDescending(p => p.TotalGasto)
        .ThenByDescending(p => p.TotalQtdComprada)
        .ToList();

        return Ok(new
        {
            totalProdutos = produtos.Count,
            totalGastoGlobal = relatorio.Sum(r => r.TotalGasto),
            produtos = relatorio
        });
    }

    /// <summary>
    /// Relatório 5: Status e Indicadores Operacionais de Cotações.
    /// </summary>
    [HttpGet("cotacoes")]
    public async Task<IActionResult> RelatorioCotacoes()
    {
        var cotacoes = await _db.Cotacoes
            .Include(c => c.Itens)
            .Include(c => c.Fornecedores).ThenInclude(f => f.Resposta)
            .Include(c => c.Fornecedores).ThenInclude(f => f.Acesso)
            .AsNoTracking()
            .OrderByDescending(c => c.DataCriacao)
            .ToListAsync();

        var total = cotacoes.Count;
        var abertas = cotacoes.Count(c => c.Status == "Aberta");
        var emAnalise = cotacoes.Count(c => c.Status == "EmAnalise");
        var aprovadas = cotacoes.Count(c => c.Status == "Aprovada");
        var canceladas = cotacoes.Count(c => c.Status == "Cancelada");

        var totalConvites = cotacoes.Sum(c => c.Fornecedores.Count);
        var totalRespostas = cotacoes.Sum(c => c.Fornecedores.Count(f => f.Resposta is { Status: "Enviada" }));
        var taxaRespostaGlobal = totalConvites > 0 ? Math.Round(((decimal)totalRespostas / totalConvites) * 100, 1) : 0m;

        var lista = cotacoes.Select(c => new
        {
            c.Id,
            c.Numero,
            c.Titulo,
            c.Status,
            DataCriacao = c.DataCriacao.ToString("yyyy-MM-dd HH:mm"),
            DataLimite = c.DataLimiteResposta.ToString("yyyy-MM-dd HH:mm"),
            QtdItens = c.Itens.Count,
            FornecedoresConvidados = c.Fornecedores.Count,
            FornecedoresResponderam = c.Fornecedores.Count(f => f.Resposta is { Status: "Enviada" }),
            Visualizacoes = c.Fornecedores.Count(f => f.Acesso is { Visualizacoes: > 0 }),
            c.TotalAprovado,
            c.EconomiaRealizada,
            c.EconomiaPotencial
        }).ToList();

        return Ok(new
        {
            kpis = new
            {
                total,
                abertas,
                emAnalise,
                aprovadas,
                canceladas,
                totalConvites,
                totalRespostas,
                taxaRespostaGlobal
            },
            cotacoes = lista
        });
    }

    /// <summary>
    /// Relatório 6: Indicadores e Métricas de Cotações e Contratações de Serviços.
    /// </summary>
    [HttpGet("servicos")]
    public async Task<IActionResult> RelatorioServicos()
    {
        var servicos = await _db.CotacoesServico
            .Include(c => c.Atividades)
            .Include(c => c.Fornecedores).ThenInclude(f => f.Proposta)
            .Include(c => c.Fornecedores).ThenInclude(f => f.Fornecedor)
            .AsNoTracking()
            .OrderByDescending(c => c.DataCriacao)
            .ToListAsync();

        var ordens = await _db.OrdensServico
            .Include(o => o.Fornecedor)
            .AsNoTracking()
            .ToListAsync();

        var total = servicos.Count;
        var rascunhos = servicos.Count(c => c.Status == "Rascunho");
        var aguardando = servicos.Count(c => c.Status == "AguardandoPropostas");
        var contratadas = servicos.Count(c => c.Status == "Contratada");
        var concluidas = ordens.Count(o => o.Status == "Concluida");

        var totalConvites = servicos.Sum(c => c.Fornecedores.Count);
        var totalPropostas = servicos.Sum(c => c.Fornecedores.Count(f => f.Proposta is { Status: "Enviada" or "Vencedora" }));
        var taxaResposta = totalConvites > 0 ? Math.Round(((decimal)totalPropostas / totalConvites) * 100, 1) : 0m;

        var totalEstimadoGlobal = servicos.Sum(c => c.TotalEstimado > 0 ? c.TotalEstimado : c.Atividades.Sum(a => a.Quantidade * a.ValorEstimadoUnitario));
        var totalContratadoGlobal = servicos.Sum(c => c.TotalContratado);
        var totalEconomiaRealizada = servicos.Sum(c => c.EconomiaRealizada);

        // Agrupamento por Categoria
        var porCategoria = servicos
            .GroupBy(c => string.IsNullOrWhiteSpace(c.Categoria) ? "Geral" : c.Categoria)
            .Select(g => new
            {
                Categoria = g.Key,
                Quantidade = g.Count(),
                TotalEstimado = g.Sum(c => c.TotalEstimado),
                TotalContratado = g.Sum(c => c.TotalContratado),
                Economia = g.Sum(c => c.EconomiaRealizada)
            })
            .OrderByDescending(x => x.TotalContratado)
            .ToList();

        // Top Fornecedores Contratados
        var topFornecedores = ordens
            .GroupBy(o => o.Fornecedor?.RazaoSocial ?? "Desconhecido")
            .Select(g => new
            {
                Fornecedor = g.Key,
                QtdOrdens = g.Count(),
                TotalContratado = g.Sum(o => o.ValorTotal)
            })
            .OrderByDescending(x => x.TotalContratado)
            .Take(10)
            .ToList();

        var lista = servicos.Select(c => new
        {
            c.Id,
            c.Numero,
            c.Titulo,
            c.Categoria,
            c.Status,
            c.Prioridade,
            c.Cidade,
            c.Uf,
            DataCriacao = c.DataCriacao.ToString("yyyy-MM-dd HH:mm"),
            DataLimite = c.DataLimiteResposta.ToString("yyyy-MM-dd HH:mm"),
            QtdAtividades = c.Atividades.Count,
            Convites = c.Fornecedores.Count,
            Propostas = c.Fornecedores.Count(f => f.Proposta is { Status: "Enviada" or "Vencedora" }),
            TotalEstimado = c.TotalEstimado > 0 ? c.TotalEstimado : c.Atividades.Sum(a => a.Quantidade * a.ValorEstimadoUnitario),
            c.TotalContratado,
            c.EconomiaRealizada,
            FornecedorVencedor = c.Fornecedores.FirstOrDefault(f => f.FornecedorId == c.FornecedorVencedorId)?.Fornecedor?.RazaoSocial ?? "-"
        }).ToList();

        return Ok(new
        {
            kpis = new
            {
                total,
                rascunhos,
                aguardando,
                contratadas,
                concluidas,
                totalConvites,
                totalPropostas,
                taxaResposta,
                totalEstimadoGlobal,
                totalContratadoGlobal,
                totalEconomiaRealizada
            },
            porCategoria,
            topFornecedores,
            servicos = lista
        });
    }

    // =========================================================================
    // CATEGORIA 1: ORDENS DE SERVIÇO
    // =========================================================================

    /// <summary>
    /// OS em aberto / pendentes com identificação de status de impedimento e atrasos.
    /// </summary>
    [HttpGet("os-pendentes")]
    public async Task<IActionResult> RelatorioOsPendentes(
        [FromQuery] DateTime? dataInicio,
        [FromQuery] DateTime? dataFim,
        [FromQuery] Guid? fornecedorId,
        [FromQuery] string? statusImpedimento)
    {
        var q = _db.OrdensServico
            .Include(o => o.Fornecedor)
            .Include(o => o.CotacaoServico)
            .Where(o => o.Status != "Concluida" && o.Status != "Cancelada")
            .AsNoTracking()
            .AsQueryable();

        if (dataInicio.HasValue) q = q.Where(o => o.DataCriacao >= dataInicio.Value.Date);
        if (dataFim.HasValue) q = q.Where(o => o.DataCriacao <= dataFim.Value.Date.AddDays(1).AddTicks(-1));
        if (fornecedorId.HasValue) q = q.Where(o => o.FornecedorId == fornecedorId.Value);
        if (!string.IsNullOrWhiteSpace(statusImpedimento) && statusImpedimento != "Todos")
            q = q.Where(o => o.StatusImpedimento == statusImpedimento);

        var lista = await q.OrderByDescending(o => o.DataCriacao).ToListAsync();
        var agora = DateTime.UtcNow;

        var itens = lista.Select(o =>
        {
            var diasAberto = (agora - o.DataCriacao).Days;
            var diasAtraso = o.PrevisaoTermino.HasValue && agora > o.PrevisaoTermino.Value
                ? (agora - o.PrevisaoTermino.Value).Days
                : 0;

            return new
            {
                o.Id,
                o.Numero,
                o.Status,
                StatusImpedimento = string.IsNullOrWhiteSpace(o.StatusImpedimento) ? "Nenhum" : o.StatusImpedimento,
                o.MotivoImpedimento,
                TecnicoResponsavel = string.IsNullOrWhiteSpace(o.TecnicoResponsavelNome) ? "Não atribuído" : o.TecnicoResponsavelNome,
                FornecedorNome = o.Fornecedor != null ? (string.IsNullOrEmpty(o.Fornecedor.NomeFantasia) ? o.Fornecedor.RazaoSocial : o.Fornecedor.NomeFantasia) : "-",
                FornecedorCnpj = o.Fornecedor?.Cnpj ?? "",
                CotacaoNumero = o.CotacaoServico?.Numero ?? "-",
                Categoria = o.CotacaoServico?.Categoria ?? "Geral",
                Prioridade = o.CotacaoServico?.Prioridade ?? "Normal",
                DataAbertura = o.DataCriacao.ToString("yyyy-MM-dd HH:mm"),
                PrevisaoTermino = o.PrevisaoTermino.HasValue ? o.PrevisaoTermino.Value.ToString("yyyy-MM-dd") : "-",
                DiasEmAberto = diasAberto,
                DiasAtraso = diasAtraso,
                EstaAtrasada = diasAtraso > 0,
                o.ValorTotal,
                o.EscopoAprovadoResumo
            };
        }).ToList();

        var totalPendentes = itens.Count;
        var comImpedimento = itens.Count(x => x.StatusImpedimento != "Nenhum");
        var atrasadas = itens.Count(x => x.EstaAtrasada);
        var valorEmExecucao = itens.Sum(x => x.ValorTotal);

        return Ok(new
        {
            kpis = new
            {
                totalPendentes,
                comImpedimento,
                atrasadas,
                valorEmExecucao
            },
            ordens = itens
        });
    }

    /// <summary>
    /// OS distribuídas por técnico ou responsável com métricas de produtividade.
    /// </summary>
    [HttpGet("os-tecnicos")]
    public async Task<IActionResult> RelatorioOsTecnicos(
        [FromQuery] DateTime? dataInicio,
        [FromQuery] DateTime? dataFim)
    {
        var q = _db.OrdensServico
            .Include(o => o.Fornecedor)
            .AsNoTracking()
            .AsQueryable();

        if (dataInicio.HasValue) q = q.Where(o => o.DataCriacao >= dataInicio.Value.Date);
        if (dataFim.HasValue) q = q.Where(o => o.DataCriacao <= dataFim.Value.Date.AddDays(1).AddTicks(-1));

        var ordens = await q.ToListAsync();

        var tecnicos = ordens
            .GroupBy(o => string.IsNullOrWhiteSpace(o.TecnicoResponsavelNome) ? "Não Atribuído" : o.TecnicoResponsavelNome.Trim())
            .Select(g =>
            {
                var total = g.Count();
                var concluidas = g.Count(o => o.Status == "Concluida");
                var emAndamento = g.Count(o => o.Status != "Concluida" && o.Status != "Cancelada");
                var comImpedimento = g.Count(o => o.StatusImpedimento != "Nenhum" && o.Status != "Concluida" && o.Status != "Cancelada");
                var atrasadas = g.Count(o => o.PrevisaoTermino.HasValue && DateTime.UtcNow > o.PrevisaoTermino.Value && o.Status != "Concluida" && o.Status != "Cancelada");

                // Prazo médio em dias
                var concluidasComData = g.Where(o => o.Status == "Concluida" && o.DataConclusaoEfetiva.HasValue).ToList();
                var prazoMedioDias = concluidasComData.Count > 0
                    ? Math.Round(concluidasComData.Average(o => (o.DataConclusaoEfetiva!.Value - o.DataCriacao).TotalDays), 1)
                    : (g.Any(o => o.PrazoExecucaoDias > 0) ? Math.Round(g.Where(o => o.PrazoExecucaoDias > 0).Average(o => (double)o.PrazoExecucaoDias), 1) : 0);

                var taxaConclusao = total > 0 ? Math.Round(((decimal)concluidas / total) * 100, 1) : 0m;
                var valorTotalGerenciado = g.Sum(o => o.ValorTotal);

                return new
                {
                    Tecnico = g.Key,
                    TotalOrdens = total,
                    Concluidas = concluidas,
                    EmAndamento = emAndamento,
                    ComImpedimento = comImpedimento,
                    Atrasadas = atrasadas,
                    PrazoMedioDias = prazoMedioDias,
                    TaxaConclusao = taxaConclusao,
                    ValorTotalGerenciado = valorTotalGerenciado
                };
            })
            .OrderByDescending(t => t.TotalOrdens)
            .ToList();

        return Ok(new
        {
            totalTecnicos = tecnicos.Count,
            totalOrdensGlobal = ordens.Count,
            ordensConcluidasGlobal = ordens.Count(o => o.Status == "Concluida"),
            tecnicos
        });
    }

    /// <summary>
    /// Apuração de Custo x Receita e Rentabilidade por Ordem de Serviço.
    /// </summary>
    [HttpGet("os-custo-receita")]
    public async Task<IActionResult> RelatorioOsCustoReceita(
        [FromQuery] DateTime? dataInicio,
        [FromQuery] DateTime? dataFim)
    {
        var q = _db.OrdensServico
            .Include(o => o.Fornecedor)
            .Include(o => o.CotacaoServico)
            .AsNoTracking()
            .AsQueryable();

        if (dataInicio.HasValue) q = q.Where(o => o.DataCriacao >= dataInicio.Value.Date);
        if (dataFim.HasValue) q = q.Where(o => o.DataCriacao <= dataFim.Value.Date.AddDays(1).AddTicks(-1));

        var ordens = await q.OrderByDescending(o => o.DataCriacao).ToListAsync();

        var itens = ordens.Select(o =>
        {
            var receita = o.ValorTotal;
            var custoMo = o.CustoRealMaoDeObra > 0 ? o.CustoRealMaoDeObra : o.ValorMaoDeObra;
            var custoPecas = o.CustoRealPecas > 0 ? o.CustoRealPecas : o.ValorMateriais;
            var custoOutros = o.CustoRealOutros > 0 ? o.CustoRealOutros : o.Deslocamento;
            var custoTotal = custoMo + custoPecas + custoOutros;
            var lucroBruto = receita - custoTotal;
            var margemPerc = receita > 0 ? Math.Round((lucroBruto / receita) * 100, 2) : 0m;

            return new
            {
                o.Id,
                o.Numero,
                o.Status,
                Data = o.DataCriacao.ToString("yyyy-MM-dd"),
                FornecedorNome = o.Fornecedor != null ? (string.IsNullOrEmpty(o.Fornecedor.NomeFantasia) ? o.Fornecedor.RazaoSocial : o.Fornecedor.NomeFantasia) : "-",
                Tecnico = string.IsNullOrWhiteSpace(o.TecnicoResponsavelNome) ? "-" : o.TecnicoResponsavelNome,
                Receita = receita,
                CustoMaoDeObra = custoMo,
                CustoPecas = custoPecas,
                CustoOutros = custoOutros,
                CustoTotal = custoTotal,
                LucroBruto = lucroBruto,
                MargemPercentual = margemPerc,
                PossuiApontamentoReal = o.CustoRealMaoDeObra > 0 || o.CustoRealPecas > 0 || o.CustoRealOutros > 0
            };
        }).ToList();

        var receitaTotal = itens.Sum(x => x.Receita);
        var custoTotalGeral = itens.Sum(x => x.CustoTotal);
        var lucroBrutoGeral = receitaTotal - custoTotalGeral;
        var margemMediaGeral = receitaTotal > 0 ? Math.Round((lucroBrutoGeral / receitaTotal) * 100, 2) : 0m;

        return Ok(new
        {
            kpis = new
            {
                receitaTotal,
                custoTotal = custoTotalGeral,
                lucroBruto = lucroBrutoGeral,
                margemMediaPercentual = margemMediaGeral,
                totalOrdens = itens.Count
            },
            ordens = itens
        });
    }

    public record ApontarCustosOsRequest(
        decimal CustoRealMaoDeObra,
        decimal CustoRealPecas,
        decimal CustoRealOutros,
        string? Observacoes,
        string? StatusImpedimento,
        string? MotivoImpedimento,
        string? TecnicoResponsavelNome,
        DateTime? DataConclusaoEfetiva
    );

    /// <summary>
    /// Lança e atualiza apontamentos de custos reais e impedimentos da Ordem de Serviço.
    /// </summary>
    [HttpPost("os/{id:guid}/apontar-custos")]
    public async Task<IActionResult> ApontarCustosOs(Guid id, [FromBody] ApontarCustosOsRequest r)
    {
        var os = await _db.OrdensServico.FirstOrDefaultAsync(x => x.Id == id);
        if (os == null) return NotFound(new { erro = "Ordem de Serviço não encontrada." });

        if (r.CustoRealMaoDeObra >= 0) os.CustoRealMaoDeObra = r.CustoRealMaoDeObra;
        if (r.CustoRealPecas >= 0) os.CustoRealPecas = r.CustoRealPecas;
        if (r.CustoRealOutros >= 0) os.CustoRealOutros = r.CustoRealOutros;

        if (!string.IsNullOrWhiteSpace(r.StatusImpedimento)) os.StatusImpedimento = r.StatusImpedimento.Trim();
        if (r.MotivoImpedimento != null) os.MotivoImpedimento = r.MotivoImpedimento.Trim();
        if (!string.IsNullOrWhiteSpace(r.TecnicoResponsavelNome)) os.TecnicoResponsavelNome = r.TecnicoResponsavelNome.Trim();
        if (r.DataConclusaoEfetiva.HasValue) os.DataConclusaoEfetiva = r.DataConclusaoEfetiva.Value;
        if (!string.IsNullOrWhiteSpace(r.Observacoes))
        {
            os.Observacoes = string.IsNullOrWhiteSpace(os.Observacoes)
                ? r.Observacoes.Trim()
                : $"{os.Observacoes}\n[{DateTime.UtcNow:dd/MM/yyyy HH:mm}] {r.Observacoes.Trim()}";
        }

        os.DataAtualizacao = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(new { sucesso = true, mensagem = "Custos e dados da Ordem de Serviço atualizados com sucesso." });
    }

    // =========================================================================
    // CATEGORIA 2: COTAÇÕES
    // =========================================================================

    /// <summary>
    /// Lista cotações disponíveis para seleção no Mapa Comparativo.
    /// </summary>
    [HttpGet("cotacoes-lista-mapa")]
    public async Task<IActionResult> RelatorioCotacoesListaMapa()
    {
        var cotacoes = await _db.Cotacoes
            .Include(c => c.Fornecedores)
            .AsNoTracking()
            .OrderByDescending(c => c.DataCriacao)
            .Take(50)
            .Select(c => new
            {
                c.Id,
                c.Numero,
                c.Titulo,
                c.Status,
                Data = c.DataCriacao.ToString("yyyy-MM-dd"),
                FornecedoresQtd = c.Fornecedores.Count
            })
            .ToListAsync();

        return Ok(cotacoes);
    }

    /// <summary>
    /// Mapa Comparativo Estruturado de Cotações: compara lado a lado todas as propostas recebidas.
    /// </summary>
    [HttpGet("cotacoes-mapa")]
    public async Task<IActionResult> RelatorioCotacoesMapa([FromQuery] Guid? cotacaoId)
    {
        Cotacao? c = null;
        if (cotacaoId.HasValue)
        {
            c = await _db.Cotacoes
                .Include(x => x.Itens)
                .Include(x => x.Fornecedores).ThenInclude(f => f.Fornecedor)
                .Include(x => x.Fornecedores).ThenInclude(f => f.Resposta).ThenInclude(r => r!.Itens)
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == cotacaoId.Value);
        }
        else
        {
            c = await _db.Cotacoes
                .Include(x => x.Itens)
                .Include(x => x.Fornecedores).ThenInclude(f => f.Fornecedor)
                .Include(x => x.Fornecedores).ThenInclude(f => f.Resposta).ThenInclude(r => r!.Itens)
                .AsNoTracking()
                .OrderByDescending(x => x.DataCriacao)
                .FirstOrDefaultAsync();
        }

        if (c == null)
            return Ok(new { vazio = true, mensagem = "Nenhuma cotação encontrada para mapa comparativo." });

        var fornecedoresValidos = c.Fornecedores
            .Where(f => f.Fornecedor != null)
            .OrderBy(f => f.Fornecedor!.NomeFantasia)
            .ToList();

        var fornecedoresHeader = fornecedoresValidos.Select(f =>
        {
            var totalProp = f.Resposta?.Itens.Sum(i => i.QuantidadeOfertada * i.PrecoUnitario) ?? 0m;
            if (f.Resposta != null) totalProp += f.Resposta.Frete + f.Resposta.OutrasDespesas;

            return new
            {
                FornecedorId = f.FornecedorId,
                Nome = string.IsNullOrEmpty(f.Fornecedor!.NomeFantasia) ? f.Fornecedor.RazaoSocial : f.Fornecedor.NomeFantasia,
                Cnpj = f.Fornecedor.Cnpj,
                StatusConvite = f.Status,
                Respondeu = f.Resposta != null,
                ValorTotalProposta = totalProp,
                PrazoEntregaDias = f.Resposta?.PrazoEntregaDias ?? 0,
                CondicaoPagamento = f.Resposta?.CondicaoPagamento ?? "-",
                Frete = f.Resposta?.Frete ?? 0m,
                Aprovada = f.Status == "Aprovada"
            };
        }).ToList();

        var gradeItens = c.Itens.Select(it =>
        {
            var precosPorForn = new Dictionary<string, object>();
            decimal menorPrecoItem = decimal.MaxValue;

            foreach (var f in fornecedoresValidos)
            {
                var respItem = f.Resposta?.Itens.FirstOrDefault(ri => ri.CotacaoItemId == it.Id);
                var preco = respItem?.PrecoUnitario ?? 0m;
                var subtotal = preco * it.Quantidade;
                var disponivel = respItem?.Disponivel ?? false;

                if (preco > 0 && preco < menorPrecoItem)
                    menorPrecoItem = preco;

                precosPorForn[f.FornecedorId.ToString()] = new
                {
                    PrecoUnitario = preco,
                    Subtotal = subtotal,
                    Disponivel = disponivel,
                    PrazoDias = f.Resposta?.PrazoEntregaDias ?? 0
                };
            }

            if (menorPrecoItem == decimal.MaxValue) menorPrecoItem = 0m;

            return new
            {
                it.Id,
                Codigo = it.Codigo,
                Descricao = it.Descricao,
                Unidade = it.Unidade,
                Quantidade = it.Quantidade,
                UltimoPrecoRef = it.UltimoCustoRef,
                MenorPreco = menorPrecoItem,
                Propostas = precosPorForn
            };
        }).ToList();

        return Ok(new
        {
            cotacao = new
            {
                c.Id,
                c.Numero,
                c.Titulo,
                c.Status,
                DataAbertura = c.DataCriacao.ToString("yyyy-MM-dd"),
                DataLimite = c.DataLimiteResposta.ToString("yyyy-MM-dd"),
                TotalAprovado = c.TotalAprovado,
                EconomiaRealizada = c.EconomiaRealizada
            },
            fornecedores = fornecedoresHeader,
            itens = gradeItens
        });
    }

    /// <summary>
    /// Economia obtida (Saving) realizado e potencial consolidado por cotação.
    /// </summary>
    [HttpGet("cotacoes-saving")]
    public async Task<IActionResult> RelatorioCotacoesSaving(
        [FromQuery] DateTime? dataInicio,
        [FromQuery] DateTime? dataFim)
    {
        var q = _db.Cotacoes
            .Include(c => c.Itens)
            .Include(c => c.Fornecedores).ThenInclude(f => f.Fornecedor)
            .Where(c => c.Status == "Aprovada" || c.TotalAprovado > 0)
            .AsNoTracking()
            .AsQueryable();

        if (dataInicio.HasValue) q = q.Where(c => c.DataCriacao >= dataInicio.Value.Date);
        if (dataFim.HasValue) q = q.Where(c => c.DataCriacao <= dataFim.Value.Date.AddDays(1).AddTicks(-1));

        var cotacoes = await q.OrderByDescending(c => c.DataCriacao).ToListAsync();

        var savingTotalRealizado = cotacoes.Sum(c => c.EconomiaRealizada);
        var savingTotalPotencial = cotacoes.Sum(c => c.EconomiaPotencial);
        var totalNegociado = cotacoes.Sum(c => c.TotalAprovado);
        var baseTotal = totalNegociado + savingTotalRealizado;
        var percentualMedio = baseTotal > 0 ? Math.Round((savingTotalRealizado / baseTotal) * 100, 2) : 0m;

        var detalhes = cotacoes.Select(c =>
        {
            var baseCot = c.TotalAprovado + c.EconomiaRealizada;
            var perc = baseCot > 0 ? Math.Round((c.EconomiaRealizada / baseCot) * 100, 2) : 0m;
            var fornecedoresVencedores = c.Fornecedores
                .Where(f => f.Status == "Aprovada" && f.Fornecedor != null)
                .Select(f => string.IsNullOrEmpty(f.Fornecedor!.NomeFantasia) ? f.Fornecedor.RazaoSocial : f.Fornecedor.NomeFantasia)
                .Distinct()
                .ToList();

            return new
            {
                c.Id,
                c.Numero,
                c.Titulo,
                Data = c.DataCriacao.ToString("yyyy-MM-dd"),
                TotalNegociado = c.TotalAprovado,
                ValorReferencia = baseCot,
                SavingRealizado = c.EconomiaRealizada,
                SavingPotencial = c.EconomiaPotencial,
                PercentualSaving = perc,
                ItensQtd = c.Itens.Count,
                Fornecedores = string.Join(", ", fornecedoresVencedores)
            };
        }).ToList();

        return Ok(new
        {
            kpis = new
            {
                totalNegociado,
                savingTotalRealizado,
                savingTotalPotencial,
                percentualMedio,
                totalCotacoesAprovadas = cotacoes.Count
            },
            detalhes
        });
    }

    /// <summary>
    /// Análise do funil operacional de cotações (Abertas, Respondidas, Vencidas, Concluídas).
    /// </summary>
    [HttpGet("cotacoes-funil")]
    public async Task<IActionResult> RelatorioCotacoesFunil(
        [FromQuery] DateTime? dataInicio,
        [FromQuery] DateTime? dataFim)
    {
        var q = _db.Cotacoes
            .Include(c => c.Fornecedores).ThenInclude(f => f.Resposta)
            .AsNoTracking()
            .AsQueryable();

        if (dataInicio.HasValue) q = q.Where(c => c.DataCriacao >= dataInicio.Value.Date);
        if (dataFim.HasValue) q = q.Where(c => c.DataCriacao <= dataFim.Value.Date.AddDays(1).AddTicks(-1));

        var lista = await q.OrderByDescending(c => c.DataCriacao).ToListAsync();
        var agora = DateTime.UtcNow;

        var total = lista.Count;
        var abertas = lista.Count(c => c.Status == "Aberta" && c.DataLimiteResposta >= agora);
        var vencidas = lista.Count(c => c.Status == "Aberta" && c.DataLimiteResposta < agora);
        var respondidas = lista.Count(c => c.Fornecedores.Any(f => f.Resposta is { Status: "Enviada" }));
        var aprovadas = lista.Count(c => c.Status == "Aprovada");
        var canceladas = lista.Count(c => c.Status == "Cancelada");

        var totalConvites = lista.Sum(c => c.Fornecedores.Count);
        var totalRespostas = lista.Sum(c => c.Fornecedores.Count(f => f.Resposta is { Status: "Enviada" }));
        var taxaResposta = totalConvites > 0 ? Math.Round(((decimal)totalRespostas / totalConvites) * 100, 1) : 0m;

        // Tempo médio de fechamento (dias)
        var cotacoesFechadas = lista.Where(c => c.Status == "Aprovada" && c.DataAtualizacao.HasValue).ToList();
        var tempoMedioFechamentoDias = cotacoesFechadas.Count > 0
            ? Math.Round(cotacoesFechadas.Average(c => (c.DataAtualizacao!.Value - c.DataCriacao).TotalDays), 1)
            : 0;

        var itens = lista.Select(c =>
        {
            var isVencida = c.Status == "Aberta" && c.DataLimiteResposta < agora;
            var statusFormatado = isVencida ? "Vencida" : c.Status;

            return new
            {
                c.Id,
                c.Numero,
                c.Titulo,
                Status = statusFormatado,
                DataCriacao = c.DataCriacao.ToString("yyyy-MM-dd HH:mm"),
                DataLimite = c.DataLimiteResposta.ToString("yyyy-MM-dd HH:mm"),
                Convites = c.Fornecedores.Count,
                Respostas = c.Fornecedores.Count(f => f.Resposta is { Status: "Enviada" }),
                TotalAprovado = c.TotalAprovado,
                EconomiaRealizada = c.EconomiaRealizada,
                isVencida
            };
        }).ToList();

        return Ok(new
        {
            kpis = new
            {
                total,
                abertas,
                respondidas,
                vencidas,
                aprovadas,
                canceladas,
                taxaResposta,
                tempoMedioFechamentoDias
            },
            cotacoes = itens
        });
    }

    // =========================================================================
    // CATEGORIA 3: PEDIDOS DE COMPRA
    // =========================================================================

    /// <summary>
    /// Monitoramento de pedidos pendentes de entrega com suporte a recebimentos parciais.
    /// </summary>
    [HttpGet("pedidos-pendentes")]
    public async Task<IActionResult> RelatorioPedidosPendentes(
        [FromQuery] DateTime? dataInicio,
        [FromQuery] DateTime? dataFim,
        [FromQuery] Guid? fornecedorId)
    {
        var q = _db.PedidosCompra
            .Include(p => p.Fornecedor)
            .Include(p => p.Itens).ThenInclude(i => i.Produto)
            .Where(p => p.Status != "Recebido" && p.Status != "Cancelado")
            .AsNoTracking()
            .AsQueryable();

        if (dataInicio.HasValue) q = q.Where(p => p.DataCriacao >= dataInicio.Value.Date);
        if (dataFim.HasValue) q = q.Where(p => p.DataCriacao <= dataFim.Value.Date.AddDays(1).AddTicks(-1));
        if (fornecedorId.HasValue) q = q.Where(p => p.FornecedorId == fornecedorId.Value);

        var pedidos = await q.OrderByDescending(p => p.DataCriacao).ToListAsync();
        var agora = DateTime.UtcNow;

        var itensFormatados = pedidos.Select(p =>
        {
            var diasAtraso = p.PrevisaoEntrega.HasValue && agora > p.PrevisaoEntrega.Value
                ? (agora - p.PrevisaoEntrega.Value).Days
                : 0;

            var totalQtdComprada = p.Itens.Sum(i => i.Quantidade);
            var totalQtdRecebida = p.Itens.Sum(i => i.QuantidadeRecebida);
            var percRecebido = totalQtdComprada > 0 ? Math.Round((totalQtdRecebida / totalQtdComprada) * 100, 1) : 0m;
            var saldoValorPendente = p.Itens.Sum(i => Math.Max(0, i.Quantidade - i.QuantidadeRecebida) * i.PrecoUnitario);

            var itensDetalhe = p.Itens.Select(it => new
            {
                it.Id,
                Codigo = it.CodigoProduto,
                Descricao = it.DescricaoProduto,
                it.Unidade,
                it.Quantidade,
                it.QuantidadeRecebida,
                SaldoPendente = Math.Max(0, it.Quantidade - it.QuantidadeRecebida),
                PercentualAtendido = it.Quantidade > 0 ? Math.Round((it.QuantidadeRecebida / it.Quantidade) * 100, 1) : 0m,
                it.PrecoUnitario,
                it.Subtotal,
                DataUltimoRecebimento = it.DataUltimoRecebimento.HasValue ? it.DataUltimoRecebimento.Value.ToString("yyyy-MM-dd HH:mm") : "-"
            }).ToList();

            return new
            {
                p.Id,
                p.Numero,
                p.Status,
                DataEmissao = p.DataCriacao.ToString("yyyy-MM-dd"),
                PrevisaoEntrega = p.PrevisaoEntrega.HasValue ? p.PrevisaoEntrega.Value.ToString("yyyy-MM-dd") : "-",
                FornecedorNome = p.Fornecedor != null ? (string.IsNullOrEmpty(p.Fornecedor.NomeFantasia) ? p.Fornecedor.RazaoSocial : p.Fornecedor.NomeFantasia) : "-",
                FornecedorCnpj = p.Fornecedor?.Cnpj ?? "",
                p.ValorTotal,
                SaldoValorPendente = saldoValorPendente,
                TotalQtdComprada = totalQtdComprada,
                TotalQtdRecebida = totalQtdRecebida,
                PercentualRecebido = percRecebido,
                DiasAtraso = diasAtraso,
                EstaAtrasado = diasAtraso > 0,
                Itens = itensDetalhe
            };
        }).ToList();

        var totalPendentes = itensFormatados.Count;
        var totalAtrasados = itensFormatados.Count(x => x.EstaAtrasado);
        var valorPendenteTotal = itensFormatados.Sum(x => x.SaldoValorPendente);

        return Ok(new
        {
            kpis = new
            {
                totalPendentes,
                totalAtrasados,
                valorPendenteTotal
            },
            pedidos = itensFormatados
        });
    }

    public record ReceberItemPedidoRequest(decimal QuantidadeRecebida, DateTime? DataRecebimento);

    /// <summary>
    /// Registra recebimento parcial ou total de item do Pedido de Compra.
    /// </summary>
    [HttpPost("pedidos/{pedidoId:guid}/itens/{itemId:guid}/receber")]
    public async Task<IActionResult> ReceberItemPedido(Guid pedidoId, Guid itemId, [FromBody] ReceberItemPedidoRequest r)
    {
        if (r.QuantidadeRecebida <= 0)
            return BadRequest(new { erro = "Quantidade a receber deve ser maior que zero." });

        var pedido = await _db.PedidosCompra
            .Include(p => p.Itens)
            .FirstOrDefaultAsync(p => p.Id == pedidoId);

        if (pedido == null) return NotFound(new { erro = "Pedido não encontrado." });

        var item = pedido.Itens.FirstOrDefault(i => i.Id == itemId);
        if (item == null) return NotFound(new { erro = "Item do pedido não encontrado." });

        item.QuantidadeRecebida += r.QuantidadeRecebida;
        item.DataUltimoRecebimento = r.DataRecebimento ?? DateTime.UtcNow;

        // Se todos os itens foram totalmente atendidos, marca o pedido como Recebido
        var todosAtendidos = pedido.Itens.All(i => i.QuantidadeRecebida >= i.Quantidade);
        if (todosAtendidos)
        {
            pedido.Status = "Recebido";
            pedido.DataEntregaEfetiva = DateTime.UtcNow;
        }
        else
        {
            pedido.Status = "ParcialmenteRecebido";
        }

        pedido.DataAtualizacao = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(new
        {
            sucesso = true,
            statusPedido = pedido.Status,
            itemAtualizado = new
            {
                item.Id,
                item.Quantidade,
                item.QuantidadeRecebida,
                SaldoPendente = Math.Max(0, item.Quantidade - item.QuantidadeRecebida)
            }
        });
    }

    /// <summary>
    /// Compras por fornecedor com análise da Curva ABC (Pareto: 80% A, 15% B, 5% C).
    /// </summary>
    [HttpGet("pedidos-curva-abc")]
    public async Task<IActionResult> RelatorioPedidosCurvaAbc(
        [FromQuery] DateTime? dataInicio,
        [FromQuery] DateTime? dataFim)
    {
        var q = _db.PedidosCompra
            .Include(p => p.Fornecedor)
            .Where(p => p.Status != "Cancelado")
            .AsNoTracking()
            .AsQueryable();

        if (dataInicio.HasValue) q = q.Where(p => p.DataCriacao >= dataInicio.Value.Date);
        if (dataFim.HasValue) q = q.Where(p => p.DataCriacao <= dataFim.Value.Date.AddDays(1).AddTicks(-1));

        var pedidos = await q.ToListAsync();
        var totalGeralComprado = pedidos.Sum(p => p.ValorTotal);

        var fornecedoresAgrupados = pedidos
            .GroupBy(p => p.FornecedorId)
            .Select(g =>
            {
                var forn = g.First().Fornecedor;
                var total = g.Sum(p => p.ValorTotal);
                var qtd = g.Count();

                return new
                {
                    FornecedorId = g.Key,
                    Nome = forn != null ? (string.IsNullOrEmpty(forn.NomeFantasia) ? forn.RazaoSocial : forn.NomeFantasia) : "Não especificado",
                    Cnpj = forn?.Cnpj ?? "",
                    Cidade = forn?.Cidade ?? "",
                    Uf = forn?.Uf ?? "",
                    TotalComprado = total,
                    QtdPedidos = qtd,
                    TicketMedio = qtd > 0 ? Math.Round(total / qtd, 2) : 0m
                };
            })
            .OrderByDescending(x => x.TotalComprado)
            .ToList();

        decimal acumuladoValor = 0m;
        var curvaAbc = fornecedoresAgrupados.Select((f, index) =>
        {
            acumuladoValor += f.TotalComprado;
            var percIndividual = totalGeralComprado > 0 ? Math.Round((f.TotalComprado / totalGeralComprado) * 100, 2) : 0m;
            var percAcumulado = totalGeralComprado > 0 ? Math.Round((acumuladoValor / totalGeralComprado) * 100, 2) : 0m;

            // Classificação Pareto
            string classe = "C";
            if (percAcumulado <= 80m || (index == 0 && percIndividual >= 80m))
                classe = "A";
            else if (percAcumulado <= 95m)
                classe = "B";

            return new
            {
                Posicao = index + 1,
                f.FornecedorId,
                f.Nome,
                f.Cnpj,
                f.Cidade,
                f.Uf,
                f.TotalComprado,
                f.QtdPedidos,
                f.TicketMedio,
                PercentualIndividual = percIndividual,
                PercentualAcumulado = percAcumulado,
                Classe = classe
            };
        }).ToList();

        var qtdA = curvaAbc.Count(x => x.Classe == "A");
        var qtdB = curvaAbc.Count(x => x.Classe == "B");
        var qtdC = curvaAbc.Count(x => x.Classe == "C");

        return Ok(new
        {
            kpis = new
            {
                totalGeralComprado,
                totalFornecedores = curvaAbc.Count,
                qtdClasseA = qtdA,
                qtdClasseB = qtdB,
                qtdClasseC = qtdC
            },
            ranking = curvaAbc
        });
    }

    /// <summary>
    /// Prazo de entrega previsto x realizado e índice de pontualidade dos fornecedores.
    /// </summary>
    [HttpGet("pedidos-pontualidade")]
    public async Task<IActionResult> RelatorioPedidosPontualidade(
        [FromQuery] DateTime? dataInicio,
        [FromQuery] DateTime? dataFim)
    {
        var q = _db.PedidosCompra
            .Include(p => p.Fornecedor)
            .Where(p => p.Status != "Cancelado")
            .AsNoTracking()
            .AsQueryable();

        if (dataInicio.HasValue) q = q.Where(p => p.DataCriacao >= dataInicio.Value.Date);
        if (dataFim.HasValue) q = q.Where(p => p.DataCriacao <= dataFim.Value.Date.AddDays(1).AddTicks(-1));

        var pedidos = await q.ToListAsync();

        var fornecedores = pedidos
            .GroupBy(p => p.FornecedorId)
            .Select(g =>
            {
                var forn = g.First().Fornecedor;
                var pedidosForn = g.ToList();

                var concluidosOuAtrasados = pedidosForn
                    .Where(p => p.Status == "Recebido" || (p.PrevisaoEntrega.HasValue && DateTime.UtcNow > p.PrevisaoEntrega.Value))
                    .ToList();

                int noPrazo = 0;
                int comAtraso = 0;
                double somaDiasAtraso = 0;

                foreach (var p in concluidosOuAtrasados)
                {
                    var dataEfetiva = p.DataEntregaEfetiva ?? (p.Status == "Recebido" ? p.DataAtualizacao ?? DateTime.UtcNow : DateTime.UtcNow);
                    var dataPrevista = p.PrevisaoEntrega ?? p.DataCriacao.AddDays(p.PrazoEntregaDias > 0 ? p.PrazoEntregaDias : 15);

                    if (dataEfetiva <= dataPrevista)
                    {
                        noPrazo++;
                    }
                    else
                    {
                        comAtraso++;
                        somaDiasAtraso += (dataEfetiva - dataPrevista).TotalDays;
                    }
                }

                var totalAvaliados = noPrazo + comAtraso;
                var indicePontualidade = totalAvaliados > 0 ? Math.Round(((decimal)noPrazo / totalAvaliados) * 100, 1) : 100m;
                var atrasoMedioDias = comAtraso > 0 ? Math.Round(somaDiasAtraso / comAtraso, 1) : 0;

                string classificacao = "Excelente";
                if (indicePontualidade < 70m) classificacao = "Crítico";
                else if (indicePontualidade < 85m) classificacao = "Regular";
                else if (indicePontualidade < 95m) classificacao = "Bom";

                return new
                {
                    FornecedorId = g.Key,
                    Nome = forn != null ? (string.IsNullOrEmpty(forn.NomeFantasia) ? forn.RazaoSocial : forn.NomeFantasia) : "-",
                    Cnpj = forn?.Cnpj ?? "",
                    TotalPedidos = pedidosForn.Count,
                    EntregasAvaliadas = totalAvaliados,
                    EntregasNoPrazo = noPrazo,
                    EntregasComAtraso = comAtraso,
                    IndicePontualidade = indicePontualidade,
                    AtrasoMedioDias = atrasoMedioDias,
                    Classificacao = classificacao
                };
            })
            .OrderByDescending(f => f.TotalPedidos)
            .ToList();

        var totalAvaliadosGlobal = fornecedores.Sum(f => f.EntregasAvaliadas);
        var totalNoPrazoGlobal = fornecedores.Sum(f => f.EntregasNoPrazo);
        var taxaPontualidadeGlobal = totalAvaliadosGlobal > 0 ? Math.Round(((decimal)totalNoPrazoGlobal / totalAvaliadosGlobal) * 100, 1) : 100m;

        return Ok(new
        {
            kpis = new
            {
                taxaPontualidadeGlobal,
                totalEntregasAvaliadas = totalAvaliadosGlobal,
                totalEntregasNoPrazo = totalNoPrazoGlobal,
                totalEntregasAtrasadas = fornecedores.Sum(f => f.EntregasComAtraso)
            },
            fornecedores
        });
    }

    // =========================================================================
    // EXPORTAÇÃO CENTRALIZADA (PDF / HTML E CSV)
    // =========================================================================

    [HttpGet("exportar-pdf")]
    public async Task<IActionResult> ExportarRelatorioPdf([FromQuery] string tipo)
    {
        var tenant = await _db.Tenants.FirstOrDefaultAsync();
        var titulo = tipo switch
        {
            "os-pendentes" => "Relatório de Ordens de Serviço em Aberto / Pendentes",
            "os-tecnicos" => "Relatório de Ordens de Serviço por Técnico / Responsável",
            "os-custo-receita" => "Relatório de Rentabilidade e Custos por Ordem de Serviço",
            "cotacoes-mapa" => "Mapa Comparativo de Cotações",
            "cotacoes-saving" => "Relatório de Economia Obtida (Saving)",
            "cotacoes-funil" => "Funil Analítico de Cotações",
            "pedidos-pendentes" => "Relatório de Pedidos Pendentes de Entrega",
            "pedidos-curva-abc" => "Curva ABC / Pareto de Compras por Fornecedor",
            "pedidos-pontualidade" => "Relatório de Pontualidade e Cumprimento de Prazos",
            _ => "Relatório Gerencial Nexo Compras"
        };

        var html = PdfDocumentService.GerarHtmlRelatorio(
            tituloRelatorio: titulo,
            subtitulo: $"Gerado em {DateTime.UtcNow:dd/MM/yyyy HH:mm} • Nexo Compras Multi-tenant",
            conteudoHtml: "<p style='padding: 20px; text-align: center; color: #64748b;'>Documento gerado a partir dos dados analíticos consolidados no sistema.</p>",
            kpisHtml: null,
            tenant: tenant,
            usuario: null
        );

        return Content(html, "text/html", System.Text.Encoding.UTF8);
    }
}
