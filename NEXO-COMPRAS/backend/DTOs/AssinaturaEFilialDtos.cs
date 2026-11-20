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
    LimitesUsoDto Limites,
    string? Slug = null,
    string? FaixaComercial = "Essencial",
    string? ModuloContratado = "Basico",
    string? ModuloDescricao = "Compras e cotações básicas",
    string? VolumeUtilizacao = "Baixo",
    decimal MensalidadeCalculada = 50.00m,
    decimal DescontoComercial = 0.00m,
    decimal AjusteManual = 0.00m,
    decimal MensalidadeFinal = 50.00m,
    string? SituacaoFinanceira = "Regular",
    DateTime? ProximoVencimento = null,
    SimulacaoPrecificacaoResponse? DetalhePrecificacao = null
);

public record LimitesUsoDto(
    int UsuariosUsados,
    int MaxUsuarios,
    int FiliaisUsadas,
    int MaxFiliais,
    int CotacoesMesUsadas,
    int MaxCotacoesMes,
    int ProdutosUsados = 0,
    int MaxProdutos = 10000,
    int SincronizadoresUsados = 1,
    int MaxSincronizadores = 3
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

// =============================================================================
// FASE SAAS MULTI-TENANT: Precificação Dinâmica (R$ 50,00 a R$ 250,00) & Slug
// =============================================================================

public record SimulacaoPrecificacaoRequest(
    int QuantidadeProdutos,
    int QuantidadeFiliais,
    int QuantidadeUsuarios,
    int QuantidadeSincronizadores,
    string? VolumeUtilizacao = "Baixo",
    string? ModuloContratado = "Basico",
    decimal Desconto = 0.00m,
    decimal AjusteManual = 0.00m
);

public record SimulacaoPrecificacaoResponse(
    int VersaoTabela,
    decimal ValorBase,
    decimal ValorMinimo,
    decimal ValorMaximo,
    decimal AdicionalProdutos,
    string FaixaProdutos,
    decimal AdicionalFiliais,
    string FaixaFiliais,
    decimal AdicionalUsuarios,
    string FaixaUsuarios,
    decimal AdicionalSincronizadores,
    string FaixaSincronizadores,
    decimal AdicionalVolume,
    string FaixaVolume,
    decimal AdicionalModulos,
    string FaixaModulos,
    string ModuloCodigo,
    decimal SomaAdicionais,
    decimal SubtotalBruto,
    decimal MensalidadeCalculada,
    decimal Desconto,
    decimal AjusteManual,
    decimal MensalidadeFinal,
    string FaixaComercial
);

public record PrecificacaoTenantDetalheDto(
    Guid TenantId,
    string RazaoSocial,
    string NomeFantasia,
    string Slug,
    string Cnpj,
    string Status,
    int ProdutosCadastrados,
    int FiliaisAtivas,
    int UsuariosAtivos,
    int SincronizadoresAtivos,
    int CotacoesNoMes,
    int PedidosNoMes,
    int SincronizacoesEfetivasNoMes,
    int SincronizacoesSemAlteracaoIgnoradas,
    int RegistrosProcessadosNoMes,
    string VolumeUtilizacao,
    string VolumeSugerido,
    string ModuloContratado,
    string ModuloDescricao,
    decimal MensalidadeCalculada,
    decimal Desconto,
    decimal AjusteManual,
    decimal MensalidadeFinal,
    decimal MensalidadeSimuladaAtual,
    string FaixaComercial,
    string JustificativaPreco,
    DateTime DataInicioCobranca,
    DateTime ProximoVencimento,
    string SituacaoFinanceira,
    int MaxUsuariosContratado,
    int MaxFiliaisContratado,
    int MaxProdutosContratado,
    int MaxSincronizadoresContratado,
    SimulacaoPrecificacaoResponse DetalheCalculo
);

public record AplicarPrecificacaoTenantRequest(
    string? ModuloContratado,
    string? VolumeUtilizacao,
    decimal Desconto,
    decimal AjusteManual,
    string? Justificativa,
    DateTime? DataInicioCobranca = null,
    DateTime? ProximoVencimento = null,
    string? SituacaoFinanceira = null,
    int? MaxUsuariosContratado = null,
    int? MaxFiliaisContratado = null,
    int? MaxProdutosContratado = null,
    int? MaxSincronizadoresContratado = null,
    decimal? MensalidadeManual = null
);

public record EmpresaPendenteSincronizacaoDto(
    Guid TenantId,
    string NomeEmpresa,
    string Cnpj,
    string? FilialNome,
    string? FilialCodigo,
    string StatusSincronizacao,
    DateTime? UltimaComunicacao,
    string MotivoPendencia
);

public record AtualizarConfiguracaoPrecificacaoRequest(
    decimal MensalidadeBase,
    decimal ValorMinimo,
    decimal ValorMaximo,
    decimal AdicProdFaixa1,
    decimal AdicProdFaixa2,
    decimal AdicProdFaixa3,
    decimal AdicProdFaixa4,
    decimal AdicFilialFaixa1,
    decimal AdicFilialFaixa2,
    decimal AdicFilialFaixa3,
    decimal AdicFilialFaixa4,
    decimal AdicUsuarioFaixa1,
    decimal AdicUsuarioFaixa2,
    decimal AdicUsuarioFaixa3,
    decimal AdicUsuarioFaixa4,
    decimal AdicSincFaixa1,
    decimal AdicSincFaixa2,
    decimal AdicSincFaixa3,
    decimal AdicVolumeBaixo,
    decimal AdicVolumeMedio,
    decimal AdicVolumeAlto,
    decimal AdicModuloBasico,
    decimal AdicModuloRelatorios,
    decimal AdicModuloCompleto,
    string? MotivoAlteracao = null
);

public record HistoricoComercialDto(
    Guid Id,
    Guid TenantId,
    string TipoAlteracao,
    decimal MensalidadeAnterior,
    decimal MensalidadeCalculada,
    decimal DescontoAplicado,
    decimal AjusteManual,
    decimal MensalidadeFinal,
    string ModuloContratado,
    string VolumeUtilizacao,
    string Justificativa,
    string AlteradoPor,
    bool NotificadoCliente,
    int VersaoTabelaPrecos,
    DateTime DataCriacao
);

public record SolicitarAlteracaoPlanoRequest(
    string ModuloSolicitado,
    string? ObservacoesCliente
);

public record ResponderSolicitacaoPlanoRequest(
    bool Aprovar,
    string? RespostaAdmin
);


