using System.Text.Json;

namespace Nexo.Api.DTOs;

public record LoginRequest(string Email, string Senha, string? Slug = null);
public record LoginResponse(string Token, UserDto Usuario, TenantSummaryDto? Tenant, string? RefreshToken = null, int ExpiresInSeconds = 0);
public record RefreshRequest(string? RefreshToken);
public record UserDto(Guid Id, string Nome, string Email, string Perfil, Guid? TenantId);
public record TenantSummaryDto(Guid Id, string RazaoSocial, string NomeFantasia, string Cnpj, string Status, string? PlanoNome, string? LogoUrl = null, string? Slug = null, decimal MensalidadeFinal = 50m, string? ModuloContratado = "Basico", string? SituacaoFinanceira = "Regular");

public record RegisterTenantRequest(string RazaoSocial, string NomeFantasia, string Cnpj, string Email, string Telefone,
    string AdminNome, string AdminEmail, string AdminSenha, Guid? PlanoId, string? Uf = null, string? Slug = null);

public record UpdateTenantStatusRequest(string Status, string? Motivo = null);
public record UpdateTenantSlugRequest(string Slug, string? Justificativa = null);
public record CreatePlanRequest(string Nome, decimal Mensalidade, int MaxUsuarios, int MaxFiliais, int MaxFornecedores, int MaxCotacoesMes, string RecursosJson);
public record AdminDashboardDto(int TotalTenants, int TenantsAtivos, int TenantsBloqueados, int TotalUsuarios, decimal MrrEstimado, List<TenantSummaryDto> UltimosTenants);

// ---------- Cadastro Completo de Empresa (Wizard do Admin Geral) ----------
public record CadastroCompletoEmpresaRequest(
    string RazaoSocial,
    string NomeFantasia,
    string Cnpj,
    string Email,
    string Telefone,
    Guid? PlanoId,
    string NomeFilial,
    string CodigoFilial,
    string? CnpjFilial = null,
    string? TipoBanco = "Firebird",
    string? HostBanco = "127.0.0.1",
    int PortaBanco = 3050,
    string? CaminhoBanco = @"C:\DATACASH\DADOS\BM.FDB",
    string? UsuarioBanco = "SYSDBA",
    string? SenhaBanco = "",
    string? CharsetBanco = "NONE",
    string ResponsavelNome = "",
    string EmailLogin = "",
    string? SenhaInicial = null,
    string? PerfilAcesso = "admin",
    bool GerarCodigoAtivacao = true,
    string? Slug = null,
    string? ModuloContratado = "Basico",
    decimal? DescontoComercial = 0m,
    decimal? AjusteManual = 0m,
    string? JustificativaPreco = null
);

public record CadastroCompletoEmpresaResponse(
    Guid TenantId,
    string RazaoSocial,
    string NomeFantasia,
    string Cnpj,
    string Status,
    Guid FilialId,
    string NomeFilial,
    string CodigoFilial,
    string TokenFilial,
    Guid UsuarioId,
    string UsuarioNome,
    string UsuarioEmail,
    string SenhaInicial,
    Guid? AgenteId,
    string? AgenteNome,
    string? CodigoAtivacao,
    DateTime? CodigoAtivacaoExpiraEm,
    string? Slug = null,
    decimal MensalidadeFinal = 50m
);

public record TestarConexaoErpRequest(
    string TipoBanco,
    string Host,
    int Porta,
    string? CaminhoOuBanco,
    string? Usuario,
    string? Senha,
    string? Charset
);

public record FalhaSincronizacaoRecenteDto(
    Guid Id,
    string EmpresaNome,
    string AgenteNome,
    string Mensagem,
    DateTime Data
);

public record CreateUserRequest(string Nome, string Email, string Senha, string? Perfil);
public record CreateBranchRequest(string? Codigo, string Nome, string? Cnpj, string? Cidade, string? Uf);

public record ConfiguracaoDto(string Uf, string RegimeTributario, bool CreditaIcms, bool CreditaIpi, decimal PercCreditoPisCofins,
    int LeadTimePadraoDias, int DiasSeguranca, int DiasCobertura, int ValidadeCotacaoDias);

// ---------- Produtos / Fornecedores ----------
public record ProdutoDto(Guid Id, string CodigoErp, string Descricao, string CodigoBarras, string Unidade, string Grupo, string Categoria,
    decimal PrecoVenda, decimal CustoAtual, decimal UltimoCusto, DateTime? UltimaCompraEm, decimal? EstoqueAtual, decimal EstoquePendente,
    decimal EstoqueMinimo, decimal Consumo90Dias, Guid? FornecedorVinculadoId, string? FornecedorVinculadoNome, string Origem,
    DateTime? UltimaSincronizacaoEm, DateTime DataAtualizacao, string NivelEstoque, decimal? CoberturaDias, decimal SugestaoCompra, decimal? MargemPerc, decimal? EstoqueCritico = null);

