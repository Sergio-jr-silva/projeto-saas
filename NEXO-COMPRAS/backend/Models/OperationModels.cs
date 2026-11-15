namespace Nexo.Api.Models;

public class Produto : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public string CodigoErp { get; set; } = string.Empty;
    public string Descricao { get; set; } = string.Empty;
    public string CodigoBarras { get; set; } = string.Empty;
    public string Unidade { get; set; } = "UN";
    public string Grupo { get; set; } = string.Empty;
    public string Categoria { get; set; } = string.Empty;
    public string Ncm { get; set; } = string.Empty;
    public decimal PrecoVenda { get; set; }
    public decimal CustoAtual { get; set; }
    public decimal UltimoCusto { get; set; }        // último preço de compra conhecido
    public DateTime? UltimaCompraEm { get; set; }
    /// <summary>Saldo disponível informado pelo ERP. NULL = desconhecido (não é o mesmo que zero).</summary>
    public decimal? EstoqueAtual { get; set; }
    /// <summary>Limite de estoque crítico definido pelo comprador (opcional, por produto).</summary>
    public decimal? EstoqueCritico { get; set; }
    public decimal EstoquePendente { get; set; }    // pedidos ainda não recebidos (informado pelo ERP)
    public decimal EstoqueMinimo { get; set; }      // definido pelo comprador; a sincronização nunca sobrescreve
    public decimal Consumo90Dias { get; set; }      // quantidade vendida nos últimos 90 dias (ERP)
    public string FornecedorErpId { get; set; } = string.Empty;
    public Guid? FornecedorVinculadoId { get; set; }
    public Fornecedor? FornecedorVinculado { get; set; }
    public string Origem { get; set; } = "manual"; // erp, manual
    public bool Ativo { get; set; } = true;
    public DateTime? UltimaSincronizacaoEm { get; set; }
}

public class Fornecedor : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public string CodigoErp { get; set; } = string.Empty;
    public string RazaoSocial { get; set; } = string.Empty;
    public string NomeFantasia { get; set; } = string.Empty;
    public string Cnpj { get; set; } = string.Empty;
    public string InscricaoEstadual { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Telefone { get; set; } = string.Empty;
    public string Whatsapp { get; set; } = string.Empty;
    public string Contato { get; set; } = string.Empty;
    public string Endereco { get; set; } = string.Empty;
    public string Cidade { get; set; } = string.Empty;
    public string Uf { get; set; } = string.Empty;
    public int PrazoEntregaPadrao { get; set; } = 3;
    public string CondicoesPagamento { get; set; } = string.Empty;
    public string Observacoes { get; set; } = string.Empty;
    public Guid? FilialId { get; set; } // Opcional: restrito a uma filial específica (null = compartilhado com toda a rede)
    public string Origem { get; set; } = "manual";
    public bool Ativo { get; set; } = true;
}

public class HistoricoPreco : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid ProdutoId { get; set; }
    public Produto? Produto { get; set; }
    public Guid? FornecedorId { get; set; }
    public Fornecedor? Fornecedor { get; set; }
    public DateTime Data { get; set; }
    public decimal Valor { get; set; }
    public decimal Quantidade { get; set; }
    public string Origem { get; set; } = "erp"; // erp, cotacao
}

public class Cotacao : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public string Numero { get; set; } = string.Empty;
    public string Titulo { get; set; } = string.Empty;
    public string Observacoes { get; set; } = string.Empty;
    public string Status { get; set; } = "Aberta"; // Aberta, Aprovada, Cancelada
    public Guid? FilialId { get; set; } // Opcional: filial solicitante / faturamento (null = compra centralizada / matriz)
    public string TipoCompra { get; set; } = "Centralizada"; // Centralizada, Filial
    public Guid? CriadaPorId { get; set; }
    public DateTime DataLimiteResposta { get; set; }
    public Guid? AprovadaPorId { get; set; }
    public string AprovadaPorNome { get; set; } = string.Empty;
    public DateTime? AprovadaEm { get; set; }
    public string ObservacaoAprovacao { get; set; } = string.Empty;
    public decimal TotalAprovado { get; set; }
    public decimal EconomiaPotencial { get; set; }
    public decimal EconomiaRealizada { get; set; }

    public ICollection<CotacaoItem> Itens { get; set; } = new List<CotacaoItem>();
    public ICollection<CotacaoFornecedor> Fornecedores { get; set; } = new List<CotacaoFornecedor>();
}

public class CotacaoItem : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid CotacaoId { get; set; }
    public Cotacao? Cotacao { get; set; }
    public Guid ProdutoId { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Descricao { get; set; } = string.Empty;
    public string Unidade { get; set; } = "UN";
    public decimal Quantidade { get; set; }
    public decimal EstoqueNoMomento { get; set; }
    public decimal UltimoCustoRef { get; set; }
    public decimal PrecoVendaRef { get; set; }
    public Guid? CotacaoFornecedorEscolhidoId { get; set; }
}

public class CotacaoFornecedor : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid CotacaoId { get; set; }
    public Cotacao? Cotacao { get; set; }
    public Guid FornecedorId { get; set; }
    public Fornecedor? Fornecedor { get; set; }
    public string Status { get; set; } = "Enviada"; // Enviada, Visualizada, Respondida, Aprovada, NaoSelecionada, Cancelada
    public AcessoFornecedor? Acesso { get; set; }
    public RespostaCotacao? Resposta { get; set; }
}

