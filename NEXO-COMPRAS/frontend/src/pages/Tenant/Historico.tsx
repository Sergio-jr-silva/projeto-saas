import React, { useState, useEffect } from 'react';
import { api } from '../../services/api';
import { HistoricoCompraItem, IndicadoresCompras, Produto } from '../../types';
import {
  History,
  Search,
  Download,
  TrendingDown,
  TrendingUp,
  DollarSign,
  Building2,
  Package,
  Calendar,
  Filter,
  RefreshCw,
  BarChart2,
  ArrowRight,
  GitCompare,
  Award
} from 'lucide-react';

export const Historico: React.FC = () => {
  const [compras, setCompras] = useState<HistoricoCompraItem[]>([]);
  const [indicadores, setIndicadores] = useState<IndicadoresCompras | null>(null);
  const [produtos, setProdutos] = useState<Produto[]>([]);
  const [loading, setLoading] = useState(true);
  const [busca, setBusca] = useState('');
  const [abaAtiva, setAbaAtiva] = useState<'compras' | 'comparativo' | 'evolucao' | 'ranking'>('compras');

  // Evolução de preço
  const [produtoSelecionadoId, setProdutoSelecionadoId] = useState<string>('');
  const [dadosEvolucao, setDadosEvolucao] = useState<any>(null);
  const [carregandoEvolucao, setCarregandoEvolucao] = useState(false);

  // Comparativo de Pedidos
  const [comparativoPedidos, setComparativoPedidos] = useState<any[]>([]);
  const [carregandoComparativo, setCarregandoComparativo] = useState(false);
  const [buscaComparativo, setBuscaComparativo] = useState('');

  const carregarDados = async () => {
    try {
      setLoading(true);
      const qs = busca ? `?busca=${encodeURIComponent(busca)}` : '';
      const [resCompras, resInd, resProd] = await Promise.all([
        api.get<any>(`/historico/compras${qs}`),
        api.get<any>('/historico/indicadores'),
        api.get<any>('/produtos?size=200')
      ]);

      const listaCompras = Array.isArray(resCompras) ? resCompras : (resCompras?.itens || []);
      const listaProdutos = Array.isArray(resProd) ? resProd : (resProd?.itens || []);

      setCompras(listaCompras);
      setIndicadores(resInd);
      setProdutos(listaProdutos);

      if (listaProdutos.length > 0 && !produtoSelecionadoId) {
        setProdutoSelecionadoId(listaProdutos[0].id);
      }
    } catch (err) {
      console.error('Erro ao carregar dados do histórico:', err);
    } finally {
      setLoading(false);
    }
  };

  const carregarComparativo = async () => {
    try {
      setCarregandoComparativo(true);
      const qs = buscaComparativo ? `?busca=${encodeURIComponent(buscaComparativo)}` : '';
      const res = await api.get<any>(`/historico/comparativo-pedidos${qs}`);
      setComparativoPedidos(res?.comparativo || []);
    } catch (err) {
      console.error('Erro ao carregar comparativo de pedidos:', err);
    } finally {
      setCarregandoComparativo(false);
    }
  };

  useEffect(() => {
    carregarDados();
  }, []);

  useEffect(() => {
    if (abaAtiva === 'comparativo') {
      carregarComparativo();
    }
  }, [abaAtiva]);

  useEffect(() => {
    if (produtoSelecionadoId && abaAtiva === 'evolucao') {
      carregarEvolucao(produtoSelecionadoId);
    }
  }, [produtoSelecionadoId, abaAtiva]);

  const carregarEvolucao = async (id: string) => {
    try {
      setCarregandoEvolucao(true);
      const res = await api.get<any>(`/historico/evolucao/${id}`);
      setDadosEvolucao(res);
    } catch (err) {
      console.error('Erro ao carregar evolução:', err);
    } finally {
      setCarregandoEvolucao(false);
    }
  };

  const exportarCsv = () => {
    if (compras.length === 0) return;
    const cabecalho = ['Data', 'Código', 'Produto', 'Fornecedor', 'Quantidade', 'Preço Unitário (R$)', 'Total (R$)', 'Origem'];
    const linhas = compras.map(c => [
      new Date(c.data).toLocaleDateString('pt-BR'),
      `"${c.produtoCodigo}"`,
      `"${c.produtoDescricao.replace(/"/g, '""')}"`,
      `"${c.fornecedorNome.replace(/"/g, '""')}"`,
      c.quantidade,
      c.valor.toFixed(2),
      c.valorTotal.toFixed(2),
      c.origem
    ]);

    const conteudoCsv = [cabecalho.join(';'), ...linhas.map(l => l.join(';'))].join('\n');
    const blob = new Blob(['\uFEFF' + conteudoCsv], { type: 'text/csv;charset=utf-8;' });
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = `historico_compras_${new Date().toISOString().slice(0, 10)}.csv`;
    link.click();
  };

  return (
    <div style={{ padding: '4px 0' }}>
      {/* HEADER */}
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', marginBottom: '24px', flexWrap: 'wrap', gap: '12px' }}>
        <div>
          <h1 style={{ fontSize: '24px', fontWeight: 800, color: 'var(--text-main)', margin: '0 0 6px 0', display: 'flex', alignItems: 'center', gap: '10px' }}>
            <History color="var(--blue-primary)" size={28} />
            Histórico e Comparação de Pedidos
          </h1>
          <p style={{ margin: 0, color: 'var(--text-muted)', fontSize: '14px' }}>
            Consulte preços praticados, compare pedidos entre fornecedores e acompanhe a evolução de custos.
          </p>
        </div>
        <div style={{ display: 'flex', gap: '10px' }}>
          <button
            onClick={exportarCsv}
            disabled={compras.length === 0}
            className="btn btn-secondary"
          >
            <Download size={16} />
            Exportar CSV
          </button>
          <button
            onClick={carregarDados}
            className="btn btn-primary"
          >
            <RefreshCw size={16} />
            Atualizar
          </button>
        </div>
      </div>

      {/* METRIC CARDS */}
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(220px, 1fr))', gap: '16px', marginBottom: '24px' }}>
        <div className="card" style={{ padding: '16px 20px' }}>
          <div style={{ fontSize: '12px', fontWeight: 600, color: 'var(--text-muted)', textTransform: 'uppercase' }}>Volume Total Comprado</div>
          <div style={{ fontSize: '24px', fontWeight: 800, color: '#1e3a8a', marginTop: '4px' }}>
            R$ {(indicadores?.totalComprado || 0).toLocaleString('pt-BR', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}
          </div>
        </div>
        <div className="card" style={{ padding: '16px 20px' }}>
          <div style={{ fontSize: '12px', fontWeight: 600, color: '#16a34a', textTransform: 'uppercase' }}>Economia Realizada</div>
          <div style={{ fontSize: '24px', fontWeight: 800, color: '#16a34a', marginTop: '4px' }}>
            R$ {(indicadores?.economiaRealizada || 0).toLocaleString('pt-BR', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}
          </div>
        </div>
        <div className="card" style={{ padding: '16px 20px' }}>
          <div style={{ fontSize: '12px', fontWeight: 600, color: '#2563eb', textTransform: 'uppercase' }}>Principal Fornecedor</div>
          <div style={{ fontSize: '18px', fontWeight: 800, color: '#2563eb', marginTop: '6px', whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis' }}>
            {indicadores?.fornecedorMaisComprado?.fornecedor || 'Nenhum'}
          </div>
        </div>
        <div className="card" style={{ padding: '16px 20px' }}>
          <div style={{ fontSize: '12px', fontWeight: 600, color: 'var(--text-muted)', textTransform: 'uppercase' }}>Pedidos Concluídos</div>
          <div style={{ fontSize: '24px', fontWeight: 800, color: 'var(--text-main)', marginTop: '4px' }}>
            {indicadores?.pedidosRecebidos || 0} de {indicadores?.totalPedidos || 0}
          </div>
        </div>
      </div>

      {/* TABS NAVEGAÇÃO */}
      <div style={{ display: 'flex', gap: '8px', borderBottom: '2px solid var(--border-light)', marginBottom: '20px', flexWrap: 'wrap' }}>
        {[
          { id: 'compras', label: 'Histórico de Entradas / Compras' },
          { id: 'comparativo', label: '📊 Comparativo de Pedidos & Preços' },
          { id: 'evolucao', label: 'Evolução de Preços por Produto' },
          { id: 'ranking', label: 'Ranking de Fornecedores' }
        ].map((t) => (
          <button
            key={t.id}
            onClick={() => setAbaAtiva(t.id as any)}
            style={{
              background: 'none',
              border: 'none',
              padding: '10px 18px',
              fontSize: '14px',
              fontWeight: abaAtiva === t.id ? 700 : 500,
              color: abaAtiva === t.id ? 'var(--blue-primary)' : 'var(--text-muted)',
              borderBottom: abaAtiva === t.id ? '3px solid var(--blue-primary)' : '3px solid transparent',
              cursor: 'pointer',
              marginBottom: '-2px'
            }}
          >
            {t.label}
          </button>
        ))}
      </div>

      {/* ABA 1: TODAS AS COMPRAS */}
      {abaAtiva === 'compras' && (
        <div>
          <div className="card" style={{ padding: '14px 18px', marginBottom: '16px', display: 'flex', gap: '16px', alignItems: 'center' }}>
            <div style={{ flex: 1, position: 'relative' }}>
              <Search size={18} style={{ position: 'absolute', left: '12px', top: '10px', color: 'var(--text-muted)' }} />
              <input
                type="text"
                placeholder="Buscar por descrição do produto, código ou fornecedor..."
                value={busca}
                onChange={(e) => setBusca(e.target.value)}
                onKeyDown={(e) => e.key === 'Enter' && carregarDados()}
                style={{
                  width: '100%',
                  padding: '9px 12px 9px 38px',
                  border: '1px solid var(--border)',
                  borderRadius: 'var(--radius-sm)',
                  fontSize: '14px',
                  outline: 'none'
                }}
              />
            </div>
            <button
              onClick={carregarDados}
              className="btn btn-primary"
            >
              Filtrar
            </button>
          </div>

          <div className="card" style={{ overflow: 'hidden' }}>
            {loading ? (
              <div style={{ padding: '60px', textAlign: 'center', color: 'var(--text-muted)' }}>Carregando histórico...</div>
            ) : compras.length === 0 ? (
              <div style={{ padding: '60px', textAlign: 'center', color: 'var(--text-muted)' }}>
                Nenhum registro de compra ou preço anterior localizado.
              </div>
            ) : (
              <table style={{ width: '100%', borderCollapse: 'collapse', textAlign: 'left', fontSize: '13px' }}>
                <thead>
                  <tr style={{ background: '#f8fafc', borderBottom: '1px solid var(--border)' }}>
                    <th style={{ padding: '12px 18px', fontWeight: 700, color: 'var(--text-muted)' }}>Data</th>
                    <th style={{ padding: '12px 18px', fontWeight: 700, color: 'var(--text-muted)' }}>Produto</th>
                    <th style={{ padding: '12px 18px', fontWeight: 700, color: 'var(--text-muted)' }}>Fornecedor</th>
                    <th style={{ padding: '12px 18px', fontWeight: 700, color: 'var(--text-muted)', textAlign: 'right' }}>Qtd</th>
                    <th style={{ padding: '12px 18px', fontWeight: 700, color: 'var(--text-muted)', textAlign: 'right' }}>Preço Unit</th>
                    <th style={{ padding: '12px 18px', fontWeight: 700, color: 'var(--text-muted)', textAlign: 'center' }}>Variação</th>
                    <th style={{ padding: '12px 18px', fontWeight: 700, color: 'var(--text-muted)', textAlign: 'right' }}>Total</th>
                    <th style={{ padding: '12px 18px', fontWeight: 700, color: 'var(--text-muted)', textAlign: 'center' }}>Origem</th>
                  </tr>
                </thead>
                <tbody>
                  {compras.map((c) => {
                    const varPerc = c.ultimoCustoReferencia > 0
                      ? ((c.valor - c.ultimoCustoReferencia) / c.ultimoCustoReferencia) * 100
                      : 0;
                    return (
                      <tr key={c.id} style={{ borderBottom: '1px solid var(--border-light)' }}>
                        <td style={{ padding: '12px 18px' }}>{new Date(c.data).toLocaleDateString('pt-BR')}</td>
                        <td style={{ padding: '12px 18px' }}>
                          <div style={{ fontWeight: 600, color: 'var(--text-main)' }}>{c.produtoDescricao}</div>
                          <div style={{ fontSize: '11px', color: 'var(--text-muted)' }}>{c.produtoCodigo} • {c.produtoUnidade}</div>
                        </td>
                        <td style={{ padding: '12px 18px', fontWeight: 500 }}>{c.fornecedorNome}</td>
                        <td style={{ padding: '12px 18px', textAlign: 'right', fontWeight: 600 }}>{c.quantidade}</td>
                        <td style={{ padding: '12px 18px', textAlign: 'right', fontWeight: 800 }}>R$ {c.valor.toFixed(2)}</td>
                        <td style={{ padding: '12px 18px', textAlign: 'center' }}>
                          {c.ultimoCustoReferencia > 0 ? (
                            <span
                              style={{
                                display: 'inline-flex',
                                alignItems: 'center',
                                gap: '2px',
                                padding: '2px 8px',
                                borderRadius: '12px',
                                fontSize: '11px',
                                fontWeight: 700,
                                backgroundColor: varPerc <= 0 ? '#dcfce7' : '#fee2e2',
                                color: varPerc <= 0 ? '#15803d' : '#b91c1c'
                              }}
                            >
                              {varPerc <= 0 ? <TrendingDown size={12} /> : <TrendingUp size={12} />}
                              {Math.abs(varPerc).toFixed(1)}%
                            </span>
                          ) : (
                            <span style={{ fontSize: '11px', color: 'var(--text-muted)' }}>—</span>
                          )}
                        </td>
                        <td style={{ padding: '12px 18px', textAlign: 'right', fontWeight: 800, color: '#1e40af' }}>
                          R$ {c.valorTotal.toLocaleString('pt-BR', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}
                        </td>
                        <td style={{ padding: '12px 18px', textAlign: 'center' }}>
                          <span className={`badge ${c.origem === 'cotacao' ? 'badge-blue' : 'badge-yellow'}`}>
                            {c.origem === 'cotacao' ? 'Cotação' : 'ERP / Pedido'}
                          </span>
                        </td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            )}
          </div>
        </div>
      )}

      {/* ABA 2: COMPARATIVO DE PEDIDOS & PREÇOS (PILLAR 4) */}
      {abaAtiva === 'comparativo' && (
        <div>
          <div className="card" style={{ padding: '14px 18px', marginBottom: '16px', display: 'flex', gap: '16px', alignItems: 'center' }}>
            <div style={{ flex: 1, position: 'relative' }}>
              <Search size={18} style={{ position: 'absolute', left: '12px', top: '10px', color: 'var(--text-muted)' }} />
              <input
                type="text"
                placeholder="Filtrar por nome ou código do produto no comparativo..."
                value={buscaComparativo}
                onChange={(e) => setBuscaComparativo(e.target.value)}
                onKeyDown={(e) => e.key === 'Enter' && carregarComparativo()}
                style={{
                  width: '100%',
                  padding: '9px 12px 9px 38px',
                  border: '1px solid var(--border)',
                  borderRadius: 'var(--radius-sm)',
                  fontSize: '14px',
                  outline: 'none'
                }}
              />
            </div>
            <button
              onClick={carregarComparativo}
              className="btn btn-primary"
            >
              Comparar Pedidos
            </button>
          </div>

          {carregandoComparativo ? (
            <div className="card" style={{ padding: '60px', textAlign: 'center', color: 'var(--text-muted)' }}>
              Comparando preços e prazos entre pedidos realizados...
            </div>
          ) : comparativoPedidos.length === 0 ? (
            <div className="card" style={{ padding: '60px', textAlign: 'center' }}>
              <GitCompare size={44} style={{ color: 'var(--border)', margin: '0 auto 12px' }} />
              <div style={{ fontWeight: 700, fontSize: '16px', color: 'var(--text-main)' }}>
                Nenhum histórico de pedidos para comparação
              </div>
              <div style={{ fontSize: '13px', color: 'var(--text-muted)', marginTop: '4px' }}>
                Conforme pedidos forem gerados ou sincronizados, você poderá comparar as variações de preços e os melhores fornecedores aqui.
              </div>
            </div>
          ) : (
            <div style={{ display: 'flex', flexDirection: 'column', gap: '18px' }}>
              {comparativoPedidos.map((prod) => (
                <div key={prod.produtoId} className="card" style={{ padding: '20px' }}>
                  <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', marginBottom: '14px', flexWrap: 'wrap', gap: '10px' }}>
                    <div>
                      <h3 style={{ fontSize: '17px', fontWeight: 800, color: 'var(--text-main)', margin: '0 0 4px 0' }}>
                        {prod.descricao}
                      </h3>
                      <div style={{ fontSize: '12px', color: 'var(--text-muted)' }}>
                        Código: <strong>{prod.codigo}</strong> • Unidade: <strong>{prod.unidade}</strong> • {prod.totalCompras} pedido(s) registrado(s)
                      </div>
                    </div>

                    <div style={{ display: 'flex', gap: '12px', flexWrap: 'wrap' }}>
                      <div style={{ background: '#f0fdf4', border: '1px solid #bbf7d0', borderRadius: '8px', padding: '6px 12px', textAlign: 'right' }}>
                        <div style={{ fontSize: '11px', color: '#16a34a', fontWeight: 600 }}>Melhor Preço Registrado</div>
                        <div style={{ fontSize: '16px', fontWeight: 800, color: '#16a34a' }}>
                          R$ {prod.menorPreco.toFixed(2)}
                        </div>
                        <div style={{ fontSize: '10.5px', color: '#15803d' }}>{prod.fornecedorMaisVantajoso}</div>
                      </div>

                      <div style={{ background: '#eff6ff', border: '1px solid #bfdbfe', borderRadius: '8px', padding: '6px 12px', textAlign: 'right' }}>
                        <div style={{ fontSize: '11px', color: '#2563eb', fontWeight: 600 }}>Preço Médio / Último</div>
                        <div style={{ fontSize: '16px', fontWeight: 800, color: '#1e40af' }}>
                          R$ {prod.precoMedio.toFixed(2)} / R$ {prod.ultimoPreco.toFixed(2)}
                        </div>
                        <div style={{ fontSize: '10.5px', color: prod.variacaoGeralPerc <= 0 ? '#16a34a' : '#dc2626', fontWeight: 600 }}>
                          Variação: {prod.variacaoGeralPerc > 0 ? `+${prod.variacaoGeralPerc}%` : `${prod.variacaoGeralPerc}%`}
                        </div>
                      </div>
                    </div>
                  </div>

                  {/* TABELA DE PEDIDOS DESTE PRODUTO */}
                  <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: '13px' }}>
                    <thead>
                      <tr style={{ background: '#f8fafc', borderBottom: '1px solid var(--border)' }}>
                        <th style={{ padding: '8px 12px', textAlign: 'left' }}>Pedido</th>
                        <th style={{ padding: '8px 12px', textAlign: 'left' }}>Data</th>
                        <th style={{ padding: '8px 12px', textAlign: 'left' }}>Fornecedor</th>
                        <th style={{ padding: '8px 12px', textAlign: 'right' }}>Qtd</th>
                        <th style={{ padding: '8px 12px', textAlign: 'right' }}>Preço Unitário</th>
                        <th style={{ padding: '8px 12px', textAlign: 'center' }}>Prazo Acordado</th>
                        <th style={{ padding: '8px 12px', textAlign: 'center' }}>Status</th>
                      </tr>
                    </thead>
                    <tbody>
                      {prod.historico.map((h: any, idx: number) => {
                        const ehMelhor = h.precoLiquidoUnit === prod.menorPreco;
                        return (
                          <tr key={idx} style={{ borderBottom: '1px solid var(--border-light)', backgroundColor: ehMelhor ? '#f0fdf4' : 'transparent' }}>
                            <td style={{ padding: '8px 12px', fontWeight: 700, color: 'var(--blue-primary)' }}>{h.pedidoNumero}</td>
                            <td style={{ padding: '8px 12px' }}>{new Date(h.dataCriacao).toLocaleDateString('pt-BR')}</td>
                            <td style={{ padding: '8px 12px', fontWeight: 600 }}>
                              {h.fornecedorNome}
                              {ehMelhor && <span className="badge badge-green" style={{ marginLeft: '6px', fontSize: '10px' }}>🏆 Menor Preço</span>}
                            </td>
                            <td style={{ padding: '8px 12px', textAlign: 'right' }}>{h.quantidade}</td>
                            <td style={{ padding: '8px 12px', textAlign: 'right', fontWeight: 800 }}>R$ {h.precoLiquidoUnit.toFixed(2)}</td>
                            <td style={{ padding: '8px 12px', textAlign: 'center' }}>{h.prazoEntregaDias} dias</td>
                            <td style={{ padding: '8px 12px', textAlign: 'center' }}>
                              <span className="badge" style={{ fontSize: '11px' }}>{h.statusPedido}</span>
                            </td>
                          </tr>
                        );
                      })}
                    </tbody>
                  </table>
                </div>
              ))}
            </div>
          )}
        </div>
      )}

      {/* ABA 3: EVOLUÇÃO DE PREÇOS POR PRODUTO */}
      {abaAtiva === 'evolucao' && (
        <div className="card" style={{ padding: '20px' }}>
          <div style={{ marginBottom: '20px' }}>
            <label style={{ display: 'block', fontSize: '13px', fontWeight: 700, marginBottom: '6px', color: 'var(--text-main)' }}>
              Selecione o Produto para Análise de Evolução:
            </label>
            <select
              value={produtoSelecionadoId}
              onChange={(e) => setProdutoSelecionadoId(e.target.value)}
              style={{
                width: '100%',
                maxWidth: '500px',
                padding: '9px 12px',
                border: '1px solid var(--border)',
                borderRadius: 'var(--radius-sm)',
                fontSize: '14px',
                backgroundColor: '#FFFFFF',
                outline: 'none'
              }}
            >
              {produtos.map((p) => (
                <option key={p.id} value={p.id}>
                  {p.descricao} ({p.codigoErp})
                </option>
              ))}
            </select>
          </div>

          {carregandoEvolucao ? (
            <div style={{ padding: '40px', textAlign: 'center' }}>Carregando evolução de preços...</div>
          ) : dadosEvolucao?.historico?.length > 0 ? (
            <div>
              <div style={{ display: 'flex', gap: '20px', marginBottom: '20px', background: '#f8fafc', padding: '14px', borderRadius: '8px', flexWrap: 'wrap' }}>
                <div><strong>Produto:</strong> {dadosEvolucao.produto.descricao}</div>
                <div><strong>Último Custo de Compra:</strong> R$ {dadosEvolucao.produto.ultimoCusto?.toFixed(2)}</div>
                <div><strong>Preço de Venda:</strong> R$ {dadosEvolucao.produto.precoVenda?.toFixed(2)}</div>
              </div>

              <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: '13px' }}>
                <thead>
                  <tr style={{ background: '#f1f5f9', borderBottom: '1px solid var(--border)' }}>
                    <th style={{ padding: '10px 14px', textAlign: 'left' }}>Data da Oferta / Compra</th>
                    <th style={{ padding: '10px 14px', textAlign: 'left' }}>Fornecedor</th>
                    <th style={{ padding: '10px 14px', textAlign: 'right' }}>Preço Unitário</th>
                    <th style={{ padding: '10px 14px', textAlign: 'right' }}>Quantidade</th>
                    <th style={{ padding: '10px 14px', textAlign: 'center' }}>Origem</th>
                  </tr>
                </thead>
                <tbody>
                  {dadosEvolucao.historico.map((pt: any, i: number) => (
                    <tr key={i} style={{ borderBottom: '1px solid var(--border-light)' }}>
                      <td style={{ padding: '10px 14px' }}>{new Date(pt.data).toLocaleDateString('pt-BR')}</td>
                      <td style={{ padding: '10px 14px', fontWeight: 600 }}>{pt.fornecedor}</td>
                      <td style={{ padding: '10px 14px', textAlign: 'right', fontWeight: 800, color: 'var(--blue-primary)' }}>
                        R$ {pt.valor.toFixed(2)}
                      </td>
                      <td style={{ padding: '10px 14px', textAlign: 'right' }}>{pt.quantidade}</td>
                      <td style={{ padding: '10px 14px', textAlign: 'center' }}>
                        <span style={{ textTransform: 'uppercase', fontSize: '11px', color: '#64748b' }}>{pt.origem}</span>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          ) : (
            <div style={{ padding: '40px', textAlign: 'center', color: 'var(--text-muted)' }}>
              Nenhum dado histórico registrado para este produto.
            </div>
          )}
        </div>
      )}

      {/* ABA 4: RANKING DE FORNECEDORES */}
      {abaAtiva === 'ranking' && (
        <div className="card" style={{ padding: '20px' }}>
          <h3 style={{ fontSize: '16px', fontWeight: 800, marginBottom: '16px', color: 'var(--text-main)' }}>
            Ranking de Compras por Fornecedor
          </h3>
          <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: '13px' }}>
            <thead>
              <tr style={{ background: '#f8fafc', borderBottom: '1px solid var(--border)' }}>
                <th style={{ padding: '12px 18px', textAlign: 'left' }}>Posição</th>
                <th style={{ padding: '12px 18px', textAlign: 'left' }}>Fornecedor</th>
                <th style={{ padding: '12px 18px', textAlign: 'center' }}>Pedidos Realizados</th>
                <th style={{ padding: '12px 18px', textAlign: 'right' }}>Total Comprado</th>
              </tr>
            </thead>
            <tbody>
              {indicadores?.rankingFornecedores?.map((rf, idx) => (
                <tr key={rf.fornecedor} style={{ borderBottom: '1px solid var(--border-light)' }}>
                  <td style={{ padding: '12px 18px', fontWeight: 800, color: idx === 0 ? '#eab308' : '#64748b' }}>
                    #{idx + 1}
                  </td>
                  <td style={{ padding: '12px 18px', fontWeight: 700, color: 'var(--text-main)' }}>
                    {rf.fornecedor}
                  </td>
                  <td style={{ padding: '12px 18px', textAlign: 'center' }}>{rf.qtdPedidos}</td>
                  <td style={{ padding: '12px 18px', textAlign: 'right', fontWeight: 800, color: '#1e40af' }}>
                    R$ {rf.total.toLocaleString('pt-BR', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
};
