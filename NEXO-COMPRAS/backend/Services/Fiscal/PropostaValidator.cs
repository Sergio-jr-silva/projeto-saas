namespace Nexo.Api.Services.Fiscal;

public record ItemParaValidar(string Descricao, decimal QuantidadeSolicitada, bool Disponivel, decimal QuantidadeOfertada, decimal PrecoUnitario,
    decimal DescontoPerc, string Cst, string Cfop, decimal IcmsAliq, decimal IpiAliq, decimal Mva, decimal IcmsStAliq, decimal IcmsStValor);

public record ResultadoValidacao(List<string> Erros, List<string> Avisos)
{
    public bool Valido => Erros.Count == 0;
}

public static class PropostaValidator
{
    /// <summary>
    /// Valida a proposta conforme o cenário tributário. Erros bloqueiam o envio final; avisos orientam o fornecedor.
    /// </summary>
    public static ResultadoValidacao Validar(IEnumerable<ItemParaValidar> itens, decimal frete, decimal outras, int prazoEntrega,
        string condicaoPagamento, DateOnly? validade, string? ufFornecedor, string? ufComprador, bool envioFinal)
    {
        var erros = new List<string>();
        var avisos = new List<string>();
        var lista = itens.ToList();

        if (frete < 0 || outras < 0) erros.Add("Frete e outras despesas não podem ser negativos.");
        if (prazoEntrega < 0 || prazoEntrega > 365) erros.Add("Prazo de entrega deve estar entre 0 e 365 dias.");

        foreach (var i in lista)
        {
            var p = $"[{i.Descricao}] ";
            if (!i.Disponivel) continue;
            if (i.QuantidadeOfertada < 0) erros.Add(p + "quantidade ofertada negativa.");
            if (i.QuantidadeOfertada > i.QuantidadeSolicitada) erros.Add(p + $"quantidade ofertada ({i.QuantidadeOfertada}) maior que a solicitada ({i.QuantidadeSolicitada}).");
            if (i.PrecoUnitario < 0) erros.Add(p + "preço unitário negativo.");
            foreach (var (nome, v) in new[] { ("desconto", i.DescontoPerc), ("ICMS", i.IcmsAliq), ("IPI", i.IpiAliq), ("ICMS ST", i.IcmsStAliq) })
                if (v < 0 || v > 100) erros.Add(p + $"alíquota de {nome} deve estar entre 0 e 100%.");
            if (i.Mva < 0 || i.Mva > 1000) erros.Add(p + "MVA deve estar entre 0 e 1000%.");
            if (i.IcmsStValor < 0) erros.Add(p + "valor de ICMS ST negativo.");

            if (!envioFinal) continue;

            if (i.QuantidadeOfertada <= 0 || i.PrecoUnitario <= 0) { erros.Add(p + "informe quantidade e preço, ou marque o item como indisponível."); continue; }

            var cstRaw = Cst.Normalize(i.Cst);
            var cst = Cst.Effective(i.Cst);
            if (cstRaw.Length == 0) erros.Add(p + "CST/CSOSN é obrigatório.");
            else if (cst == null) erros.Add(p + $"CST/CSOSN '{i.Cst}' inválido. Use CST (00, 10, 20, 30, 40, 41, 50, 51, 60, 70, 90, com ou sem dígito de origem) ou CSOSN (101, 102, 103, 201, 202, 203, 300, 400, 500, 900).");

            if (string.IsNullOrWhiteSpace(i.Cfop)) erros.Add(p + "CFOP é obrigatório.");
            else if (i.Cfop.Length != 4 || !i.Cfop.All(char.IsDigit) || i.Cfop[0] is not ('5' or '6' or '7'))
                erros.Add(p + "CFOP inválido: operações de saída do fornecedor usam CFOP de 4 dígitos iniciado em 5, 6 ou 7.");
            else if (!string.IsNullOrEmpty(ufFornecedor) && !string.IsNullOrEmpty(ufComprador))
            {
                var interna = string.Equals(ufFornecedor, ufComprador, StringComparison.OrdinalIgnoreCase);
                if (interna && i.Cfop[0] != '5') avisos.Add(p + "operação dentro do mesmo estado normalmente usa CFOP 5xxx.");
                if (!interna && i.Cfop[0] != '6') avisos.Add(p + "operação interestadual normalmente usa CFOP 6xxx.");
            }

            if (cst != null)
            {
                if (Cst.IsentoNaoTributado.Contains(cst) && i.IcmsAliq > 0)
                    erros.Add(p + $"CST/CSOSN {cst} é isento/não tributado: a alíquota de ICMS deve ser 0.");
                if (Cst.TributadoComIcms.Contains(cst) && i.IcmsAliq == 0)
                    avisos.Add(p + $"CST {cst} indica operação tributada, mas a alíquota de ICMS está zerada.");
                if (Cst.ComSt.Contains(cst) && i.IcmsStAliq == 0 && i.IcmsStValor == 0)
                    avisos.Add(p + $"CST/CSOSN {cst} indica substituição tributária, mas o ICMS ST não foi informado.");
                if (!Cst.ComSt.Contains(cst) && (i.IcmsStAliq > 0 || i.IcmsStValor > 0))
                    avisos.Add(p + $"ICMS ST informado, mas o CST/CSOSN {cst} não é de substituição tributária.");
            }
        }

        if (envioFinal)
        {
            if (!lista.Any(i => i.Disponivel && i.QuantidadeOfertada > 0 && i.PrecoUnitario > 0)) erros.Add("Informe preço e quantidade de ao menos um item.");
            if (string.IsNullOrWhiteSpace(condicaoPagamento)) erros.Add("Informe a condição de pagamento.");
            if (prazoEntrega <= 0) avisos.Add("Prazo de entrega não informado.");
            if (validade == null) erros.Add("Informe a validade da proposta.");
            else if (validade < DateOnly.FromDateTime(DateTime.UtcNow.Date)) erros.Add("A validade da proposta não pode estar no passado.");
        }
        return new ResultadoValidacao(erros, avisos);
    }
}

