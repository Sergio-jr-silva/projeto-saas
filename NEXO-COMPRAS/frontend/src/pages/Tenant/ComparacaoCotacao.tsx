import React, { useState, useEffect } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { api } from '../../services/api';
import { ComparativoCotacao, ComparacaoFornecedor, ComparacaoItem } from '../../types';
import {
  ArrowLeft,
  CheckCircle,
  TrendingDown,
  Sparkles,
  Award,
  Truck,
  CreditCard,
  Check,
  AlertCircle,
  DollarSign
} from 'lucide-react';

export const ComparacaoCotacaoPage: React.FC = () => {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();

  const [comparativo, setComparativo] = useState<ComparativoCotacao | null>(null);
  const [loading, setLoading] = useState(true);
  const [escolhas, setEscolhas] = useState<Record<string, string>>({}); // { cotacaoItemId: cotacaoFornecedorId }
  const [observacaoAprovacao, setObservacaoAprovacao] = useState('');
  const [showModalAprovar, setShowModalAprovar] = useState(false);
  const [aprovando, setAprovando] = useState(false);

  const carregarComparativo = async () => {
    if (!id) return;
    setLoading(true);
    try {
      const res = await api.get<ComparativoCotacao>(`/cotacoes/${id}/comparacao`);
      setComparativo(res);

      // Pré-carrega as escolhas da recomendação ou escolhas já salvas
      if (res.recomendacao?.escolhas && Object.keys(res.recomendacao.escolhas).length > 0) {
        setEscolhas(res.recomendacao.escolhas);
      } else if (res.escolhas && Object.keys(res.escolhas).length > 0) {
        setEscolhas(res.escolhas);
      } else {
        // Seleciona por padrão o de melhor custo efetivo por item
        const defaultEscolhas: Record<string, string> = {};
        res.itens.forEach((it) => {
          const melhor = it.ofertas.find((o) => o.melhorCusto && o.disponivel) || it.ofertas[0];
          if (melhor) defaultEscolhas[it.cotacaoItemId] = melhor.cotacaoFornecedorId;
        });
        setEscolhas(defaultEscolhas);
      }
    } catch (err: any) {
      alert('Erro ao carregar comparativo: ' + err.message);
      navigate('/cotacoes');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    carregarComparativo();
  }, [id]);

  const aplicarRecomendacao = () => {
    if (comparativo?.recomendacao?.escolhas) {
      setEscolhas(comparativo.recomendacao.escolhas);
    }
  };

  const selecionarFornecedorParaTodos = (cfId: string) => {
    if (!comparativo) return;
    const novas: Record<string, string> = {};
    comparativo.itens.forEach((it) => {
      if (it.ofertas.some((o) => o.cotacaoFornecedorId === cfId && o.disponivel)) {
        novas[it.cotacaoItemId] = cfId;
      }
    });
    setEscolhas(novas);
  };

  const alterarEscolhaItem = (cotacaoItemId: string, cfId: string) => {
    setEscolhas((prev) => ({ ...prev, [cotacaoItemId]: cfId }));
  };

  // Cálculos dinâmicos em tempo real conforme as escolhas do comprador
  const calcularTotais = () => {
    if (!comparativo) return { totalAprovado: 0, totalReferencia: 0, economia: 0, economiaPerc: 0 };
    let totalAprovado = 0;
    let totalReferencia = 0;

    comparativo.itens.forEach((it) => {
      const cfId = escolhas[it.cotacaoItemId];
      const oferta = it.ofertas.find((o) => o.cotacaoFornecedorId === cfId && o.disponivel);
      if (oferta) {
        totalAprovado += oferta.custoEfetivoUnit * it.quantidade;
        totalReferencia += it.referenciaCustoUnit * it.quantidade;
      }
    });

    // Soma fretes dos fornecedores que tiveram pelo menos 1 item selecionado
    const fornecedoresComItens = new Set(Object.values(escolhas));
    fornecedoresComItens.forEach((cfId) => {
      const f = comparativo.fornecedores.find((x) => x.cotacaoFornecedorId === cfId);
      if (f) totalAprovado += f.frete + f.outrasDespesas;
    });

    const economia = Math.max(0, totalReferencia - totalAprovado);
    const economiaPerc = totalReferencia > 0 ? (economia / totalReferencia) * 100 : 0;

    return { totalAprovado, totalReferencia, economia, economiaPerc };
  };

  const { totalAprovado, totalReferencia, economia, economiaPerc } = calcularTotais();

  const handleAprovar = async () => {
    if (Object.keys(escolhas).length === 0) {
      alert('Selecione pelo menos um fornecedor para os itens da cotação.');
      return;
    }

    setAprovando(true);
    try {
      await api.post(`/cotacoes/${id}/aprovar`, {
        escolhas,
        observacao: observacaoAprovacao.trim() || 'Aprovação baseada no comparativo de custo efetivo da proposta campeã.',
      });
      alert('Cotação aprovada com sucesso! O Pedido de Compra Oficial foi gerado com número sequencial e está pronto para envio ao fornecedor.');
      navigate('/pedidos');
    } catch (err: any) {
      alert('Erro ao aprovar cotação: ' + err.message);
    } finally {
      setAprovando(false);
      setShowModalAprovar(false);
    }
  };

  if (loading || !comparativo) {
    return <div style={{ padding: '60px', textAlign: 'center' }}>Calculando comparativo comercial e custo efetivo...</div>;
  }

  return (
    <div>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '20px', flexWrap: 'wrap', gap: '12px' }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: '12px' }}>
          <button onClick={() => navigate(`/cotacoes/${id}`)} className="btn btn-secondary" style={{ padding: '8px 12px' }}>
            <ArrowLeft size={16} /> Voltar à Cotação
          </button>
          <div>
            <h1 style={{ fontSize: '24px', fontWeight: 800, color: 'var(--text-main)' }}>
              Comparativo Comercial e Custo Efetivo
            </h1>
            <p style={{ fontSize: '14px', color: 'var(--text-muted)' }}>
              Regime Tributário da Empresa: <strong>{comparativo.regime}</strong> (Crédito ICMS: {comparativo.creditaIcms ? 'Sim' : 'Não'} • IPI: {comparativo.creditaIpi ? 'Sim' : 'Não'})
            </p>
          </div>
        </div>

        {comparativo.status !== 'Aprovada' && comparativo.status !== 'Cancelada' && (
          <button
            onClick={() => setShowModalAprovar(true)}
            className="btn btn-primary"
            style={{ padding: '10px 20px', fontSize: '15px' }}
          >
            <CheckCircle size={18} /> Aprovar Compra Selecionada
          </button>
        )}
      </div>

      {/* CARDS DE INDICADORES / KPI */}
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(220px, 1fr))', gap: '16px', marginBottom: '24px' }}>
        <div className="card" style={{ padding: '18px 20px' }}>
          <div style={{ fontSize: '13px', color: 'var(--text-muted)', marginBottom: '4px' }}>Economia Calculada</div>
          <div style={{ fontSize: '24px', fontWeight: 800, color: 'var(--green)' }}>
            R$ {economia.toLocaleString('pt-BR', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}
          </div>
          <div style={{ fontSize: '12px', color: 'var(--green)', marginTop: '2px', fontWeight: 600 }}>
            {economiaPerc.toFixed(1)}% de economia vs custo de referência
          </div>
        </div>

        <div className="card" style={{ padding: '18px 20px' }}>
          <div style={{ fontSize: '13px', color: 'var(--text-muted)', marginBottom: '4px' }}>Total Estimado da Compra</div>
          <div style={{ fontSize: '24px', fontWeight: 800, color: 'var(--text-main)' }}>
            R$ {totalAprovado.toLocaleString('pt-BR', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}
          </div>
          <div style={{ fontSize: '12px', color: 'var(--text-muted)', marginTop: '2px' }}>
            Valor de referência: R$ {totalReferencia.toLocaleString('pt-BR', { minimumFractionDigits: 2 })}
          </div>
        </div>

        <div className="card" style={{ padding: '18px 20px' }}>
          <div style={{ fontSize: '13px', color: 'var(--text-muted)', marginBottom: '4px' }}>Fornecedores Participantes</div>
          <div style={{ fontSize: '24px', fontWeight: 800, color: 'var(--primary)' }}>
            {comparativo.fornecedores.length}
          </div>
          <div style={{ fontSize: '12px', color: 'var(--text-muted)', marginTop: '2px' }}>
            Propostas comerciais recebidas
          </div>
        </div>
      </div>

      {/* CARD DE RECOMENDAÇÃO INTELIGENTE (FASE 5) */}
      {comparativo.recomendacao && (
        <div
          className="card"
          style={{
            padding: '20px',
            marginBottom: '24px',
            border: '2px solid var(--primary)',
            backgroundColor: 'var(--blue-light)',
          }}
        >
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap', gap: '12px' }}>
            <div style={{ display: 'flex', alignItems: 'flex-start', gap: '14px' }}>
              <div
                style={{
                  width: '42px',
                  height: '42px',
                  borderRadius: '10px',
                  backgroundColor: 'var(--primary)',
                  color: '#fff',
                  display: 'flex',
                  alignItems: 'center',
                  justifyContent: 'center',
                  flexShrink: 0,
                }}
              >
                <Sparkles size={22} />
              </div>
              <div>
                <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
                  <h3 style={{ fontSize: '16px', fontWeight: 800, color: 'var(--text-main)' }}>
                    Recomendação de Compra Inteligente
                  </h3>
                  <span className="badge badge-blue">
                    {comparativo.recomendacao.tipo === 'FornecedorUnico' ? 'Fornecedor Único Vencedor' : 'Combinação por Melhor Custo'}
                  </span>
                </div>
                <p style={{ fontSize: '13.5px', color: 'var(--text-muted)', marginTop: '4px' }}>
                  {comparativo.recomendacao.justificativa}
                </p>
              </div>
            </div>

            <button onClick={aplicarRecomendacao} className="btn btn-primary" style={{ height: '40px' }}>
              <Award size={16} /> Aplicar Esta Recomendação
            </button>
          </div>
        </div>
      )}

      {/* AVISO SE NÃO HOUVER PROPOSTAS */}
      {comparativo.fornecedores.length === 0 && (
        <div className="card" style={{ padding: '36px 24px', textAlign: 'center', marginBottom: '24px' }}>
          <AlertCircle size={44} style={{ color: 'var(--yellow)', margin: '0 auto 12px' }} />
          <h2 style={{ fontSize: '18px', fontWeight: 800, color: 'var(--text-main)', marginBottom: '8px' }}>
            Aguardando Envio de Propostas Comerciais
          </h2>
          <p style={{ fontSize: '14px', color: 'var(--text-muted)', maxWidth: '600px', margin: '0 auto 20px' }}>
            Nenhum fornecedor participante enviou proposta para esta cotação até o momento. Para comparar preços, os fornecedores convidados devem acessar o link seguro com o código de validação e preencher os valores no Portal do Fornecedor.
          </p>
          <button onClick={() => navigate(`/cotacoes/${id}`)} className="btn btn-primary" style={{ padding: '10px 20px' }}>
            📋 Ver Links de Acesso dos Fornecedores
          </button>
        </div>
      )}

      {/* RESUMO DOS FORNECEDORES (FRETE, PRAZO, CONDIÇÃO) */}
      {comparativo.fornecedores.length > 0 && (
      <div className="card" style={{ marginBottom: '24px', padding: '20px' }}>
        <h2 style={{ fontSize: '16px', fontWeight: 700, marginBottom: '14px', color: 'var(--text-main)' }}>
          Condições Comerciais Gerais dos Fornecedores
        </h2>
        <div style={{ display: 'grid', gridTemplateColumns: `repeat(auto-fit, minmax(260px, 1fr))`, gap: '16px' }}>
          {comparativo.fornecedores.map((f) => (
            <div
              key={f.cotacaoFornecedorId}
              style={{
                border: f.melhorCustoTotal ? '2px solid #16A34A' : '1px solid var(--border)',
                borderRadius: '8px',
                padding: '16px',
                backgroundColor: f.melhorCustoTotal ? '#F0FDF4' : 'var(--bg-main)',
                position: 'relative',
                boxShadow: f.melhorCustoTotal ? '0 4px 12px rgba(22, 163, 74, 0.15)' : 'none'
              }}
            >
              <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '8px', flexWrap: 'wrap', gap: '6px' }}>
                <span style={{ fontWeight: 800, fontSize: '15px', color: 'var(--text-main)' }}>{f.nome}</span>
                {f.melhorCustoTotal && (
                  <span className="badge badge-green" style={{ fontSize: '11px', fontWeight: 800, display: 'inline-flex', alignItems: 'center', gap: '4px' }}>
                    🏆 PROPOSTA CAMPEÃ
                  </span>
                )}
              </div>

              <div style={{ display: 'flex', flexDirection: 'column', gap: '6px', fontSize: '13px', color: 'var(--text-muted)' }}>
                <div style={{ display: 'flex', alignItems: 'center', gap: '6px' }}>
                  <Truck size={14} />
                  <span>Prazo: <strong>{f.prazoEntregaDias} dias</strong></span>
                  {f.melhorPrazo && <span className="badge badge-blue" style={{ fontSize: '10px' }}>Mais Rápido</span>}
                </div>
                <div style={{ display: 'flex', alignItems: 'center', gap: '6px' }}>
                  <CreditCard size={14} />
                  <span>Pagamento: <strong>{f.condicaoPagamento || 'Não informado'}</strong></span>
                </div>
                <div style={{ display: 'flex', alignItems: 'center', gap: '6px' }}>
                  <DollarSign size={14} />
                  <span>Frete: <strong>{f.frete === 0 ? 'Grátis' : `R$ ${f.frete.toFixed(2).replace('.', ',')}`}</strong></span>
                </div>
                <div style={{ marginTop: '8px', paddingTop: '8px', borderTop: '1px solid var(--border)' }}>
                  <span style={{ fontSize: '12px', display: 'block' }}>Total Efetivo das Ofertas:</span>
                  <span style={{ fontSize: '17px', fontWeight: 800, color: f.melhorCustoTotal ? '#16A34A' : 'var(--text-main)' }}>
                    R$ {f.custoEfetivoTotal.toLocaleString('pt-BR', { minimumFractionDigits: 2 })}
                  </span>
                </div>
              </div>

              {comparativo.status !== 'Aprovada' && (
                <button
                  onClick={() => selecionarFornecedorParaTodos(f.cotacaoFornecedorId)}
                  className={`btn ${f.melhorCustoTotal ? 'btn-primary' : 'btn-secondary'}`}
                  style={{ width: '100%', marginTop: '12px', fontSize: '12.5px', padding: '7px 10px', fontWeight: 700 }}
                >
                  {f.melhorCustoTotal ? '🏆 Comprar Tudo com a Campeã' : 'Comprar Todos com Este Fornecedor'}
                </button>
              )}
            </div>
          ))}
        </div>
      </div>
      )}

      {/* MATRIZ DE COMPARATIVO ITEM A ITEM */}
      <div className="card" style={{ padding: '20px' }}>
        <h2 style={{ fontSize: '16px', fontWeight: 700, marginBottom: '6px', color: 'var(--text-main)' }}>
          Matriz Comparativa de Propostas por Item
        </h2>
        <p style={{ fontSize: '13px', color: 'var(--text-muted)', marginBottom: '16px' }}>
          O Custo Efetivo considera: Preço - Desconto + IPI + ICMS ST + Rateio de Frete - Créditos de ICMS/PIS/COFINS (conforme regime).
        </p>

        <div className="table-container desktop-table-view">
          <table>
            <thead>
              <tr>
                <th style={{ width: '220px' }}>Produto</th>
                <th style={{ textAlign: 'center', width: '80px' }}>Qtd</th>
                <th style={{ textAlign: 'right', width: '120px' }}>Último Custo Ref.</th>
                {comparativo.fornecedores.map((f) => (
                  <th key={f.cotacaoFornecedorId} style={{ textAlign: 'center', minWidth: '180px' }}>
                    <div style={{ fontWeight: 700 }}>{f.nome}</div>
                  </th>
                ))}
              </tr>
            </thead>
            <tbody>
              {comparativo.itens.map((it) => (
                <tr key={it.cotacaoItemId}>
                  <td>
                    <div style={{ fontWeight: 600, color: 'var(--text-main)' }}>{it.descricao}</div>
                    <div style={{ fontSize: '11px', fontFamily: 'monospace', color: 'var(--text-muted)' }}>
                      {it.codigo} • {it.unidade}
                    </div>
                  </td>
                  <td style={{ textAlign: 'center', fontWeight: 600 }}>{it.quantidade}</td>
                  <td style={{ textAlign: 'right', color: 'var(--text-muted)' }}>
                    R$ {it.referenciaCustoUnit.toFixed(2).replace('.', ',')}
                  </td>

                  {comparativo.fornecedores.map((f) => {
                    const oferta = it.ofertas.find((o) => o.cotacaoFornecedorId === f.cotacaoFornecedorId);
                    const selecionado = escolhas[it.cotacaoItemId] === f.cotacaoFornecedorId;

                    if (!oferta || !oferta.disponivel) {
                      return (
                        <td key={f.cotacaoFornecedorId} style={{ textAlign: 'center', color: 'var(--text-muted)', backgroundColor: '#fafafa' }}>
                          <span style={{ fontSize: '12px' }}>Indisponível</span>
                        </td>
                      );
                    }

                    return (
                      <td
                        key={f.cotacaoFornecedorId}
                        onClick={() => alterarEscolhaItem(it.cotacaoItemId, f.cotacaoFornecedorId)}
                        style={{
                          textAlign: 'center',
                          cursor: 'pointer',
                          backgroundColor: selecionado ? 'var(--blue-light)' : oferta.melhorCusto ? '#f0fdf4' : '#fff',
                          border: selecionado ? '2px solid var(--primary)' : '1px solid var(--border)',
                          transition: 'all 0.15s ease',
                          padding: '12px 8px',
                        }}
                      >
                        <div style={{ display: 'flex', justifyContent: 'center', alignItems: 'center', gap: '6px', marginBottom: '4px' }}>
                          <input
                            type="radio"
                            name={`item_${it.cotacaoItemId}`}
                            checked={selecionado}
                            onChange={() => alterarEscolhaItem(it.cotacaoItemId, f.cotacaoFornecedorId)}
                            style={{ width: 'auto' }}
                          />
                          <span style={{ fontSize: '15px', fontWeight: 800, color: oferta.melhorCusto ? 'var(--green)' : 'var(--text-main)' }}>
                            R$ {oferta.custoEfetivoUnit.toFixed(2).replace('.', ',')}
                          </span>
                        </div>

                        <div style={{ fontSize: '11px', color: 'var(--text-muted)' }}>
                          Bruto: R$ {oferta.precoUnitario.toFixed(2).replace('.', ',')}
                          {oferta.ipi > 0 && ` • IPI: ${oferta.ipi}%`}
                          {oferta.st > 0 && ` • ST: R$ ${oferta.st.toFixed(2)}`}
                        </div>

                        <div style={{ marginTop: '4px' }}>
                          {oferta.melhorCusto && (
                            <span className="badge badge-green" style={{ fontSize: '10px' }}>
                              Melhor Custo Efetivo
                            </span>
                          )}
                          {oferta.menorPreco && !oferta.melhorCusto && (
                            <span className="badge badge-yellow" style={{ fontSize: '10px' }}>
                              Menor Preço Bruto
                            </span>
                          )}
                        </div>
                      </td>
                    );
                  })}
                </tr>
              ))}
            </tbody>
          </table>
        </div>

        {/* MOBILE CARD VIEW: SELEÇÃO ITEM A ITEM */}
        <div className="mobile-card-view" style={{ display: 'flex', flexDirection: 'column', gap: '16px' }}>
          {comparativo.itens.map((it) => (
            <div
              key={it.cotacaoItemId}
              style={{
                backgroundColor: '#FFFFFF',
                border: '1px solid var(--border)',
                borderRadius: 'var(--radius-md)',
                padding: '14px',
                boxShadow: 'var(--shadow-sm)'
              }}
            >
              <div style={{ borderBottom: '1px solid var(--border-light)', paddingBottom: '8px', marginBottom: '10px' }}>
                <div style={{ fontWeight: 700, fontSize: '14.5px', color: 'var(--text-main)' }}>
                  {it.descricao}
                </div>
                <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: '11.5px', color: 'var(--text-muted)', marginTop: '2px' }}>
                  <span>{it.codigo} • Qtd: <strong>{it.quantidade} {it.unidade}</strong></span>
                  <span>Último custo ref: <strong>R$ {it.referenciaCustoUnit.toFixed(2).replace('.', ',')}</strong></span>
                </div>
              </div>

              <div style={{ display: 'flex', flexDirection: 'column', gap: '8px' }}>
                {comparativo.fornecedores.map((f) => {
                  const oferta = it.ofertas.find((o) => o.cotacaoFornecedorId === f.cotacaoFornecedorId);
                  const selecionado = escolhas[it.cotacaoItemId] === f.cotacaoFornecedorId;

                  if (!oferta || !oferta.disponivel) {
                    return (
                      <div
                        key={f.cotacaoFornecedorId}
                        style={{
                          padding: '10px 12px',
                          borderRadius: '8px',
                          backgroundColor: '#f8fafc',
                          border: '1px solid #e2e8f0',
                          opacity: 0.6,
                          display: 'flex',
                          justifyContent: 'space-between',
                          fontSize: '12px'
                        }}
                      >
                        <span style={{ fontWeight: 600 }}>{f.nome}</span>
                        <span style={{ color: 'var(--text-muted)' }}>Indisponível</span>
                      </div>
                    );
                  }

                  return (
                    <div
                      key={f.cotacaoFornecedorId}
                      onClick={() => alterarEscolhaItem(it.cotacaoItemId, f.cotacaoFornecedorId)}
                      style={{
                        padding: '12px',
                        borderRadius: '8px',
                        cursor: 'pointer',
                        backgroundColor: selecionado ? '#eff6ff' : '#ffffff',
                        border: selecionado ? '2px solid var(--blue-primary)' : '1px solid var(--border)',
                        boxShadow: selecionado ? '0 2px 8px rgba(37,99,235,0.15)' : 'none',
                        transition: 'all 0.15s ease'
                      }}
                    >
                      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                        <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
                          <input
                            type="radio"
                            name={`mobile_item_${it.cotacaoItemId}`}
                            checked={selecionado}
                            onChange={() => alterarEscolhaItem(it.cotacaoItemId, f.cotacaoFornecedorId)}
                            style={{ width: '18px', height: '18px', cursor: 'pointer' }}
                          />
                          <span style={{ fontWeight: 700, fontSize: '13.5px', color: 'var(--text-main)' }}>
                            {f.nome}
                          </span>
                        </div>
                        <span style={{ fontSize: '15px', fontWeight: 800, color: oferta.melhorCusto ? 'var(--green)' : 'var(--text-main)' }}>
                          R$ {oferta.custoEfetivoUnit.toFixed(2).replace('.', ',')}
                          <span style={{ fontSize: '10.5px', fontWeight: 500, color: 'var(--text-muted)', display: 'block', textAlign: 'right' }}>por un.</span>
                        </span>
                      </div>

                      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginTop: '6px', fontSize: '11.5px', color: 'var(--text-muted)' }}>
                        <span>Bruto: R$ {oferta.precoUnitario.toFixed(2).replace('.', ',')} {oferta.ipi > 0 ? `+ IPI ${oferta.ipi}%` : ''}</span>
                        <span>Total: <strong>R$ {(oferta.custoEfetivoUnit * it.quantidade).toLocaleString('pt-BR', { minimumFractionDigits: 2 })}</strong></span>
                      </div>

                      {oferta.melhorCusto && (
                        <div style={{ marginTop: '6px' }}>
                          <span className="badge badge-green" style={{ fontSize: '10.5px', padding: '2px 8px' }}>
                            🏆 Menor Custo Efetivo
                          </span>
                        </div>
                      )}
                    </div>
                  );
                })}
              </div>
            </div>
          ))}
        </div>
      </div>

      {/* MODAL DE CONFIRMAÇÃO DA APROVAÇÃO */}
      {showModalAprovar && (
        <div className="modal-overlay">
          <div className="modal-content-responsive" style={{ maxWidth: '540px' }}>
            <h3 style={{ fontSize: '18px', fontWeight: 800, color: 'var(--text-main)', marginBottom: '8px' }}>
              Confirmar Aprovação da Cotação
            </h3>
            <p style={{ fontSize: '13.5px', color: 'var(--text-muted)', marginBottom: '16px' }}>
              A cotação será finalizada com as escolhas atuais e os pedidos formais serão registrados para os fornecedores vencedores.
            </p>

            <div
              style={{
                backgroundColor: 'var(--bg-main)',
                padding: '14px 18px',
                borderRadius: '8px',
                marginBottom: '16px',
                border: '1px solid var(--border)',
              }}
            >
              <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: '6px' }}>
                <span style={{ fontSize: '13.5px', color: 'var(--text-muted)' }}>Total Aprovado:</span>
                <span style={{ fontWeight: 800, fontSize: '15px' }}>
                  R$ {totalAprovado.toLocaleString('pt-BR', { minimumFractionDigits: 2 })}
                </span>
              </div>
              <div style={{ display: 'flex', justifyContent: 'space-between' }}>
                <span style={{ fontSize: '13.5px', color: 'var(--text-muted)' }}>Economia Realizada:</span>
                <span style={{ fontWeight: 800, fontSize: '15px', color: 'var(--green)' }}>
                  R$ {economia.toLocaleString('pt-BR', { minimumFractionDigits: 2 })} ({economiaPerc.toFixed(1)}%)
                </span>
              </div>
            </div>

            <div style={{ marginBottom: '20px' }}>
              <label style={{ fontSize: '13px', fontWeight: 600, display: 'block', marginBottom: '6px' }}>
                Justificativa / Observação do Comprador:
              </label>
              <textarea
                value={observacaoAprovacao}
                onChange={(e) => setObservacaoAprovacao(e.target.value)}
                rows={3}
                style={{ width: '100%', resize: 'vertical' }}
                placeholder="Ex: Proposta com menor custo efetivo tributário e prazo de entrega adequado para a demanda."
              />
            </div>

            <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '10px', flexWrap: 'wrap' }}>
              <button
                type="button"
                onClick={() => setShowModalAprovar(false)}
                className="btn btn-secondary"
                disabled={aprovando}
                style={{ minHeight: '42px', flex: '1 1 120px' }}
              >
                Cancelar
              </button>
              <button
                type="button"
                onClick={handleAprovar}
                className="btn btn-primary"
                disabled={aprovando}
                style={{ minHeight: '42px', flex: '2 1 180px', fontWeight: 700 }}
              >
                {aprovando ? 'Aprovando...' : 'Confirmar e Aprovar Compra'}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
};

