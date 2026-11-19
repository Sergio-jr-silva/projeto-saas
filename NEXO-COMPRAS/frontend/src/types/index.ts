export interface User {
  id: string;
  nome: string;
  email: string;
  perfil: 'PlatformAdmin' | 'AdministradorGeral' | 'TenantAdmin' | 'Buyer' | 'Viewer';
  tenantId?: string;
}

export interface TenantSummary {
  id: string;
  razaoSocial: string;
  nomeFantasia: string;
  cnpj: string;
  status: 'Ativo' | 'Bloqueado' | 'Suspenso' | 'Inativo' | 'Encerrado' | 'Pendente';
  planoNome?: string;
  logoUrl?: string;
  slug?: string;
  mensalidadeFinal?: number;
  moduloContratado?: string;
  situacaoFinanceira?: string;
}

export interface Plano {
  id: string;
  nome: string;
  mensalidade: number;
  maxUsuarios: number;
  maxFiliais: number;
  maxFornecedores: number;
  maxCotacoesMes: number;
  recursosJson: string;
  ativo: boolean;
}

export interface AdminDashboard {
  totalTenants?: number;
  tenantsAtivos?: number;
  tenantsBloqueados?: number;
  totalEmpresas?: number;
  empresasAtivas?: number;
  empresasBloqueadas?: number;
  totalFiliais?: number;
  totalSincronizadores?: number;
  sincronizadoresOnline?: number;
  sincronizadoresOffline?: number;
  sincronizadoresPendentes?: number;
  totalUsuarios: number;
  totalProdutos?: number;
  totalFornecedores?: number;
  totalCotacoes?: number;
  totalPedidos?: number;
  totalOrdensServico?: number;
  mrrEstimado: number;
  ultimosTenants?: TenantSummary[];
  ultimasEmpresas?: TenantSummary[];
  falhasRecentes?: FalhaSincronizacaoRecente[];
  statusPlataforma?: string;
}

export interface FalhaSincronizacaoRecente {
  id: string;
  empresa: string;
  origem: string;
  mensagem: string;
  dataCriacao: string;
}

export interface CadastroCompletoEmpresaRequest {
  razaoSocial: string;
  nomeFantasia?: string;
  cnpj: string;
  email?: string;
  telefone?: string;
  planoId?: string;
  nomeFilial?: string;
  codigoFilial?: string;
  cnpjFilial?: string;
  tipoBanco: 'Firebird' | 'PostgreSQL';
  hostBanco?: string;
  portaBanco?: number;
  caminhoBanco?: string;
  usuarioBanco?: string;
  senhaBanco?: string;
  charsetBanco?: string;
  responsavelNome?: string;
  emailLogin: string;
  senhaInicial?: string;
  perfilAcesso?: string;
  gerarCodigoAtivacao: boolean;
  slug?: string;
  moduloContratado?: string;
  descontoComercial?: number;
  ajusteManual?: number;
  justificativaPreco?: string;
}

export interface CadastroCompletoEmpresaResponse {
  tenantId: string;
  razaoSocial: string;
  nomeFantasia: string;
  cnpj: string;
  status: string;
  filialId: string;
  nomeFilial: string;
  codigoFilial: string;
  tokenFilial: string;
  usuarioId: string;
  usuarioNome: string;
  usuarioEmail: string;
  senhaInicial: string;
  agenteId?: string;
  agenteNome?: string;
  codigoAtivacao?: string;
  codigoAtivacaoExpiraEm?: string;
  slug?: string;
  mensalidadeFinal?: number;
}

export interface AdminFilialItem {
  id: string;
  tenantId: string;
  empresaNome: string;
  codigo: string;
  nome: string;
  cnpj: string;
  cidade: string;
  uf: string;
  ativa: boolean;
  token: string;
  tokenRevogado: boolean;
  dataCriacao: string;
}

export interface AdminUsuarioItem {
  id: string;
  tenantId?: string;
  empresaNome: string;
  nome: string;
  email: string;
  perfil: string;
  ativo: boolean;
  ultimoLogin?: string;
  dataCriacao: string;
}

export interface AdminSincronizadorItem {
  id: string;
  tenantId: string;
  empresaNome: string;
  filialId?: string;
  nome: string;
  status: string;
  tipoBanco: string;
  versao?: string;
  nomeMaquina?: string;
  chavePrefixo?: string;
  revogado: boolean;
  ultimoHeartbeat?: string;
  ultimaSincronizacao?: string;
  codigoAtivacaoUsado: boolean;
  codigoExpirado: boolean;
  dataCriacao: string;
}

