import React, { useState, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { api } from '../../services/api';
import { CotacaoResumo } from '../../types';
import { Plus, Search, Eye, BarChart2, CheckCircle2, Clock, XCircle, AlertCircle, ChevronLeft, ChevronRight, ChevronsRight, ChevronsLeft, RefreshCw } from 'lucide-react';

const ITENS_POR_PAGINA = 10;

export const CotacoesEmAndamento: React.FC = () => {
  const navigate = useNavigate();
  const [cotacoes, setCotacoes] = useState<CotacaoResumo[]>([]);
  const [loading, setLoading] = useState(true);
  const [statusFiltro, setStatusFiltro] = useState<string>('Todas');
  const [busca, setBusca] = useState('');
  const [paginaAtual, setPaginaAtual] = useState(1);

  const carregarCotacoes = async () => {
    setLoading(true);
    try {
      const res = await api.get<any>('/cotacoes?size=200');
      const lista = Array.isArray(res) ? res : (res?.itens || []);
      setCotacoes(lista);
    } catch (err: any) {
      alert('Erro ao carregar cotações: ' + err.message);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    carregarCotacoes();
  }, []);

  useEffect(() => {
    setPaginaAtual(1);
  }, [statusFiltro, busca]);

  const badgeStatus = (c: CotacaoResumo) => {
    if (c.status === 'Cancelada') {
      return <span className="badge badge-red"><XCircle size={12} /> Cancelada</span>;
    }
    if (c.status === 'Aprovada') {
      return <span className="badge badge-green"><CheckCircle2 size={12} /> Encerrada / Aprovada</span>;
    }
    if (c.status === 'EmAnalise' || (c.status === 'Aberta' && c.respondidas > 0)) {
      return <span className="badge badge-blue"><Clock size={12} /> Em Andamento</span>;
    }
    if (c.status === 'Aberta' && c.respondidas === 0) {
      return <span className="badge badge-yellow"><AlertCircle size={12} /> Aguardando Propostas</span>;
    }
    return <span className="badge">{c.status}</span>;
  };

  const cotacoesFiltradas = cotacoes.filter((c) => {
    let matchStatus = true;
    if (statusFiltro === 'Rascunhos') {
      matchStatus = c.status === 'Aberta' && c.respondidas === 0 && c.visualizadas === 0;
    } else if (statusFiltro === 'Aguardando propostas') {
      matchStatus = c.status === 'Aberta' && c.respondidas === 0;
    } else if (statusFiltro === 'Em andamento') {
      matchStatus = (c.status === 'Aberta' && c.respondidas > 0) || c.status === 'EmAnalise';
    } else if (statusFiltro === 'Encerradas') {
      matchStatus = c.status === 'Aprovada';
    } else if (statusFiltro === 'Canceladas') {
      matchStatus = c.status === 'Cancelada';
    }

    const matchBusca =
      !busca ||
      c.numero.toLowerCase().includes(busca.toLowerCase()) ||
      c.titulo.toLowerCase().includes(busca.toLowerCase());

    return matchStatus && matchBusca;
  });

  const totalPaginas = Math.max(1, Math.ceil(cotacoesFiltradas.length / ITENS_POR_PAGINA));
  const indiceInicial = (paginaAtual - 1) * ITENS_POR_PAGINA;
  const cotacoesPaginadas = cotacoesFiltradas.slice(indiceInicial, indiceInicial + ITENS_POR_PAGINA);

  return (
    <div>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '20px', flexWrap: 'wrap', gap: '12px' }}>
        <div>
          <h1 style={{ fontSize: '24px', fontWeight: 800, color: 'var(--text-main)' }}>
            Cotações em Andamento
          </h1>
          <p style={{ fontSize: '14px', color: 'var(--text-muted)' }}>
            Gerenciamento de solicitações de compras enviadas aos fornecedores.
          </p>
        </div>

        <div style={{ display: 'flex', gap: '10px' }}>
          <button onClick={carregarCotacoes} className="btn btn-secondary">
            <RefreshCw size={15} /> Atualizar
          </button>
          <button onClick={() => navigate('/cotacoes/nova')} className="btn btn-primary">
            <Plus size={16} /> Nova Cotação Multi-Fornecedor
          </button>
        </div>
      </div>

      {/* FILTROS */}
      <div className="card" style={{ marginBottom: '20px', padding: '14px 18px' }}>
        <div style={{ display: 'flex', gap: '16px', alignItems: 'center', flexWrap: 'wrap' }}>
          <div style={{ flex: 1, minWidth: '240px', position: 'relative' }}>
            <Search size={16} style={{ position: 'absolute', left: '10px', top: '11px', color: 'var(--text-muted)' }} />
            <input
              placeholder="Pesquisar por número ou título da cotação..."
              value={busca}
              onChange={(e) => setBusca(e.target.value)}
              style={{ paddingLeft: '34px', width: '100%' }}
            />
          </div>

          <div style={{ display: 'flex', gap: '6px', flexWrap: 'wrap' }}>
            {[
              { id: 'Todas', label: 'Todas' },
              { id: 'Rascunhos', label: 'Rascunhos' },
              { id: 'Aguardando propostas', label: 'Aguardando Propostas' },
              { id: 'Em andamento', label: 'Em Andamento' },
              { id: 'Encerradas', label: 'Encerradas' },
              { id: 'Canceladas', label: 'Canceladas' }
            ].map((f) => (
              <button
                key={f.id}
                onClick={() => setStatusFiltro(f.id)}
                className={`btn ${statusFiltro === f.id ? 'btn-primary' : 'btn-secondary'}`}
                style={{ fontSize: '12.5px', padding: '6px 12px' }}
              >
                {f.label}
              </button>
            ))}
          </div>
        </div>
      </div>

      {/* TABELA DE COTAÇÕES */}
      <div className="card">
        <div className="table-container">
          <table>
            <thead>
              <tr>
                <th>Número</th>
                <th>Título / Objeto</th>
                <th>Status</th>
                <th>Data Limite</th>
                <th style={{ textAlign: 'center' }}>Itens</th>
                <th style={{ textAlign: 'center' }}>Fornecedores</th>
                <th style={{ textAlign: 'center' }}>Respostas</th>
                <th style={{ textAlign: 'right' }}>Total Aprovado</th>
                <th style={{ textAlign: 'right' }}>Ações</th>
              </tr>
            </thead>
            <tbody>
              {loading ? (
                <tr>
                  <td colSpan={9} style={{ textAlign: 'center', padding: '30px', color: 'var(--text-muted)' }}>
                    Carregando cotações...
                  </td>
                </tr>
              ) : cotacoesFiltradas.length === 0 ? (
                <tr>
                  <td colSpan={9} style={{ textAlign: 'center', padding: '40px', color: 'var(--text-muted)' }}>
                    Nenhuma cotação encontrada com os filtros selecionados.
                  </td>
                </tr>
              ) : (
                cotacoesPaginadas.map((c) => {
                  const temRespostas = c.respondidas > 0;
                  return (
                    <tr key={c.id}>
                      <td style={{ fontFamily: 'monospace', fontWeight: 700, color: 'var(--primary)' }}>
                        {c.numero}
                      </td>
                      <td>
                        <div style={{ fontWeight: 600, color: 'var(--text-main)' }}>{c.titulo}</div>
                        <div style={{ fontSize: '11.5px', color: 'var(--text-muted)' }}>
                          Criada em: {new Date(c.dataCriacao).toLocaleDateString('pt-BR')}
                        </div>
                      </td>
                      <td>{badgeStatus(c)}</td>
                      <td style={{ fontSize: '13px' }}>
                        {new Date(c.dataLimiteResposta).toLocaleDateString('pt-BR')}
                      </td>
                      <td style={{ textAlign: 'center', fontWeight: 600 }}>{c.itens}</td>
                      <td style={{ textAlign: 'center' }}>
                        <span style={{ fontWeight: 600 }}>{c.fornecedores}</span>
                        <span style={{ fontSize: '11px', color: 'var(--text-muted)', display: 'block' }}>
                          {c.visualizadas} visualizada(s)
                        </span>
                      </td>
                      <td style={{ textAlign: 'center' }}>
                        <span
                          className={`badge ${c.respondidas > 0 ? 'badge-green' : 'badge-yellow'}`}
                          style={{ fontSize: '12px' }}
                        >
                          {c.respondidas} de {c.fornecedores}
                        </span>
                      </td>
                      <td style={{ textAlign: 'right', fontWeight: 700, color: c.totalAprovado > 0 ? 'var(--green)' : 'var(--text-muted)' }}>
                        {c.totalAprovado > 0 ? `R$ ${c.totalAprovado.toLocaleString('pt-BR', { minimumFractionDigits: 2 })}` : '—'}
                      </td>
                      <td style={{ textAlign: 'right' }}>
                        <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '8px' }}>
                          <button
                            onClick={() => navigate(`/cotacoes/${c.id}`)}
                            className="btn btn-secondary"
                            style={{ padding: '6px 10px', fontSize: '12px' }}
                            title="Ver detalhes da cotação"
                          >
                            <Eye size={14} /> Detalhes
                          </button>

                          <button
                            onClick={() => navigate(`/cotacoes/${c.id}/comparacao`)}
                            className={`btn ${temRespostas ? 'btn-primary' : 'btn-secondary'}`}
                            style={{ padding: '6px 10px', fontSize: '12px' }}
                            title={temRespostas ? 'Comparar propostas comerciais recebidas' : 'Ver comparativo'}
                          >
                            <BarChart2 size={14} /> Comparar
                          </button>
                        </div>
                      </td>
                    </tr>
                  );
                })
              )}
            </tbody>
          </table>
        </div>

        {/* PAGINAÇÃO */}
        {cotacoesFiltradas.length > 0 && (
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', padding: '14px 20px', borderTop: '1px solid var(--border-light)', flexWrap: 'wrap', gap: '12px' }}>
            <div style={{ fontSize: '13px', color: 'var(--text-muted)' }}>
              Mostrando <strong>{indiceInicial + 1}</strong> a <strong>{Math.min(indiceInicial + ITENS_POR_PAGINA, cotacoesFiltradas.length)}</strong> de <strong>{cotacoesFiltradas.length}</strong> cotações
            </div>

            <div style={{ display: 'flex', gap: '6px', alignItems: 'center' }}>
              <button
                onClick={() => setPaginaAtual(1)}
                disabled={paginaAtual === 1}
                className="btn btn-secondary"
                style={{ padding: '6px 10px', fontSize: '12px' }}
                title="Primeira página"
              >
                <ChevronsLeft size={14} />
              </button>
              <button
                onClick={() => setPaginaAtual(prev => Math.max(1, prev - 1))}
                disabled={paginaAtual === 1}
                className="btn btn-secondary"
                style={{ padding: '6px 10px', fontSize: '12px' }}
                title="Página anterior"
              >
                <ChevronLeft size={14} />
              </button>

              {Array.from({ length: totalPaginas }, (_, i) => i + 1)
                .filter(p => p === 1 || p === totalPaginas || Math.abs(p - paginaAtual) <= 2)
                .map((p, idx, arr) => {
                  const prev = arr[idx - 1];
                  const showEllipsis = prev && p - prev > 1;
                  return (
                    <React.Fragment key={p}>
                      {showEllipsis && <span style={{ padding: '0 4px', color: 'var(--text-muted)' }}>...</span>}
                      <button
                        onClick={() => setPaginaAtual(p)}
                        className={`btn ${paginaAtual === p ? 'btn-primary' : 'btn-secondary'}`}
                        style={{ padding: '6px 12px', fontSize: '12px', fontWeight: paginaAtual === p ? 700 : 500 }}
                      >
                        {p}
                      </button>
                    </React.Fragment>
                  );
                })}

              <button
                onClick={() => setPaginaAtual(prev => Math.min(totalPaginas, prev + 1))}
                disabled={paginaAtual === totalPaginas}
                className="btn btn-secondary"
                style={{ padding: '6px 10px', fontSize: '12px' }}
                title="Próxima página"
              >
                <ChevronRight size={14} />
              </button>
              <button
                onClick={() => setPaginaAtual(totalPaginas)}
                disabled={paginaAtual === totalPaginas}
                className="btn btn-secondary"
                style={{ padding: '6px 10px', fontSize: '12px' }}
                title="Última página"
              >
                <ChevronsRight size={14} />
              </button>
            </div>
          </div>
        )}
      </div>
    </div>
  );
};
