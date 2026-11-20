export type CategoriaServico =
  | 'ManutencaoPredial'
  | 'TI'
  | 'Facilities'
  | 'Obras'
  | 'Consultoria'
  | 'Juridico'
  | 'Logistica'
  | 'Marketing'
  | 'Engenharia'
  | 'Geral'
  | 'Outros';

export type StatusCotacaoServico =
  | 'Rascunho'
  | 'AguardandoPropostas'
  | 'EmAnalise'
  | 'AguardandoAprovacao'
  | 'Aprovada'
  | 'Contratada'
  | 'EmExecucao'
  | 'Concluida'
  | 'Cancelada';

export type StatusOrdemServico =
  | 'Criada'
  | 'AguardandoAssinatura'
  | 'EmExecucao'
  | 'Concluida'
  | 'Cancelada';

export interface AtividadeServico {
  id: string;
  ordem: number;
  etapa?: string;
  nome: string;
  descricao?: string;
  quantidade: number;
  unidadeMedicao: string;
  localExecucao?: string;
  requisitosTecnicos?: string;
  resultadoEsperado?: string;
  valorEstimadoUnitario?: number;
  subtotal?: number;
}

export interface FornecedorServicoItem {
  id: string;
  fornecedorId: string;
  status: string;
  razaoSocial: string;
  nomeFantasia: string;
  cnpj: string;
  email: string;
  telefone: string;
  linkAcesso?: string;
  codigoHash?: string;
  temProposta: boolean;
  propostaStatus?: string;
  valorTotalProposta?: number;
  prazoExecucaoDias?: number;
  pontuacaoPonderada?: number;
}

export interface CotacaoServicoListaItem {
  id: string;
  numero: string;
  titulo: string;
  categoria: string;
  prioridade: string;
  status: StatusCotacaoServico;
  dataAbertura: string;
  dataLimiteResposta: string;
  dataDesejadaInicio?: string;
  prazoEstimadoDias: number;
  responsavelNome: string;
  cidade?: string;
  uf?: string;
  qtdAtividades: number;
  qtdFornecedores: number;
  qtdPropostas: number;
  totalEstimado: number;
  totalContratado: number;
  economiaRealizada: number;
  fornecedorVencedorNome?: string;
}

export interface CotacaoServicoDetalhe {
  id: string;
  numero: string;
  titulo: string;
  categoria: string;
  prioridade: string;
  status: StatusCotacaoServico;
  dataAbertura: string;
  dataLimiteResposta: string;
  dataDesejadaInicio?: string;
  prazoEstimadoDias: number;
  necessidadeMotivacao?: string;
  descricaoDetalhada?: string;
  objetivoEsperado?: string;
  resultadoEsperado?: string;
  atendimentoPresencial: boolean;
  cep?: string;
  endereco?: string;
  numeroLocal?: string;
  complemento?: string;
  bairro?: string;
  cidade?: string;
  uf?: string;
  pontoReferencia?: string;
  horarioPermitido?: string;
  materiaisInclusosExigidos?: string;
  equipamentosExigidos?: string;
  garantiaEsperadaMeses: number;
  condicoesEspeciais?: string;
  certificacoesExigidas?: string;
  pesoPreco: number;
  pesoPrazo: number;
  pesoTecnico: number;
  pesoCondicoes: number;
  totalEstimado: number;
  totalContratado: number;
  economiaRealizada: number;
  justificativaEscolha?: string;
  aprovadoPorNome?: string;
  aprovadoEm?: string;
  observacaoAprovacao?: string;
  atividades: AtividadeServico[];
  fornecedores: FornecedorServicoItem[];
  anexos: {
    id: string;
    nomeArquivo: string;
    tamanho: number;
    descricao?: string;
    contentType: string;
  }[];
}

export interface PropostaComparativo {
  propostaId: string;
  fornecedorServicoId: string;
  fornecedorId: string;
  razaoSocial: string;
  nomeFantasia: string;
  cnpj: string;
  valorTotal: number;
  valorMaoDeObra: number;
  valorMateriaisInclusos: number;
  valorEquipamentos: number;
  deslocamento: number;
  prazoInicioDias: number;
  prazoExecucaoDias: number;
  condicoesPagamento: string;
  garantiaMeses: number;
  descricaoSolucao: string;
  metodologia: string;
  equipeAlocada: string;
  materiaisInclusosTexto: string;
  materiaisNaoInclusosTexto: string;
  certificacoesEquipe: string;
  scoreComercial: number;
  scorePrazo: number;
  scoreTecnico: number;
  scoreCondicoes: number;
  pontuacaoPonderada: number;
  parecerAvaliacao?: string;
  isVencedora: boolean;
  etapas: {
    atividadeServicoId: string;
    valorTotal: number;
    detalhesExecucao?: string;
  }[];
}

export interface ComparacaoServicoResponse {
  id: string;
  numero: string;
  titulo: string;
  status: string;
  temPropostas: boolean;
  pesos: {
    pesoPreco: number;
    pesoPrazo: number;
    pesoTecnico: number;
    pesoCondicoes: number;
  };
  atividades: {
    id: string;
    ordem: number;
    nome: string;
    quantidade: number;
    unidadeMedicao: string;
  }[];
  propostas: PropostaComparativo[];
}

export interface OrdemServicoItem {
  id: string;
  numero: string;
  cotacaoNumero: string;
  cotacaoTitulo: string;
  fornecedorRazaoSocial: string;
  status: StatusOrdemServico;
  valorTotal: number;
  prazoExecucaoDias: number;
  previsaoInicio?: string;
  previsaoTermino?: string;
  garantiaMeses: number;
  aprovadoPorNome?: string;
  dataCriacao: string;
}

