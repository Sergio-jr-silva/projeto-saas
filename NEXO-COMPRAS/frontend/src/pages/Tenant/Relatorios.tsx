import React, { useState, useEffect } from 'react';
import { useLocation } from 'react-router-dom';
import { api } from '../../services/api';
import {
  Wrench,
  FileSpreadsheet,
  ShoppingBag,
  TrendingDown,
  Users,
  Clock,
  Download,
  Printer,
  Search,
  Calendar,
  Filter,
  DollarSign,
  CheckCircle2,
  AlertCircle,
  FileText,
  RefreshCw,
  ChevronsLeft,
  ChevronLeft,
  ChevronRight,
  ChevronsRight,
  Award,
  Layers,
  Percent,
  TrendingUp,
  PackageCheck,
  Truck,
  Sliders,
  X
} from 'lucide-react';

const ITENS_POR_PAGINA = 10;

export const Relatorios: React.FC = () => {
  const location = useLocation();

  // Categorias Principais: 'servicos' (Ordens de Serviço), 'cotacoes' (Cotações), 'pedidos' (Pedidos de Compra)
  const getInitialCategoria = (): 'servicos' | 'cotacoes' | 'pedidos' => {
    if (location.pathname.includes('/servico') || location.pathname.includes('/os')) return 'servicos';
    if (location.pathname.includes('/cotacao') || location.pathname.includes('/economia')) return 'cotacoes';
    return 'pedidos';
  };

  const [categoriaAtiva, setCategoriaAtiva] = useState<'servicos' | 'cotacoes' | 'pedidos'>(getInitialCategoria());

  // Sub-relatórios de cada categoria
  const [subRelatorio, setSubRelatorio] = useState<string>('pendentes');
  const [loading, setLoading] = useState(true);

  // Filtros Globais
  const [dataInicio, setDataInicio] = useState('');
  const [dataFim, setDataFim] = useState('');
  const [fornecedorFiltro, setFornecedorFiltro] = useState('');
  const [buscaTexto, setBuscaTexto] = useState('');
  const [statusImpedimentoFiltro, setStatusImpedimentoFiltro] = useState('Todos');
  const [fornecedoresLista, setFornecedoresLista] = useState<any[]>([]);

  // Dados dos Relatórios
  // 1. Ordens de Serviço
  const [dadosOsPendentes, setDadosOsPendentes] = useState<any>(null);
  const [dadosOsTecnicos, setDadosOsTecnicos] = useState<any>(null);
  const [dadosOsCustoReceita, setDadosOsCustoReceita] = useState<any>(null);

  // 2. Cotações
  const [dadosCotacoesMapa, setDadosCotacoesMapa] = useState<any>(null);
  const [cotacoesDisponiveisMapa, setCotacoesDisponiveisMapa] = useState<any[]>([]);
  const [cotacaoSelecionadaMapa, setCotacaoSelecionadaMapa] = useState<string>('');
  const [dadosCotacoesSaving, setDadosCotacoesSaving] = useState<any>(null);
  const [dadosCotacoesFunil, setDadosCotacoesFunil] = useState<any>(null);

  // 3. Pedidos de Compra
  const [dadosPedidosPendentes, setDadosPedidosPendentes] = useState<any>(null);
  const [dadosCurvaAbc, setDadosCurvaAbc] = useState<any>(null);
  const [dadosPontualidade, setDadosPontualidade] = useState<any>(null);

  // Paginação
  const [paginaAtual, setPaginaAtual] = useState(1);

  // Modal: Apontamento de Custos da OS
  const [modalCustosAberto, setModalCustosAberto] = useState(false);
  const [osSelecionadaCustos, setOsSelecionadaCustos] = useState<any>(null);
  const [formCustos, setFormCustos] = useState({
    custoRealMaoDeObra: 0,
    custoRealPecas: 0,
    custoRealOutros: 0,
    statusImpedimento: 'Nenhum',
    motivoImpedimento: '',
    tecnicoResponsavelNome: '',
    observacoes: ''
  });
  const [salvandoCustos, setSalvandoCustos] = useState(false);

  // Modal: Recebimento Parcial de Pedido
  const [modalReceberAberto, setModalReceberAberto] = useState(false);
  const [pedidoSelecionadoReceber, setPedidoSelecionadoReceber] = useState<any>(null);
  const [itemSelecionadoReceber, setItemSelecionadoReceber] = useState<any>(null);
  const [qtdReceber, setQtdReceber] = useState<number>(0);
  const [salvandoRecebimento, setSalvandoRecebimento] = useState(false);

  // Mensagens
  const [mensagemSucesso, setMensagemSucesso] = useState<string | null>(null);
  const [mensagemErro, setMensagemErro] = useState<string | null>(null);

  // Carregar fornecedores e cotações para filtros
  useEffect(() => {
    api.get<any>('/fornecedores?size=200')
      .then(res => setFornecedoresLista(Array.isArray(res) ? res : (res?.itens || [])))
      .catch(() => {});

    api.get<any[]>('/relatorios/cotacoes-lista-mapa')
      .then(res => {
        if (Array.isArray(res)) {
          setCotacoesDisponiveisMapa(res);
          if (res.length > 0 && !cotacaoSelecionadaMapa) {
            setCotacaoSelecionadaMapa(res[0].id);
          }
        }
      })
      .catch(() => {});
  }, []);

  // Ajusta subRelatorio padrão ao mudar categoria
  useEffect(() => {
    if (categoriaAtiva === 'servicos') setSubRelatorio('os-pendentes');
    else if (categoriaAtiva === 'cotacoes') setSubRelatorio('mapa');
    else if (categoriaAtiva === 'pedidos') setSubRelatorio('pendentes');
  }, [categoriaAtiva]);

  // Recarregar dados ao mudar de aba ou filtros
  useEffect(() => {
    carregarRelatorioAtual();
    setPaginaAtual(1);
  }, [categoriaAtiva, subRelatorio, cotacaoSelecionadaMapa]);

  const carregarRelatorioAtual = async () => {
    setLoading(true);
    setMensagemErro(null);
    try {
      const params = new URLSearchParams();
      if (dataInicio) params.append('dataInicio', dataInicio);
      if (dataFim) params.append('dataFim', dataFim);
      if (fornecedorFiltro) params.append('fornecedorId', fornecedorFiltro);
      if (buscaTexto) params.append('busca', buscaTexto);
      if (statusImpedimentoFiltro && statusImpedimentoFiltro !== 'Todos')
        params.append('statusImpedimento', statusImpedimentoFiltro);

      const qs = params.toString() ? `?${params.toString()}` : '';

      if (categoriaAtiva === 'servicos') {
        if (subRelatorio === 'os-pendentes') {
          const res = await api.get<any>(`/relatorios/os-pendentes${qs}`);
          setDadosOsPendentes(res);
        } else if (subRelatorio === 'os-tecnicos') {
          const res = await api.get<any>(`/relatorios/os-tecnicos${qs}`);
          setDadosOsTecnicos(res);
        } else if (subRelatorio === 'os-custo-receita') {
          const res = await api.get<any>(`/relatorios/os-custo-receita${qs}`);
          setDadosOsCustoReceita(res);
        }
      } else if (categoriaAtiva === 'cotacoes') {
        if (subRelatorio === 'mapa') {
          const mapaQs = cotacaoSelecionadaMapa ? `?cotacaoId=${cotacaoSelecionadaMapa}` : qs;
          const res = await api.get<any>(`/relatorios/cotacoes-mapa${mapaQs}`);
          setDadosCotacoesMapa(res);
        } else if (subRelatorio === 'saving') {
          const res = await api.get<any>(`/relatorios/cotacoes-saving${qs}`);
          setDadosCotacoesSaving(res);
        } else if (subRelatorio === 'funil') {
          const res = await api.get<any>(`/relatorios/cotacoes-funil${qs}`);
          setDadosCotacoesFunil(res);
        }
      } else if (categoriaAtiva === 'pedidos') {
        if (subRelatorio === 'pendentes') {
          const res = await api.get<any>(`/relatorios/pedidos-pendentes${qs}`);
          setDadosPedidosPendentes(res);
        } else if (subRelatorio === 'curva-abc') {
          const res = await api.get<any>(`/relatorios/pedidos-curva-abc${qs}`);
          setDadosCurvaAbc(res);
        } else if (subRelatorio === 'pontualidade') {
          const res = await api.get<any>(`/relatorios/pedidos-pontualidade${qs}`);
          setDadosPontualidade(res);
        }
      }
    } catch (err: any) {
      setMensagemErro(err.message || 'Erro ao carregar dados do relatório.');
    } finally {
      setLoading(false);
    }
  };

  const aplicarFiltros = (e: React.FormEvent) => {
    e.preventDefault();
    setPaginaAtual(1);
    carregarRelatorioAtual();
  };

  const limparFiltros = () => {
    setDataInicio('');
    setDataFim('');
    setFornecedorFiltro('');
    setBuscaTexto('');
    setStatusImpedimentoFiltro('Todos');
    setPaginaAtual(1);
    setTimeout(() => carregarRelatorioAtual(), 50);
  };

  // Funções de Modal: Apontamento de Custos
  const abrirModalCustos = (os: any) => {
    setOsSelecionadaCustos(os);
    setFormCustos({
      custoRealMaoDeObra: os.CustoMaoDeObra || os.custoMaoDeObra || 0,
      custoRealPecas: os.CustoPecas || os.custoPecas || 0,
      custoRealOutros: os.CustoOutros || os.custoOutros || 0,
      statusImpedimento: os.StatusImpedimento || os.statusImpedimento || 'Nenhum',
      motivoImpedimento: os.MotivoImpedimento || os.motivoImpedimento || '',
      tecnicoResponsavelNome: os.Tecnico || os.tecnicoResponsavel || '',
      observacoes: ''
    });
    setModalCustosAberto(true);
  };

  const handleSalvarCustosOs = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!osSelecionadaCustos) return;
    setSalvandoCustos(true);
    setMensagemErro(null);
    try {
      await api.post(`/relatorios/os/${osSelecionadaCustos.Id || osSelecionadaCustos.id}/apontar-custos`, formCustos);
      setMensagemSucesso('Custos e status da OS atualizados com sucesso!');
      setModalCustosAberto(false);
      await carregarRelatorioAtual();
    } catch (err: any) {
      setMensagemErro(err.response?.data?.erro || err.message || 'Erro ao lançar custos da OS.');
    } finally {
      setSalvandoCustos(false);
    }
  };

  // Funções de Modal: Recebimento Parcial
  const abrirModalReceber = (pedido: any, item: any) => {
    setPedidoSelecionadoReceber(pedido);
    setItemSelecionadoReceber(item);
    setQtdReceber(item.SaldoPendente || (item.quantidade - item.quantidadeRecebida) || 1);
    setModalReceberAberto(true);
  };

  const handleSalvarRecebimento = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!pedidoSelecionadoReceber || !itemSelecionadoReceber) return;
    setSalvandoRecebimento(true);
    setMensagemErro(null);
    try {
      await api.post(
        `/relatorios/pedidos/${pedidoSelecionadoReceber.id || pedidoSelecionadoReceber.Id}/itens/${itemSelecionadoReceber.id || itemSelecionadoReceber.Id}/receber`,
        { quantidadeRecebida: Number(qtdReceber) }
      );
      setMensagemSucesso(`Recebimento de ${qtdReceber} ${itemSelecionadoReceber.unidade || 'UN'} registrado com sucesso!`);
      setModalReceberAberto(false);
      await carregarRelatorioAtual();
    } catch (err: any) {
      setMensagemErro(err.response?.data?.erro || err.message || 'Erro ao registrar recebimento.');
    } finally {
      setSalvandoRecebimento(false);
    }
  };

  // Exportação CSV
  const exportarCsv = (nomeArquivo: string, linhas: any[], colunas: { chave: string; rotulo: string }[]) => {
    if (!linhas || linhas.length === 0) {
      alert('Não há dados para exportar no relatório atual.');
      return;
    }

    const cabecalho = colunas.map(c => `"${c.rotulo}"`).join(';');
    const corpo = linhas.map(l =>
      colunas.map(c => {
        const val = l[c.chave];
        if (val === null || val === undefined) return '""';
        if (typeof val === 'number') return `"${val.toLocaleString('pt-BR')}"`;
        return `"${String(val).replace(/"/g, '""')}"`;
      }).join(';')
    ).join('\n');

    const csvContent = '\uFEFF' + cabecalho + '\n' + corpo;
    const blob = new Blob([csvContent], { type: 'text/csv;charset=utf-8;' });
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.setAttribute('download', `${nomeArquivo}_${new Date().toISOString().slice(0, 10)}.csv`);
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
  };

  // Exportação PDF via Backend
  const exportarPdf = () => {
    const tipo = `${categoriaAtiva}-${subRelatorio}`;
    window.open(`${api.getBaseUrl()}/relatorios/exportar-pdf?tipo=${tipo}`, '_blank');
  };

  // Renderizador de Paginação
  const renderPaginacao = (totalItens: number) => {
    const totalPaginas = Math.ceil(totalItens / ITENS_POR_PAGINA);
    if (totalPaginas <= 1) return null;

    return (
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginTop: '16px', padding: '10px 0', borderTop: '1px solid var(--border)' }}>
        <div style={{ fontSize: '13px', color: 'var(--text-muted)' }}>
          Exibindo {((paginaAtual - 1) * ITENS_POR_PAGINA) + 1} a {Math.min(paginaAtual * ITENS_POR_PAGINA, totalItens)} de {totalItens} registros
        </div>
        <div style={{ display: 'flex', gap: '6px' }}>
          <button
            className="btn btn-secondary"
            style={{ padding: '4px 8px' }}
            disabled={paginaAtual === 1}
            onClick={() => setPaginaAtual(1)}
          >
            <ChevronsLeft size={16} />
          </button>
          <button
            className="btn btn-secondary"
            style={{ padding: '4px 8px' }}
            disabled={paginaAtual === 1}
            onClick={() => setPaginaAtual(prev => Math.max(1, prev - 1))}
          >
            <ChevronLeft size={16} />
          </button>
          <span style={{ padding: '4px 12px', fontSize: '13px', fontWeight: 600, display: 'flex', alignItems: 'center' }}>
            {paginaAtual} de {totalPaginas}
          </span>
          <button
            className="btn btn-secondary"
            style={{ padding: '4px 8px' }}
            disabled={paginaAtual === totalPaginas}
            onClick={() => setPaginaAtual(prev => Math.min(totalPaginas, prev + 1))}
          >
            <ChevronRight size={16} />
          </button>
          <button
            className="btn btn-secondary"
            style={{ padding: '4px 8px' }}
            disabled={paginaAtual === totalPaginas}
            onClick={() => setPaginaAtual(totalPaginas)}
          >
            <ChevronsRight size={16} />
          </button>
        </div>
      </div>
    );
  };

  return (
    <div className="container" style={{ maxWidth: '1440px', margin: '0 auto', padding: '24px 16px' }}>

      {/* CABEÇALHO DA PÁGINA */}
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', marginBottom: '24px', flexWrap: 'wrap', gap: '16px' }}>
        <div>
          <h1 style={{ fontSize: '24px', fontWeight: 800, color: 'var(--text-main)', margin: '0 0 6px 0', display: 'flex', alignItems: 'center', gap: '10px' }}>
            <FileSpreadsheet size={26} color="var(--blue-primary)" />
            Relatórios Gerenciais e Indicadores
          </h1>
          <p style={{ fontSize: '13.5px', color: 'var(--text-muted)', margin: 0 }}>
            Visão analítica completa de Ordens de Serviço, Cotações e Pedidos de Compra com dados reais e isolamento multi-tenant.
          </p>
        </div>

        <div style={{ display: 'flex', gap: '10px' }}>
          <button onClick={exportarPdf} className="btn btn-secondary" style={{ display: 'flex', alignItems: 'center', gap: '6px' }}>
            <Printer size={16} />
            <span>Imprimir / PDF</span>
          </button>
          <button onClick={carregarRelatorioAtual} className="btn btn-secondary" style={{ display: 'flex', alignItems: 'center', gap: '6px' }}>
            <RefreshCw size={16} className={loading ? 'spin' : ''} />
            <span>Atualizar</span>
          </button>
        </div>
      </div>

      {/* ALERTAS DE SUCESSO / ERRO */}
      {mensagemSucesso && (
        <div style={{ padding: '12px 16px', background: '#ecfdf5', border: '1px solid #a7f3d0', borderRadius: '8px', color: '#065f46', marginBottom: '16px', display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
            <CheckCircle2 size={18} />
            <span>{mensagemSucesso}</span>
          </div>
          <button onClick={() => setMensagemSucesso(null)} style={{ background: 'none', border: 'none', cursor: 'pointer', color: '#065f46' }}>
            <X size={16} />
          </button>
        </div>
      )}

      {mensagemErro && (
        <div style={{ padding: '12px 16px', background: '#fef2f2', border: '1px solid #fecaca', borderRadius: '8px', color: '#991b1b', marginBottom: '16px', display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
            <AlertCircle size={18} />
            <span>{mensagemErro}</span>
          </div>
          <button onClick={() => setMensagemErro(null)} style={{ background: 'none', border: 'none', cursor: 'pointer', color: '#991b1b' }}>
            <X size={16} />
          </button>
        </div>
      )}

      {/* 1. SELETOR DE CATEGORIA PRINCIPAL (3 ABAS PRINCIPAIS) */}
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: '12px', marginBottom: '20px' }}>
        <button
          onClick={() => setCategoriaAtiva('servicos')}
          style={{
            padding: '16px 20px',
            background: categoriaAtiva === 'servicos' ? 'var(--blue-primary)' : '#fff',
            color: categoriaAtiva === 'servicos' ? '#fff' : 'var(--text-main)',
            border: categoriaAtiva === 'servicos' ? 'none' : '1px solid var(--border)',
            borderRadius: '10px',
            cursor: 'pointer',
            fontWeight: 700,
            fontSize: '15px',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            gap: '10px',
            boxShadow: categoriaAtiva === 'servicos' ? '0 4px 12px rgba(37,99,235,0.25)' : 'none',
            transition: 'all 0.2s'
          }}
        >
          <Wrench size={20} />
          <span>1. Ordens de Serviço</span>
        </button>

        <button
          onClick={() => setCategoriaAtiva('cotacoes')}
          style={{
            padding: '16px 20px',
            background: categoriaAtiva === 'cotacoes' ? 'var(--blue-primary)' : '#fff',
            color: categoriaAtiva === 'cotacoes' ? '#fff' : 'var(--text-main)',
            border: categoriaAtiva === 'cotacoes' ? 'none' : '1px solid var(--border)',
            borderRadius: '10px',
            cursor: 'pointer',
            fontWeight: 700,
            fontSize: '15px',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            gap: '10px',
            boxShadow: categoriaAtiva === 'cotacoes' ? '0 4px 12px rgba(37,99,235,0.25)' : 'none',
            transition: 'all 0.2s'
          }}
        >
          <FileSpreadsheet size={20} />
          <span>2. Cotações</span>
        </button>

        <button
          onClick={() => setCategoriaAtiva('pedidos')}
          style={{
            padding: '16px 20px',
            background: categoriaAtiva === 'pedidos' ? 'var(--blue-primary)' : '#fff',
            color: categoriaAtiva === 'pedidos' ? '#fff' : 'var(--text-main)',
            border: categoriaAtiva === 'pedidos' ? 'none' : '1px solid var(--border)',
            borderRadius: '10px',
            cursor: 'pointer',
            fontWeight: 700,
            fontSize: '15px',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            gap: '10px',
            boxShadow: categoriaAtiva === 'pedidos' ? '0 4px 12px rgba(37,99,235,0.25)' : 'none',
            transition: 'all 0.2s'
          }}
        >
          <ShoppingBag size={20} />
          <span>3. Pedidos de Compra</span>
        </button>
      </div>

      {/* 2. SUB-ABAS DE CADA CATEGORIA */}
      <div style={{ display: 'flex', gap: '8px', marginBottom: '20px', borderBottom: '2px solid var(--border)', paddingBottom: '8px', overflowX: 'auto' }}>
        {categoriaAtiva === 'servicos' && (
          <>
            <button
              onClick={() => setSubRelatorio('os-pendentes')}
              className={`btn ${subRelatorio === 'os-pendentes' ? 'btn-primary' : 'btn-secondary'}`}
              style={{ padding: '8px 16px', borderRadius: '20px' }}
            >
              OS em Aberto / Pendentes
            </button>
            <button
              onClick={() => setSubRelatorio('os-tecnicos')}
              className={`btn ${subRelatorio === 'os-tecnicos' ? 'btn-primary' : 'btn-secondary'}`}
              style={{ padding: '8px 16px', borderRadius: '20px' }}
            >
              OS por Técnico / Responsável
            </button>
            <button
              onClick={() => setSubRelatorio('os-custo-receita')}
              className={`btn ${subRelatorio === 'os-custo-receita' ? 'btn-primary' : 'btn-secondary'}`}
              style={{ padding: '8px 16px', borderRadius: '20px' }}
            >
              Custo x Receita e Rentabilidade
            </button>
          </>
        )}

        {categoriaAtiva === 'cotacoes' && (
          <>
            <button
              onClick={() => setSubRelatorio('mapa')}
              className={`btn ${subRelatorio === 'mapa' ? 'btn-primary' : 'btn-secondary'}`}
              style={{ padding: '8px 16px', borderRadius: '20px' }}
            >
              Mapa Comparativo de Cotações
            </button>
            <button
              onClick={() => setSubRelatorio('saving')}
              className={`btn ${subRelatorio === 'saving' ? 'btn-primary' : 'btn-secondary'}`}
              style={{ padding: '8px 16px', borderRadius: '20px' }}
            >
              Economia Obtida (Saving)
            </button>
            <button
              onClick={() => setSubRelatorio('funil')}
              className={`btn ${subRelatorio === 'funil' ? 'btn-primary' : 'btn-secondary'}`}
              style={{ padding: '8px 16px', borderRadius: '20px' }}
            >
              Cotações em Aberto / Respondidas / Vencidas
            </button>
          </>
        )}

        {categoriaAtiva === 'pedidos' && (
          <>
            <button
              onClick={() => setSubRelatorio('pendentes')}
              className={`btn ${subRelatorio === 'pendentes' ? 'btn-primary' : 'btn-secondary'}`}
              style={{ padding: '8px 16px', borderRadius: '20px' }}
            >
              Pedidos Pendentes de Entrega
            </button>
            <button
              onClick={() => setSubRelatorio('curva-abc')}
              className={`btn ${subRelatorio === 'curva-abc' ? 'btn-primary' : 'btn-secondary'}`}
              style={{ padding: '8px 16px', borderRadius: '20px' }}
            >
              Compras por Fornecedor (Curva ABC / Pareto)
            </button>
            <button
              onClick={() => setSubRelatorio('pontualidade')}
              className={`btn ${subRelatorio === 'pontualidade' ? 'btn-primary' : 'btn-secondary'}`}
              style={{ padding: '8px 16px', borderRadius: '20px' }}
            >
              Prazo de Entrega: Previsto x Realizado
            </button>
          </>
        )}
      </div>

      {/* 3. BARRA DE FILTROS GLOBAIS */}
      <div className="card" style={{ padding: '16px 20px', marginBottom: '24px' }}>
        <form onSubmit={aplicarFiltros} style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(200px, 1fr))', gap: '14px', alignItems: 'flex-end' }}>
          <div>
            <label style={{ display: 'block', fontSize: '11.5px', fontWeight: 600, color: 'var(--text-muted)', marginBottom: '4px' }}>
              Data Início
            </label>
            <input
              type="date"
              className="input"
              value={dataInicio}
              onChange={(e) => setDataInicio(e.target.value)}
            />
          </div>

          <div>
            <label style={{ display: 'block', fontSize: '11.5px', fontWeight: 600, color: 'var(--text-muted)', marginBottom: '4px' }}>
              Data Fim
            </label>
            <input
              type="date"
              className="input"
              value={dataFim}
              onChange={(e) => setDataFim(e.target.value)}
            />
          </div>

          <div>
            <label style={{ display: 'block', fontSize: '11.5px', fontWeight: 600, color: 'var(--text-muted)', marginBottom: '4px' }}>
              Fornecedor / Prestador
            </label>
            <select
              className="input"
              value={fornecedorFiltro}
              onChange={(e) => setFornecedorFiltro(e.target.value)}
            >
              <option value="">Todos os fornecedores</option>
              {fornecedoresLista.map((f: any) => (
                <option key={f.id} value={f.id}>
                  {f.nomeFantasia || f.razaoSocial}
                </option>
              ))}
            </select>
          </div>

          {categoriaAtiva === 'servicos' && subRelatorio === 'os-pendentes' && (
            <div>
              <label style={{ display: 'block', fontSize: '11.5px', fontWeight: 600, color: 'var(--text-muted)', marginBottom: '4px' }}>
                Impedimento
              </label>
              <select
                className="input"
                value={statusImpedimentoFiltro}
                onChange={(e) => setStatusImpedimentoFiltro(e.target.value)}
              >
                <option value="Todos">Todos os status</option>
                <option value="Nenhum">Sem Impedimento</option>
                <option value="AguardandoPeça">Aguardando Peça</option>
                <option value="AguardandoAprovação">Aguardando Aprovação</option>
                <option value="AguardandoCliente">Aguardando Cliente</option>
                <option value="ClimaDesfavoravel">Clima Desfavorável</option>
                <option value="Pausada">Pausada</option>
              </select>
            </div>
          )}

          {categoriaAtiva === 'cotacoes' && subRelatorio === 'mapa' && (
            <div style={{ gridColumn: 'span 2' }}>
              <label style={{ display: 'block', fontSize: '11.5px', fontWeight: 600, color: 'var(--text-muted)', marginBottom: '4px' }}>
                Selecione a Cotação para Comparar:
              </label>
              <select
                className="input"
                value={cotacaoSelecionadaMapa}
                onChange={(e) => setCotacaoSelecionadaMapa(e.target.value)}
                style={{ fontWeight: 600, color: 'var(--blue-primary)' }}
              >
                {cotacoesDisponiveisMapa.map((c: any) => (
                  <option key={c.id} value={c.id}>
                    {c.numero} - {c.titulo} ({c.fornecedoresQtd} participantes)
                  </option>
                ))}
              </select>
            </div>
          )}

          <div>
            <label style={{ display: 'block', fontSize: '11.5px', fontWeight: 600, color: 'var(--text-muted)', marginBottom: '4px' }}>
              Busca Textual
            </label>
            <div style={{ position: 'relative' }}>
              <Search size={15} style={{ position: 'absolute', left: '10px', top: '10px', color: 'var(--text-muted)' }} />
              <input
                type="text"
                className="input"
                placeholder="Número, produto, técnico..."
                value={buscaTexto}
                onChange={(e) => setBuscaTexto(e.target.value)}
                style={{ paddingLeft: '32px' }}
              />
            </div>
          </div>

          <div style={{ display: 'flex', gap: '8px' }}>
            <button type="submit" className="btn btn-primary" style={{ flex: 1, display: 'flex', alignItems: 'center', justifyContent: 'center', gap: '6px' }}>
              <Filter size={15} />
              <span>Filtrar</span>
            </button>
            <button type="button" onClick={limparFiltros} className="btn btn-secondary">
              Limpar
            </button>
          </div>
        </form>
      </div>

      {/* 4. CONTEÚDO DOS RELATÓRIOS CONFORME CATEGORIA E SUB-RELATÓRIO */}
      {loading ? (
        <div className="card" style={{ padding: '60px', textAlign: 'center', color: 'var(--text-muted)' }}>
          <RefreshCw size={32} className="spin" style={{ margin: '0 auto 12px auto' }} />
          <div>Carregando indicadores e métricas...</div>
        </div>
      ) : (
        <>
          {/* ========================================================================= */}
          {/* CATEGORIA 1: ORDENS DE SERVIÇO */}
          {/* ========================================================================= */}
          {categoriaAtiva === 'servicos' && subRelatorio === 'os-pendentes' && (
            <div>
              {/* KPIs de OS Pendentes */}
              <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(220px, 1fr))', gap: '16px', marginBottom: '24px' }}>
                <div className="card" style={{ borderLeft: '4px solid var(--blue-primary)', padding: '18px' }}>
                  <div style={{ fontSize: '12px', fontWeight: 700, color: 'var(--text-muted)' }}>TOTAL OS PENDENTES</div>
                  <div style={{ fontSize: '24px', fontWeight: 800, color: 'var(--text-main)', marginTop: '4px' }}>
                    {dadosOsPendentes?.kpis?.totalPendentes || 0}
                  </div>
                  <div style={{ fontSize: '11.5px', color: 'var(--text-muted)', marginTop: '4px' }}>Em execução ou abertas</div>
                </div>

                <div className="card" style={{ borderLeft: '4px solid #f59e0b', padding: '18px' }}>
                  <div style={{ fontSize: '12px', fontWeight: 700, color: 'var(--text-muted)' }}>COM IMPEDIMENTO</div>
                  <div style={{ fontSize: '24px', fontWeight: 800, color: '#d97706', marginTop: '4px' }}>
                    {dadosOsPendentes?.kpis?.comImpedimento || 0}
                  </div>
                  <div style={{ fontSize: '11.5px', color: 'var(--text-muted)', marginTop: '4px' }}>Aguardando peças ou cliente</div>
                </div>

                <div className="card" style={{ borderLeft: '4px solid #ef4444', padding: '18px' }}>
                  <div style={{ fontSize: '12px', fontWeight: 700, color: 'var(--text-muted)' }}>OS EM ATRASO</div>
                  <div style={{ fontSize: '24px', fontWeight: 800, color: '#dc2626', marginTop: '4px' }}>
                    {dadosOsPendentes?.kpis?.atrasadas || 0}
                  </div>
                  <div style={{ fontSize: '11.5px', color: 'var(--text-muted)', marginTop: '4px' }}>Prazo limite expirado</div>
                </div>

                <div className="card" style={{ borderLeft: '4px solid #10b981', padding: '18px' }}>
                  <div style={{ fontSize: '12px', fontWeight: 700, color: 'var(--text-muted)' }}>VALOR EM EXECUÇÃO</div>
                  <div style={{ fontSize: '22px', fontWeight: 800, color: '#059669', marginTop: '4px' }}>
                    {(dadosOsPendentes?.kpis?.valorEmExecucao || 0).toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' })}
                  </div>
                  <div style={{ fontSize: '11.5px', color: 'var(--text-muted)', marginTop: '4px' }}>Contratos ativos</div>
                </div>
              </div>

              {/* Tabela de OS Pendentes */}
              <div className="card" style={{ padding: '20px' }}>
                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '14px' }}>
                  <h3 style={{ fontSize: '16px', fontWeight: 700, margin: 0 }}>Listagem de Ordens de Serviço em Aberto</h3>
                  <button
                    onClick={() => exportarCsv('os_pendentes', dadosOsPendentes?.ordens || [], [
                      { chave: 'numero', rotulo: 'Número OS' },
                      { chave: 'fornecedorNome', rotulo: 'Prestador' },
                      { chave: 'tecnicoResponsavel', rotulo: 'Técnico' },
                      { chave: 'statusImpedimento', rotulo: 'Impedimento' },
                      { chave: 'diasEmAberto', rotulo: 'Dias em Aberto' },
                      { chave: 'diasAtraso', rotulo: 'Dias Atraso' },
                      { chave: 'valorTotal', rotulo: 'Valor Contratado' }
                    ])}
                    className="btn btn-secondary"
                    style={{ fontSize: '12.5px', padding: '6px 12px' }}
                  >
                    <Download size={14} /> Exportar CSV
                  </button>
                </div>

                <div className="table-container">
                  <table>
                    <thead>
                      <tr>
                        <th>Número</th>
                        <th>Prestador</th>
                        <th>Técnico Responsável</th>
                        <th>Status / Impedimento</th>
                        <th>Abertura</th>
                        <th>Previsão Término</th>
                        <th>Dias Decorridos</th>
                        <th style={{ textAlign: 'right' }}>Valor Total</th>
                        <th style={{ textAlign: 'center' }}>Ações</th>
                      </tr>
                    </thead>
                    <tbody>
                      {(dadosOsPendentes?.ordens || []).slice((paginaAtual - 1) * ITENS_POR_PAGINA, paginaAtual * ITENS_POR_PAGINA).map((os: any) => (
                        <tr key={os.id}>
                          <td style={{ fontWeight: 700 }}>
                            {os.numero}
                            <div style={{ fontSize: '11px', color: 'var(--text-muted)' }}>{os.categoria}</div>
                          </td>
                          <td>
                            <div style={{ fontWeight: 600 }}>{os.fornecedorNome}</div>
                            <div style={{ fontSize: '11px', color: 'var(--text-muted)' }}>{os.fornecedorCnpj}</div>
                          </td>
                          <td>
                            <span style={{ fontWeight: 600, color: 'var(--blue-primary)' }}>{os.tecnicoResponsavel}</span>
                          </td>
                          <td>
                            {os.statusImpedimento !== 'Nenhum' ? (
                              <span className="badge badge-yellow" title={os.motivoImpedimento || ''}>
                                ⚠️ {os.statusImpedimento}
                              </span>
                            ) : (
                              <span className="badge badge-green">🟢 Normal</span>
                            )}
                            {os.estaAtrasada && (
                              <span className="badge badge-red" style={{ marginLeft: '6px' }}>
                                Atrasada ({os.diasAtraso}d)
                              </span>
                            )}
                          </td>
                          <td>{new Date(os.dataAbertura).toLocaleDateString('pt-BR')}</td>
                          <td>{os.previsaoTermino !== '-' ? new Date(os.previsaoTermino).toLocaleDateString('pt-BR') : '-'}</td>
                          <td>
                            <div style={{ fontWeight: 700 }}>{os.diasEmAberto} dias</div>
                          </td>
                          <td style={{ textAlign: 'right', fontWeight: 800 }}>
                            {os.valorTotal.toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' })}
                          </td>
                          <td style={{ textAlign: 'center' }}>
                            <button
                              onClick={() => abrirModalCustos(os)}
                              className="btn btn-secondary"
                              style={{ fontSize: '11.5px', padding: '4px 8px' }}
                              title="Apontar custos reais ou atualizar impedimento"
                            >
                              <Sliders size={13} /> Apontar
                            </button>
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>

                {renderPaginacao(dadosOsPendentes?.ordens?.length || 0)}
              </div>
            </div>
          )}

          {categoriaAtiva === 'servicos' && subRelatorio === 'os-tecnicos' && (
            <div>
              <div className="card" style={{ padding: '20px', marginBottom: '24px' }}>
                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '16px' }}>
                  <h3 style={{ fontSize: '16px', fontWeight: 700, margin: 0, display: 'flex', alignItems: 'center', gap: '8px' }}>
                    <Users size={18} color="var(--blue-primary)" />
                    Produtividade e Distribuição por Técnico / Responsável
                  </h3>
                  <button
                    onClick={() => exportarCsv('os_tecnicos', dadosOsTecnicos?.tecnicos || [], [
                      { chave: 'tecnico', rotulo: 'Técnico Responsável' },
                      { chave: 'totalOrdens', rotulo: 'Total OS' },
                      { chave: 'concluidas', rotulo: 'Concluídas' },
                      { chave: 'emAndamento', rotulo: 'Em Andamento' },
                      { chave: 'comImpedimento', rotulo: 'Com Impedimento' },
                      { chave: 'atrasadas', rotulo: 'Atrasadas' },
                      { chave: 'prazoMedioDias', rotulo: 'Prazo Médio (dias)' },
                      { chave: 'taxaConclusao', rotulo: 'Taxa Conclusão %' },
                      { chave: 'valorTotalGerenciado', rotulo: 'Valor Gerenciado' }
                    ])}
                    className="btn btn-secondary"
                    style={{ fontSize: '12.5px', padding: '6px 12px' }}
                  >
                    <Download size={14} /> Exportar CSV
                  </button>
                </div>

                <div className="table-container">
                  <table>
                    <thead>
                      <tr>
                        <th>Técnico / Responsável</th>
                        <th style={{ textAlign: 'center' }}>Total OS</th>
                        <th style={{ textAlign: 'center' }}>Concluídas</th>
                        <th style={{ textAlign: 'center' }}>Em Andamento</th>
                        <th style={{ textAlign: 'center' }}>Impedimentos</th>
                        <th style={{ textAlign: 'center' }}>Atrasadas</th>
                        <th style={{ textAlign: 'center' }}>Prazo Médio</th>
                        <th style={{ textAlign: 'center' }}>Taxa Conclusão</th>
                        <th style={{ textAlign: 'right' }}>Valor Total Gerenciado</th>
                      </tr>
                    </thead>
                    <tbody>
                      {(dadosOsTecnicos?.tecnicos || []).map((t: any) => (
                        <tr key={t.tecnico}>
                          <td style={{ fontWeight: 700, color: 'var(--text-main)' }}>{t.tecnico}</td>
                          <td style={{ textAlign: 'center', fontWeight: 700 }}>{t.totalOrdens}</td>
                          <td style={{ textAlign: 'center', color: '#10b981', fontWeight: 700 }}>{t.concluidas}</td>
                          <td style={{ textAlign: 'center', color: 'var(--blue-primary)', fontWeight: 700 }}>{t.emAndamento}</td>
                          <td style={{ textAlign: 'center', color: '#f59e0b', fontWeight: 700 }}>{t.comImpedimento}</td>
                          <td style={{ textAlign: 'center', color: '#ef4444', fontWeight: 700 }}>{t.atrasadas}</td>
                          <td style={{ textAlign: 'center' }}>{t.prazoMedioDias} dias</td>
                          <td style={{ textAlign: 'center' }}>
                            <div style={{ display: 'flex', alignItems: 'center', gap: '6px', justifyContent: 'center' }}>
                              <div style={{ width: '60px', height: '6px', background: '#e2e8f0', borderRadius: '3px', overflow: 'hidden' }}>
                                <div style={{ width: `${Math.min(100, t.taxaConclusao)}%`, height: '100%', background: '#10b981' }} />
                              </div>
                              <span style={{ fontSize: '11px', fontWeight: 700 }}>{t.taxaConclusao}%</span>
                            </div>
                          </td>
                          <td style={{ textAlign: 'right', fontWeight: 800 }}>
                            {t.valorTotalGerenciado.toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' })}
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              </div>
            </div>
          )}

          {categoriaAtiva === 'servicos' && subRelatorio === 'os-custo-receita' && (
            <div>
              {/* KPIs Custo x Receita */}
              <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(220px, 1fr))', gap: '16px', marginBottom: '24px' }}>
                <div className="card" style={{ borderLeft: '4px solid var(--blue-primary)', padding: '18px' }}>
                  <div style={{ fontSize: '12px', fontWeight: 700, color: 'var(--text-muted)' }}>RECEITA TOTAL (FATURADO)</div>
                  <div style={{ fontSize: '24px', fontWeight: 800, color: 'var(--text-main)', marginTop: '4px' }}>
                    {(dadosOsCustoReceita?.kpis?.receitaTotal || 0).toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' })}
                  </div>
                </div>

                <div className="card" style={{ borderLeft: '4px solid #ef4444', padding: '18px' }}>
                  <div style={{ fontSize: '12px', fontWeight: 700, color: 'var(--text-muted)' }}>CUSTO TOTAL CONSOLIDADO</div>
                  <div style={{ fontSize: '24px', fontWeight: 800, color: '#dc2626', marginTop: '4px' }}>
                    {(dadosOsCustoReceita?.kpis?.custoTotal || 0).toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' })}
                  </div>
                </div>

                <div className="card" style={{ borderLeft: '4px solid #10b981', padding: '18px' }}>
                  <div style={{ fontSize: '12px', fontWeight: 700, color: 'var(--text-muted)' }}>LUCRO BRUTO CONSOLIDADO</div>
                  <div style={{ fontSize: '24px', fontWeight: 800, color: '#059669', marginTop: '4px' }}>
                    {(dadosOsCustoReceita?.kpis?.lucroBruto || 0).toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' })}
                  </div>
                </div>

                <div className="card" style={{ borderLeft: '4px solid #8b5cf6', padding: '18px' }}>
                  <div style={{ fontSize: '12px', fontWeight: 700, color: 'var(--text-muted)' }}>MARGEM MÉDIA BRUTA</div>
                  <div style={{ fontSize: '24px', fontWeight: 800, color: '#7c3aed', marginTop: '4px' }}>
                    {dadosOsCustoReceita?.kpis?.margemMediaPercentual || 0}%
                  </div>
                </div>
              </div>

              {/* Tabela de Rentabilidade por OS */}
              <div className="card" style={{ padding: '20px' }}>
                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '14px' }}>
                  <h3 style={{ fontSize: '16px', fontWeight: 700, margin: 0 }}>Rentabilidade e Apuração de Custos por OS</h3>
                  <button
                    onClick={() => exportarCsv('os_rentabilidade', dadosOsCustoReceita?.ordens || [], [
                      { chave: 'numero', rotulo: 'Número OS' },
                      { chave: 'fornecedorNome', rotulo: 'Prestador' },
                      { chave: 'receita', rotulo: 'Receita Total' },
                      { chave: 'custoMaoDeObra', rotulo: 'Custo M.O.' },
                      { chave: 'custoPecas', rotulo: 'Custo Peças' },
                      { chave: 'custoOutros', rotulo: 'Outros Custos' },
                      { chave: 'custoTotal', rotulo: 'Custo Total' },
                      { chave: 'lucroBruto', rotulo: 'Lucro Bruto' },
                      { chave: 'margemPercentual', rotulo: 'Margem %' }
                    ])}
                    className="btn btn-secondary"
                    style={{ fontSize: '12.5px', padding: '6px 12px' }}
                  >
                    <Download size={14} /> Exportar CSV
                  </button>
                </div>

                <div className="table-container">
                  <table>
                    <thead>
                      <tr>
                        <th>Número</th>
                        <th>Prestador</th>
                        <th style={{ textAlign: 'right' }}>Receita</th>
                        <th style={{ textAlign: 'right' }}>Mão de Obra</th>
                        <th style={{ textAlign: 'right' }}>Peças / Material</th>
                        <th style={{ textAlign: 'right' }}>Outros</th>
                        <th style={{ textAlign: 'right' }}>Custo Total</th>
                        <th style={{ textAlign: 'right' }}>Lucro Bruto</th>
                        <th style={{ textAlign: 'center' }}>Margem %</th>
                        <th style={{ textAlign: 'center' }}>Ação</th>
                      </tr>
                    </thead>
                    <tbody>
                      {(dadosOsCustoReceita?.ordens || []).slice((paginaAtual - 1) * ITENS_POR_PAGINA, paginaAtual * ITENS_POR_PAGINA).map((os: any) => (
                        <tr key={os.id}>
                          <td style={{ fontWeight: 700 }}>
                            {os.numero}
                            {os.possuiApontamentoReal ? (
                              <span className="badge badge-green" style={{ display: 'block', width: 'fit-content', marginTop: '2px', fontSize: '9px' }}>
                                Real Apontado
                              </span>
                            ) : (
                              <span className="badge badge-gray" style={{ display: 'block', width: 'fit-content', marginTop: '2px', fontSize: '9px' }}>
                                Previsto
                              </span>
                            )}
                          </td>
                          <td>{os.fornecedorNome}</td>
                          <td style={{ textAlign: 'right', fontWeight: 700 }}>
                            {os.receita.toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' })}
                          </td>
                          <td style={{ textAlign: 'right', color: 'var(--text-muted)' }}>
                            {os.custoMaoDeObra.toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' })}
                          </td>
                          <td style={{ textAlign: 'right', color: 'var(--text-muted)' }}>
                            {os.custoPecas.toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' })}
                          </td>
                          <td style={{ textAlign: 'right', color: 'var(--text-muted)' }}>
                            {os.custoOutros.toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' })}
                          </td>
                          <td style={{ textAlign: 'right', fontWeight: 700, color: '#dc2626' }}>
                            {os.custoTotal.toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' })}
                          </td>
                          <td style={{ textAlign: 'right', fontWeight: 800, color: os.lucroBruto >= 0 ? '#059669' : '#dc2626' }}>
                            {os.lucroBruto.toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' })}
                          </td>
                          <td style={{ textAlign: 'center', fontWeight: 700, color: os.margemPercentual >= 0 ? '#059669' : '#dc2626' }}>
                            {os.margemPercentual}%
                          </td>
                          <td style={{ textAlign: 'center' }}>
                            <button
                              onClick={() => abrirModalCustos(os)}
                              className="btn btn-secondary"
                              style={{ fontSize: '11.5px', padding: '4px 8px' }}
                            >
                              Lançar Custos
                            </button>
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>

                {renderPaginacao(dadosOsCustoReceita?.ordens?.length || 0)}
              </div>
            </div>
          )}

          {/* ========================================================================= */}
          {/* CATEGORIA 2: COTAÇÕES */}
          {/* ========================================================================= */}
          {categoriaAtiva === 'cotacoes' && subRelatorio === 'mapa' && (
            <div>
              {dadosCotacoesMapa?.vazio ? (
                <div className="card" style={{ padding: '40px', textAlign: 'center', color: 'var(--text-muted)' }}>
                  {dadosCotacoesMapa.mensagem}
                </div>
              ) : (
                <div className="card" style={{ padding: '20px' }}>
                  {/* Cabeçalho da Cotação */}
                  <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '20px', flexWrap: 'wrap', gap: '12px' }}>
                    <div>
                      <h3 style={{ fontSize: '18px', fontWeight: 800, margin: '0 0 4px 0' }}>
                        {dadosCotacoesMapa?.cotacao?.numero} — {dadosCotacoesMapa?.cotacao?.titulo}
                      </h3>
                      <div style={{ fontSize: '13px', color: 'var(--text-muted)' }}>
                        Abertura: {dadosCotacoesMapa?.cotacao?.dataAbertura} • Data Limite: {dadosCotacoesMapa?.cotacao?.dataLimite}
                      </div>
                    </div>

                    <div style={{ display: 'flex', gap: '10px' }}>
                      <button onClick={exportarPdf} className="btn btn-secondary">
                        <Printer size={15} /> Imprimir Mapa
                      </button>
                    </div>
                  </div>

                  {/* Matriz Comparativa Lado a Lado */}
                  <div className="table-container" style={{ overflowX: 'auto' }}>
                    <table style={{ borderCollapse: 'collapse', width: '100%' }}>
                      <thead>
                        <tr style={{ background: '#f8fafc' }}>
                          <th style={{ minWidth: '220px', borderRight: '2px solid var(--border)' }}>Item / Especificação</th>
                          <th style={{ textAlign: 'center', width: '80px', borderRight: '2px solid var(--border)' }}>Qtd / Ref</th>
                          {(dadosCotacoesMapa?.fornecedores || []).map((f: any) => (
                            <th key={f.fornecedorId} style={{ textAlign: 'center', minWidth: '180px', background: f.aprovada ? '#f0fdf4' : '#fff' }}>
                              <div style={{ fontWeight: 800, fontSize: '13px' }}>{f.nome}</div>
                              <div style={{ fontSize: '11px', color: 'var(--text-muted)' }}>{f.cnpj}</div>
                              {f.aprovada && (
                                <span className="badge badge-green" style={{ marginTop: '4px' }}>
                                  🏆 Vencedor
                                </span>
                              )}
                              <div style={{ fontSize: '11px', marginTop: '4px', fontWeight: 600 }}>
                                Prazo: {f.prazoEntregaDias}d • Frete: {f.frete.toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' })}
                              </div>
                            </th>
                          ))}
                        </tr>
                      </thead>
                      <tbody>
                        {(dadosCotacoesMapa?.itens || []).map((it: any) => (
                          <tr key={it.id}>
                            <td style={{ borderRight: '2px solid var(--border)' }}>
                              <div style={{ fontWeight: 700 }}>{it.descricao}</div>
                              <div style={{ fontSize: '11px', color: 'var(--text-muted)' }}>Cód: {it.codigo} • UM: {it.unidade}</div>
                            </td>
                            <td style={{ textAlign: 'center', borderRight: '2px solid var(--border)' }}>
                              <div style={{ fontWeight: 800 }}>{it.quantidade}</div>
                              <div style={{ fontSize: '11px', color: 'var(--text-muted)' }}>
                                Ref: {it.ultimoPrecoRef.toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' })}
                              </div>
                            </td>
                            {(dadosCotacoesMapa?.fornecedores || []).map((f: any) => {
                              const prop = it.propostas[f.fornecedorId];
                              const preco = prop?.precoUnitario || 0;
                              const subtotal = prop?.subtotal || 0;
                              const isMenor = preco > 0 && preco <= it.menorPreco;

                              return (
                                <td key={f.fornecedorId} style={{ textAlign: 'center', background: isMenor ? '#f0fdf4' : '#fff' }}>
                                  {preco > 0 ? (
                                    <>
                                      <div style={{ fontWeight: 800, color: isMenor ? '#166534' : 'var(--text-main)', fontSize: '13.5px' }}>
                                        {preco.toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' })}
                                        {isMenor && <span style={{ marginLeft: '4px', fontSize: '10px' }}>⭐ Menor</span>}
                                      </div>
                                      <div style={{ fontSize: '11px', color: 'var(--text-muted)' }}>
                                        Total: {subtotal.toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' })}
                                      </div>
                                    </>
                                  ) : (
                                    <span style={{ color: 'var(--text-muted)', fontSize: '12px' }}>Não cotado</span>
                                  )}
                                </td>
                              );
                            })}
                          </tr>
                        ))}

                        {/* Linha de Totais da Proposta */}
                        <tr style={{ background: '#f8fafc', borderTop: '2px solid var(--border)' }}>
                          <td style={{ fontWeight: 800, fontSize: '13px', borderRight: '2px solid var(--border)' }}>
                            TOTAL DA PROPOSTA
                          </td>
                          <td style={{ borderRight: '2px solid var(--border)' }}></td>
                          {(dadosCotacoesMapa?.fornecedores || []).map((f: any) => (
                            <td key={f.fornecedorId} style={{ textAlign: 'center', fontWeight: 900, fontSize: '14px', color: f.aprovada ? '#166534' : 'var(--text-main)' }}>
                              {f.valorTotalProposta.toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' })}
                            </td>
                          ))}
                        </tr>
                      </tbody>
                    </table>
                  </div>
                </div>
              )}
            </div>
          )}

          {categoriaAtiva === 'cotacoes' && subRelatorio === 'saving' && (
            <div>
              {/* KPIs de Saving */}
              <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(220px, 1fr))', gap: '16px', marginBottom: '24px' }}>
                <div className="card" style={{ borderLeft: '4px solid var(--blue-primary)', padding: '18px' }}>
                  <div style={{ fontSize: '12px', fontWeight: 700, color: 'var(--text-muted)' }}>TOTAL NEGOCIADO</div>
                  <div style={{ fontSize: '24px', fontWeight: 800, color: 'var(--text-main)', marginTop: '4px' }}>
                    {(dadosCotacoesSaving?.kpis?.totalNegociado || 0).toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' })}
                  </div>
                </div>

                <div className="card" style={{ borderLeft: '4px solid #10b981', padding: '18px' }}>
                  <div style={{ fontSize: '12px', fontWeight: 700, color: 'var(--text-muted)' }}>SAVING TOTAL REALIZADO</div>
                  <div style={{ fontSize: '24px', fontWeight: 800, color: '#059669', marginTop: '4px' }}>
                    {(dadosCotacoesSaving?.kpis?.savingTotalRealizado || 0).toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' })}
                  </div>
                </div>

                <div className="card" style={{ borderLeft: '4px solid #8b5cf6', padding: '18px' }}>
                  <div style={{ fontSize: '12px', fontWeight: 700, color: 'var(--text-muted)' }}>ECONOMIA MÉDIA (%)</div>
                  <div style={{ fontSize: '24px', fontWeight: 800, color: '#7c3aed', marginTop: '4px' }}>
                    {dadosCotacoesSaving?.kpis?.percentualMedio || 0}%
                  </div>
                </div>

                <div className="card" style={{ borderLeft: '4px solid #f59e0b', padding: '18px' }}>
                  <div style={{ fontSize: '12px', fontWeight: 700, color: 'var(--text-muted)' }}>COTAÇÕES COM ECONOMIA</div>
                  <div style={{ fontSize: '24px', fontWeight: 800, color: '#d97706', marginTop: '4px' }}>
                    {dadosCotacoesSaving?.kpis?.totalCotacoesAprovadas || 0}
                  </div>
                </div>
              </div>

              {/* Tabela de Saving */}
              <div className="card" style={{ padding: '20px' }}>
                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '14px' }}>
                  <h3 style={{ fontSize: '16px', fontWeight: 700, margin: 0 }}>Demonstrativo Detalhado de Economia por Cotação</h3>
                  <button
                    onClick={() => exportarCsv('cotacoes_saving', dadosCotacoesSaving?.detalhes || [], [
                      { chave: 'numero', rotulo: 'Cotação' },
                      { chave: 'titulo', rotulo: 'Título' },
                      { chave: 'data', rotulo: 'Data' },
                      { chave: 'fornecedores', rotulo: 'Fornecedor Vencedor' },
                      { chave: 'valorReferencia', rotulo: 'Base Referência' },
                      { chave: 'totalNegociado', rotulo: 'Total Fechado' },
                      { chave: 'savingRealizado', rotulo: 'Economia Realizada' },
                      { chave: 'percentualSaving', rotulo: 'Economia %' }
                    ])}
                    className="btn btn-secondary"
                    style={{ fontSize: '12.5px', padding: '6px 12px' }}
                  >
                    <Download size={14} /> Exportar CSV
                  </button>
                </div>

                <div className="table-container">
                  <table>
                    <thead>
                      <tr>
                        <th>Cotação</th>
                        <th>Fornecedor Vencedor</th>
                        <th>Data</th>
                        <th style={{ textAlign: 'right' }}>Base Referência</th>
                        <th style={{ textAlign: 'right' }}>Total Negociado</th>
                        <th style={{ textAlign: 'right' }}>Economia Obtida</th>
                        <th style={{ textAlign: 'center' }}>% Economia</th>
                      </tr>
                    </thead>
                    <tbody>
                      {(dadosCotacoesSaving?.detalhes || []).slice((paginaAtual - 1) * ITENS_POR_PAGINA, paginaAtual * ITENS_POR_PAGINA).map((c: any) => (
                        <tr key={c.id}>
                          <td style={{ fontWeight: 700 }}>
                            {c.numero}
                            <div style={{ fontSize: '11px', color: 'var(--text-muted)' }}>{c.titulo}</div>
                          </td>
                          <td style={{ fontWeight: 600 }}>{c.fornecedores || '-'}</td>
                          <td>{new Date(c.data).toLocaleDateString('pt-BR')}</td>
                          <td style={{ textAlign: 'right', color: 'var(--text-muted)' }}>
                            {c.valorReferencia.toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' })}
                          </td>
                          <td style={{ textAlign: 'right', fontWeight: 700 }}>
                            {c.totalNegociado.toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' })}
                          </td>
                          <td style={{ textAlign: 'right', fontWeight: 800, color: '#10b981' }}>
                            {c.savingRealizado.toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' })}
                          </td>
                          <td style={{ textAlign: 'center' }}>
                            <span className="badge badge-green" style={{ fontWeight: 700 }}>
                              {c.percentualSaving}%
                            </span>
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>

                {renderPaginacao(dadosCotacoesSaving?.detalhes?.length || 0)}
              </div>
            </div>
          )}

          {categoriaAtiva === 'cotacoes' && subRelatorio === 'funil' && (
            <div>
              <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(200px, 1fr))', gap: '16px', marginBottom: '24px' }}>
                <div className="card" style={{ padding: '18px', borderLeft: '4px solid var(--blue-primary)' }}>
                  <div style={{ fontSize: '12px', fontWeight: 700, color: 'var(--text-muted)' }}>TOTAL NO PERÍODO</div>
                  <div style={{ fontSize: '24px', fontWeight: 800, color: 'var(--text-main)', marginTop: '4px' }}>
                    {dadosCotacoesFunil?.kpis?.total || 0}
                  </div>
                </div>

                <div className="card" style={{ padding: '18px', borderLeft: '4px solid #f59e0b' }}>
                  <div style={{ fontSize: '12px', fontWeight: 700, color: 'var(--text-muted)' }}>EM ABERTO</div>
                  <div style={{ fontSize: '24px', fontWeight: 800, color: '#d97706', marginTop: '4px' }}>
                    {dadosCotacoesFunil?.kpis?.abertas || 0}
                  </div>
                </div>

                <div className="card" style={{ padding: '18px', borderLeft: '4px solid #10b981' }}>
                  <div style={{ fontSize: '12px', fontWeight: 700, color: 'var(--text-muted)' }}>RESPONDIDAS</div>
                  <div style={{ fontSize: '24px', fontWeight: 800, color: '#059669', marginTop: '4px' }}>
                    {dadosCotacoesFunil?.kpis?.respondidas || 0}
                  </div>
                </div>

                <div className="card" style={{ padding: '18px', borderLeft: '4px solid #ef4444' }}>
                  <div style={{ fontSize: '12px', fontWeight: 700, color: 'var(--text-muted)' }}>VENCIDAS / EXPIRADAS</div>
                  <div style={{ fontSize: '24px', fontWeight: 800, color: '#dc2626', marginTop: '4px' }}>
                    {dadosCotacoesFunil?.kpis?.vencidas || 0}
                  </div>
                </div>

                <div className="card" style={{ padding: '18px', borderLeft: '4px solid #8b5cf6' }}>
                  <div style={{ fontSize: '12px', fontWeight: 700, color: 'var(--text-muted)' }}>TAXA DE RESPOSTA</div>
                  <div style={{ fontSize: '24px', fontWeight: 800, color: '#7c3aed', marginTop: '4px' }}>
                    {dadosCotacoesFunil?.kpis?.taxaResposta || 0}%
                  </div>
                </div>
              </div>

              <div className="card" style={{ padding: '20px' }}>
                <h3 style={{ fontSize: '16px', fontWeight: 700, marginBottom: '14px' }}>Status Operacional de Cotações</h3>
                <div className="table-container">
                  <table>
                    <thead>
                      <tr>
                        <th>Número</th>
                        <th>Título</th>
                        <th>Status</th>
                        <th>Data Criação</th>
                        <th>Data Limite</th>
                        <th style={{ textAlign: 'center' }}>Convites</th>
                        <th style={{ textAlign: 'center' }}>Respostas</th>
                        <th style={{ textAlign: 'right' }}>Total Fechado</th>
                      </tr>
                    </thead>
                    <tbody>
                      {(dadosCotacoesFunil?.cotacoes || []).slice((paginaAtual - 1) * ITENS_POR_PAGINA, paginaAtual * ITENS_POR_PAGINA).map((c: any) => (
                        <tr key={c.id}>
                          <td style={{ fontWeight: 700 }}>{c.numero}</td>
                          <td>{c.titulo}</td>
                          <td>
                            <span className={`badge ${c.status === 'Aprovada' ? 'badge-green' : c.status === 'Vencida' ? 'badge-red' : 'badge-yellow'}`}>
                              {c.status}
                            </span>
                          </td>
                          <td>{new Date(c.dataCriacao).toLocaleDateString('pt-BR')}</td>
                          <td>{new Date(c.dataLimite).toLocaleDateString('pt-BR')}</td>
                          <td style={{ textAlign: 'center', fontWeight: 600 }}>{c.convites}</td>
                          <td style={{ textAlign: 'center', fontWeight: 700, color: '#10b981' }}>{c.respostas}</td>
                          <td style={{ textAlign: 'right', fontWeight: 800 }}>
                            {c.totalAprovado > 0 ? c.totalAprovado.toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' }) : '-'}
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>

                {renderPaginacao(dadosCotacoesFunil?.cotacoes?.length || 0)}
              </div>
            </div>
          )}

          {/* ========================================================================= */}
          {/* CATEGORIA 3: PEDIDOS DE COMPRA */}
          {/* ========================================================================= */}
          {categoriaAtiva === 'pedidos' && subRelatorio === 'pendentes' && (
            <div>
              {/* KPIs de Pedidos Pendentes */}
              <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(220px, 1fr))', gap: '16px', marginBottom: '24px' }}>
                <div className="card" style={{ borderLeft: '4px solid var(--blue-primary)', padding: '18px' }}>
                  <div style={{ fontSize: '12px', fontWeight: 700, color: 'var(--text-muted)' }}>PEDIDOS PENDENTES</div>
                  <div style={{ fontSize: '24px', fontWeight: 800, color: 'var(--text-main)', marginTop: '4px' }}>
                    {dadosPedidosPendentes?.kpis?.totalPendentes || 0}
                  </div>
                  <div style={{ fontSize: '11.5px', color: 'var(--text-muted)', marginTop: '4px' }}>Aguardando entrega</div>
                </div>

                <div className="card" style={{ borderLeft: '4px solid #ef4444', padding: '18px' }}>
                  <div style={{ fontSize: '12px', fontWeight: 700, color: 'var(--text-muted)' }}>PEDIDOS EM ATRASO</div>
                  <div style={{ fontSize: '24px', fontWeight: 800, color: '#dc2626', marginTop: '4px' }}>
                    {dadosPedidosPendentes?.kpis?.totalAtrasados || 0}
                  </div>
                  <div style={{ fontSize: '11.5px', color: 'var(--text-muted)', marginTop: '4px' }}>Previsão de entrega estourada</div>
                </div>

                <div className="card" style={{ borderLeft: '4px solid #10b981', padding: '18px' }}>
                  <div style={{ fontSize: '12px', fontWeight: 700, color: 'var(--text-muted)' }}>SALDO VALOR PENDENTE</div>
                  <div style={{ fontSize: '22px', fontWeight: 800, color: '#059669', marginTop: '4px' }}>
                    {(dadosPedidosPendentes?.kpis?.valorPendenteTotal || 0).toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' })}
                  </div>
                  <div style={{ fontSize: '11.5px', color: 'var(--text-muted)', marginTop: '4px' }}>Mercadorias a receber</div>
                </div>
              </div>

              {/* Tabela de Pedidos Pendentes com Suporte a Recebimento Parcial */}
              <div className="card" style={{ padding: '20px' }}>
                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '14px' }}>
                  <h3 style={{ fontSize: '16px', fontWeight: 700, margin: 0 }}>Pedidos com Entregas Pendentes ou Parciais</h3>
                  <button
                    onClick={() => exportarCsv('pedidos_pendentes', dadosPedidosPendentes?.pedidos || [], [
                      { chave: 'numero', rotulo: 'Número Pedido' },
                      { chave: 'fornecedorNome', rotulo: 'Fornecedor' },
                      { chave: 'status', rotulo: 'Status' },
                      { chave: 'dataEmissao', rotulo: 'Data Emissão' },
                      { chave: 'previsaoEntrega', rotulo: 'Previsão Entrega' },
                      { chave: 'diasAtraso', rotulo: 'Dias Atraso' },
                      { chave: 'percentualRecebido', rotulo: '% Recebido' },
                      { chave: 'valorTotal', rotulo: 'Valor Total' },
                      { chave: 'saldoValorPendente', rotulo: 'Saldo Pendente' }
                    ])}
                    className="btn btn-secondary"
                    style={{ fontSize: '12.5px', padding: '6px 12px' }}
                  >
                    <Download size={14} /> Exportar CSV
                  </button>
                </div>

                <div style={{ display: 'flex', flexDirection: 'column', gap: '16px' }}>
                  {(dadosPedidosPendentes?.pedidos || []).slice((paginaAtual - 1) * ITENS_POR_PAGINA, paginaAtual * ITENS_POR_PAGINA).map((p: any) => (
                    <div key={p.id} style={{ border: '1px solid var(--border)', borderRadius: '8px', padding: '16px', background: p.estaAtrasado ? '#fffbfb' : '#fff' }}>
                      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', marginBottom: '12px', flexWrap: 'wrap', gap: '8px' }}>
                        <div>
                          <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
                            <span style={{ fontWeight: 800, fontSize: '16px' }}>{p.numero}</span>
                            <span className={`badge ${p.status === 'ParcialmenteRecebido' ? 'badge-yellow' : p.status === 'Recebido' ? 'badge-green' : 'badge-blue'}`}>
                              {p.status}
                            </span>
                            {p.estaAtrasado && (
                              <span className="badge badge-red">
                                ⚠️ Atrasado ({p.diasAtraso} dias)
                              </span>
                            )}
                          </div>
                          <div style={{ fontSize: '13px', color: 'var(--text-main)', marginTop: '2px', fontWeight: 600 }}>
                            Fornecedor: {p.fornecedorNome} ({p.fornecedorCnpj})
                          </div>
                          <div style={{ fontSize: '12px', color: 'var(--text-muted)' }}>
                            Emissão: {p.dataEmissao} • Previsão Entrega: <strong>{p.previsaoEntrega}</strong>
                          </div>
                        </div>

                        <div style={{ textAlign: 'right' }}>
                          <div style={{ fontSize: '11px', color: 'var(--text-muted)' }}>VALOR TOTAL / SALDO PENDENTE</div>
                          <div style={{ fontSize: '16px', fontWeight: 800, color: 'var(--text-main)' }}>
                            {p.valorTotal.toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' })}
                          </div>
                          <div style={{ fontSize: '13px', fontWeight: 700, color: '#dc2626' }}>
                            Saldo: {p.saldoValorPendente.toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' })}
                          </div>
                          <div style={{ fontSize: '11px', color: '#10b981', fontWeight: 600 }}>
                            {p.percentualRecebido}% recebido
                          </div>
                        </div>
                      </div>

                      {/* Itens do Pedido */}
                      <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: '11.5px', marginTop: '8px' }}>
                        <thead>
                          <tr style={{ background: '#f8fafc', borderBottom: '1px solid var(--border)' }}>
                            <th style={{ textAlign: 'left', padding: '6px' }}>Material / Produto</th>
                            <th style={{ textAlign: 'center', padding: '6px' }}>UM</th>
                            <th style={{ textAlign: 'right', padding: '6px' }}>Comprada</th>
                            <th style={{ textAlign: 'right', padding: '6px' }}>Recebida</th>
                            <th style={{ textAlign: 'right', padding: '6px' }}>Saldo a Receber</th>
                            <th style={{ textAlign: 'center', padding: '6px' }}>Atendimento</th>
                            <th style={{ textAlign: 'center', padding: '6px' }}>Ação</th>
                          </tr>
                        </thead>
                        <tbody>
                          {(p.itens || []).map((it: any) => (
                            <tr key={it.id} style={{ borderBottom: '1px solid #f1f5f9' }}>
                              <td style={{ padding: '6px', fontWeight: 600 }}>
                                {it.descricao}
                                <span style={{ fontSize: '10.5px', color: 'var(--text-muted)', marginLeft: '6px' }}>({it.codigo})</span>
                              </td>
                              <td style={{ textAlign: 'center', padding: '6px' }}>{it.unidade}</td>
                              <td style={{ textAlign: 'right', padding: '6px', fontWeight: 700 }}>{it.quantidade}</td>
                              <td style={{ textAlign: 'right', padding: '6px', color: '#10b981', fontWeight: 700 }}>{it.quantidadeRecebida}</td>
                              <td style={{ textAlign: 'right', padding: '6px', color: it.saldoPendente > 0 ? '#dc2626' : '#10b981', fontWeight: 800 }}>
                                {it.saldoPendente}
                              </td>
                              <td style={{ textAlign: 'center', padding: '6px' }}>
                                <span className={`badge ${it.percentualAtendido >= 100 ? 'badge-green' : it.percentualAtendido > 0 ? 'badge-yellow' : 'badge-gray'}`}>
                                  {it.percentualAtendido}%
                                </span>
                              </td>
                              <td style={{ textAlign: 'center', padding: '6px' }}>
                                {it.saldoPendente > 0 ? (
                                  <button
                                    onClick={() => abrirModalReceber(p, it)}
                                    className="btn btn-secondary"
                                    style={{ fontSize: '11px', padding: '3px 8px' }}
                                  >
                                    <PackageCheck size={12} /> Receber
                                  </button>
                                ) : (
                                  <span style={{ fontSize: '11px', color: '#10b981', fontWeight: 700 }}>Concluído</span>
                                )}
                              </td>
                            </tr>
                          ))}
                        </tbody>
                      </table>
                    </div>
                  ))}
                </div>

                {renderPaginacao(dadosPedidosPendentes?.pedidos?.length || 0)}
              </div>
            </div>
          )}

          {categoriaAtiva === 'pedidos' && subRelatorio === 'curva-abc' && (
            <div>
              {/* KPIs Curva ABC */}
              <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(220px, 1fr))', gap: '16px', marginBottom: '24px' }}>
                <div className="card" style={{ borderLeft: '4px solid var(--blue-primary)', padding: '18px' }}>
                  <div style={{ fontSize: '12px', fontWeight: 700, color: 'var(--text-muted)' }}>TOTAL COMPRADO GERAL</div>
                  <div style={{ fontSize: '24px', fontWeight: 800, color: 'var(--text-main)', marginTop: '4px' }}>
                    {(dadosCurvaAbc?.kpis?.totalGeralComprado || 0).toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' })}
                  </div>
                </div>

                <div className="card" style={{ borderLeft: '4px solid #10b981', padding: '18px' }}>
                  <div style={{ fontSize: '12px', fontWeight: 700, color: 'var(--text-muted)' }}>CLASSE A (80% DO VOLUME)</div>
                  <div style={{ fontSize: '24px', fontWeight: 800, color: '#059669', marginTop: '4px' }}>
                    {dadosCurvaAbc?.kpis?.qtdClasseA || 0} fornecedores
                  </div>
                </div>

                <div className="card" style={{ borderLeft: '4px solid #f59e0b', padding: '18px' }}>
                  <div style={{ fontSize: '12px', fontWeight: 700, color: 'var(--text-muted)' }}>CLASSE B (15% DO VOLUME)</div>
                  <div style={{ fontSize: '24px', fontWeight: 800, color: '#d97706', marginTop: '4px' }}>
                    {dadosCurvaAbc?.kpis?.qtdClasseB || 0} fornecedores
                  </div>
                </div>

                <div className="card" style={{ borderLeft: '4px solid #64748b', padding: '18px' }}>
                  <div style={{ fontSize: '12px', fontWeight: 700, color: 'var(--text-muted)' }}>CLASSE C (5% DO VOLUME)</div>
                  <div style={{ fontSize: '24px', fontWeight: 800, color: '#475569', marginTop: '4px' }}>
                    {dadosCurvaAbc?.kpis?.qtdClasseC || 0} fornecedores
                  </div>
                </div>
              </div>

              {/* Tabela Curva ABC */}
              <div className="card" style={{ padding: '20px' }}>
                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '14px' }}>
                  <h3 style={{ fontSize: '16px', fontWeight: 700, margin: 0, display: 'flex', alignItems: 'center', gap: '8px' }}>
                    <Layers size={18} color="var(--blue-primary)" />
                    Classificação Pareto de Fornecedores (Curva ABC)
                  </h3>
                  <button
                    onClick={() => exportarCsv('curva_abc_fornecedores', dadosCurvaAbc?.ranking || [], [
                      { chave: 'posicao', rotulo: 'Ranking' },
                      { chave: 'classe', rotulo: 'Classe' },
                      { chave: 'nome', rotulo: 'Fornecedor' },
                      { chave: 'cnpj', rotulo: 'CNPJ' },
                      { chave: 'totalComprado', rotulo: 'Total Comprado' },
                      { chave: 'qtdPedidos', rotulo: 'Qtd Pedidos' },
                      { chave: 'ticketMedio', rotulo: 'Ticket Médio' },
                      { chave: 'percentualIndividual', rotulo: '% Individual' },
                      { chave: 'percentualAcumulado', rotulo: '% Acumulado' }
                    ])}
                    className="btn btn-secondary"
                    style={{ fontSize: '12.5px', padding: '6px 12px' }}
                  >
                    <Download size={14} /> Exportar CSV
                  </button>
                </div>

                <div className="table-container">
                  <table>
                    <thead>
                      <tr>
                        <th style={{ width: '60px', textAlign: 'center' }}>Pos.</th>
                        <th style={{ width: '80px', textAlign: 'center' }}>Classe</th>
                        <th>Fornecedor</th>
                        <th style={{ textAlign: 'center' }}>Pedidos</th>
                        <th style={{ textAlign: 'right' }}>Total Comprado</th>
                        <th style={{ textAlign: 'right' }}>Ticket Médio</th>
                        <th style={{ textAlign: 'center' }}>% Individual</th>
                        <th style={{ textAlign: 'center' }}>% Acumulado</th>
                      </tr>
                    </thead>
                    <tbody>
                      {(dadosCurvaAbc?.ranking || []).map((f: any) => (
                        <tr key={f.fornecedorId}>
                          <td style={{ textAlign: 'center', fontWeight: 800 }}>#{f.posicao}</td>
                          <td style={{ textAlign: 'center' }}>
                            <span className={`badge ${f.classe === 'A' ? 'badge-green' : f.classe === 'B' ? 'badge-yellow' : 'badge-gray'}`} style={{ fontWeight: 800 }}>
                              Classe {f.classe}
                            </span>
                          </td>
                          <td>
                            <div style={{ fontWeight: 700 }}>{f.nome}</div>
                            <div style={{ fontSize: '11px', color: 'var(--text-muted)' }}>{f.cnpj} • {f.cidade}/{f.uf}</div>
                          </td>
                          <td style={{ textAlign: 'center', fontWeight: 700 }}>{f.qtdPedidos}</td>
                          <td style={{ textAlign: 'right', fontWeight: 800 }}>
                            {f.totalComprado.toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' })}
                          </td>
                          <td style={{ textAlign: 'right', color: 'var(--text-muted)' }}>
                            {f.ticketMedio.toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' })}
                          </td>
                          <td style={{ textAlign: 'center', fontWeight: 700 }}>{f.percentualIndividual}%</td>
                          <td style={{ textAlign: 'center' }}>
                            <div style={{ display: 'flex', alignItems: 'center', gap: '6px', justifyContent: 'center' }}>
                              <div style={{ width: '70px', height: '6px', background: '#e2e8f0', borderRadius: '3px', overflow: 'hidden' }}>
                                <div style={{ width: `${Math.min(100, f.percentualAcumulado)}%`, height: '100%', background: f.classe === 'A' ? '#10b981' : f.classe === 'B' ? '#f59e0b' : '#64748b' }} />
                              </div>
                              <span style={{ fontSize: '11px', fontWeight: 700 }}>{f.percentualAcumulado}%</span>
                            </div>
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              </div>
            </div>
          )}

          {categoriaAtiva === 'pedidos' && subRelatorio === 'pontualidade' && (
            <div>
              {/* KPIs de Pontualidade */}
              <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(220px, 1fr))', gap: '16px', marginBottom: '24px' }}>
                <div className="card" style={{ borderLeft: '4px solid #10b981', padding: '18px' }}>
                  <div style={{ fontSize: '12px', fontWeight: 700, color: 'var(--text-muted)' }}>ÍNDICE GERAL DE PONTUALIDADE</div>
                  <div style={{ fontSize: '24px', fontWeight: 800, color: '#059669', marginTop: '4px' }}>
                    {dadosPontualidade?.kpis?.taxaPontualidadeGlobal || 100}%
                  </div>
                  <div style={{ fontSize: '11.5px', color: 'var(--text-muted)', marginTop: '4px' }}>Entregas no prazo</div>
                </div>

                <div className="card" style={{ borderLeft: '4px solid var(--blue-primary)', padding: '18px' }}>
                  <div style={{ fontSize: '12px', fontWeight: 700, color: 'var(--text-muted)' }}>ENTREGAS AVALIADAS</div>
                  <div style={{ fontSize: '24px', fontWeight: 800, color: 'var(--text-main)', marginTop: '4px' }}>
                    {dadosPontualidade?.kpis?.totalEntregasAvaliadas || 0}
                  </div>
                </div>

                <div className="card" style={{ borderLeft: '4px solid #10b981', padding: '18px' }}>
                  <div style={{ fontSize: '12px', fontWeight: 700, color: 'var(--text-muted)' }}>ENTREGAS NO PRAZO</div>
                  <div style={{ fontSize: '24px', fontWeight: 800, color: '#059669', marginTop: '4px' }}>
                    {dadosPontualidade?.kpis?.totalEntregasNoPrazo || 0}
                  </div>
                </div>

                <div className="card" style={{ borderLeft: '4px solid #ef4444', padding: '18px' }}>
                  <div style={{ fontSize: '12px', fontWeight: 700, color: 'var(--text-muted)' }}>ENTREGAS COM ATRASO</div>
                  <div style={{ fontSize: '24px', fontWeight: 800, color: '#dc2626', marginTop: '4px' }}>
                    {dadosPontualidade?.kpis?.totalEntregasAtrasadas || 0}
                  </div>
                </div>
              </div>

              {/* Tabela de Pontualidade por Fornecedor */}
              <div className="card" style={{ padding: '20px' }}>
                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '14px' }}>
                  <h3 style={{ fontSize: '16px', fontWeight: 700, margin: 0, display: 'flex', alignItems: 'center', gap: '8px' }}>
                    <Truck size={18} color="var(--blue-primary)" />
                    Cumprimento de Prazos e Pontualidade por Fornecedor
                  </h3>
                  <button
                    onClick={() => exportarCsv('pontualidade_fornecedores', dadosPontualidade?.fornecedores || [], [
                      { chave: 'nome', rotulo: 'Fornecedor' },
                      { chave: 'cnpj', rotulo: 'CNPJ' },
                      { chave: 'totalPedidos', rotulo: 'Total Pedidos' },
                      { chave: 'entregasAvaliadas', rotulo: 'Entregas Avaliadas' },
                      { chave: 'entregasNoPrazo', rotulo: 'No Prazo' },
                      { chave: 'entregasComAtraso', rotulo: 'Com Atraso' },
                      { chave: 'indicePontualidade', rotulo: 'Pontualidade %' },
                      { chave: 'atrasoMedioDias', rotulo: 'Atraso Médio (dias)' },
                      { chave: 'classificacao', rotulo: 'Classificação' }
                    ])}
                    className="btn btn-secondary"
                    style={{ fontSize: '12.5px', padding: '6px 12px' }}
                  >
                    <Download size={14} /> Exportar CSV
                  </button>
                </div>

                <div className="table-container">
                  <table>
                    <thead>
                      <tr>
                        <th>Fornecedor</th>
                        <th style={{ textAlign: 'center' }}>Total Pedidos</th>
                        <th style={{ textAlign: 'center' }}>Avaliados</th>
                        <th style={{ textAlign: 'center' }}>No Prazo</th>
                        <th style={{ textAlign: 'center' }}>Com Atraso</th>
                        <th style={{ textAlign: 'center' }}>Índice de Pontualidade</th>
                        <th style={{ textAlign: 'center' }}>Atraso Médio</th>
                        <th style={{ textAlign: 'center' }}>Confiabilidade</th>
                      </tr>
                    </thead>
                    <tbody>
                      {(dadosPontualidade?.fornecedores || []).map((f: any) => (
                        <tr key={f.fornecedorId}>
                          <td>
                            <div style={{ fontWeight: 700 }}>{f.nome}</div>
                            <div style={{ fontSize: '11px', color: 'var(--text-muted)' }}>{f.cnpj}</div>
                          </td>
                          <td style={{ textAlign: 'center', fontWeight: 700 }}>{f.totalPedidos}</td>
                          <td style={{ textAlign: 'center' }}>{f.entregasAvaliadas}</td>
                          <td style={{ textAlign: 'center', color: '#10b981', fontWeight: 700 }}>{f.entregasNoPrazo}</td>
                          <td style={{ textAlign: 'center', color: f.entregasComAtraso > 0 ? '#ef4444' : 'var(--text-muted)', fontWeight: 700 }}>
                            {f.entregasComAtraso}
                          </td>
                          <td style={{ textAlign: 'center' }}>
                            <div style={{ display: 'flex', alignItems: 'center', gap: '6px', justifyContent: 'center' }}>
                              <div style={{ width: '60px', height: '6px', background: '#e2e8f0', borderRadius: '3px', overflow: 'hidden' }}>
                                <div style={{ width: `${Math.min(100, f.indicePontualidade)}%`, height: '100%', background: f.indicePontualidade >= 85 ? '#10b981' : '#f59e0b' }} />
                              </div>
                              <span style={{ fontSize: '11.5px', fontWeight: 700 }}>{f.indicePontualidade}%</span>
                            </div>
                          </td>
                          <td style={{ textAlign: 'center' }}>
                            {f.atrasoMedioDias > 0 ? `${f.atrasoMedioDias} dias` : '-'}
                          </td>
                          <td style={{ textAlign: 'center' }}>
                            <span className={`badge ${f.classificacao === 'Excelente' ? 'badge-green' : f.classificacao === 'Bom' ? 'badge-blue' : f.classificacao === 'Regular' ? 'badge-yellow' : 'badge-red'}`} style={{ fontWeight: 700 }}>
                              {f.classificacao}
                            </span>
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              </div>
            </div>
          )}
        </>
      )}

      {/* MODAL: APONTAR CUSTOS E IMPEDIMENTO DA OS */}
      {modalCustosAberto && osSelecionadaCustos && (
        <div style={{ position: 'fixed', inset: 0, background: 'rgba(0,0,0,0.5)', display: 'flex', alignItems: 'center', justifyContent: 'center', zIndex: 1000, padding: '16px' }}>
          <div className="card" style={{ maxWidth: '540px', width: '100%', padding: '24px', maxHeight: '90vh', overflowY: 'auto' }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '16px' }}>
              <h3 style={{ fontSize: '17px', fontWeight: 800, margin: 0 }}>
                Apontar Custos e Impedimento — {osSelecionadaCustos.Numero || osSelecionadaCustos.numero}
              </h3>
              <button onClick={() => setModalCustosAberto(false)} style={{ background: 'none', border: 'none', cursor: 'pointer' }}>
                <X size={20} />
              </button>
            </div>

            <form onSubmit={handleSalvarCustosOs} style={{ display: 'flex', flexDirection: 'column', gap: '14px' }}>
              <div>
                <label style={{ display: 'block', fontSize: '12px', fontWeight: 600, color: 'var(--text-muted)', marginBottom: '4px' }}>
                  Custo Real Mão de Obra (R$)
                </label>
                <input
                  type="number"
                  step="0.01"
                  className="input"
                  value={formCustos.custoRealMaoDeObra}
                  onChange={(e) => setFormCustos({ ...formCustos, custoRealMaoDeObra: parseFloat(e.target.value) || 0 })}
                  required
                />
              </div>

              <div>
                <label style={{ display: 'block', fontSize: '12px', fontWeight: 600, color: 'var(--text-muted)', marginBottom: '4px' }}>
                  Custo Real Peças / Materiais (R$)
                </label>
                <input
                  type="number"
                  step="0.01"
                  className="input"
                  value={formCustos.custoRealPecas}
                  onChange={(e) => setFormCustos({ ...formCustos, custoRealPecas: parseFloat(e.target.value) || 0 })}
                  required
                />
              </div>

              <div>
                <label style={{ display: 'block', fontSize: '12px', fontWeight: 600, color: 'var(--text-muted)', marginBottom: '4px' }}>
                  Outros Custos / Deslocamento (R$)
                </label>
                <input
                  type="number"
                  step="0.01"
                  className="input"
                  value={formCustos.custoRealOutros}
                  onChange={(e) => setFormCustos({ ...formCustos, custoRealOutros: parseFloat(e.target.value) || 0 })}
                />
              </div>

              <div>
                <label style={{ display: 'block', fontSize: '12px', fontWeight: 600, color: 'var(--text-muted)', marginBottom: '4px' }}>
                  Técnico / Responsável pela Execução
                </label>
                <input
                  type="text"
                  className="input"
                  placeholder="Nome do técnico"
                  value={formCustos.tecnicoResponsavelNome}
                  onChange={(e) => setFormCustos({ ...formCustos, tecnicoResponsavelNome: e.target.value })}
                />
              </div>

              <div>
                <label style={{ display: 'block', fontSize: '12px', fontWeight: 600, color: 'var(--text-muted)', marginBottom: '4px' }}>
                  Status de Impedimento
                </label>
                <select
                  className="input"
                  value={formCustos.statusImpedimento}
                  onChange={(e) => setFormCustos({ ...formCustos, statusImpedimento: e.target.value })}
                >
                  <option value="Nenhum">Sem Impedimento (Fluxo Normal)</option>
                  <option value="AguardandoPeça">Aguardando Peça</option>
                  <option value="AguardandoAprovação">Aguardando Aprovação</option>
                  <option value="AguardandoCliente">Aguardando Cliente</option>
                  <option value="ClimaDesfavoravel">Clima Desfavorável</option>
                  <option value="Pausada">Pausada</option>
                </select>
              </div>

              {formCustos.statusImpedimento !== 'Nenhum' && (
                <div>
                  <label style={{ display: 'block', fontSize: '12px', fontWeight: 600, color: 'var(--text-muted)', marginBottom: '4px' }}>
                    Motivo Detalhado do Impedimento
                  </label>
                  <textarea
                    className="input"
                    rows={2}
                    placeholder="Descreva o motivo da pausa ou espera..."
                    value={formCustos.motivoImpedimento}
                    onChange={(e) => setFormCustos({ ...formCustos, motivoImpedimento: e.target.value })}
                  />
                </div>
              )}

              <div>
                <label style={{ display: 'block', fontSize: '12px', fontWeight: 600, color: 'var(--text-muted)', marginBottom: '4px' }}>
                  Observações do Apontamento
                </label>
                <textarea
                  className="input"
                  rows={2}
                  placeholder="Histórico de custos ou evolução..."
                  value={formCustos.observacoes}
                  onChange={(e) => setFormCustos({ ...formCustos, observacoes: e.target.value })}
                />
              </div>

              <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '10px', marginTop: '12px' }}>
                <button type="button" onClick={() => setModalCustosAberto(false)} className="btn btn-secondary">
                  Cancelar
                </button>
                <button type="submit" disabled={salvandoCustos} className="btn btn-primary">
                  {salvandoCustos ? 'Salvando...' : 'Salvar Apontamento'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* MODAL: RECEBIMENTO PARCIAL OU TOTAL DE ITEM DE PEDIDO */}
      {modalReceberAberto && pedidoSelecionadoReceber && itemSelecionadoReceber && (
        <div style={{ position: 'fixed', inset: 0, background: 'rgba(0,0,0,0.5)', display: 'flex', alignItems: 'center', justifyContent: 'center', zIndex: 1000, padding: '16px' }}>
          <div className="card" style={{ maxWidth: '480px', width: '100%', padding: '24px' }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '16px' }}>
              <h3 style={{ fontSize: '17px', fontWeight: 800, margin: 0 }}>
                Recebimento de Mercadoria
              </h3>
              <button onClick={() => setModalReceberAberto(false)} style={{ background: 'none', border: 'none', cursor: 'pointer' }}>
                <X size={20} />
              </button>
            </div>

            <div style={{ background: '#f8fafc', padding: '12px', borderRadius: '6px', marginBottom: '16px', fontSize: '12.5px' }}>
              <div><strong>Pedido:</strong> {pedidoSelecionadoReceber.numero}</div>
              <div><strong>Material:</strong> {itemSelecionadoReceber.descricao} ({itemSelecionadoReceber.codigo})</div>
              <div><strong>Qtd Comprada:</strong> {itemSelecionadoReceber.quantidade} {itemSelecionadoReceber.unidade}</div>
              <div><strong>Já Recebida:</strong> {itemSelecionadoReceber.quantidadeRecebida} {itemSelecionadoReceber.unidade}</div>
              <div style={{ color: '#dc2626', fontWeight: 700, marginTop: '4px' }}>
                Saldo Pendente: {itemSelecionadoReceber.saldoPendente} {itemSelecionadoReceber.unidade}
              </div>
            </div>

            <form onSubmit={handleSalvarRecebimento} style={{ display: 'flex', flexDirection: 'column', gap: '14px' }}>
              <div>
                <label style={{ display: 'block', fontSize: '12px', fontWeight: 600, color: 'var(--text-muted)', marginBottom: '4px' }}>
                  Quantidade a Receber Agora *
                </label>
                <input
                  type="number"
                  step="0.0001"
                  max={itemSelecionadoReceber.saldoPendente}
                  min={0.0001}
                  className="input"
                  value={qtdReceber}
                  onChange={(e) => setQtdReceber(parseFloat(e.target.value) || 0)}
                  required
                />
              </div>

              <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '10px', marginTop: '12px' }}>
                <button type="button" onClick={() => setModalReceberAberto(false)} className="btn btn-secondary">
                  Cancelar
                </button>
                <button type="submit" disabled={salvandoRecebimento} className="btn btn-primary">
                  {salvandoRecebimento ? 'Salvando...' : 'Confirmar Recebimento'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

    </div>
  );
};
