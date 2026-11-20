namespace Nexo.Api.Models;

public class Tenant : BaseEntity
{
    public string Slug { get; set; } = string.Empty;
    public string RazaoSocial { get; set; } = string.Empty;
    public string NomeFantasia { get; set; } = string.Empty;
    public string Cnpj { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Telefone { get; set; } = string.Empty;
    public string Status { get; set; } = "Ativo"; // Ativo, Bloqueado, Suspenso, Inativo, Pendente
    public Guid? PlanoId { get; set; }
    public Plano? Plano { get; set; }
    public DateTime? DataExpiracao { get; set; }

    // Precificação Dinâmica e Condições Comerciais (R$ 50,00 a R$ 250,00)
    public string ModuloContratado { get; set; } = "Basico"; // Basico, RelatoriosAvancados, CompletoServicos
    public string VolumeUtilizacao { get; set; } = "Baixo"; // Baixo, Medio, Alto
    public decimal MensalidadeCalculada { get; set; } = 50.00m;
    public decimal DescontoComercial { get; set; } = 0.00m;
    public decimal AjusteManual { get; set; } = 0.00m;
    public decimal MensalidadeFinal { get; set; } = 50.00m;
    public string JustificativaPreco { get; set; } = string.Empty;
    public DateTime? DataInicioCobranca { get; set; }
    public DateTime? ProximoVencimento { get; set; }
    public string SituacaoFinanceira { get; set; } = "Regular"; // Regular, Pendente, Inadimplente, Suspenso, Isento, Trial
    public int MaxUsuariosContratado { get; set; } = 0; // 0 = utiliza limite do Plano atual; > 0 = limite customizado pelo Admin Geral
    public int MaxFiliaisContratado { get; set; } = 0;  // 0 = utiliza limite do Plano atual; > 0 = limite customizado pelo Admin Geral
    public int MaxProdutosContratado { get; set; } = 10000;
    public int MaxSincronizadoresContratado { get; set; } = 3;

    // Dados Cadastrais e Fiscais da Empresa
    public string InscricaoEstadual { get; set; } = string.Empty;
    public string InscricaoMunicipal { get; set; } = string.Empty;
    public string Endereco { get; set; } = string.Empty;
    public string Numero { get; set; } = string.Empty;
    public string Complemento { get; set; } = string.Empty;
    public string Bairro { get; set; } = string.Empty;
    public string Cidade { get; set; } = string.Empty;
    public string Uf { get; set; } = string.Empty;
    public string Cep { get; set; } = string.Empty;

    public string LogoUrl { get; set; } = string.Empty;
    public string LogoStoragePath { get; set; } = string.Empty;
    public string LogoMimeType { get; set; } = string.Empty;
    public DateTime? LogoAtualizadaEm { get; set; }

    public ICollection<Usuario> Usuarios { get; set; } = new List<Usuario>();
    public ICollection<Filial> Filiais { get; set; } = new List<Filial>();
    public ICollection<Assinatura> Assinaturas { get; set; } = new List<Assinatura>();
    public ICollection<Agente> Agentes { get; set; } = new List<Agente>();
    public ICollection<HistoricoComercialTenant> HistoricosComerciais { get; set; } = new List<HistoricoComercialTenant>();
}

public class Plano : BaseEntity
{
    public string Nome { get; set; } = string.Empty;
    public decimal Mensalidade { get; set; }
    public decimal PrecoAnual { get; set; } // Valor anual com desconto (ex: 10x mensalidade)
    public int DiasTeste { get; set; } = 14;  // Período de teste gratuito
    public int MaxUsuarios { get; set; }
    public int MaxFiliais { get; set; }
    public int MaxFornecedores { get; set; }
    public int MaxCotacoesMes { get; set; }
    public string RecursosJson { get; set; } = "{}";
    public bool Ativo { get; set; } = true;
}

public class Assinatura : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Tenant? Tenant { get; set; }
    public Guid PlanoId { get; set; }
    public Plano? Plano { get; set; }
    public string Status { get; set; } = "Ativa"; // Teste, Ativa, Cancelada, Inadimplente, Suspensa
    public string Ciclo { get; set; } = "Mensal"; // Mensal, Anual
    public DateTime DataInicio { get; set; } = DateTime.UtcNow;
    public DateTime? DataFim { get; set; }
    public DateTime? DataRenovacao { get; set; }
    public DateTime? DataCancelamento { get; set; }
    public string MotivoCancelamento { get; set; } = string.Empty;
    public decimal Valor { get; set; }
    public string FormaPagamento { get; set; } = "Cartao"; // Cartao, Pix, Boleto
    public string Gateway { get; set; } = "Asaas";
    public string GatewayAssinaturaId { get; set; } = string.Empty;

