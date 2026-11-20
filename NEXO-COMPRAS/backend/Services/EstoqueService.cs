using Nexo.Api.Models;

namespace Nexo.Api.Services;

public record AlertaEstoque(string Nivel, decimal ConsumoDia, decimal? CoberturaDias, int LeadTimeDias, decimal Sugerida);

/// <summary>Níveis de estoque. "Ok" foi substituído por "Normal" (nome já usado pelo frontend).</summary>
public static class NivelEstoque
{
    public const string Zerado = "Zerado";
    public const string Critico = "Critico";
    public const string Baixo = "Baixo";
    public const string Normal = "Normal";
    /// <summary>Estoque desconhecido (NULL no ERP/cadastro). Nunca é tratado como zero.</summary>
    public const string Indeterminado = "Indeterminado";

    /// <summary>Níveis que representam necessidade de reposição (filtro "Baixo, Crítico ou Zerado").</summary>
    public static bool EhAlerta(string nivel) => nivel is Zerado or Critico or Baixo;
}

public static class EstoqueService
{
    private static readonly HashSet<string> UnidadesFracionadas = new(StringComparer.OrdinalIgnoreCase) { "KG", "G", "LT", "L", "ML", "MT", "M" };

    /// <summary>
    /// Quantidade usada na classificação: <c>Produto.EstoqueAtual</c> = saldo disponível informado pelo ERP
    /// (o ERP já desconta reservas; <c>EstoquePendente</c> é mercadoria a receber e NÃO entra na classificação,
    /// apenas na sugestão de compra). NULL = desconhecido → "Indeterminado".
    ///
    /// Regra (por produto, respeitando os limites configurados de cada um):
    ///  • Zerado:  disponível ≤ 0 (sempre, independentemente de consumo ou mínimo cadastrado).
    ///  • Crítico: 0 &lt; disponível ≤ limite crítico. Limite crítico = <c>EstoqueCritico</c> do produto; se não configurado
    ///             e houver mínimo, 50% do mínimo; sem nenhum dos dois, cobertura menor que o prazo de entrega.
    ///  • Baixo:   limite crítico &lt; disponível ≤ <c>EstoqueMinimo</c> (ou cobertura menor que prazo + dias de segurança).
    ///  • Normal:  acima do mínimo.
    /// </summary>
    public static AlertaEstoque Avaliar(Produto p, ConfiguracaoTenant cfg, int? prazoFornecedor)
    {
        var lead = prazoFornecedor is > 0 ? prazoFornecedor.Value : cfg.LeadTimePadraoDias;
        var consumo = p.Consumo90Dias / 90m;

        if (p.EstoqueAtual is null)
            return new AlertaEstoque(NivelEstoque.Indeterminado, Math.Round(consumo, 3), null, lead, 0);

        var estoque = p.EstoqueAtual.Value;
        decimal? cobertura = consumo > 0 ? Math.Max(estoque, 0) / consumo : null;
        var minimo = p.EstoqueMinimo;

        string nivel;
        if (estoque <= 0) nivel = NivelEstoque.Zerado;
        else
        {
            decimal? limiteCritico = p.EstoqueCritico is > 0 ? p.EstoqueCritico
                                   : minimo > 0 ? minimo * 0.5m
                                   : null;
            var critico = limiteCritico.HasValue ? estoque <= limiteCritico.Value : cobertura < lead;
            var baixo = (minimo > 0 && estoque <= minimo) || cobertura < lead + cfg.DiasSeguranca;
            nivel = critico ? NivelEstoque.Critico : baixo ? NivelEstoque.Baixo : NivelEstoque.Normal;
        }

        decimal sugerida = 0;
        if (nivel != NivelEstoque.Normal)
        {
            var alvo = consumo * (lead + cfg.DiasCobertura);
            if (minimo > 0) alvo = Math.Max(alvo, minimo * 2);
            sugerida = Math.Max(0, alvo - Math.Max(estoque, 0) - Math.Max(p.EstoquePendente, 0));
            sugerida = UnidadesFracionadas.Contains(p.Unidade) ? Math.Ceiling(sugerida * 10) / 10 : Math.Ceiling(sugerida);
        }
        return new AlertaEstoque(nivel, Math.Round(consumo, 3), cobertura == null ? null : Math.Round(cobertura.Value, 1), lead, sugerida);
    }
}
