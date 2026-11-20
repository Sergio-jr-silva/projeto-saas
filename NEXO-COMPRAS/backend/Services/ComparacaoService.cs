using System.Text.RegularExpressions;
using Nexo.Api.Models;
using Nexo.Api.Services.Fiscal;

namespace Nexo.Api.Services;

public record OfertaDto(Guid CotacaoFornecedorId, bool Disponivel, decimal QuantidadeOfertada, decimal PrecoUnitario, decimal PrecoLiquidoUnit,
    decimal CustoEfetivoUnit, decimal CustoSemFreteUnit, decimal TotalNota, string Cst, string Cfop, decimal Icms, decimal Ipi, decimal St,
    decimal CreditoTotal, decimal? MargemPerc, bool QuantidadeParcial, bool MenorPreco, bool MelhorCusto);

public record ComparacaoItemDto(Guid CotacaoItemId, string Codigo, string Descricao, string Unidade, decimal Quantidade, decimal UltimoCustoRef,
    decimal PrecoVendaRef, decimal ReferenciaCustoUnit, string ReferenciaOrigem, List<OfertaDto> Ofertas);

public record ComparacaoFornecedorDto(Guid CotacaoFornecedorId, Guid FornecedorId, string Nome, string Status, decimal Frete, decimal OutrasDespesas,
    int PrazoEntregaDias, string CondicaoPagamento, decimal? DiasPagamento, DateOnly? ValidadeProposta, decimal TotalNota, decimal CustoEfetivoTotal,
    bool CobreTudo, int ItensOfertados, bool MelhorPrazo, bool MelhorPagamento, bool MelhorCustoTotal);

public record RecomendacaoDto(string Tipo, Dictionary<Guid, Guid> Escolhas, decimal CustoEfetivoTotal, string Justificativa);

public record ComparacaoDto(Guid CotacaoId, string Status, bool TemPropostas, string Regime, bool CreditaIcms, bool CreditaIpi, decimal PercCreditoPisCofins,
    List<ComparacaoFornecedorDto> Fornecedores, List<ComparacaoItemDto> Itens, RecomendacaoDto? Recomendacao, decimal EconomiaPotencial,
    Dictionary<Guid, Guid> Escolhas);

public record ResultadoEconomia(decimal Potencial, decimal Realizada, decimal TotalAprovado, decimal? MargemPerc);

public static class ComparacaoService
{
    public static decimal? DiasPagamento(string? condicao)
    {
        if (string.IsNullOrWhiteSpace(condicao)) return null;
        var c = condicao.ToLowerInvariant();
        var nums = Regex.Matches(c, @"\d+").Select(m => decimal.Parse(m.Value)).Where(n => n <= 365).ToList();
        if (nums.Count > 0) return Math.Round(nums.Average(), 1);
        if (c.Contains("vista") || c.Contains("antecip")) return 0;
        return null;
    }

    private record Linha(CotacaoItem Item, CotacaoFornecedor Cf, RespostaCotacaoItem R, ItemCusto C, decimal QtdPedido);

