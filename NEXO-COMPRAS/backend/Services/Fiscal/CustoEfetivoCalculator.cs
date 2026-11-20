namespace Nexo.Api.Services.Fiscal;

public record TributacaoConfig(string Regime, bool CreditaIcms, bool CreditaIpi, decimal PercCreditoPisCofins)
{
    public static readonly string[] Regimes = { "SimplesNacional", "LucroPresumido", "LucroReal" };

    /// <summary>Valida a coerência da configuração. Não se presume que todo tributo é recuperável.</summary>
    public IReadOnlyList<string> Validate()
    {
        var erros = new List<string>();
        if (!Regimes.Contains(Regime)) erros.Add("Regime tributário inválido.");
        if (PercCreditoPisCofins < 0 || PercCreditoPisCofins > 20) erros.Add("Crédito de PIS/COFINS deve estar entre 0 e 20%.");
        if (Regime == "SimplesNacional" && (CreditaIcms || CreditaIpi || PercCreditoPisCofins > 0))
            erros.Add("Empresas do Simples Nacional não apropriam créditos de ICMS, IPI ou PIS/COFINS nas compras.");
        if (Regime == "LucroPresumido" && PercCreditoPisCofins > 0)
            erros.Add("PIS/COFINS cumulativo (Lucro Presumido) não gera crédito nas compras.");
        return erros;
    }
}

public record ItemProposta(decimal Quantidade, decimal PrecoUnitario, decimal DescontoPerc, string Cst,
    decimal IcmsAliq, decimal IpiAliq, decimal Mva, decimal IcmsStAliq, decimal IcmsStValor);

public record ItemCusto(decimal Bruto, decimal Desconto, decimal Liquido, decimal Icms, decimal Ipi, decimal St, decimal TotalNota,
    decimal CreditoIcms, decimal CreditoIpi, decimal CreditoPisCofins, decimal Rateio, decimal CustoEfetivo,
    decimal PrecoLiquidoUnit, decimal CustoEfetivoUnit);

public record PropostaCusto(IReadOnlyList<ItemCusto> Itens, decimal Frete, decimal OutrasDespesas, decimal TotalNota, decimal CustoEfetivoTotal);

public static class CustoEfetivoCalculator
{
    private static readonly HashSet<string> CstTributadoCredito = new() { "00", "10", "20", "70", "90" };

    public static decimal R2(decimal v) => Math.Round(v, 2, MidpointRounding.AwayFromZero);
    public static decimal R4(decimal v) => Math.Round(v, 4, MidpointRounding.AwayFromZero);

    /// <summary>CST de 2 dígitos (ou origem+CST em 3) tributado integralmente/parcialmente gera crédito de ICMS. CSOSN nunca.</summary>
    public static bool CstGeraCreditoIcms(string? cst)
    {
        var c = Cst.Normalize(cst);
        if (c.Length == 3 && Cst.Csosn.Contains(c)) return false;
        var last2 = c.Length >= 2 ? c[^2..] : c;
        return CstTributadoCredito.Contains(last2);
    }

    public static PropostaCusto Calcular(TributacaoConfig cfg, IReadOnlyList<ItemProposta> itens, decimal frete, decimal outrasDespesas)
    {
        var partes = new List<(decimal bruto, decimal desc, decimal liq, decimal icms, decimal ipi, decimal st, decimal credIcms, decimal credIpi, decimal credPc)>();
        foreach (var i in itens)
        {
            var bruto = i.Quantidade * i.PrecoUnitario;
            var desc = bruto * i.DescontoPerc / 100m;
            var liq = bruto - desc;
            var icms = liq * i.IcmsAliq / 100m;                     // ICMS próprio, já embutido no preço
            var ipi = liq * i.IpiAliq / 100m;                       // IPI é somado ao valor da nota
            decimal st = 0;
            if (i.IcmsStValor > 0) st = i.IcmsStValor;
            else if (i.IcmsStAliq > 0) st = Math.Max(0, (liq + ipi) * (1 + i.Mva / 100m) * i.IcmsStAliq / 100m - icms);

            var credIcms = cfg.CreditaIcms && CstGeraCreditoIcms(i.Cst) ? icms : 0;
            var credIpi = cfg.CreditaIpi ? ipi : 0;
            var credPc = cfg.Regime == "LucroReal" ? Math.Max(0, liq - icms) * cfg.PercCreditoPisCofins / 100m : 0;
            partes.Add((bruto, desc, liq, icms, ipi, st, credIcms, credIpi, credPc));
        }

        var somaLiq = partes.Sum(p => p.liq);
        var extras = frete + outrasDespesas;
        var res = new List<ItemCusto>();
        for (var k = 0; k < itens.Count; k++)
        {
            var p = partes[k];
            var peso = somaLiq > 0 ? p.liq / somaLiq : (itens.Count > 0 ? 1m / itens.Count : 0);
            var rateio = extras * peso;
            var totalNota = p.liq + p.ipi + p.st;
            var custo = totalNota - p.credIcms - p.credIpi - p.credPc + rateio;
            var q = itens[k].Quantidade;
            res.Add(new ItemCusto(R2(p.bruto), R2(p.desc), R2(p.liq), R2(p.icms), R2(p.ipi), R2(p.st), R2(totalNota),
                R2(p.credIcms), R2(p.credIpi), R2(p.credPc), R2(rateio), R2(custo),
                q > 0 ? R4(p.liq / q) : 0, q > 0 ? R4(custo / q) : 0));
        }
        return new PropostaCusto(res, R2(frete), R2(outrasDespesas), R2(partes.Sum(p => p.liq + p.ipi + p.st) + extras), R2(res.Sum(r => r.CustoEfetivo)));
    }
}

public static class Cst
{
    public static readonly HashSet<string> Icms = new() { "00", "10", "20", "30", "40", "41", "50", "51", "60", "70", "90" };
    public static readonly HashSet<string> Csosn = new() { "101", "102", "103", "201", "202", "203", "300", "400", "500", "900" };
    public static readonly HashSet<string> IsentoNaoTributado = new() { "40", "41", "50", "60", "102", "103", "300", "400", "500" };
    public static readonly HashSet<string> TributadoComIcms = new() { "00", "10", "20", "70" };
    public static readonly HashSet<string> ComSt = new() { "10", "30", "70", "201", "202", "203" };

    public static string Normalize(string? c) => new((c ?? string.Empty).Where(char.IsDigit).ToArray());

    /// <summary>Retorna o código "efetivo" para regras: CST de 2 dígitos ou CSOSN de 3 dígitos (descarta o dígito de origem).</summary>
    public static string? Effective(string? raw)
    {
        var c = Normalize(raw);
        if (c.Length == 2 && Icms.Contains(c)) return c;
        if (c.Length == 3 && Csosn.Contains(c)) return c;
        if (c.Length == 3 && c[0] is >= '0' and <= '8' && Icms.Contains(c[1..])) return c[1..];
        return null;
    }
}