    public ICollection<Fatura> Faturas { get; set; } = new List<Fatura>();
}

public class Fatura : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Tenant? Tenant { get; set; }
    public Guid? AssinaturaId { get; set; }
    public Assinatura? Assinatura { get; set; }
    public string Numero { get; set; } = string.Empty;
    public string Descricao { get; set; } = string.Empty;
    public decimal Valor { get; set; }
    public string Status { get; set; } = "Pendente"; // Pendente, Paga, Vencida, Cancelada
    public DateTime DataEmissao { get; set; } = DateTime.UtcNow;
    public DateTime DataVencimento { get; set; }
    public DateTime? DataPagamento { get; set; }
    public string MetodoPagamento { get; set; } = "Pix"; // Pix, Cartao, Boleto
    public string PixQrCode { get; set; } = string.Empty;
    public string PixCopiaECola { get; set; } = string.Empty;
    public string UrlBoleto { get; set; } = string.Empty;
    public string GatewayTransacaoId { get; set; } = string.Empty;
    public string ComprovanteUrl { get; set; } = string.Empty;
}

public class Usuario : BaseEntity
{
    public Guid? TenantId { get; set; } // nulo para PlatformAdmin
    public Tenant? Tenant { get; set; }
    public Guid? FilialId { get; set; } // Opcional: restringe o usuário a uma filial específica (null = matriz / todas)
    public Filial? Filial { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string SenhaHash { get; set; } = string.Empty;
    public string Perfil { get; set; } = "TenantAdmin"; // PlatformAdmin, TenantAdmin, Buyer, Viewer
    public bool Ativo { get; set; } = true;
    public DateTime? UltimoLogin { get; set; }
    public int FalhasLogin { get; set; }
    public DateTime? BloqueadoAte { get; set; }
}

/// <summary>Refresh token opaco (somente o hash SHA-256 é armazenado). FamilyId identifica a sessão; rotação a cada uso.</summary>
public class RefreshToken : BaseEntity
{
    public Guid UsuarioId { get; set; }
    public Guid FamilyId { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public DateTime ExpiraEm { get; set; }
    public DateTime? RevogadoEm { get; set; }
    public Guid? SubstituidoPorId { get; set; }
    public string Motivo { get; set; } = string.Empty;
    public string Ip { get; set; } = string.Empty;
}

public class Filial : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Tenant? Tenant { get; set; }
    public string Codigo { get; set; } = string.Empty; // Código interno (ex: FIL-01, MATRIZ)
    public string Nome { get; set; } = string.Empty;
    public string Cnpj { get; set; } = string.Empty;
    public string Endereco { get; set; } = string.Empty;
    public string Cidade { get; set; } = string.Empty;
    public string Uf { get; set; } = string.Empty;
    public string Telefone { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Responsavel { get; set; } = string.Empty;
    public bool Ativa { get; set; } = true;
    public string Token { get; set; } = string.Empty; // Token exclusivo da filial (Web ID Filial)
    public DateTime? TokenGeradoEm { get; set; }
    public bool TokenRevogado { get; set; }
    public Guid? AgenteId { get; set; } // Sincronizador dedicado da filial (banco Firebird local desta unidade)
    public Agente? Agente { get; set; }
}

public class EstoqueFilial : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid FilialId { get; set; }
    public Filial? Filial { get; set; }
    public Guid ProdutoId { get; set; }
    public Produto? Produto { get; set; }
    public decimal EstoqueAtual { get; set; }
    public decimal EstoqueMinimo { get; set; }
    public decimal EstoquePendente { get; set; }
    public DateTime UltimaAtualizacao { get; set; } = DateTime.UtcNow;
}

public class SolicitacaoTransferencia : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public string Numero { get; set; } = string.Empty;
    public Guid FilialOrigemId { get; set; }
    public Filial? FilialOrigem { get; set; }
    public Guid FilialDestinoId { get; set; }
    public Filial? FilialDestino { get; set; }
    public string Status { get; set; } = "Pendente"; // Pendente, Aprovada, EmTransito, Concluida, Cancelada
    public string Observacoes { get; set; } = string.Empty;
    public Guid? SolicitadoPorId { get; set; }
    public string SolicitadoPorNome { get; set; } = string.Empty;
    public Guid? AprovadoPorId { get; set; }
    public string AprovadoPorNome { get; set; } = string.Empty;
    public DateTime? DataAprovacao { get; set; }
    public DateTime? DataConclusao { get; set; }

