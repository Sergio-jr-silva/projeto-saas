import React, { useState, useEffect } from 'react';
import { api } from '../../services/api';
import { PedidoCompra } from '../../types';
import {
  FileCheck,
  Search,
  Printer,
  ChevronRight,
  ChevronLeft,
  ChevronsLeft,
  ChevronsRight,
  Clock,
  CheckCircle2,
  Truck,
  Package,
  AlertCircle,
  XCircle,
  Calendar,
  DollarSign,
  ArrowRight,
  RefreshCw
} from 'lucide-react';

const STATUS_CONFIG: Record<string, { label: string; bg: string; text: string; border: string }> = {
  Criado: { label: 'Criado', bg: '#eff6ff', text: '#2563eb', border: '#bfdbfe' },
  Enviado: { label: 'Enviado ao Fornecedor', bg: '#e0e7ff', text: '#4338ca', border: '#c7d2fe' },
  AguardandoConfirmacao: { label: 'Aguardando Confirmação', bg: '#fef3c7', text: '#b45309', border: '#fde68a' },
  Confirmado: { label: 'Confirmado', bg: '#ccfbf1', text: '#0f766e', border: '#99f6e4' },
  EmPreparacao: { label: 'Em Separação', bg: '#f3e8ff', text: '#7e22ce', border: '#e9d5ff' },
  EmTransporte: { label: 'Em Transporte', bg: '#ffedd5', text: '#c2410c', border: '#fed7aa' },
  Recebido: { label: 'Recebido / Entregue', bg: '#dcfce7', text: '#15803d', border: '#bbf7d0' },
  Cancelado: { label: 'Cancelado', bg: '#fee2e2', text: '#b91c1c', border: '#fecaca' }
};

const STATUS_STEPS = [
  'Criado',
  'Enviado',
  'AguardandoConfirmacao',
  'Confirmado',
  'EmPreparacao',
  'EmTransporte',
  'Recebido'
];

