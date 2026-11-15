import React, { useState, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { api } from '../../services/api';
import { CotacaoServicoListaItem } from '../../types/service';
import {
  Wrench,
  PlusCircle,
  Search,
  Filter,
  FileText,
  Clock,
  CheckCircle2,
  Calendar,
  AlertTriangle,
  Building2,
  Printer,
  ChevronRight,
  TrendingDown
} from 'lucide-react';

export const CotacoesServicoLista: React.FC = () => {
  const navigate = useNavigate();
  const [cotacoes, setCotacoes] = useState<CotacaoServicoListaItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [filtroStatus, setFiltroStatus] = useState<string>('todos');
  const [filtroCategoria, setFiltroCategoria] = useState<string>('todas');
  const [busca, setBusca] = useState('');

  const carregarCotacoes = async () => {
    setLoading(true);
    try {
      let url = '/servicos';
      const params = new URLSearchParams();
      if (filtroStatus !== 'todos') params.append('status', filtroStatus);
      if (filtroCategoria !== 'todas') params.append('categoria', filtroCategoria);
      if (busca.trim()) params.append('busca', busca.trim());

      const qs = params.toString();
      if (qs) url += `?${qs}`;

      const res = await api.get<any>(url);
      const lista = Array.isArray(res) ? res : (Array.isArray(res?.itens) ? res.itens : []);
      setCotacoes(lista);
    } catch (err: any) {
      console.error('Erro ao carregar cotações de serviços:', err);
      setCotacoes([]);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    carregarCotacoes();
  }, [filtroStatus, filtroCategoria]);

  const handleBuscar = (e: React.FormEvent) => {
    e.preventDefault();
    carregarCotacoes();
  };

  const getStatusBadge = (status: string) => {
    switch (status) {
      case 'Rascunho':
        return <span className="badge badge-gray">Rascunho</span>;
      case 'AguardandoPropostas':
        return <span className="badge badge-blue">Aguardando Propostas</span>;
      case 'EmAnalise':
        return <span className="badge badge-yellow">Em Análise</span>;
      case 'Contratada':
        return <span className="badge badge-green">Contratada</span>;
      case 'Concluida':
        return <span className="badge badge-purple">Concluída</span>;
      case 'Cancelada':
        return <span className="badge badge-red">Cancelada</span>;
      default:
        return <span className="badge badge-blue">{status}</span>;
    }
  };

  const abrirPdf = (id: string, e: React.MouseEvent) => {
    e.stopPropagation();
    const baseUrl = api.getBaseUrl();
    window.open(`${baseUrl}/servicos/${id}/pdf`, '_blank');
  };

  const listaSegura = Array.isArray(cotacoes) ? cotacoes : [];
  const totalEstimado = listaSegura.reduce((acc, c) => acc + (c.totalEstimado || 0), 0);
  const totalContratado = listaSegura.reduce((acc, c) => acc + (c.totalContratado || 0), 0);
  const totalEconomia = listaSegura.reduce((acc, c) => acc + (c.economiaRealizada || 0), 0);

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: '24px' }}>
      {/* HEADER DA PÁGINA */}
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <div>
          <h1 style={{ fontSize: '24px', fontWeight: 800, color: 'var(--text-main)', margin: '0 0 4px 0', display: 'flex', alignItems: 'center', gap: '10px' }}>
            <Wrench size={26} color="var(--blue-primary)" />
            Cotação e Contratação de Serviços
          </h1>
          <p style={{ margin: 0, fontSize: '14px', color: 'var(--text-muted)' }}>
            Módulo dedicado à gestão de escopos, etapas técnicas, concorrência de propostas e contratação terceirizada.
          </p>
        </div>
        <div style={{ display: 'flex', gap: '12px' }}>
          <button
            onClick={() => navigate('/servicos/ordens')}
            className="btn btn-secondary"
            style={{ display: 'flex', alignItems: 'center', gap: '8px' }}
          >
            <FileText size={16} />
            Ordens de Serviço
          </button>
          <button
            onClick={() => navigate('/servicos/nova')}
            className="btn btn-primary"
            style={{ display: 'flex', alignItems: 'center', gap: '8px' }}
          >
            <PlusCircle size={16} />
            Nova Cotação de Serviço
          </button>
        </div>
      </div>

      {/* CARDS DE INDICADORES / KPIS */}
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(220px, 1fr))', gap: '16px' }}>
        <div className="card" style={{ padding: '16px 20px' }}>
          <div style={{ fontSize: '12px', color: 'var(--text-muted)', fontWeight: 600, textTransform: 'uppercase' }}>
            Total de Cotações
          </div>
          <div style={{ fontSize: '24px', fontWeight: 800, color: 'var(--text-main)', marginTop: '4px' }}>
            {cotacoes.length}
          </div>
        </div>
        <div className="card" style={{ padding: '16px 20px' }}>
          <div style={{ fontSize: '12px', color: 'var(--text-muted)', fontWeight: 600, textTransform: 'uppercase' }}>
            Volume Estimado
          </div>
          <div style={{ fontSize: '24px', fontWeight: 800, color: 'var(--text-main)', marginTop: '4px' }}>
            R$ {totalEstimado.toLocaleString('pt-BR', { minimumFractionDigits: 2 })}
          </div>
        </div>
        <div className="card" style={{ padding: '16px 20px' }}>
          <div style={{ fontSize: '12px', color: 'var(--text-muted)', fontWeight: 600, textTransform: 'uppercase' }}>
            Total Contratado
          </div>
          <div style={{ fontSize: '24px', fontWeight: 800, color: '#0284c7', marginTop: '4px' }}>
            R$ {totalContratado.toLocaleString('pt-BR', { minimumFractionDigits: 2 })}
          </div>
        </div>
        <div className="card" style={{ padding: '16px 20px' }}>
          <div style={{ fontSize: '12px', color: 'var(--text-muted)', fontWeight: 600, textTransform: 'uppercase' }}>
            Economia Realizada
          </div>
          <div style={{ fontSize: '24px', fontWeight: 800, color: '#16a34a', marginTop: '4px', display: 'flex', alignItems: 'center', gap: '6px' }}>
            <TrendingDown size={22} />
            R$ {totalEconomia.toLocaleString('pt-BR', { minimumFractionDigits: 2 })}
          </div>
        </div>
      </div>

      {/* FILTROS E BUSCA */}
      <div className="card" style={{ padding: '16px 20px', display: 'flex', flexWrap: 'wrap', gap: '16px', alignItems: 'center', justifyContent: 'space-between' }}>
        <form onSubmit={handleBuscar} style={{ display: 'flex', gap: '10px', flex: 1, minWidth: '280px', maxWidth: '460px' }}>
          <div style={{ position: 'relative', width: '100%' }}>
            <Search size={18} style={{ position: 'absolute', left: '12px', top: '50%', transform: 'translateY(-50%)', color: 'var(--text-muted)' }} />
            <input
              type="text"
              className="input"
              placeholder="Buscar por número, título ou categoria..."
              value={busca}
              onChange={(e) => setBusca(e.target.value)}
              style={{ paddingLeft: '38px' }}
            />
          </div>
          <button type="submit" className="btn btn-secondary">Buscar</button>
        </form>

        <div style={{ display: 'flex', gap: '12px', alignItems: 'center' }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: '6px', fontSize: '13px' }}>
            <Filter size={15} color="var(--text-muted)" />
            <span style={{ fontWeight: 600 }}>Status:</span>
            <select
              className="input"
              value={filtroStatus}
              onChange={(e) => setFiltroStatus(e.target.value)}
              style={{ width: 'auto', padding: '6px 12px' }}
            >
              <option value="todos">Todos os status</option>
              <option value="Rascunho">Rascunho</option>
              <option value="AguardandoPropostas">Aguardando Propostas</option>
              <option value="EmAnalise">Em Análise</option>
              <option value="Contratada">Contratada</option>
              <option value="Concluida">Concluída</option>
              <option value="Cancelada">Cancelada</option>
            </select>
          </div>

          <div style={{ display: 'flex', alignItems: 'center', gap: '6px', fontSize: '13px' }}>
            <span style={{ fontWeight: 600 }}>Categoria:</span>
            <select
              className="input"
              value={filtroCategoria}
              onChange={(e) => setFiltroCategoria(e.target.value)}
              style={{ width: 'auto', padding: '6px 12px' }}
            >
              <option value="todas">Todas as categorias</option>
              <option value="ManutencaoPredial">Manutenção Predial</option>
              <option value="TI">TI & Redes</option>
              <option value="Facilities">Facilities & Limpeza</option>
              <option value="Obras">Obras & Reformas</option>
              <option value="Consultoria">Consultoria</option>
              <option value="Juridico">Jurídico</option>
              <option value="Logistica">Logística & Fretes</option>
              <option value="Outros">Outros</option>
            </select>
          </div>
        </div>
      </div>

      {/* TABELA DE COTAÇÕES DE SERVIÇOS */}
      <div className="card" style={{ padding: 0, overflow: 'hidden' }}>
        {loading ? (
          <div style={{ padding: '60px', textAlign: 'center', color: 'var(--text-muted)' }}>
            Carregando cotações de serviços...
          </div>
        ) : listaSegura.length === 0 ? (
          <div style={{ padding: '60px', textAlign: 'center' }}>
            <Wrench size={42} style={{ color: 'var(--text-muted)', opacity: 0.4, marginBottom: '12px' }} />
            <div style={{ fontSize: '16px', fontWeight: 600, color: 'var(--text-main)' }}>
              Nenhuma cotação de serviços encontrada
            </div>
            <p style={{ fontSize: '13px', color: 'var(--text-muted)', maxWidth: '420px', margin: '6px auto 16px' }}>
              Crie uma nova cotação de serviço para estruturar o escopo, definir etapas técnicas e convidar prestadores.
            </p>
            <button onClick={() => navigate('/servicos/nova')} className="btn btn-primary">
              <PlusCircle size={15} style={{ marginRight: '6px' }} />
              Criar Primeira Cotação de Serviço
            </button>
          </div>
        ) : (
          <table className="table" style={{ width: '100%', borderCollapse: 'collapse' }}>
            <thead>
              <tr style={{ background: '#f8fafc', borderBottom: '1px solid var(--border)' }}>
                <th style={{ padding: '12px 16px', textAlign: 'left', fontSize: '12px', fontWeight: 700, color: 'var(--text-muted)' }}>NÚMERO / TÍTULO</th>
                <th style={{ padding: '12px 16px', textAlign: 'left', fontSize: '12px', fontWeight: 700, color: 'var(--text-muted)' }}>CATEGORIA</th>
                <th style={{ padding: '12px 16px', textAlign: 'center', fontSize: '12px', fontWeight: 700, color: 'var(--text-muted)' }}>STATUS</th>
                <th style={{ padding: '12px 16px', textAlign: 'center', fontSize: '12px', fontWeight: 700, color: 'var(--text-muted)' }}>PRAZO LIMITE</th>
                <th style={{ padding: '12px 16px', textAlign: 'center', fontSize: '12px', fontWeight: 700, color: 'var(--text-muted)' }}>PROPOSTAS</th>
                <th style={{ padding: '12px 16px', textAlign: 'right', fontSize: '12px', fontWeight: 700, color: 'var(--text-muted)' }}>VALOR TOTAL</th>
                <th style={{ padding: '12px 16px', textAlign: 'center', fontSize: '12px', fontWeight: 700, color: 'var(--text-muted)' }}>AÇÕES</th>
              </tr>
            </thead>
            <tbody>
              {listaSegura.map((c) => (
                <tr
                  key={c.id}
                  onClick={() => navigate(`/servicos/${c.id}`)}
                  style={{ cursor: 'pointer', borderBottom: '1px solid var(--border)', transition: 'background-color 0.15s' }}
                  onMouseEnter={(e) => (e.currentTarget.style.backgroundColor = '#f8fafc')}
                  onMouseLeave={(e) => (e.currentTarget.style.backgroundColor = 'transparent')}
                >
                  <td style={{ padding: '14px 16px' }}>
                    <div style={{ fontWeight: 700, color: 'var(--blue-primary)', fontSize: '14px' }}>
                      {c.numero}
                    </div>
                    <div style={{ fontWeight: 600, color: 'var(--text-main)', fontSize: '13.5px', marginTop: '2px' }}>
                      {c.titulo}
                    </div>
                    <div style={{ fontSize: '11px', color: 'var(--text-muted)', marginTop: '2px' }}>
                      {c.qtdAtividades} atividade(s) • Responsável: {c.responsavelNome}
                    </div>
                  </td>
                  <td style={{ padding: '14px 16px' }}>
                    <span style={{ fontSize: '12.5px', fontWeight: 600, color: 'var(--text-main)' }}>
                      {c.categoria}
                    </span>
                    <div style={{ fontSize: '11px', color: 'var(--text-muted)' }}>
                      Prioridade: {c.prioridade}
                    </div>
                  </td>
                  <td style={{ padding: '14px 16px', textAlign: 'center' }}>
                    {getStatusBadge(c.status)}
                  </td>
                  <td style={{ padding: '14px 16px', textAlign: 'center', fontSize: '13px' }}>
                    <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'center', gap: '4px' }}>
                      <Calendar size={13} color="var(--text-muted)" />
                      <span>{new Date(c.dataLimiteResposta).toLocaleDateString('pt-BR')}</span>
                    </div>
                  </td>
                  <td style={{ padding: '14px 16px', textAlign: 'center' }}>
                    <div style={{ fontWeight: 700, fontSize: '13.5px', color: c.qtdPropostas > 0 ? '#16a34a' : 'var(--text-muted)' }}>
                      {c.qtdPropostas} / {c.qtdFornecedores}
                    </div>
                    <div style={{ fontSize: '10.5px', color: 'var(--text-muted)' }}>
                      {c.qtdPropostas > 0 ? 'propostas recebidas' : 'aguardando propostas'}
                    </div>
                  </td>
                  <td style={{ padding: '14px 16px', textAlign: 'right' }}>
                    <div style={{ fontWeight: 800, fontSize: '14px', color: c.totalContratado > 0 ? '#16a34a' : 'var(--text-main)' }}>
                      R$ {(c.totalContratado > 0 ? c.totalContratado : c.totalEstimado).toLocaleString('pt-BR', { minimumFractionDigits: 2 })}
                    </div>
                    {c.fornecedorVencedorNome && (
                      <div style={{ fontSize: '11px', color: '#16a34a', fontWeight: 600 }}>
                        Vencedor: {c.fornecedorVencedorNome}
                      </div>
                    )}
                  </td>
                  <td style={{ padding: '14px 16px', textAlign: 'center' }} onClick={(e) => e.stopPropagation()}>
                    <div style={{ display: 'flex', gap: '8px', justifyContent: 'center' }}>
                      <button
                        title="Imprimir Especificação PDF (ProSyst)"
                        className="btn btn-secondary"
                        style={{ padding: '6px 10px', fontSize: '12px' }}
                        onClick={(e) => abrirPdf(c.id, e)}
                      >
                        <Printer size={14} />
                      </button>
                      <button
                        title="Ver Detalhes da Cotação"
                        className="btn btn-primary"
                        style={{ padding: '6px 12px', fontSize: '12px', display: 'flex', alignItems: 'center', gap: '4px' }}
                        onClick={() => navigate(`/servicos/${c.id}`)}
                      >
                        <span>Abrir</span>
                        <ChevronRight size={13} />
                      </button>
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>
    </div>
  );
};