    public ICollection<SolicitacaoTransferenciaItem> Itens { get; set; } = new List<SolicitacaoTransferenciaItem>();
}

public class SolicitacaoTransferenciaItem : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid SolicitacaoTransferenciaId { get; set; }
    public SolicitacaoTransferencia? SolicitacaoTransferencia { get; set; }
    public Guid ProdutoId { get; set; }
    public Produto? Produto { get; set; }
    public string CodigoProduto { get; set; } = string.Empty;
    public string DescricaoProduto { get; set; } = string.Empty;
    public decimal QuantidadeSolicitada { get; set; }
    public decimal QuantidadeAtendida { get; set; }
}

/// <summary>Parâmetros da empresa: política de estoque e tratamento tributário (configurável e validado).</summary>
public class ConfiguracaoTenant : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Tenant? Tenant { get; set; }
    public string Uf { get; set; } = string.Empty;
    public string RegimeTributario { get; set; } = "SimplesNacional"; // SimplesNacional, LucroPresumido, LucroReal
    public bool CreditaIcms { get; set; }
    public bool CreditaIpi { get; set; }
    public decimal PercCreditoPisCofins { get; set; } // só faz sentido em LucroReal (ex.: 9.25)
    public int LeadTimePadraoDias { get; set; } = 3;
    public int DiasSeguranca { get; set; } = 7;
    public int DiasCobertura { get; set; } = 15;
    public int ValidadeCotacaoDias { get; set; } = 7;
}

public class Agente : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Tenant? Tenant { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string ChaveHash { get; set; } = string.Empty;       // SHA-256 do segredo (alta entropia); o segredo nunca é gravado
    public string ChavePrefixo { get; set; } = string.Empty;    // primeiros caracteres, apenas para identificação visual
    public DateTime? ChaveRotacionadaEm { get; set; }
    public bool Revogado { get; set; }
    public DateTime? RevogadoEm { get; set; }
    public string? RevogadoPor { get; set; }
    public string Status { get; set; } = "Pendente"; // Pendente, Online, Offline, Erro, Revogado
    public string Versao { get; set; } = string.Empty;
    public string NomeMaquina { get; set; } = string.Empty;
    public string AmbienteJson { get; set; } = "{}";  // versão do Firebird, charset, dialeto, verificação de estrutura
    public DateTime? UltimoHeartbeat { get; set; }
    public DateTime? UltimaSincronizacao { get; set; }
    public int IntervaloEstoqueMin { get; set; } = 5;
    public int IntervaloProdutosMin { get; set; } = 30;
    public int IntervaloFornecedoresMin { get; set; } = 30;
    public int IntervaloHeartbeatSeg { get; set; } = 60;

