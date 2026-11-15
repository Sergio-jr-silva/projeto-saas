using System.Text.Json.Serialization;

namespace Nexo.Api.Models;

/// <summary>
/// Cotação de Serviço: gestão completa de escopo, etapas, requisitos técnicos e contratação de serviços terceirizados.
/// Totalmente independente de produtos/estoque.
/// </summary>
public class CotacaoServico : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public string Numero { get; set; } = string.Empty; // ex.: SRV-2026-0001
    public string Titulo { get; set; } = string.Empty;
    public string Categoria { get; set; } = "Geral"; // ManutencaoPredial, TI, Facilities, Obras, Consultoria, Juridico, Logistica, Outros
    public string Prioridade { get; set; } = "Media"; // Baixa, Media, Alta, Urgente
    public Guid? FilialId { get; set; }
    public Filial? Filial { get; set; }

    // Responsável
    public Guid? ResponsavelId { get; set; }
    public string ResponsavelNome { get; set; } = string.Empty;
    public string ResponsavelEmail { get; set; } = string.Empty;

    // Prazos e Datas
    public DateTime DataAbertura { get; set; } = DateTime.UtcNow;
    public DateTime DataLimiteResposta { get; set; }
    public DateTime? DataDesejadaInicio { get; set; }
    public int PrazoEstimadoDias { get; set; } = 30;

    // Escopo e Justificativa
    public string NecessidadeMotivacao { get; set; } = string.Empty;
    public string DescricaoDetalhada { get; set; } = string.Empty;
    public string ObjetivoEsperado { get; set; } = string.Empty;
    public string ResultadoEsperado { get; set; } = string.Empty;

    // Local de Execução
    public bool AtendimentoPresencial { get; set; } = true;
    public string Cep { get; set; } = string.Empty;
    public string Endereco { get; set; } = string.Empty;
    public string NumeroLocal { get; set; } = string.Empty;
    public string Complemento { get; set; } = string.Empty;
    public string Bairro { get; set; } = string.Empty;
    public string Cidade { get; set; } = string.Empty;
    public string Uf { get; set; } = string.Empty;
    public string PontoReferencia { get; set; } = string.Empty;

    // Requisitos e Condições Técnicas
    public string HorarioPermitido { get; set; } = "Horário comercial padrão";
    public string MateriaisInclusosExigidos { get; set; } = string.Empty;
    public string EquipamentosExigidos { get; set; } = string.Empty;
    public int GarantiaEsperadaMeses { get; set; } = 12;
    public string CondicoesEspeciais { get; set; } = string.Empty;
    public string CertificacoesExigidas { get; set; } = string.Empty;

    // Pesos para Comparação Técnica / Comercial (Soma = 100)
    public decimal PesoPreco { get; set; } = 50;
    public decimal PesoPrazo { get; set; } = 20;
    public decimal PesoTecnico { get; set; } = 20;
    public decimal PesoCondicoes { get; set; } = 10;

    // Status do Ciclo de Vida:
    // Rascunho, AguardandoPropostas, EmAnalise, AguardandoAprovacao, Aprovada, Contratada, EmExecucao, Concluida, Cancelada
    public string Status { get; set; } = "Rascunho";

    // Contratação / Resultado
    public Guid? PropostaVencedoraId { get; set; }
    public Guid? FornecedorVencedorId { get; set; }
    public Fornecedor? FornecedorVencedor { get; set; }
    public decimal TotalEstimado { get; set; }
    public decimal TotalContratado { get; set; }
    public decimal EconomiaRealizada { get; set; }
    public string JustificativaEscolha { get; set; } = string.Empty;
    public Guid? AprovadoPorId { get; set; }
    public string AprovadoPorNome { get; set; } = string.Empty;
    public DateTime? AprovadoEm { get; set; }
    public string ObservacaoAprovacao { get; set; } = string.Empty;

    public string TimelineJson { get; set; } = "[]";

    // Coleções
    public ICollection<AtividadeServico> Atividades { get; set; } = new List<AtividadeServico>();
    public ICollection<FornecedorServico> Fornecedores { get; set; } = new List<FornecedorServico>();
    public ICollection<AnexoServico> Anexos { get; set; } = new List<AnexoServico>();
}