export interface Produto {
  id: string;
  codigoErp: string;
  descricao: string;
  codigoBarras: string;
  unidade: string;
  grupo: string;
  categoria?: string;
  precoVenda: number;
  custoAtual: number;
  ultimoCusto: number;
  ultimaCompraEm?: string;
  /** null = estoque desconhecido (diferente de zero) */
  estoqueAtual: number | null;
  estoqueCritico?: number | null;
  estoquePendente: number;
  estoqueMinimo: number;
  consumo90Dias: number;
  fornecedorVinculadoId?: string;
  fornecedorVinculadoNome?: string;
  origem: string;
  ultimaSincronizacaoEm?: string;
  dataAtualizacao: string;
  nivelEstoque: 'Zerado' | 'Critico' | 'Baixo' | 'Normal' | 'Indeterminado';
  coberturaDias?: number;
  sugestaoCompra: number;
  margemPerc?: number;
  ativo: boolean;
}

export interface Fornecedor {
  id: string;
  codigoErp: string;
  razaoSocial: string;
  nomeFantasia: string;
  cnpj: string;
  inscricaoEstadual?: string;
  email: string;
  telefone: string;
  whatsapp: string;
  contato: string;
  endereco?: string;
  cidade: string;
  uf: string;
  prazoEntregaPadrao: number;
  condicoesPagamento?: string;
  observacoes?: string;
  origem: string;
  ativo: boolean;
}

export interface AgenteStatus {
  id: string;
  nome: string;
  chavePrefixo: string;
  status: string;
  versao: string;
  nomeMaquina: string;
  ultimoHeartbeat?: string;
  ultimaSincronizacao?: string;
  revogado: boolean;
  dataCriacao?: string;
  criadoEm?: string;
  intervaloEstoqueMin: number;
  intervaloProdutosMin: number;
  intervaloFornecedoresMin: number;
  intervaloHeartbeatSeg: number;
}

export interface NovoCodigoDto {
  cotacaoFornecedorId: string;
  link: string;
  codigo: string;
  expiraEm: string;
}

// ---------- Cotações ----------
export interface CotacaoItem {
  id: string;
  produtoId: string;
  codigo: string;
  descricao: string;
  unidade: string;
  quantidade: number;
  estoqueNoMomento: number;
  ultimoCustoRef: number;
  precoVendaRef: number;
}

export interface AcessoInfo {
  tokenParcial?: string;
  expiraEm: string;
  revogado: boolean;
  bloqueado: boolean;
  tentativas: number;
  visualizacoes: number;
  primeiraVisualizacaoEm?: string;
  ultimaVisualizacaoEm?: string;
}

export interface CotacaoFornecedor {
  id: string;
  fornecedorId: string;
  nome: string;
  email: string;
  whatsapp: string;
  status: string;
  acesso?: AcessoInfo;
  enviadaEm?: string;
  respondida: boolean;
}

export interface CotacaoResumo {
  id: string;
  numero: string;
  titulo: string;
  status: 'Aberta' | 'EmAnalise' | 'Aprovada' | 'Finalizada' | 'Cancelada';
  dataCriacao: string;
  dataLimiteResposta: string;
  itens: number;
  fornecedores: number;
  respondidas: number;
  visualizadas: number;
  totalAprovado: number;
  economiaRealizada: number;
}

export interface CotacaoDetalhe {
  id: string;
  numero: string;
  titulo: string;
  observacoes: string;
  status: 'Aberta' | 'EmAnalise' | 'Aprovada' | 'Finalizada' | 'Cancelada';
  dataCriacao: string;
  dataLimiteResposta: string;
  aprovadaPorNome?: string;
  aprovadaEm?: string;
  observacaoAprovacao?: string;
  totalAprovado: number;
  economiaPotencial: number;
  economiaRealizada: number;
  itens: CotacaoItem[];
  fornecedores: CotacaoFornecedor[];
}

export interface AcessoCriado {
  cotacaoFornecedorId: string;
  fornecedorId: string;
  fornecedor: string;
  link: string;
  codigo: string;
  expiraEm: string;
}

export interface CotacaoCriada {
  id: string;
  numero: string;
  acessos: AcessoCriado[];
}

// ---------- Comparação Inteligente (Fase 5) ----------
export interface OfertaItem {
  cotacaoFornecedorId: string;
  disponivel: boolean;
  quantidadeOfertada: number;
  precoUnitario: number;
  precoLiquidoUnit: number;
  custoEfetivoUnit: number;
  custoSemFreteUnit: number;
  totalNota: number;
  cst: string;
  cfop: string;
  icms: number;
  ipi: number;
  st: number;
  creditoTotal: number;
  margemPerc?: number;
  quantidadeParcial: boolean;
  menorPreco: boolean;
  melhorCusto: boolean;
}

export interface ComparacaoItem {
  cotacaoItemId: string;
  codigo: string;
  descricao: string;
  unidade: string;
  quantidade: number;
  ultimoCustoRef: number;
  precoVendaRef: number;
  referenciaCustoUnit: number;
  referenciaOrigem: string;
  ofertas: OfertaItem[];
}