    public static ComparacaoDto Comparar(Cotacao cot, TributacaoConfig cfg)
    {
        var propostas = cot.Fornecedores.Where(f => f.Resposta is { Status: "Enviada" }).ToList();
        var linhas = new List<Linha>();
        var fornDtos = new List<ComparacaoFornecedorDto>();

        foreach (var cf in propostas)
        {
            var resp = cf.Resposta!;
            var ofertados = resp.Itens.Where(r => r.Disponivel && r.QuantidadeOfertada > 0 && r.PrecoUnitario > 0).ToList();
            var itens = ofertados.Select(r => new ItemProposta(r.QuantidadeOfertada, r.PrecoUnitario, r.DescontoPerc, r.Cst, r.IcmsAliq, r.IpiAliq, r.Mva, r.IcmsStAliq, r.IcmsStValor)).ToList();
            var calc = CustoEfetivoCalculator.Calcular(cfg, itens, resp.Frete, resp.OutrasDespesas);
            for (var k = 0; k < ofertados.Count; k++)
            {
                var item = cot.Itens.FirstOrDefault(i => i.Id == ofertados[k].CotacaoItemId);
                if (item != null)
                {
                    linhas.Add(new Linha(item, cf, ofertados[k], calc.Itens[k], Math.Min(item.Quantidade, ofertados[k].QuantidadeOfertada)));
                }
            }
            var cobre = cot.Itens.All(i => ofertados.Any(o => o.CotacaoItemId == i.Id && o.QuantidadeOfertada >= i.Quantidade));
            fornDtos.Add(new ComparacaoFornecedorDto(cf.Id, cf.FornecedorId, Nome(cf), cf.Status, resp.Frete, resp.OutrasDespesas, resp.PrazoEntregaDias,
                resp.CondicaoPagamento, DiasPagamento(resp.CondicaoPagamento), resp.ValidadeProposta, calc.TotalNota, calc.CustoEfetivoTotal,
                cobre, ofertados.Count, false, false, false));
        }

        // destaques por fornecedor
        var comPrazo = fornDtos.Where(f => f.PrazoEntregaDias > 0).ToList();
        var minPrazo = comPrazo.Count > 0 ? comPrazo.Min(f => f.PrazoEntregaDias) : 0;
        var comPag = fornDtos.Where(f => f.DiasPagamento != null).ToList();
        var maxPag = comPag.Count > 0 ? comPag.Max(f => f.DiasPagamento) : null;
        var completos = fornDtos.Where(f => f.CobreTudo).ToList();
        var minTotal = completos.Count > 0 ? completos.Min(f => f.CustoEfetivoTotal) : (decimal?)null;
        fornDtos = fornDtos.Select(f => f with
        {
            MelhorPrazo = f.PrazoEntregaDias > 0 && f.PrazoEntregaDias == minPrazo && comPrazo.Count > 1,
            MelhorPagamento = f.DiasPagamento != null && f.DiasPagamento == maxPag && maxPag > 0 && comPag.Count > 1,
            MelhorCustoTotal = f.CobreTudo && f.CustoEfetivoTotal == minTotal,
        }).ToList();

        // itens
        var itensDto = new List<ComparacaoItemDto>();
        foreach (var item in cot.Itens)
        {
            var ls = linhas.Where(l => l.Item.Id == item.Id).ToList();
            var menorPreco = ls.Count > 0 ? ls.Min(l => l.C.PrecoLiquidoUnit) : 0;
            var melhorCusto = ls.Count > 0 ? ls.Min(l => l.C.CustoEfetivoUnit) : 0;
            var media = ls.Count > 0 ? ls.Average(l => l.C.CustoEfetivoUnit) : 0;
            var (refUnit, refOrigem) = item.UltimoCustoRef > 0 ? (item.UltimoCustoRef, "ultimo_custo") : (media, "media_propostas");
            var ofertas = ls.Select(l =>
            {
                var credito = l.C.CreditoIcms + l.C.CreditoIpi + l.C.CreditoPisCofins;
                decimal? margem = item.PrecoVendaRef > 0 ? Math.Round((item.PrecoVendaRef - l.C.CustoEfetivoUnit) / item.PrecoVendaRef * 100, 2) : null;
                var qtd = l.R.QuantidadeOfertada;
                return new OfertaDto(l.Cf.Id, true, qtd, l.R.PrecoUnitario, l.C.PrecoLiquidoUnit, l.C.CustoEfetivoUnit,
                    qtd > 0 ? CustoEfetivoCalculator.R4((l.C.CustoEfetivo - l.C.Rateio) / qtd) : 0, l.C.TotalNota, l.R.Cst, l.R.Cfop, l.C.Icms, l.C.Ipi, l.C.St, credito, margem,
                    qtd < item.Quantidade, ls.Count > 1 && l.C.PrecoLiquidoUnit == menorPreco, ls.Count > 1 && l.C.CustoEfetivoUnit == melhorCusto);
            }).OrderBy(o => o.CustoEfetivoUnit).ToList();
            itensDto.Add(new ComparacaoItemDto(item.Id, item.Codigo, item.Descricao, item.Unidade, item.Quantidade, item.UltimoCustoRef, item.PrecoVendaRef,
                CustoEfetivoCalculator.R4(refUnit), refOrigem, ofertas));
        }

        var rec = Recomendar(cot, fornDtos, itensDto, linhas);
        var potencial = 0m;
        foreach (var i in itensDto.Where(i => i.Ofertas.Count > 0))
        {
            var best = i.Ofertas[0];
            potencial += (i.ReferenciaCustoUnit - best.CustoEfetivoUnit) * Math.Min(i.Quantidade, best.QuantidadeOfertada);
        }

        var escolhas = cot.Itens.Where(i => i.CotacaoFornecedorEscolhidoId != null).ToDictionary(i => i.Id, i => i.CotacaoFornecedorEscolhidoId!.Value);
        return new ComparacaoDto(cot.Id, cot.Status, propostas.Count > 0, cfg.Regime, cfg.CreditaIcms, cfg.CreditaIpi, cfg.PercCreditoPisCofins,
            fornDtos, itensDto, rec, CustoEfetivoCalculator.R2(potencial), escolhas);
    }

