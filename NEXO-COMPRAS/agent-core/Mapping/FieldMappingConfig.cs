namespace Nexo.Agent.Core.Mapping;

public class FieldMappingConfig
{
    public ProdutoTableMapping Produtos { get; set; } = new();
    public FornecedorTableMapping Fornecedores { get; set; } = new();
    public EstoqueTableMapping Estoque { get; set; } = new();
}

public class ProdutoTableMapping
{
    public string TableName { get; set; } = "PRODUTOS";
    public string Codigo { get; set; } = "CODIGO";
    public string Descricao { get; set; } = "DESCRICAO";
    public string CodigoBarras { get; set; } = "CODBARRA";
    public string Unidade { get; set; } = "UNIDADE";
    public string Grupo { get; set; } = "GRUPO";
    public string Categoria { get; set; } = "CATEGORIA";
    public string Ncm { get; set; } = "NCM";
    public string PrecoVenda { get; set; } = "PRECOVENDA";
    public string CustoAtual { get; set; } = "PRECOCUSTO";
    public string FornecedorErpId { get; set; } = "CODFORN";
    public string EstoqueAtual { get; set; } = "ESTOQUEATUAL";
    public string EstoqueMinimo { get; set; } = "ESTOQUEMINIMO";
    public string UltimaAtualizacao { get; set; } = "DATAATUALIZACAO";
    public string Ativo { get; set; } = "ATIVO";
}

public class FornecedorTableMapping
{
    public string TableName { get; set; } = "FORNECEDORES";
    public string Codigo { get; set; } = "CODIGO";
    public string RazaoSocial { get; set; } = "RAZAO";
    public string NomeFantasia { get; set; } = "FANTASIA";
    public string Cnpj { get; set; } = "CNPJ";
    public string InscricaoEstadual { get; set; } = "IE";
    public string Email { get; set; } = "EMAIL";
    public string Telefone { get; set; } = "TELEFONE";
    public string Whatsapp { get; set; } = "WHATSAPP";
    public string Contato { get; set; } = "CONTATO";
    public string Endereco { get; set; } = "ENDERECO";
    public string Cidade { get; set; } = "CIDADE";
    public string Uf { get; set; } = "UF";
    public string PrazoEntrega { get; set; } = "PRAZOENTREGA";
}

public class EstoqueTableMapping
{
    public string TableName { get; set; } = "PRODUTOS";
    public string Codigo { get; set; } = "CODIGO";
    public string EstoqueAtual { get; set; } = "ESTOQUEATUAL";
    public string EstoquePendente { get; set; } = "ESTOQUEPENDENTE";
}