public record CreateProdutoRequest(string? CodigoErp, string Descricao, string? CodigoBarras, string? Unidade, string? Grupo, string? Categoria,
    decimal PrecoVenda, decimal CustoAtual, decimal? EstoqueAtual, decimal EstoqueMinimo, Guid? FornecedorVinculadoId, decimal? EstoqueCritico = null);
public record EstoqueMinimoRequest(decimal EstoqueMinimo);
public record EstoqueCriticoRequest(decimal? EstoqueCritico);

public record FornecedorDto(Guid Id, string CodigoErp, string RazaoSocial, string NomeFantasia, string Cnpj, string InscricaoEstadual,
    string Email, string Telefone, string Whatsapp, string Contato, string Endereco, string Cidade, string Uf, int PrazoEntregaPadrao,
    string CondicoesPagamento, string Observacoes, string Origem, bool Ativo);

public record SaveFornecedorRequest(string? RazaoSocial, string? NomeFantasia, string? Cnpj, string? InscricaoEstadual, string? Email,
    string? Telefone, string? Whatsapp, string? Contato, string? Endereco, string? Cidade, string? Uf, int? PrazoEntregaPadrao,
    string? CondicoesPagamento, string? Observacoes);

// ---------- Integração / agente ----------
public record AgenteDto(Guid Id, string Nome, string ChavePrefixo, string Status, string Versao, string NomeMaquina, DateTime? UltimoHeartbeat,
    DateTime? UltimaSincronizacao, bool Revogado, DateTime DataCriacao, DateTime? ChaveRotacionadaEm, JsonElement? Ambiente,
    int IntervaloEstoqueMin, int IntervaloProdutosMin, int IntervaloFornecedoresMin, int IntervaloHeartbeatSeg);
public record GenerateAgentKeyRequest(string? Nome);
public record AgentKeyResponse(Guid AgenteId, string ChaveAtivacao, string Aviso);
public record UpdateAgentIntervalsRequest(int IntervaloEstoqueMin, int IntervaloProdutosMin, int IntervaloFornecedoresMin, int IntervaloHeartbeatSeg);
public record SincronizacaoDto(Guid Id, Guid AgenteId, string AgenteNome, string Tipo, int Recebidos, int Criados, int Atualizados, int Ignorados, string Status, string Mensagem, DateTime DataCriacao);
public record SincronizacaoErroDto(Guid Id, Guid AgenteId, string Origem, string Mensagem, string Detalhe, DateTime DataCriacao);

public record AgentHeartbeatRequest(string? Versao, string? NomeMaquina, JsonElement? Ambiente);
public record AgentConfigDto(int IntervaloEstoqueMin, int IntervaloProdutosMin, int IntervaloFornecedoresMin, int IntervaloHeartbeatSeg);
public record ProdutoSync(string CodigoErp, string Descricao, string? CodigoBarras, string? Unidade, string? Grupo, string? Categoria, string? Ncm,
    decimal PrecoVenda, decimal CustoAtual, string? FornecedorErpId, decimal Consumo90Dias, decimal UltimoCusto, DateTime? UltimaCompraEm,
    decimal QuantidadeUltimaCompra, string? UltimoFornecedorErpId);
public record EstoqueSync(string CodigoErp, decimal? EstoqueAtual, decimal EstoquePendente);
public record FornecedorSync(string CodigoErp, string RazaoSocial, string? NomeFantasia, string? Cnpj, string? InscricaoEstadual, string? Email,
    string? Telefone, string? Whatsapp, string? Contato, string? Endereco, string? Cidade, string? Uf, int PrazoEntregaDias);
public record SyncBatch<T>(List<T> Itens, Guid? TenantId = null, Guid? BranchId = null);
public record SyncResult(int Recebidos, int Criados, int Atualizados, int Ignorados);

// ---------- Seção 7: Ativação e Gestão de Agentes ----------
public record CriarAgenteAdminRequest(
    string Nome,
    string TipoBanco,
    string? Host,
    int? Porta,
    string? CaminhoOuBanco,
    string? UsuarioBanco,
    string? SenhaBanco,
    string? Charset,
    int? Dialeto,
    int? IntervaloEstoqueMin,
    int? IntervaloProdutosMin,
    int? IntervaloFornecedoresMin,
    int? IntervaloHeartbeatSeg
);

public record CriarAgenteAdminResponse(
    Guid AgentId,
    Guid TenantId,
    string Nome,
    string CodigoAtivacao,
    DateTime ExpiraEm,
    string TipoBanco
);

public record AtivarAgenteRequest(
    string CodigoAtivacao,
    string? CnpjBase = null,
    string? NomeMaquina = null,
    object? Ambiente = null
);

public record EmpresaAtivadaDto(
    string RazaoSocial,
    string NomeFantasia,
    string Cnpj,
    string? FilialId = null,
    string? FilialNome = null,
    string? FilialCodigo = null
);