    private static string Nome(CotacaoFornecedor cf) => cf.Fornecedor == null ? "(fornecedor)" : (string.IsNullOrWhiteSpace(cf.Fornecedor.NomeFantasia) ? cf.Fornecedor.RazaoSocial : cf.Fornecedor.NomeFantasia);

    private static RecomendacaoDto? Recomendar(Cotacao cot, List<ComparacaoFornecedorDto> forns, List<ComparacaoItemDto> itens, List<Linha> linhas)
    {
        if (forns.Count == 0) return null;
        // combinação item a item: menor custo efetivo unitário; frete/despesas de cada fornecedor usado entram uma única vez
        var comb = new Dictionary<Guid, Guid>();
        foreach (var i in itens.Where(i => i.Ofertas.Count > 0)) comb[i.CotacaoItemId] = i.Ofertas[0].CotacaoFornecedorId;
        if (comb.Count == 0) return null;
        var totalComb = 0m;
        foreach (var (itemId, cfId) in comb)
        {
            var l = linhas.FirstOrDefault(x => x.Item.Id == itemId && x.Cf.Id == cfId);
            if (l != null) totalComb += l.C.CustoEfetivo - l.C.Rateio;
        }
        foreach (var cfId in comb.Values.Distinct())
        {
            var f = forns.FirstOrDefault(x => x.CotacaoFornecedorId == cfId);
            if (f != null) totalComb += f.Frete + f.OutrasDespesas;
        }
        totalComb = CustoEfetivoCalculator.R2(totalComb);

        var melhorUnico = forns.Where(f => f.CobreTudo).OrderBy(f => f.CustoEfetivoTotal).FirstOrDefault();
        if (melhorUnico != null && cot.Itens.Count > 0 && melhorUnico.CustoEfetivoTotal <= totalComb + 0.005m)
        {
            return new RecomendacaoDto("FornecedorUnico", cot.Itens.ToDictionary(i => i.Id, _ => melhorUnico.CotacaoFornecedorId), melhorUnico.CustoEfetivoTotal,
                $"{melhorUnico.Nome} atende todos os itens com o menor custo efetivo total (R$ {melhorUnico.CustoEfetivoTotal:N2}).");
        }
        var usados = comb.Values.Distinct().Count();
        var txt = melhorUnico != null
            ? $"Combinar {usados} fornecedores custa R$ {totalComb:N2}, abaixo do melhor fornecedor único (R$ {melhorUnico.CustoEfetivoTotal:N2})."
            : $"Nenhum fornecedor atende todos os itens; a melhor combinação item a item custa R$ {totalComb:N2}.";
        return new RecomendacaoDto("Combinacao", comb, totalComb, txt);
    }

    /// <summary>Calcula economia potencial e realizada de uma seleção (item → proposta do fornecedor).</summary>
    public static ResultadoEconomia CalcularEconomia(ComparacaoDto cmp, Dictionary<Guid, Guid> escolhas)
    {
        decimal potencial = 0, realizada = 0, total = 0, vendaTot = 0, custoTot = 0;
        var usados = new HashSet<Guid>();
        foreach (var i in cmp.Itens.Where(i => i.Ofertas.Count > 0))
        {
            var best = i.Ofertas[0];
            potencial += (i.ReferenciaCustoUnit - best.CustoEfetivoUnit) * Math.Min(i.Quantidade, best.QuantidadeOfertada);
            if (!escolhas.TryGetValue(i.CotacaoItemId, out var cfId)) continue;
            var o = i.Ofertas.FirstOrDefault(x => x.CotacaoFornecedorId == cfId);
            if (o == null) continue;
            var q = Math.Min(i.Quantidade, o.QuantidadeOfertada);
            realizada += (i.ReferenciaCustoUnit - o.CustoEfetivoUnit) * q;
            total += o.TotalNota * (o.QuantidadeOfertada > 0 ? q / o.QuantidadeOfertada : 0);
            usados.Add(cfId);
            if (i.PrecoVendaRef > 0) { vendaTot += i.PrecoVendaRef * q; custoTot += o.CustoEfetivoUnit * q; }
        }
        foreach (var cfId in usados)
        {
            var f = cmp.Fornecedores.FirstOrDefault(x => x.CotacaoFornecedorId == cfId);
            if (f != null) total += f.Frete + f.OutrasDespesas;
        }
        decimal? margem = vendaTot > 0 ? Math.Round((vendaTot - custoTot) / vendaTot * 100, 2) : null;
        return new ResultadoEconomia(CustoEfetivoCalculator.R2(potencial), CustoEfetivoCalculator.R2(realizada), CustoEfetivoCalculator.R2(total), margem);
    }
}