export const Pedidos: React.FC = () => {
  const [pedidos, setPedidos] = useState<PedidoCompra[]>([]);
  const [loading, setLoading] = useState(true);
  const [busca, setBusca] = useState('');
  const [filtroStatus, setFiltroStatus] = useState<string>('todos');
  const [pedidoSelecionado, setPedidoSelecionado] = useState<PedidoCompra | null>(null);
  const [modalDetalhes, setModalDetalhes] = useState(false);
  const [carregandoDetalhe, setCarregandoDetalhe] = useState(false);
  const [avancandoStatus, setAvancandoStatus] = useState(false);
  const [observacaoStatus, setObservacaoStatus] = useState('');
  const [novoStatusEscolhido, setNovoStatusEscolhido] = useState('');
  const [copiadoPedidoId, setCopiadoPedidoId] = useState<string | null>(null);

  const handleCopiarLinkPedido = (id: string, numero: string) => {
    const baseUrl = api.getBaseUrl();
    const link = `${baseUrl}/pedidos/${id}/pdf`;
    const texto = `Prezado fornecedor, segue o Pedido de Compra Oficial nº ${numero} (Modelo ProSyst):\n${link}\n\nPor favor, confirme o recebimento e o prazo de entrega.`;
    navigator.clipboard.writeText(texto);
    setCopiadoPedidoId(id);
    setTimeout(() => setCopiadoPedidoId(null), 3000);
  };

  const [paginaAtual, setPaginaAtual] = useState(1);
  const ITENS_POR_PAGINA = 10;

  const carregarPedidos = async () => {
    try {
      setLoading(true);
      const params = new URLSearchParams();
      if (filtroStatus !== 'todos') params.append('status', filtroStatus);
      if (busca) params.append('busca', busca);
      const qs = params.toString();

      const res = await api.get<any>(`/pedidos${qs ? `?${qs}` : ''}`);
      const lista = Array.isArray(res) ? res : (res?.itens || []);
      setPedidos(lista);
    } catch (err) {
      console.error('Erro ao carregar pedidos:', err);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    setPaginaAtual(1);
  }, [filtroStatus, busca]);

  useEffect(() => {
    carregarPedidos();
  }, [filtroStatus]);

  const abrirDetalhes = async (id: string) => {
    try {
      setCarregandoDetalhe(true);
      setModalDetalhes(true);
      const res = await api.get<PedidoCompra>(`/pedidos/${id}`);
      setPedidoSelecionado(res);
      setNovoStatusEscolhido(proximoStatus(res.status));
      setObservacaoStatus('');
    } catch (err) {
      console.error('Erro ao carregar detalhe do pedido:', err);
    } finally {
      setCarregandoDetalhe(false);
    }
  };

  const proximoStatus = (atual: string) => {
    const idx = STATUS_STEPS.indexOf(atual);
    if (idx >= 0 && idx < STATUS_STEPS.length - 1) {
      return STATUS_STEPS[idx + 1];
    }
    return atual;
  };

  const handleAtualizarStatus = async () => {
    if (!pedidoSelecionado || !novoStatusEscolhido) return;
    try {
      setAvancandoStatus(true);
      await api.put(`/pedidos/${pedidoSelecionado.id}/status`, {
        novoStatus: novoStatusEscolhido,
        descricao: observacaoStatus || undefined
      });
      // Recarrega detalhe e lista
      await abrirDetalhes(pedidoSelecionado.id);
      carregarPedidos();
    } catch (err) {
      alert('Erro ao atualizar status do pedido.');
    } finally {
      setAvancandoStatus(false);
    }
  };

  const handleImprimir = (id: string) => {
    const baseUrl = api.getBaseUrl();
    const url = `${baseUrl}/pedidos/${id}/pdf`;
    window.open(url, '_blank');
  };

  // Métricas
  const totalValor = pedidos.reduce((acc, p) => acc + (p.status !== 'Cancelado' ? p.valorTotal : 0), 0);
  const emAndamento = pedidos.filter(p => !['Recebido', 'Cancelado'].includes(p.status)).length;
  const recebidos = pedidos.filter(p => p.status === 'Recebido').length;

  return (
    <div style={{ padding: '24px 32px' }}>
      {/* HEADER */}
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', marginBottom: '24px' }}>
        <div>
          <h1 style={{ fontSize: '24px', fontWeight: 800, color: 'var(--text-main)', margin: '0 0 6px 0', display: 'flex', alignItems: 'center', gap: '10px' }}>
            <FileCheck color="var(--blue-primary)" size={28} />
            Pedidos de Compra
          </h1>
          <p style={{ margin: 0, color: 'var(--text-muted)', fontSize: '14px' }}>
            Acompanhe pedidos formalizados, gerencie o pipeline de entrega e gere ordens de fornecimento.
          </p>
        </div>
        <button
          onClick={carregarPedidos}
          style={{
            display: 'flex',
            alignItems: 'center',
            gap: '8px',
            backgroundColor: '#FFFFFF',
            border: '1px solid var(--border)',
            borderRadius: 'var(--radius-sm)',
            padding: '9px 16px',
            fontSize: '13px',
            fontWeight: 600,
            cursor: 'pointer',
            color: 'var(--text-main)'
          }}
        >
          <RefreshCw size={16} />
          Atualizar Lista
        </button>
      </div>

      {/* METRIC CARDS */}
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(4, 1fr)', gap: '16px', marginBottom: '24px' }}>
        <div style={{ background: '#FFFFFF', border: '1px solid var(--border)', borderRadius: 'var(--radius-md)', padding: '16px 20px' }}>
          <div style={{ fontSize: '12px', fontWeight: 600, color: 'var(--text-muted)', textTransform: 'uppercase' }}>Total de Pedidos</div>
          <div style={{ fontSize: '26px', fontWeight: 800, color: 'var(--text-main)', marginTop: '4px' }}>{pedidos.length}</div>
        </div>
        <div style={{ background: '#FFFFFF', border: '1px solid var(--border)', borderRadius: 'var(--radius-md)', padding: '16px 20px' }}>
          <div style={{ fontSize: '12px', fontWeight: 600, color: '#2563eb', textTransform: 'uppercase' }}>Em Andamento</div>
          <div style={{ fontSize: '26px', fontWeight: 800, color: '#2563eb', marginTop: '4px' }}>{emAndamento}</div>
        </div>
        <div style={{ background: '#FFFFFF', border: '1px solid var(--border)', borderRadius: 'var(--radius-md)', padding: '16px 20px' }}>
          <div style={{ fontSize: '12px', fontWeight: 600, color: '#16a34a', textTransform: 'uppercase' }}>Recebidos / Concluídos</div>
          <div style={{ fontSize: '26px', fontWeight: 800, color: '#16a34a', marginTop: '4px' }}>{recebidos}</div>
        </div>
        <div style={{ background: '#FFFFFF', border: '1px solid var(--border)', borderRadius: 'var(--radius-md)', padding: '16px 20px' }}>
          <div style={{ fontSize: '12px', fontWeight: 600, color: '#1e40af', textTransform: 'uppercase' }}>Volume Total Comprado</div>
          <div style={{ fontSize: '24px', fontWeight: 800, color: '#1e40af', marginTop: '4px' }}>
            R$ {totalValor.toLocaleString('pt-BR', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}
          </div>
        </div>
      </div>

      {/* FILTROS E BUSCA */}
      <div style={{ background: '#FFFFFF', border: '1px solid var(--border)', borderRadius: 'var(--radius-md)', padding: '16px 20px', marginBottom: '20px' }}>
        <div style={{ display: 'flex', gap: '16px', alignItems: 'center', marginBottom: '14px', flexWrap: 'wrap' }}>
          <div style={{ flex: 1, minWidth: '260px', position: 'relative' }}>
            <Search size={18} style={{ position: 'absolute', left: '12px', top: '10px', color: 'var(--text-muted)' }} />
            <input
              type="text"
              placeholder="Buscar por número do pedido, fornecedor ou CNPJ..."
              value={busca}
              onChange={(e) => setBusca(e.target.value)}
              onKeyDown={(e) => e.key === 'Enter' && carregarPedidos()}
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
            onClick={carregarPedidos}
            style={{
              backgroundColor: 'var(--blue-primary)',
              color: '#FFFFFF',
              border: 'none',
              borderRadius: 'var(--radius-sm)',
              padding: '9px 18px',
              fontSize: '13.5px',
              fontWeight: 600,
              cursor: 'pointer'
            }}
          >
            Buscar
          </button>
        </div>

        {/* BOTOES DE STATUS */}
        <div style={{ display: 'flex', gap: '6px', flexWrap: 'wrap' }}>
          {[
            { id: 'todos', label: 'Todos os Status' },
            { id: 'Criado', label: 'Rascunho / Criado' },
            { id: 'Enviado', label: 'Enviado' },
            { id: 'AguardandoConfirmacao', label: 'Aguardando Aprovação / Confirmação' },
            { id: 'Confirmado', label: 'Aprovado / Confirmado' },
            { id: 'EmPreparacao', label: 'Em Separação' },
            { id: 'EmTransporte', label: 'Em Transporte' },
            { id: 'Recebido', label: 'Concluído / Recebido' },
            { id: 'Cancelado', label: 'Cancelado' }
          ].map((f) => (
            <button
              key={f.id}
              onClick={() => setFiltroStatus(f.id)}
              className={`btn ${filtroStatus === f.id ? 'btn-primary' : 'btn-secondary'}`}
              style={{ fontSize: '12.5px', padding: '6px 12px' }}
            >
              {f.label}
            </button>
          ))}
        </div>
      </div>

      {/* LISTAGEM DE PEDIDOS */}
      <div style={{ background: '#FFFFFF', border: '1px solid var(--border)', borderRadius: 'var(--radius-md)', overflow: 'hidden' }}>
        {loading ? (
          <div style={{ padding: '60px', textAlign: 'center', color: 'var(--text-muted)' }}>Carregando pedidos de compra...</div>
        ) : pedidos.length === 0 ? (
          <div style={{ padding: '60px', textAlign: 'center' }}>
            <FileCheck size={48} style={{ color: '#cbd5e1', marginBottom: '12px' }} />
            <div style={{ fontWeight: 700, fontSize: '16px', color: 'var(--text-main)' }}>Nenhum pedido de compra encontrado</div>
            <div style={{ fontSize: '13px', color: 'var(--text-muted)', marginTop: '4px' }}>
              Pedidos de compra são gerados automaticamente ao aprovar uma cotação na aba "Comparação de Ofertas".
            </div>
          </div>
        ) : (
          <table style={{ width: '100%', borderCollapse: 'collapse', textAlign: 'left' }}>
            <thead>
              <tr style={{ background: '#f8fafc', borderBottom: '1px solid var(--border)' }}>
                <th style={{ padding: '12px 20px', fontSize: '12px', fontWeight: 700, color: 'var(--text-muted)', textTransform: 'uppercase' }}>Pedido</th>
                <th style={{ padding: '12px 20px', fontSize: '12px', fontWeight: 700, color: 'var(--text-muted)', textTransform: 'uppercase' }}>Fornecedor</th>
                <th style={{ padding: '12px 20px', fontSize: '12px', fontWeight: 700, color: 'var(--text-muted)', textTransform: 'uppercase' }}>Condição / Prazo</th>
                <th style={{ padding: '12px 20px', fontSize: '12px', fontWeight: 700, color: 'var(--text-muted)', textTransform: 'uppercase', textAlign: 'right' }}>Valor Total</th>
                <th style={{ padding: '12px 20px', fontSize: '12px', fontWeight: 700, color: 'var(--text-muted)', textTransform: 'uppercase' }}>Status</th>
                <th style={{ padding: '12px 20px', fontSize: '12px', fontWeight: 700, color: 'var(--text-muted)', textTransform: 'uppercase', textAlign: 'right' }}>Ações</th>
              </tr>
            </thead>
            <tbody>
              {pedidos.length === 0 ? (
                <tr>
                  <td colSpan={6} style={{ padding: '60px', textAlign: 'center', color: 'var(--text-muted)' }}>
                    Nenhum pedido de compra encontrado para os filtros selecionados.
                  </td>
                </tr>
              ) : (
                (() => {
                  const totalPaginas = Math.max(1, Math.ceil(pedidos.length / ITENS_POR_PAGINA));
                  const indiceInicial = (paginaAtual - 1) * ITENS_POR_PAGINA;
                  const pedidosPaginados = pedidos.slice(indiceInicial, indiceInicial + ITENS_POR_PAGINA);

                  return pedidosPaginados.map((p) => {
                    const st = STATUS_CONFIG[p.status] || { label: p.status, bg: '#f1f5f9', text: '#475569', border: '#cbd5e1' };
                    return (
                      <tr key={p.id} style={{ borderBottom: '1px solid var(--border-light)' }}>
                        <td style={{ padding: '14px 20px' }}>
                          <div style={{ fontWeight: 800, color: 'var(--blue-primary)', fontSize: '14px' }}>{p.numero}</div>
                          <div style={{ fontSize: '12px', color: 'var(--text-muted)' }}>
                            {new Date(p.dataCriacao).toLocaleDateString('pt-BR')}
                            {p.cotacaoNumero && <span> • Cot. {p.cotacaoNumero}</span>}
                          </div>
                        </td>
                        <td style={{ padding: '14px 20px' }}>
                          <div style={{ fontWeight: 600, color: 'var(--text-main)', fontSize: '13.5px' }}>{p.fornecedorNome}</div>
                          <div style={{ fontSize: '11px', color: 'var(--text-muted)' }}>CNPJ: {p.fornecedorCnpj}</div>
                        </td>
                        <td style={{ padding: '14px 20px' }}>
                          <div style={{ fontSize: '13px', color: 'var(--text-main)' }}>{p.condicaoPagamento}</div>
                          <div style={{ fontSize: '11px', color: 'var(--text-muted)' }}>Prazo: {p.prazoEntregaDias} dias</div>
                        </td>
                        <td style={{ padding: '14px 20px', textAlign: 'right' }}>
                          <div style={{ fontWeight: 800, color: 'var(--text-main)', fontSize: '14px' }}>
                            R$ {p.valorTotal.toLocaleString('pt-BR', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}
                          </div>
                          <div style={{ fontSize: '11px', color: 'var(--text-muted)' }}>{p.qtdItens} item(ns)</div>
                        </td>
                        <td style={{ padding: '14px 20px' }}>
                          <span
                            style={{
                              display: 'inline-block',
                              padding: '4px 10px',
                              borderRadius: '12px',
                              fontSize: '11.5px',
                              fontWeight: 700,
                              backgroundColor: st.bg,
                              color: st.text,
                              border: `1px solid ${st.border}`
                            }}
                          >
                            {st.label}
                          </span>
                        </td>
                        <td style={{ padding: '14px 20px', textAlign: 'right' }}>
                          <div style={{ display: 'flex', gap: '8px', justifyContent: 'flex-end', flexWrap: 'wrap' }}>
                            <button
                              onClick={() => handleCopiarLinkPedido(p.id, p.numero)}
                              title="Copiar link do pedido para enviar ao fornecedor via WhatsApp ou E-mail"
                              style={{
                                background: copiadoPedidoId === p.id ? '#DCFCE7' : '#FFFFFF',
                                border: copiadoPedidoId === p.id ? '1px solid #86EFAC' : '1px solid var(--border)',
                                borderRadius: '6px',
                                padding: '6px 10px',
                                cursor: 'pointer',
                                color: copiadoPedidoId === p.id ? '#166534' : 'var(--blue-primary)',
                                display: 'flex',
                                alignItems: 'center',
                                gap: '4px',
                                fontSize: '12px',
                                fontWeight: 600
                              }}
                            >
                              {copiadoPedidoId === p.id ? '✓ Link Copiado' : '🔗 Link Fornecedor'}
                            </button>
                            <button
                              onClick={() => handleImprimir(p.id)}
                              title="Imprimir Pedido (PDF)"
                              style={{
                                background: '#FFFFFF',
                                border: '1px solid var(--border)',
                                borderRadius: '6px',
                                padding: '6px 10px',
                                cursor: 'pointer',
                                color: '#475569',
                                display: 'flex',
                                alignItems: 'center',
                                gap: '4px',
                                fontSize: '12px',
                                fontWeight: 600
                              }}
                            >
                              <Printer size={14} />
                              PDF
                            </button>
                            <button
                              onClick={() => abrirDetalhes(p.id)}
                              style={{
                                backgroundColor: 'var(--blue-primary)',
                                color: '#FFFFFF',
                                border: 'none',
                                borderRadius: '6px',
                                padding: '6px 12px',
                                cursor: 'pointer',
                                fontSize: '12px',
                                fontWeight: 600,
                                display: 'flex',
                                alignItems: 'center',
                                gap: '4px'
                              }}
                            >
                              Acompanhar
                              <ChevronRight size={14} />
                            </button>
                          </div>
                        </td>
                      </tr>
                    );
                  });
                })()
              )}
            </tbody>
          </table>
        )}

        {/* PAGINACAO PEDIDOS */}
        {pedidos.length > 0 && (
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', padding: '14px 20px', borderTop: '1px solid var(--border-light)', flexWrap: 'wrap', gap: '12px' }}>
            {(() => {
              const totalPaginas = Math.max(1, Math.ceil(pedidos.length / ITENS_POR_PAGINA));
              const indiceInicial = (paginaAtual - 1) * ITENS_POR_PAGINA;
              return (
                <>
                  <div style={{ fontSize: '13px', color: 'var(--text-muted)' }}>
                    Mostrando <strong>{indiceInicial + 1}</strong> a <strong>{Math.min(indiceInicial + ITENS_POR_PAGINA, pedidos.length)}</strong> de <strong>{pedidos.length}</strong> pedidos
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
                </>
              );
            })()}
          </div>
        )}
      </div>

      {/* MODAL DE DETALHES E TIMELINE */}
      {modalDetalhes && (
        <div
          style={{
            position: 'fixed',
            inset: 0,
            backgroundColor: 'rgba(15, 23, 42, 0.6)',
            display: 'flex',
            justifyContent: 'center',
            alignItems: 'center',
            zIndex: 9999,
            padding: '20px'
          }}
        >
          <div
            style={{
              backgroundColor: '#FFFFFF',
              borderRadius: '12px',
              width: '100%',
              maxWidth: '960px',
              maxHeight: '90vh',
              overflowY: 'auto',
              boxShadow: '0 20px 25px -5px rgba(0, 0, 0, 0.1)',
              padding: '24px 30px'
            }}
          >
            {carregandoDetalhe || !pedidoSelecionado ? (
              <div style={{ padding: '60px', textAlign: 'center' }}>Carregando dados completos do pedido...</div>
            ) : (
              <div>
                {/* TOPO DO MODAL */}
                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', borderBottom: '1px solid var(--border-light)', paddingBottom: '16px', marginBottom: '20px' }}>
                  <div>
                    <div style={{ display: 'flex', alignItems: 'center', gap: '10px' }}>
                      <h2 style={{ fontSize: '20px', fontWeight: 800, color: 'var(--blue-primary)', margin: 0 }}>
                        {pedidoSelecionado.numero}
                      </h2>
                      <span
                        style={{
                          padding: '3px 10px',
                          borderRadius: '12px',
                          fontSize: '11px',
                          fontWeight: 700,
                          backgroundColor: STATUS_CONFIG[pedidoSelecionado.status]?.bg || '#f1f5f9',
                          color: STATUS_CONFIG[pedidoSelecionado.status]?.text || '#475569'
                        }}
                      >
                        {STATUS_CONFIG[pedidoSelecionado.status]?.label || pedidoSelecionado.status}
                      </span>
                    </div>
                    <div style={{ fontSize: '13px', color: 'var(--text-muted)', marginTop: '4px' }}>
                      Fornecedor: <strong>{pedidoSelecionado.fornecedor?.razaoSocial}</strong> • CNPJ: {pedidoSelecionado.fornecedor?.cnpj}
                    </div>
                  </div>
                  <div style={{ display: 'flex', gap: '10px' }}>
                    <button
                      onClick={() => handleCopiarLinkPedido(pedidoSelecionado.id, pedidoSelecionado.numero)}
                      style={{
                        display: 'flex',
                        alignItems: 'center',
                        gap: '6px',
                        background: copiadoPedidoId === pedidoSelecionado.id ? '#dcfce7' : '#ffffff',
                        color: copiadoPedidoId === pedidoSelecionado.id ? '#166534' : 'var(--blue-primary)',
                        border: '1px solid var(--border)',
                        borderRadius: '6px',
                        padding: '8px 14px',
                        fontWeight: 600,
                        fontSize: '13px',
                        cursor: 'pointer'
                      }}
                    >
                      {copiadoPedidoId === pedidoSelecionado.id ? '✓ Link Copiado' : '🔗 Copiar Link para o Fornecedor'}
                    </button>
                    <button
                      onClick={() => handleImprimir(pedidoSelecionado.id)}
                      style={{
                        display: 'flex',
                        alignItems: 'center',
                        gap: '6px',
                        background: '#eff6ff',
                        color: 'var(--blue-primary)',
                        border: '1px solid #bfdbfe',
                        borderRadius: '6px',
                        padding: '8px 14px',
                        fontWeight: 600,
                        fontSize: '13px',
                        cursor: 'pointer'
                      }}
                    >
                      <Printer size={16} />
                      Visualizar Impressão (PDF)
                    </button>
                    <button
                      onClick={() => setModalDetalhes(false)}
                      style={{
                        background: '#f1f5f9',
                        border: 'none',
                        borderRadius: '6px',
                        padding: '8px 14px',
                        fontWeight: 600,
                        fontSize: '13px',
                        cursor: 'pointer',
                        color: '#64748b'
                      }}
                    >
                      Fechar
                    </button>
                  </div>
                </div>

                {/* PIPELINE DE STATUS (TIMELINE HORIZONTAL) */}
                <div style={{ marginBottom: '24px', background: '#f8fafc', border: '1px solid var(--border)', borderRadius: '8px', padding: '16px 20px' }}>
                  <div style={{ fontSize: '12px', fontWeight: 700, color: 'var(--text-muted)', textTransform: 'uppercase', marginBottom: '14px' }}>
                    Esteira de Atendimento e Entrega
                  </div>
                  <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', position: 'relative' }}>
                    {STATUS_STEPS.map((step, idx) => {
                      const stepIndexAtual = STATUS_STEPS.indexOf(pedidoSelecionado.status);
                      const isCompleted = idx <= stepIndexAtual;
                      const isCurrent = idx === stepIndexAtual;
                      return (
                        <div key={step} style={{ display: 'flex', flexDirection: 'column', alignItems: 'center', flex: 1, zIndex: 2 }}>
                          <div
                            style={{
                              width: '32px',
                              height: '32px',
                              borderRadius: '50%',
                              backgroundColor: isCompleted ? (isCurrent ? '#2563eb' : '#16a34a') : '#e2e8f0',
                              color: isCompleted ? '#ffffff' : '#94a3b8',
                              display: 'flex',
                              alignItems: 'center',
                              justifyContent: 'center',
                              fontSize: '13px',
                              fontWeight: 700,
                              boxShadow: isCurrent ? '0 0 0 4px #dbeafe' : 'none'
                            }}
                          >
                            {isCompleted && !isCurrent ? <CheckCircle2 size={16} /> : idx + 1}
                          </div>
                          <span
                            style={{
                              fontSize: '11px',
                              fontWeight: isCurrent ? 700 : 500,
                              color: isCurrent ? '#2563eb' : isCompleted ? '#16a34a' : '#94a3b8',
                              marginTop: '6px',
                              textAlign: 'center'
                            }}
                          >
                            {STATUS_CONFIG[step]?.label.split(' ')[0]}
                          </span>
                        </div>
                      );
                    })}
                  </div>
                </div>

                {/* PAINEL DE ATUALIZAÇÃO DE STATUS */}
                <div style={{ background: '#eff6ff', border: '1px solid #bfdbfe', borderRadius: '8px', padding: '14px 18px', marginBottom: '20px', display: 'flex', gap: '14px', alignItems: 'center' }}>
                  <div style={{ flex: 1 }}>
                    <div style={{ fontSize: '12px', fontWeight: 700, color: '#1e40af', marginBottom: '4px' }}>
                      Atualizar Status do Pedido
                    </div>
                    <div style={{ display: 'flex', gap: '10px' }}>
                      <select
                        value={novoStatusEscolhido}
                        onChange={(e) => setNovoStatusEscolhido(e.target.value)}
                        style={{
                          padding: '7px 12px',
                          borderRadius: '6px',
                          border: '1px solid #93c5fd',
                          fontSize: '13px',
                          backgroundColor: '#FFFFFF',
                          outline: 'none'
                        }}
                      >
                        {STATUS_STEPS.map((s) => (
                          <option key={s} value={s}>{STATUS_CONFIG[s]?.label}</option>
                        ))}
                        <option value="Cancelado">Cancelado</option>
                      </select>
                      <input
                        type="text"
                        placeholder="Observação da alteração (opcional)..."
                        value={observacaoStatus}
                        onChange={(e) => setObservacaoStatus(e.target.value)}
                        style={{
                          flex: 1,
                          padding: '7px 12px',
                          borderRadius: '6px',
                          border: '1px solid #93c5fd',
                          fontSize: '13px',
                          outline: 'none'
                        }}
                      />
                    </div>
                  </div>
                  <button
                    onClick={handleAtualizarStatus}
                    disabled={avancandoStatus || novoStatusEscolhido === pedidoSelecionado.status}
                    style={{
                      marginTop: '18px',
                      backgroundColor: '#2563eb',
                      color: '#ffffff',
                      border: 'none',
                      borderRadius: '6px',
                      padding: '8px 16px',
                      fontSize: '13px',
                      fontWeight: 600,
                      cursor: 'pointer',
                      opacity: novoStatusEscolhido === pedidoSelecionado.status ? 0.5 : 1
                    }}
                  >
                    {avancandoStatus ? 'Salvando...' : 'Confirmar Novo Status'}
                  </button>
                </div>

                {/* ITENS DO PEDIDO */}
                <div style={{ marginBottom: '20px' }}>
                  <h4 style={{ margin: '0 0 10px 0', fontSize: '14px', fontWeight: 700, color: 'var(--text-main)' }}>
                    Itens do Pedido ({pedidoSelecionado.itens?.length || 0})
                  </h4>
                  <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: '13px' }}>
                    <thead>
                      <tr style={{ background: '#f8fafc', borderBottom: '1px solid var(--border)' }}>
                        <th style={{ padding: '8px 12px', textAlign: 'left' }}>Item</th>
                        <th style={{ padding: '8px 12px', textAlign: 'center' }}>UN</th>
                        <th style={{ padding: '8px 12px', textAlign: 'right' }}>Qtd</th>
                        <th style={{ padding: '8px 12px', textAlign: 'right' }}>Preço Unit</th>
                        <th style={{ padding: '8px 12px', textAlign: 'right' }}>Desc %</th>
                        <th style={{ padding: '8px 12px', textAlign: 'right' }}>Preço Líq</th>
                        <th style={{ padding: '8px 12px', textAlign: 'right' }}>ST</th>
                        <th style={{ padding: '8px 12px', textAlign: 'right' }}>Subtotal</th>
                      </tr>
                    </thead>
                    <tbody>
                      {pedidoSelecionado.itens?.map((i) => (
                        <tr key={i.id} style={{ borderBottom: '1px solid var(--border-light)' }}>
                          <td style={{ padding: '8px 12px' }}>
                            <strong>{i.codigoProduto}</strong> - {i.descricaoProduto}
                          </td>
                          <td style={{ padding: '8px 12px', textAlign: 'center' }}>{i.unidade}</td>
                          <td style={{ padding: '8px 12px', textAlign: 'right' }}>{i.quantidade}</td>
                          <td style={{ padding: '8px 12px', textAlign: 'right' }}>R$ {i.precoUnitario.toFixed(2)}</td>
                          <td style={{ padding: '8px 12px', textAlign: 'right' }}>{i.descontoPerc.toFixed(1)}%</td>
                          <td style={{ padding: '8px 12px', textAlign: 'right' }}>R$ {i.precoLiquidoUnit.toFixed(2)}</td>
                          <td style={{ padding: '8px 12px', textAlign: 'right' }}>R$ {i.icmsStValor.toFixed(2)}</td>
                          <td style={{ padding: '8px 12px', textAlign: 'right', fontWeight: 700 }}>R$ {i.subtotal.toFixed(2)}</td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>

                {/* RESUMO DE VALORES E HISTÓRICO DE LOGS */}
                <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '20px' }}>
                  {/* HISTÓRICO DE EVENTOS */}
                  <div style={{ border: '1px solid var(--border)', borderRadius: '8px', padding: '14px' }}>
                    <div style={{ fontSize: '12px', fontWeight: 700, color: 'var(--text-muted)', textTransform: 'uppercase', marginBottom: '8px' }}>
                      Linha do Tempo de Eventos
                    </div>
                    <div style={{ maxHeight: '180px', overflowY: 'auto' }}>
                      {pedidoSelecionado.timeline?.map((ev, i) => (
                        <div key={i} style={{ borderLeft: '2px solid #3b82f6', paddingLeft: '10px', marginBottom: '10px' }}>
                          <div style={{ fontSize: '11px', color: 'var(--text-muted)' }}>
                            {new Date(ev.data).toLocaleString('pt-BR')} • {ev.usuario}
                          </div>
                          <div style={{ fontSize: '12.5px', fontWeight: 600, color: 'var(--text-main)' }}>
                            {ev.status}: {ev.descricao}
                          </div>
                        </div>
                      ))}
                    </div>
                  </div>

                  {/* TOTAIS DO PEDIDO */}
                  <div style={{ background: '#f8fafc', border: '1px solid var(--border)', borderRadius: '8px', padding: '16px' }}>
                    <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: '6px', fontSize: '13px' }}>
                      <span>Subtotal Produtos:</span>
                      <strong>R$ {pedidoSelecionado.subtotal.toFixed(2)}</strong>
                    </div>
                    <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: '6px', fontSize: '13px', color: '#16a34a' }}>
                      <span>Descontos:</span>
                      <span>- R$ {pedidoSelecionado.desconto.toFixed(2)}</span>
                    </div>
                    <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: '6px', fontSize: '13px' }}>
                      <span>Frete:</span>
                      <span>+ R$ {pedidoSelecionado.frete.toFixed(2)}</span>
                    </div>
                    <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: '6px', fontSize: '13px' }}>
                      <span>Impostos (ST):</span>
                      <span>+ R$ {pedidoSelecionado.impostosTotal.toFixed(2)}</span>
                    </div>
                    <div style={{ borderTop: '2px solid #cbd5e1', paddingTop: '8px', marginTop: '8px', display: 'flex', justifyContent: 'space-between', fontSize: '16px', fontWeight: 800, color: '#1e3a8a' }}>
                      <span>TOTAL DO PEDIDO:</span>
                      <span>R$ {pedidoSelecionado.valorTotal.toFixed(2)}</span>
                    </div>
                  </div>
                </div>
              </div>
            )}
          </div>
        </div>
      )}
    </div>
  );
};