/// <summary>
/// Etapa ou atividade que compõe o escopo da cotação de serviços.
/// </summary>
public class AtividadeServico : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid CotacaoServicoId { get; set; }
    [JsonIgnore]
    public CotacaoServico? CotacaoServico { get; set; }

    public int Ordem { get; set; } = 1;
    public string Etapa { get; set; } = string.Empty; // ex.: Fase 1 - Infraestrutura Inicial
    public string Nome { get; set; } = string.Empty;
    public string Descricao { get; set; } = string.Empty;
    public decimal Quantidade { get; set; } = 1;
    public string UnidadeMedicao { get; set; } = "SV"; // SV (Serviço), Horas, Diárias, Pontos, m², etc.
    public string LocalExecucao { get; set; } = string.Empty;
    public string RequisitosTecnicos { get; set; } = string.Empty;
    public string ResultadoEsperado { get; set; } = string.Empty;
    public decimal ValorEstimadoUnitario { get; set; }
}

/// <summary>
/// Convite de fornecedor para responder à cotação de serviços.
/// </summary>
public class FornecedorServico : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid CotacaoServicoId { get; set; }
    [JsonIgnore]
    public CotacaoServico? CotacaoServico { get; set; }

    public Guid FornecedorId { get; set; }
    public Fornecedor? Fornecedor { get; set; }

    // Status: Convidado, Visualizado, PropostaEnviada, Recusado, Selecionado, NaoSelecionado, Cancelado
    public string Status { get; set; } = "Convidado";
    public string MotivoRecusa { get; set; } = string.Empty;
    public DateTime? RecusadoEm { get; set; }

    // Credencial pública de acesso
    public AcessoFornecedorServico? Acesso { get; set; }
    public PropostaServico? Proposta { get; set; }
}

/// <summary>
/// Token de segurança e código hash para fornecedor responder à cotação de serviço.
/// </summary>
public class AcessoFornecedorServico : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid FornecedorServicoId { get; set; }
    [JsonIgnore]
    public FornecedorServico? FornecedorServico { get; set; }

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

/// <summary>
/// Proposta técnica e comercial enviada pelo fornecedor de serviço.
/// Totalmente isolada dos demais fornecedores concorrentes.
/// </summary>
public class PropostaServico : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid FornecedorServicoId { get; set; }
    [JsonIgnore]
    public FornecedorServico? FornecedorServico { get; set; }

    public string Status { get; set; } = "Rascunho"; // Rascunho, Enviada, EmAnalise, Vencedora, NaoSelecionada

    // Valores Comerciais
    public decimal ValorMaoDeObra { get; set; }
    public decimal ValorMateriaisInclusos { get; set; }
    public decimal ValorEquipamentos { get; set; }
    public decimal Deslocamento { get; set; }
    public decimal OutrasDespesas { get; set; }
    public decimal Desconto { get; set; }
    public decimal ImpostosTotal { get; set; }
    public decimal ValorTotal { get; set; }

    // Prazos e Condições
    public int PrazoInicioDias { get; set; } = 3;
    public int PrazoExecucaoDias { get; set; } = 15;
    public string CondicoesPagamento { get; set; } = "30 dias";
    public DateOnly? ValidadeProposta { get; set; }
    public int GarantiaMeses { get; set; } = 12;

    // Especificações Técnicas
    public string DescricaoSolucao { get; set; } = string.Empty;
    public string Metodologia { get; set; } = string.Empty;
    public string EquipeAlocada { get; set; } = string.Empty;
    public string EquipamentosFerramental { get; set; } = string.Empty;
    public string MateriaisInclusosTexto { get; set; } = string.Empty;
    public string MateriaisNaoInclusosTexto { get; set; } = string.Empty;
    public string CertificacoesEquipe { get; set; } = string.Empty;
    public string LimitacoesExclusoes { get; set; } = string.Empty;
    public string Observacoes { get; set; } = string.Empty;
    public string ContatoNome { get; set; } = string.Empty;
    public string ContatoTelefone { get; set; } = string.Empty;
    public string ContatoEmail { get; set; } = string.Empty;
    public DateTime? EnviadaEm { get; set; }

    // Notas e Pontuação na Avaliação do Comprador
    public decimal NotaComercial { get; set; } // 0 a 100
    public decimal NotaTecnica { get; set; }    // 0 a 100
    public decimal NotaPrazo { get; set; }      // 0 a 100
    public decimal NotaCondicoes { get; set; }  // 0 a 100
    public decimal PontuacaoPonderada { get; set; } // Calculada com base nos pesos da cotação
    public string ParecerAvaliacao { get; set; } = string.Empty;

    public ICollection<PropostaServicoEtapa> Etapas { get; set; } = new List<PropostaServicoEtapa>();
    public ICollection<AnexoServico> Anexos { get; set; } = new List<AnexoServico>();
}