public record BancoConfigDto(
    string Tipo,
    string Host,
    int Porta,
    string CaminhoOuBanco,
    string Usuario,
    string Senha,
    string Charset,
    int Dialeto
);

public record IntervalosConfigDto(
    int EstoqueMin,
    int ProdutosMin,
    int FornecedoresMin,
    int HeartbeatSeg
);

public record AtivarAgenteResponse(
    Guid AgentId,
    Guid TenantId,
    string AgentCredential,
    EmpresaAtivadaDto Empresa,
    BancoConfigDto BancoConfig,
    IntervalosConfigDto Intervalos
);

public record RotacionarCredencialResponse(
    Guid AgentId,
    string NovaChave,
    string Prefixo,
    DateTime RotacionadoEm
);
public record AgentErroRequest(string? Origem, string? Mensagem, string? Detalhe);

// ---------- Cotações ----------
public record CotacaoItemRequest(Guid ProdutoId, decimal Quantidade);
public record CriarCotacaoRequest(string? Titulo, string? Observacoes, int? PrazoRespostaDias, List<CotacaoItemRequest> Itens, List<Guid> FornecedorIds, Guid? FilialId = null, string? TipoCompra = null);
public record AcessoCriadoDto(Guid CotacaoFornecedorId, Guid FornecedorId, string Fornecedor, string Link, string Codigo, DateTime ExpiraEm);
public record CotacaoCriadaDto(Guid Id, string Numero, List<AcessoCriadoDto> Acessos);
public record CotacaoResumoDto(Guid Id, string Numero, string Titulo, string Status, DateTime DataCriacao, DateTime DataLimiteResposta, int Itens,
    int Fornecedores, int Respondidas, int Visualizadas, decimal TotalAprovado, decimal EconomiaRealizada);
public record AcessoInfoDto(string? TokenParcial, DateTime ExpiraEm, bool Revogado, bool Bloqueado, int Tentativas, int Visualizacoes, DateTime? PrimeiraVisualizacaoEm, DateTime? UltimaVisualizacaoEm);
public record CotacaoFornecedorDto(Guid Id, Guid FornecedorId, string Nome, string Email, string Whatsapp, string Status, AcessoInfoDto? Acesso, DateTime? EnviadaEm, bool Respondida);
public record CotacaoItemDto(Guid Id, Guid ProdutoId, string Codigo, string Descricao, string Unidade, decimal Quantidade, decimal EstoqueNoMomento, decimal UltimoCustoRef, decimal PrecoVendaRef);
public record CotacaoDetalheDto(Guid Id, string Numero, string Titulo, string Observacoes, string Status, DateTime DataCriacao, DateTime DataLimiteResposta,
    string? AprovadaPorNome, DateTime? AprovadaEm, string ObservacaoAprovacao, decimal TotalAprovado, decimal EconomiaPotencial, decimal EconomiaRealizada,
    List<CotacaoItemDto> Itens, List<CotacaoFornecedorDto> Fornecedores);
public record AprovarCotacaoRequest(Dictionary<Guid, Guid> Escolhas, string? Observacao);
public record NovoCodigoDto(Guid CotacaoFornecedorId, string Link, string Codigo, DateTime ExpiraEm);

// ---------- Portal do fornecedor ----------
public record AcessarPortalRequest(string? Codigo);
public record AcessarPortalResponse(string SessaoToken, DateTime ExpiraEm);
public record PortalItemDto(Guid Id, string Codigo, string Descricao, string Unidade, decimal Quantidade);
public record RespostaItemDto(Guid CotacaoItemId, bool Disponivel, decimal QuantidadeOfertada, decimal PrecoUnitario, decimal DescontoPerc, string? Cst,
    string? Cfop, decimal IcmsAliq, decimal IpiAliq, decimal Mva, decimal IcmsStAliq, decimal IcmsStValor, string? Observacao);
public record RespostaDto(string Status, decimal Frete, decimal OutrasDespesas, int PrazoEntregaDias, string CondicaoPagamento, DateOnly? ValidadeProposta,
    string ContatoNome, string Observacoes, DateTime? EnviadaEm, List<RespostaItemDto> Itens);
public record AnexoDto(Guid Id, string NomeArquivo, string ContentType, long Tamanho, DateTime DataCriacao);
public record PortalCotacaoDto(string Empresa, string Numero, string Titulo, string Observacoes, DateTime DataLimiteResposta, string FornecedorNome,
    string Status, bool Editavel, string? Mensagem, List<PortalItemDto> Itens, RespostaDto? Resposta, List<AnexoDto> Anexos, List<string> Avisos);
public record SalvarRespostaRequest(decimal Frete, decimal OutrasDespesas, int PrazoEntregaDias, string? CondicaoPagamento, DateOnly? ValidadeProposta,
    string? ContatoNome, string? Observacoes, List<RespostaItemDto> Itens);
public record ValidacaoDto(bool Valido, List<string> Erros, List<string> Avisos);
