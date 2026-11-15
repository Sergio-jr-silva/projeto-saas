using System.Text.Json.Serialization;

namespace Nexo.Agent.Core;

public record ErpProdutoDto(
    string CodigoErp,
    string Descricao,
    string? CodigoBarras,
    string? Unidade,
    string? Grupo,
    string? Categoria,
    string? Ncm,
    decimal PrecoVenda,
    decimal CustoAtual,
    string? FornecedorErpId,
    decimal Consumo90Dias,
    decimal UltimoCusto,
    DateTime? UltimaCompraEm,
    decimal QuantidadeUltimaCompra,
    string? UltimoFornecedorErpId,
    decimal EstoqueMinimo = 0,
    DateTime? UltimaAtualizacao = null,
    bool Ativo = true
);

public record ErpEstoqueDto(
    string CodigoErp,
    decimal? EstoqueAtual,   // null = estoque desconhecido no ERP (não é zero)
    decimal EstoquePendente
);

public record ErpFornecedorDto(
    string CodigoErp,
    string RazaoSocial,
    string? NomeFantasia,
    string? Cnpj,
    string? InscricaoEstadual,
    string? Email,
    string? Telefone,
    string? Whatsapp,
    string? Contato,
    string? Endereco,
    string? Cidade,
    string? Uf,
    int PrazoEntregaDias = 3,
    bool Ativo = true
);

public record ErpPrecoDto(
    string CodigoProdutoErp,
    decimal PrecoCusto,
    decimal PrecoVenda,
    DateTime Data,
    string? FornecedorErpId = null,
    decimal Quantidade = 0
);

public record SyncBatch<T>(
    Guid BatchId,
    DateTime DataEnvio,
    List<T> Itens,
    bool IsDelta = false,
    Guid? TenantId = null,
    Guid? BranchId = null
);

public record SyncResultResponse(
    int Recebidos,
    int Criados,
    int Atualizados,
    int Ignorados
);

public record AgentConfigResponse(
    int IntervaloEstoqueMin,
    int IntervaloProdutosMin,
    int IntervaloFornecedoresMin,
    int IntervaloPrecosMin,
    int IntervaloHeartbeatSeg,
    string? TenantRazaoSocial,
    string? TenantNomeFantasia,
    string? TenantCnpj
);

