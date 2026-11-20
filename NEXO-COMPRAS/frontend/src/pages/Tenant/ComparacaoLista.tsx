import React, { useState, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { api } from '../../services/api';
import { CotacaoResumo } from '../../types';
import {
  GitCompare,
  Search,
  Award,
  BarChart2,
  CheckCircle2,
  Clock,
  ArrowRight,
  TrendingDown,
  Users,
  Eye
} from 'lucide-react';

export const ComparacaoLista: React.FC = () => {
  const navigate = useNavigate();
  const [cotacoes, setCotacoes] = useState<CotacaoResumo[]>([]);
  const [loading, setLoading] = useState(true);
  const [busca, setBusca] = useState('');
  const [selecionadas, setSelecionadas] = useState<string[]>([]);

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

  const toggleSelect = (id: string) => {
    setSelecionadas((prev) =>
      prev.includes(id) ? prev.filter((x) => x !== id) : [...prev, id]
    );
  };

  const cotacoesFiltradas = cotacoes.filter((c) => {
    const matchBusca =
      !busca ||
      c.numero.toLowerCase().includes(busca.toLowerCase()) ||
      c.titulo.toLowerCase().includes(busca.toLowerCase());
    return matchBusca;
  });

  const cotacoesComResposta = cotacoes.filter((c) => c.respondidas > 0);
  const totalRespostas = cotacoes.reduce((acc, c) => acc + c.respondidas, 0);

  const handleCompararSelecionada = () => {
    if (selecionadas.length === 0) return;
    // Navega para a primeira cotação selecionada
    navigate(`/cotacoes/${selecionadas[0]}/comparacao`);
  };

  return (
    <div>
      {/* CABEÇALHO */}
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '20px', flexWrap: 'wrap', gap: '12px' }}>
        <div>
          <h1 style={{ fontSize: '24px', fontWeight: 800, color: 'var(--text-main)', display: 'flex', alignItems: 'center', gap: '10px' }}>
            <GitCompare color="var(--blue-primary)" size={28} />
            Comparação de Ofertas e Propostas Comerciais
          </h1>
          <p style={{ fontSize: '14px', color: 'var(--text-muted)' }}>
            Compare propostas recebidas de múltiplos fornecedores, analise o custo efetivo com impostos (ICMS, ST, IPI) e aprove a oferta campeã.
          </p>
        </div>

        {selecionadas.length > 0 && (
          <button
            onClick={handleCompararSelecionada}
            className="btn btn-primary"
            style={{ display: 'flex', alignItems: 'center', gap: '8px', padding: '10px 18px', fontSize: '14px', fontWeight: 700 }}
          >
            <Award size={18} />
            {selecionadas.length === 1
              ? '🏆 Comparar Ofertas desta Cotação (Ver Campeã)'
              : `🏆 Comparar Propostas (${selecionadas.length} selecionadas)`}
          </button>
        )}
      </div>

      {/* CARDS DE RESUMO */}
      <div className="grid-cols-3" style={{ marginBottom: '24px' }}>
        <div className="card" style={{ borderTop: '4px solid var(--blue-primary)' }}>
          <div style={{ fontSize: '12px', fontWeight: 700, color: 'var(--text-muted)', textTransform: 'uppercase' }}>
            COTAÇÕES COM RESPOSTAS
          </div>
          <div style={{ fontSize: '26px', fontWeight: 800, color: 'var(--text-main)', margin: '6px 0' }}>
            {cotacoesComResposta.length}
          </div>
          <div style={{ fontSize: '12px', color: 'var(--text-muted)' }}>
            Prontas para análise e aprovação da campeã
          </div>
        </div>

        <div className="card" style={{ borderTop: '4px solid var(--green)' }}>
          <div style={{ fontSize: '12px', fontWeight: 700, color: 'var(--text-muted)', textTransform: 'uppercase' }}>
            PROPOSTAS DE FORNECEDORES
          </div>
          <div style={{ fontSize: '26px', fontWeight: 800, color: 'var(--green)', margin: '6px 0' }}>
            {totalRespostas}
          </div>
          <div style={{ fontSize: '12px', color: 'var(--text-muted)' }}>
            Respostas recebidas via portal seguro
          </div>
        </div>

        <div className="card" style={{ borderTop: '4px solid #8B5CF6' }}>
          <div style={{ fontSize: '12px', fontWeight: 700, color: 'var(--text-muted)', textTransform: 'uppercase' }}>
            CRITÉRIO DE DECISÃO
          </div>
          <div style={{ fontSize: '16px', fontWeight: 800, color: '#6D28D9', margin: '6px 0' }}>
            Menor Custo Efetivo Real
          </div>
          <div style={{ fontSize: '12px', color: 'var(--text-muted)' }}>
            Preço - Desconto + ST + IPI + Frete
          </div>
        </div>
      </div>

      {/* BARRA DE PESQUISA */}
      <div className="card" style={{ marginBottom: '20px', padding: '14px 18px' }}>
        <div style={{ display: 'flex', gap: '16px', alignItems: 'center' }}>
          <div style={{ flex: 1, position: 'relative' }}>
            <Search size={16} style={{ position: 'absolute', left: '10px', top: '11px', color: 'var(--text-muted)' }} />
            <input
              placeholder="Pesquisar por número ou título da cotação..."
              value={busca}
              onChange={(e) => setBusca(e.target.value)}
              style={{ paddingLeft: '34px' }}
            />
          </div>
        </div>
      </div>

      {/* TABELA DE COTAÇÕES PARA COMPARAÇÃO */}
      <div className="card">
        {/* DESKTOP TABLE */}
        <div className="table-container desktop-table-view">
          <table>
            <thead>
              <tr>
                <th style={{ width: '40px', textAlign: 'center' }}>Sel.</th>
                <th>Número</th>
                <th>Título / Objeto</th>
                <th>Data Limite</th>
                <th style={{ textAlign: 'center' }}>Fornecedores</th>
                <th style={{ textAlign: 'center' }}>Propostas Recebidas</th>
                <th>Status</th>
                <th style={{ textAlign: 'right' }}>Ação de Comparação</th>
              </tr>
            </thead>
            <tbody>
              {loading ? (
                <tr>
                  <td colSpan={8} style={{ textAlign: 'center', padding: '30px', color: 'var(--text-muted)' }}>
                    Carregando cotações para comparação...
                  </td>
                </tr>
              ) : cotacoesFiltradas.length === 0 ? (
                <tr>
                  <td colSpan={8} style={{ textAlign: 'center', padding: '40px', color: 'var(--text-muted)' }}>
                    Nenhuma cotação encontrada no momento.
                  </td>
                </tr>
              ) : (
                cotacoesFiltradas.map((c) => {
                  const temRespostas = c.respondidas > 0;
                  const isSelected = selecionadas.includes(c.id);

                  return (
                    <tr
                      key={c.id}
                      style={{
                        backgroundColor: isSelected ? '#EFF6FF' : 'transparent',
                        transition: 'background-color 0.15s ease'
                      }}
                    >
                      <td style={{ textAlign: 'center' }}>
                        <input
                          type="checkbox"
                          checked={isSelected}
                          onChange={() => toggleSelect(c.id)}
                          style={{ cursor: 'pointer', width: '16px', height: '16px' }}
                        />
                      </td>
                      <td style={{ fontFamily: 'monospace', fontWeight: 700, color: 'var(--primary)' }}>
                        {c.numero}
                      </td>
                      <td>
                        <div style={{ fontWeight: 600, color: 'var(--text-main)' }}>{c.titulo}</div>
                        <div style={{ fontSize: '11.5px', color: 'var(--text-muted)' }}>
                          {c.itens} item(ns) cotado(s)
                        </div>
                      </td>
                      <td style={{ fontSize: '13px' }}>
                        {new Date(c.dataLimiteResposta).toLocaleDateString('pt-BR')}
                      </td>
                      <td style={{ textAlign: 'center', fontWeight: 600 }}>
                        {c.fornecedores}
                      </td>
                      <td style={{ textAlign: 'center' }}>
                        <span
                          className={`badge ${temRespostas ? 'badge-green' : 'badge-yellow'}`}
                          style={{ fontSize: '12px', fontWeight: 700 }}
                        >
                          {c.respondidas} de {c.fornecedores} respondida(s)
                        </span>
                      </td>
                      <td>
                        <span className={`badge ${c.status === 'Aprovada' ? 'badge-green' : c.status === 'Cancelada' ? 'badge-red' : 'badge-blue'}`}>
                          {c.status}
                        </span>
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
                            disabled={!temRespostas}
                            className={`btn ${temRespostas ? 'btn-primary' : 'btn-secondary'}`}
                            style={{
                              padding: '6px 12px',
                              fontSize: '12px',
                              fontWeight: 700,
                              display: 'inline-flex',
                              alignItems: 'center',
                              gap: '6px'
                            }}
                            title={temRespostas ? 'Abrir comparativo de ofertas e identificar proposta campeã' : 'Aguardando respostas dos fornecedores'}
                          >
                            <Award size={14} /> Comparar & Ver Campeã
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

        {/* MOBILE CARDS VIEW */}
        <div className="mobile-card-view" style={{ padding: '12px', display: 'flex', flexDirection: 'column', gap: '12px' }}>
          {loading ? (
            <div style={{ textAlign: 'center', padding: '30px', color: 'var(--text-muted)' }}>
              Carregando cotações...
            </div>
          ) : cotacoesFiltradas.length === 0 ? (
            <div style={{ textAlign: 'center', padding: '30px', color: 'var(--text-muted)' }}>
              Nenhuma cotação encontrada.
            </div>
          ) : (
            cotacoesFiltradas.map((c) => {
              const temRespostas = c.respondidas > 0;
              const isSelected = selecionadas.includes(c.id);

              return (
                <div
                  key={c.id}
                  style={{
                    backgroundColor: isSelected ? '#EFF6FF' : '#FFFFFF',
                    border: isSelected ? '2px solid var(--blue-primary)' : '1px solid var(--border)',
                    borderRadius: 'var(--radius-md)',
                    padding: '14px',
                    display: 'flex',
                    flexDirection: 'column',
                    gap: '10px',
                    boxShadow: 'var(--shadow-sm)'
                  }}
                >
                  <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                    <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
                      <input
                        type="checkbox"
                        checked={isSelected}
                        onChange={() => toggleSelect(c.id)}
                        style={{ cursor: 'pointer', width: '18px', height: '18px' }}
                      />
                      <span style={{ fontFamily: 'monospace', fontWeight: 800, color: 'var(--blue-primary)', fontSize: '15px' }}>
                        {c.numero}
                      </span>
                    </div>
                    <span className={`badge ${c.status === 'Aprovada' ? 'badge-green' : c.status === 'Cancelada' ? 'badge-red' : 'badge-blue'}`}>
                      {c.status}
                    </span>
                  </div>

                  <div>
                    <div style={{ fontWeight: 700, fontSize: '14px', color: 'var(--text-main)', marginBottom: '2px' }}>
                      {c.titulo}
                    </div>
                    <div style={{ fontSize: '12px', color: 'var(--text-muted)' }}>
                      {c.itens} item(ns) cotado(s) • Limite: {new Date(c.dataLimiteResposta).toLocaleDateString('pt-BR')}
                    </div>
                  </div>

                  <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', padding: '8px 12px', backgroundColor: 'var(--bg-page)', borderRadius: 'var(--radius-sm)', fontSize: '12px' }}>
                    <span>Fornecedores: <strong>{c.fornecedores}</strong></span>
                    <span className={`badge ${temRespostas ? 'badge-green' : 'badge-yellow'}`} style={{ fontSize: '11px', fontWeight: 700 }}>
                      {c.respondidas} de {c.fornecedores} respondida(s)
                    </span>
                  </div>

                  <div style={{ display: 'grid', gridTemplateColumns: '1fr 2fr', gap: '8px', marginTop: '4px' }}>
                    <button
                      onClick={() => navigate(`/cotacoes/${c.id}`)}
                      className="btn btn-secondary"
                      style={{ minHeight: '42px', fontSize: '13px' }}
                    >
                      <Eye size={15} /> Detalhes
                    </button>
                    <button
                      onClick={() => navigate(`/cotacoes/${c.id}/comparacao`)}
                      disabled={!temRespostas}
                      className={`btn ${temRespostas ? 'btn-primary' : 'btn-secondary'}`}
                      style={{ minHeight: '42px', fontSize: '13px', fontWeight: 700 }}
                    >
                      <Award size={15} /> Comparar & Campeã
                    </button>
                  </div>
                </div>
              );
            })
          )}
        </div>
      </div>
    </div>
  );
};