export interface ComparacaoFornecedor {
  cotacaoFornecedorId: string;
  fornecedorId: string;
  nome: string;
  status: string;
  frete: number;
  outrasDespesas: number;
  prazoEntregaDias: number;
  condicaoPagamento: string;
  diasPagamento?: number;
  validadeProposta?: string;
  totalNota: number;
  custoEfetivoTotal: number;
  cobreTudo: boolean;
  itensOfertados: number;
  melhorPrazo: boolean;
  melhorPagamento: boolean;
  melhorCustoTotal: boolean;
}

export interface Recomendacao {
  tipo: 'FornecedorUnico' | 'CombinacaoItens';
  escolhas: Record<string, string>;
  custoEfetivoTotal: number;
  justificativa: string;
}

export interface ComparativoCotacao {
  cotacaoId: string;
  status: string;
  temPropostas: boolean;
  regime: string;
  creditaIcms: boolean;
  creditaIpi: boolean;
  percCreditoPisCofins: number;
  fornecedores: ComparacaoFornecedor[];
  itens: ComparacaoItem[];
  recomendacao?: Recomendacao;
  economiaPotencial: number;
  escolhas: Record<string, string>;
}

// ---------- Portal do Fornecedor ----------
export interface PortalItem {
  id: string;
  codigo: string;
  descricao: string;
  unidade: string;
  quantidade: number;
}

export interface RespostaItem {
  cotacaoItemId: string;
  disponivel: boolean;
  quantidadeOfertada: number;
  precoUnitario: number;
  descontoPerc: number;
  cst?: string;
  cfop?: string;
  icmsAliq: number;
  ipiAliq: number;
  mva: number;
  icmsStAliq: number;
  icmsStValor: number;
  observacao?: string;
}

export interface PortalResposta {
  status: string;
  frete: number;
  outrasDespesas: number;
  prazoEntregaDias: number;
  condicaoPagamento: string;
  validadeProposta?: string;
  contatoNome: string;
  observacoes: string;
  enviadaEm?: string;
  itens: RespostaItem[];
}

export interface Anexo {
  id: string;
  nomeArquivo: string;
  contentType: string;
  tamanho: number;
  dataCriacao: string;
}

export interface PortalCotacao {
  empresa: string;
  numero: string;
  titulo: string;
  observacoes: string;
  dataLimiteResposta: string;
  fornecedorNome: string;
  status: string;
  editavel: boolean;
  mensagem?: string;
  itens: PortalItem[];
  resposta?: PortalResposta;
  anexos: Anexo[];
  avisos: string[];
}

export interface TimelineEvento {
  data: string;
  status: string;
  descricao: string;
  usuario: string;
}

export interface PedidoCompraItem {
  id: string;
  produtoId: string;
  codigoProduto: string;
  descricaoProduto: string;
  unidade: string;
  ncm: string;
  quantidade: number;
  precoUnitario: number;
  descontoPerc: number;
  precoLiquidoUnit: number;
  subtotal: number;
  cst: string;
  cfop: string;
  icmsAliq: number;
  ipiAliq: number;
  mva: number;
  icmsStAliq: number;
  icmsStValor: number;
  custoEfetivoUnit: number;
  custoEfetivoTotal: number;
}

export interface PedidoCompra {
  id: string;
  numero: string;
  status: 'Criado' | 'Enviado' | 'AguardandoConfirmacao' | 'Confirmado' | 'EmPreparacao' | 'EmTransporte' | 'Recebido' | 'Cancelado';
  cotacaoId?: string;
  cotacaoNumero?: string;
  fornecedorId: string;
  fornecedorNome?: string;
  fornecedorCnpj?: string;
  fornecedor?: Partial<Fornecedor>;
  subtotal: number;
  desconto: number;
  frete: number;
  outrasDespesas: number;
  impostosTotal: number;
  valorTotal: number;
  condicaoPagamento: string;
  prazoEntregaDias: number;
  previsaoEntrega?: string;
  observacoes?: string;
  aprovadoPorNome?: string;
  aprovadoEm?: string;
  dataCriacao: string;
  qtdItens?: number;
  timeline?: TimelineEvento[];
  itens?: PedidoCompraItem[];
}

export interface HistoricoCompraItem {
  id: string;
  data: string;
  produtoId: string;
  produtoCodigo: string;
  produtoDescricao: string;
  produtoUnidade: string;
  ultimoCustoReferencia: number;
  precoVenda: number;
  fornecedorId?: string;
  fornecedorNome: string;
  valor: number;
  quantidade: number;
  valorTotal: number;
  origem: string;
}

export interface IndicadoresCompras {
  totalComprado: number;
  totalPedidos: number;
  pedidosRecebidos: number;
  economiaRealizada: number;
  economiaPotencial: number;
  fornecedorMaisComprado?: { fornecedor: string; total: number; qtdPedidos: number };
  fornecedorMenosComprado?: { fornecedor: string; total: number; qtdPedidos: number };
  rankingFornecedores: { fornecedor: string; total: number; qtdPedidos: number }[];
}