/// <summary>
/// Preço e observações de cada atividade/etapa específica da proposta.
/// </summary>
public class PropostaServicoEtapa : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid PropostaServicoId { get; set; }
    [JsonIgnore]
    public PropostaServico? PropostaServico { get; set; }

    public Guid AtividadeServicoId { get; set; }
    public AtividadeServico? AtividadeServico { get; set; }

    public decimal ValorUnitario { get; set; }
    public decimal ValorTotal { get; set; }
    public string DetalhesExecucao { get; set; } = string.Empty;
    public string Observacao { get; set; } = string.Empty;
}

/// <summary>
/// Anexos técnicos e propostas em PDF/arquivos.
/// </summary>
public class AnexoServico : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public string Origem { get; set; } = "Cotacao"; // Cotacao, Proposta, OrdemServico
    public Guid ReferenciaId { get; set; } // CotacaoServicoId ou PropostaServicoId
    public Guid? CotacaoServicoId { get; set; }
    public Guid? PropostaServicoId { get; set; }
    public string NomeArquivo { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long Tamanho { get; set; }
    public string CaminhoStorage { get; set; } = string.Empty;
    public string Descricao { get; set; } = string.Empty;
}

/// <summary>
/// Ordem de Serviço / Contrato de Serviço gerado a partir de uma cotação aprovada.
/// </summary>
public class OrdemServico : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public string Numero { get; set; } = string.Empty; // ex.: OS-2026-0001
    public Guid CotacaoServicoId { get; set; }
    public CotacaoServico? CotacaoServico { get; set; }

    public Guid PropostaServicoId { get; set; }
    public PropostaServico? PropostaServico { get; set; }

    public Guid FornecedorId { get; set; }
    public Fornecedor? Fornecedor { get; set; }

    public Guid? FilialId { get; set; }
    public Filial? Filial { get; set; }

    // Status: Criada, AguardandoAssinatura, EmExecucao, Concluida, Cancelada
    public string Status { get; set; } = "Criada";

    // Gestão de Impedimentos e Responsável
    public string StatusImpedimento { get; set; } = "Nenhum"; // Nenhum, AguardandoPeça, AguardandoAprovação, AguardandoCliente, ClimaDesfavoravel, Pausada
    public string MotivoImpedimento { get; set; } = string.Empty;
    public string TecnicoResponsavelNome { get; set; } = string.Empty;
    public Guid? TecnicoResponsavelId { get; set; }

    public decimal ValorTotal { get; set; }
    public decimal ValorMaoDeObra { get; set; }
    public decimal ValorMateriais { get; set; }
    public decimal Deslocamento { get; set; }
    public decimal ImpostosTotal { get; set; }

    // Apontamento de Custos Reais para Análise de Rentabilidade
    public decimal CustoRealMaoDeObra { get; set; }
    public decimal CustoRealPecas { get; set; }
    public decimal CustoRealOutros { get; set; }
    public DateTime? DataConclusaoEfetiva { get; set; }

    public string CondicaoPagamento { get; set; } = string.Empty;
    public int PrazoExecucaoDias { get; set; }
    public DateTime? PrevisaoInicio { get; set; }
    public DateTime? PrevisaoTermino { get; set; }
    public int GarantiaMeses { get; set; } = 12;

    public string EscopoAprovadoResumo { get; set; } = string.Empty;
    public string ClausulasEspeciais { get; set; } = string.Empty;
    public string Observacoes { get; set; } = string.Empty;

    public Guid? AprovadoPorId { get; set; }
    public string AprovadoPorNome { get; set; } = string.Empty;
    public DateTime? AprovadoEm { get; set; }

    public string TimelineJson { get; set; } = "[]";
}