/// <summary>Credencial pública do fornecedor: token imprevisível no link + código independente (somente o hash é guardado).</summary>
public class AcessoFornecedor : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid CotacaoFornecedorId { get; set; }
    public CotacaoFornecedor? CotacaoFornecedor { get; set; }
    public string Token { get; set; } = string.Empty;
    public string CodigoHash { get; set; } = string.Empty;
    public DateTime ExpiraEm { get; set; }
    public bool Revogado { get; set; }
    public int Tentativas { get; set; }
    public DateTime? BloqueadoAte { get; set; }
    public int Visualizacoes { get; set; }
    public DateTime? PrimeiraVisualizacaoEm { get; set; }
    public DateTime? UltimaVisualizacaoEm { get; set; }
}

public class RespostaCotacao : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid CotacaoFornecedorId { get; set; }
    public CotacaoFornecedor? CotacaoFornecedor { get; set; }
    public string Status { get; set; } = "Rascunho"; // Rascunho, Enviada
    public decimal Frete { get; set; }
    public decimal OutrasDespesas { get; set; }
    public int PrazoEntregaDias { get; set; }
    public string CondicaoPagamento { get; set; } = string.Empty;
    public DateOnly? ValidadeProposta { get; set; }
    public string ContatoNome { get; set; } = string.Empty;
    public string Observacoes { get; set; } = string.Empty;
    public DateTime? EnviadaEm { get; set; }

    public ICollection<RespostaCotacaoItem> Itens { get; set; } = new List<RespostaCotacaoItem>();
    public ICollection<AnexoResposta> Anexos { get; set; } = new List<AnexoResposta>();
}

public class RespostaCotacaoItem : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid RespostaCotacaoId { get; set; }
    public RespostaCotacao? Resposta { get; set; }
    public Guid CotacaoItemId { get; set; }
    public bool Disponivel { get; set; } = true;
    public decimal QuantidadeOfertada { get; set; }
    public decimal PrecoUnitario { get; set; }
    public decimal DescontoPerc { get; set; }
    public string Cst { get; set; } = string.Empty;   // CST (regime normal) ou CSOSN (Simples Nacional)
    public string Cfop { get; set; } = string.Empty;
    public decimal IcmsAliq { get; set; }
    public decimal IpiAliq { get; set; }
    public decimal Mva { get; set; }
    public decimal IcmsStAliq { get; set; }
    public decimal IcmsStValor { get; set; }          // opcional: se informado, substitui o cálculo do ST
    public string Observacao { get; set; } = string.Empty;
}

public class AnexoResposta : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid RespostaCotacaoId { get; set; }
    public RespostaCotacao? Resposta { get; set; }
    public string NomeArquivo { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long Tamanho { get; set; }
    public string CaminhoStorage { get; set; } = string.Empty;
}

public class PedidoCompra : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public string Numero { get; set; } = string.Empty; // ex.: PED-2026-0001
    public Guid? CotacaoId { get; set; }
    public Cotacao? Cotacao { get; set; }
    public Guid FornecedorId { get; set; }
    public Fornecedor? Fornecedor { get; set; }
    public Guid? FilialId { get; set; } // Opcional: filial de entrega e faturamento
    public Filial? Filial { get; set; }

    public string Status { get; set; } = "Criado"; // Criado, Enviado, AguardandoConfirmacao, Confirmado, EmPreparacao, EmTransporte, Recebido, Cancelado
    public decimal Subtotal { get; set; }
    public decimal Desconto { get; set; }
    public decimal Frete { get; set; }
    public decimal OutrasDespesas { get; set; }
    public decimal ImpostosTotal { get; set; }
    public decimal ValorTotal { get; set; }

    public string CondicaoPagamento { get; set; } = string.Empty;
    public int PrazoEntregaDias { get; set; }
    public DateTime? PrevisaoEntrega { get; set; }
    public DateTime? DataEntregaEfetiva { get; set; }
    public string Observacoes { get; set; } = string.Empty;

    public Guid? AprovadoPorId { get; set; }
    public string AprovadoPorNome { get; set; } = string.Empty;
    public DateTime? AprovadoEm { get; set; }

    public string TimelineJson { get; set; } = "[]";

    public ICollection<PedidoCompraItem> Itens { get; set; } = new List<PedidoCompraItem>();
}

public class PedidoCompraItem : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid PedidoCompraId { get; set; }
    public PedidoCompra? PedidoCompra { get; set; }

    public Guid ProdutoId { get; set; }
    public Produto? Produto { get; set; }
    public string CodigoProduto { get; set; } = string.Empty;
    public string DescricaoProduto { get; set; } = string.Empty;
    public string Unidade { get; set; } = "UN";
    public string Ncm { get; set; } = string.Empty;

    public decimal Quantidade { get; set; }
    public decimal QuantidadeRecebida { get; set; }
    public DateTime? DataUltimoRecebimento { get; set; }
    public decimal PrecoUnitario { get; set; }
    public decimal DescontoPerc { get; set; }
    public decimal PrecoLiquidoUnit { get; set; }
    public decimal Subtotal { get; set; }

    public string Cst { get; set; } = string.Empty;
    public string Cfop { get; set; } = string.Empty;
    public decimal IcmsAliq { get; set; }
    public decimal IpiAliq { get; set; }
    public decimal Mva { get; set; }
    public decimal IcmsStAliq { get; set; }
    public decimal IcmsStValor { get; set; }
    public decimal CustoEfetivoUnit { get; set; }
    public decimal CustoEfetivoTotal { get; set; }
}