    // Seção 7: Ativação, identificador exclusivo e configuração autorizada
    public string TipoBanco { get; set; } = "Firebird"; // Firebird, PostgreSQL
    public string BancoConfigJson { get; set; } = "{}"; // Parâmetros de conexão autorizados pelo Administrador Geral
    public Guid? FilialId { get; set; } // Filial vinculada
    public Filial? Filial { get; set; }
    public string CodigoAtivacaoHash { get; set; } = string.Empty; // SHA-256 do código temporário de ativação
    public DateTime? CodigoAtivacaoExpiracao { get; set; }
    public bool CodigoAtivacaoUsado { get; set; }
    public int TentativasAtivacaoInvalidas { get; set; }
    public DateTime? BloqueadoAte { get; set; }
}

public class Sincronizacao : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid AgenteId { get; set; }
    public Agente? Agente { get; set; }
    public string Tipo { get; set; } = string.Empty; // Produtos, Fornecedores, Estoque
    public int Recebidos { get; set; }
    public int Criados { get; set; }
    public int Atualizados { get; set; }
    public int Ignorados { get; set; }
    public string Status { get; set; } = "Sucesso";
    public string Mensagem { get; set; } = string.Empty;
}

public class SincronizacaoErro : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid AgenteId { get; set; }
    public string Origem { get; set; } = string.Empty;
    public string Mensagem { get; set; } = string.Empty;
    public string Detalhe { get; set; } = string.Empty;
}

public class LogAuditoria : BaseEntity
{
    public Guid? TenantId { get; set; }
    public Guid? UsuarioId { get; set; }
    public string Acao { get; set; } = string.Empty;
    public string Entidade { get; set; } = string.Empty;
    public string EntidadeId { get; set; } = string.Empty;
    public string DadosJson { get; set; } = "{}";
    public string Ip { get; set; } = string.Empty;
}

/// <summary>Tabela versionada de precificação dinâmica SaaS (R$ 50,00 a R$ 250,00).</summary>
public class ConfiguracaoPrecificacao : BaseEntity
{
    public int Versao { get; set; } = 1;
    public bool Ativa { get; set; } = true;
    public decimal MensalidadeBase { get; set; } = 50.00m;
    public decimal ValorMinimo { get; set; } = 50.00m;
    public decimal ValorMaximo { get; set; } = 250.00m;

    // Critério 1: Quantidade de produtos
    public decimal AdicProdFaixa1 { get; set; } = 0.00m;   // Até 1.000
    public decimal AdicProdFaixa2 { get; set; } = 15.00m;  // 1.001 a 3.000
    public decimal AdicProdFaixa3 { get; set; } = 30.00m;  // 3.001 a 8.000
    public decimal AdicProdFaixa4 { get; set; } = 45.00m;  // Acima de 8.000

    // Critério 2: Quantidade de filiais
    public decimal AdicFilialFaixa1 { get; set; } = 0.00m;  // 1 filial
    public decimal AdicFilialFaixa2 { get; set; } = 20.00m; // 2 a 3
    public decimal AdicFilialFaixa3 { get; set; } = 40.00m; // 4 a 6
    public decimal AdicFilialFaixa4 { get; set; } = 60.00m; // Acima de 6

    // Critério 3: Quantidade de usuários
    public decimal AdicUsuarioFaixa1 { get; set; } = 0.00m;  // 1 a 2
    public decimal AdicUsuarioFaixa2 { get; set; } = 10.00m; // 3 a 5
    public decimal AdicUsuarioFaixa3 { get; set; } = 20.00m; // 6 a 10
    public decimal AdicUsuarioFaixa4 { get; set; } = 30.00m; // Acima de 10

