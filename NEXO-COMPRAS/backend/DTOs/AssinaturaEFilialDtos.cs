namespace Nexo.Api.DTOs;

// =============================================================================
// FASE 8: Planos, Assinaturas e Pagamentos DTOs
// =============================================================================

public record CheckoutAssinaturaRequest(
    Guid PlanoId,
    string Ciclo, // Mensal, Anual
    string FormaPagamento, // Pix, Cartao, Boleto
    string? CartaoNumero = null,
    string? CartaoNome = null,
    string? CartaoValidade = null,
    string? CartaoCvv = null
);

public record AssinaturaDetalheDto(
    Guid Id,
    Guid PlanoId,
    string PlanoNome,
    string Status, // Teste, Ativa, Cancelada, Inadimplente, Suspensa
    string Ciclo, // Mensal, Anual
    decimal Valor,
    DateTime DataInicio,
    DateTime? DataFim,
    DateTime? DataRenovacao,
    string FormaPagamento,
    int DiasRestantesTeste,
    LimitesUsoDto Limites
);

public record LimitesUsoDto(
    int UsuariosUsados,
    int MaxUsuarios,
    int FiliaisUsadas,
    int MaxFiliais,
    int CotacoesMesUsadas,
    int MaxCotacoesMes
);

public record FaturaDto(
    Guid Id,
    string Numero,
    string Descricao,
    decimal Valor,
    string Status, // Pendente, Paga, Vencida, Cancelada
    DateTime DataEmissao,
    DateTime DataVencimento,
    DateTime? DataPagamento,
    string MetodoPagamento,
    string PixQrCode,
    string PixCopiaECola,
    string UrlBoleto,
    string ComprovanteUrl
);

public record CancelarAssinaturaRequest(string Motivo);

public record PagarFaturaSimuladaRequest(string Metodo);

public record WebhookPagamentoPayload(
    string Evento, // fatura.paga, fatura.falha, fatura.vencida, assinatura.cancelada
    string TransacaoId,
    string FaturaNumero,
    decimal Valor,
    string Status,
    DateTime Timestamp
);

// =============================================================================
// FASE 9: Gestão de Múltiplas Filiais DTOs
// =============================================================================

public record FilialDto(
    Guid Id,
    string Codigo,
    string Nome,
    string Cnpj,
    string Endereco,
    string Cidade,
    string Uf,
    string Telefone,
    string Email,
    string Responsavel,
    bool Ativa,
    Guid? AgenteId,
    string? AgenteNome,
    string Token,
    bool TokenRevogado = false
);

public record SalvarFilialRequest(
    string Codigo,
    string Nome,
    string Cnpj,
    string? Endereco,
    string? Cidade,
    string? Uf,
    string? Telefone,
    string? Email,
    string? Responsavel,
    Guid? AgenteId
);

public record EstoqueFilialDto(
    Guid ProdutoId,
    string CodigoErp,
    string Descricao,
    string Unidade,
    decimal EstoqueAtual,
    decimal EstoqueMinimo,
    decimal EstoquePendente,
    decimal CustoAtual,
    decimal PrecoVenda,
    DateTime UltimaAtualizacao
);

public record AtualizarEstoqueFilialRequest(
    List<AtualizarEstoqueItemInput> Itens
);

public record AtualizarEstoqueItemInput(
    Guid ProdutoId,
    decimal EstoqueAtual,
    decimal? EstoqueMinimo,
    decimal? EstoquePendente
);

public record SolicitacaoTransferenciaDto(
    Guid Id,
    string Numero,
    Guid FilialOrigemId,
    string FilialOrigemNome,
    Guid FilialDestinoId,
    string FilialDestinoNome,
    string Status,
    string Observacoes,
    string SolicitadoPorNome,
    string AprovadoPorNome,
    DateTime DataCriacao,
    DateTime? DataAprovacao,
    DateTime? DataConclusao,
    List<SolicitacaoTransferenciaItemDto> Itens
);

public record SolicitacaoTransferenciaItemDto(
    Guid ProdutoId,
    string CodigoProduto,
    string DescricaoProduto,
    decimal QuantidadeSolicitada,
    decimal QuantidadeAtendida
);

public record CriarTransferenciaRequest(
    Guid FilialOrigemId,
    Guid FilialDestinoId,
    string? Observacoes,
    List<ItemTransferenciaInput> Itens
);

public record ItemTransferenciaInput(
    Guid ProdutoId,
    decimal Quantidade
);

public record AtualizarStatusTransferenciaRequest(
    string NovoStatus, // Aprovada, EmTransito, Concluida, Cancelada
    string? Observacoes
);

public record RelatorioConsolidadoFiliaisDto(
    int TotalFiliais,
    int TotalProdutos,
    decimal ValorEstoqueTotal,
    List<FilialResumoDto> Filiais
);

public record FilialResumoDto(
    Guid FilialId,
    string Nome,
    string Codigo,
    string Cidade,
    string Uf,
    int TotalItensEstoque,
    decimal EstoqueFisicoTotal,
    decimal ValorEstoqueEstimado,
    string StatusSincronizacao
);