    // Critério 4: Sincronizadores SINc
    public decimal AdicSincFaixa1 { get; set; } = 0.00m;  // 1
    public decimal AdicSincFaixa2 { get; set; } = 10.00m; // 2 a 3
    public decimal AdicSincFaixa3 { get; set; } = 20.00m; // 4 ou mais

    // Critério 5: Volume de utilização
    public decimal AdicVolumeBaixo { get; set; } = 0.00m;
    public decimal AdicVolumeMedio { get; set; } = 10.00m;
    public decimal AdicVolumeAlto { get; set; } = 25.00m;

    // Critério 6: Módulos contratados
    public decimal AdicModuloBasico { get; set; } = 0.00m;      // Compras e cotações básicas
    public decimal AdicModuloRelatorios { get; set; } = 15.00m; // Compras e relatórios avançados
    public decimal AdicModuloCompleto { get; set; } = 30.00m;   // Compras, relatórios e serviços

    public string RegrasJson { get; set; } = "{}";
    public string AtualizadoPor { get; set; } = "Sistema";
    public string MotivoAlteracao { get; set; } = "Configuração inicial";
}

/// <summary>Histórico de alterações comerciais, precificação, descontos e ajustes manuais de cada empresa.</summary>
public class HistoricoComercialTenant : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Tenant? Tenant { get; set; }
    public string TipoAlteracao { get; set; } = "Precificacao"; // Precificacao, Slug, Status, SolicitacaoCliente, Pagamento
    public decimal MensalidadeAnterior { get; set; }
    public decimal MensalidadeCalculada { get; set; }
    public decimal DescontoAplicado { get; set; }
    public decimal AjusteManual { get; set; }
    public decimal MensalidadeFinal { get; set; }
    public string ModuloContratado { get; set; } = "Basico";
    public string VolumeUtilizacao { get; set; } = "Baixo";
    public string Justificativa { get; set; } = string.Empty;
    public string AlteradoPor { get; set; } = string.Empty;
    public bool NotificadoCliente { get; set; } = true;
    public int VersaoTabelaPrecos { get; set; } = 1;
    public string SnapshotConsumoJson { get; set; } = "{}";
}

/// <summary>Histórico de transações e eventos de webhooks de cobrança recorrente (desacoplado de gateway).</summary>
public class TransacaoPagamento : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Tenant? Tenant { get; set; }
    public Guid? FaturaId { get; set; }
    public Fatura? Fatura { get; set; }
    public Guid? AssinaturaId { get; set; }
    public Assinatura? Assinatura { get; set; }
    public string Gateway { get; set; } = "Configuravel";
    public string GatewayTransacaoId { get; set; } = string.Empty;
    public string Evento { get; set; } = string.Empty; // fatura.gerada, fatura.paga, fatura.vencida, fatura.falha, assinatura.suspensa, assinatura.reativada
    public decimal Valor { get; set; }
    public string Status { get; set; } = "Pendente";
    public string MetodoPagamento { get; set; } = string.Empty;
    public string ComprovanteUrl { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = "{}";
    public DateTime? ConfirmadoEm { get; set; }
}

/// <summary>Solicitações de alteração de plano/módulos enviadas pelo cliente para aprovação do Admin Geral.</summary>
public class SolicitacaoAlteracaoPlano : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Tenant? Tenant { get; set; }
    public Guid? SolicitadoPorUsuarioId { get; set; }
    public string SolicitadoPorNome { get; set; } = string.Empty;
    public string ModuloAtual { get; set; } = "Basico";
    public string ModuloSolicitado { get; set; } = "CompletoServicos";
    public string ObservacoesCliente { get; set; } = string.Empty;
    public string Status { get; set; } = "Pendente"; // Pendente, Aprovada, Recusada
    public string RespostaAdmin { get; set; } = string.Empty;
    public string AnalisadoPor { get; set; } = string.Empty;
    public DateTime? AnalisadoEm { get; set; }
}
