import React, { useState, useEffect } from 'react';
import { api } from '../../services/api';
import {
  Building2,
  Plus,
  RefreshCw,
  Layers,
  ArrowRightLeft,
  BarChart3,
  CheckCircle,
  Truck,
  AlertCircle,
  Store,
  MapPin,
  Phone,
  UserCheck,
  Server,
  Copy,
  Check,
  Key
} from 'lucide-react';

interface Filial {
  id: string;
  codigo: string;
  nome: string;
  cnpj: string;
  endereco: string;
  cidade: string;
  uf: string;
  telefone: string;
  email: string;
  responsavel: string;
  ativa: boolean;
  token?: string;
  tokenRevogado?: boolean;
  agenteId?: string;
  agenteNome?: string;
}

interface EstoqueFilial {
  produtoId: string;
  codigoErp: string;
  descricao: string;
  unidade: string;
  estoqueAtual: number;
  estoqueMinimo: number;
  estoquePendente: number;
  custoAtual: number;
  precoVenda: number;
  ultimaAtualizacao: string;
}

interface SolicitacaoTransferencia {
  id: string;
  numero: string;
  filialOrigemId: string;
  filialOrigemNome: string;
  filialDestinoId: string;
  filialDestinoNome: string;
  status: string;
  observacoes: string;
  solicitadoPorNome: string;
  aprovadoPorNome?: string;
  dataCriacao: string;
  itens: {
    produtoId: string;
    codigoProduto: string;
    descricaoProduto: string;
    quantidadeSolicitada: number;
    quantidadeAtendida: number;
  }[];
}

interface RelatorioConsolidado {
  totalFiliais: number;
  totalProdutos: number;
  valorEstoqueTotal: number;
  filiais: {
    filialId: string;
    nome: string;
    codigo: string;
    cidade: string;
    uf: string;
    totalItensEstoque: number;
    estoqueFisicoTotal: number;
    valorEstoqueEstimado: number;
    statusSincronizacao: string;
  }[];
}

interface ProdutoItem {
  id: string;
  codigoErp: string;
  descricao: string;
  unidade: string;
  estoqueAtual: number;
}

export const Filiais: React.FC = () => {
  const [tabAtiva, setTabAtiva] = useState<'filiais' | 'estoque' | 'transferencias' | 'consolidado'>('filiais');
  const [filiais, setFiliais] = useState<Filial[]>([]);
  const [loading, setLoading] = useState(true);
  const [copiadoToken, setCopiadoToken] = useState<string | null>(null);

  const copiarToken = (token: string) => {
    navigator.clipboard.writeText(token);
    setCopiadoToken(token);
    setTimeout(() => setCopiadoToken(null), 2500);
  };

  // Modal Filial
  const [modalNovaFilial, setModalNovaFilial] = useState(false);
  const [formFilial, setFormFilial] = useState({
    codigo: '',
    nome: '',
    cnpj: '',
    endereco: '',
    cidade: '',
    uf: '',
    telefone: '',
    email: '',
    responsavel: ''
  });

  // Tab Estoque
  const [filialSelecionada, setFilialSelecionada] = useState<string>('');
  const [estoqueFilial, setEstoqueFilial] = useState<EstoqueFilial[]>([]);
  const [loadingEstoque, setLoadingEstoque] = useState(false);

  // Tab Transferências
  const [transferencias, setTransferencias] = useState<SolicitacaoTransferencia[]>([]);
  const [modalNovaTransf, setModalNovaTransf] = useState(false);
  const [produtosDisponiveis, setProdutosDisponiveis] = useState<ProdutoItem[]>([]);
  const [formTransf, setFormTransf] = useState({
    origemId: '',
    destinoId: '',
    observacoes: '',
    produtoId: '',
    quantidade: 10
  });

  // Tab Consolidado
  const [relatorioConsolidado, setRelatorioConsolidado] = useState<RelatorioConsolidado | null>(null);

  const carregarFiliais = async () => {
    try {
      setLoading(true);
      const res = await api.get<Filial[]>('/filiais');
      setFiliais(res);
      if (res.length > 0 && !filialSelecionada) {
        setFilialSelecionada(res[0].id);
      }
    } catch (err: any) {
      console.error('Erro ao carregar filiais:', err);
    } finally {
      setLoading(false);
    }
  };

  const carregarEstoque = async (filialId: string) => {
    if (!filialId) return;
    try {
      setLoadingEstoque(true);
      const res = await api.get<EstoqueFilial[]>(`/filiais/${filialId}/estoque`);
      setEstoqueFilial(res);
    } catch (err: any) {
      console.error('Erro ao carregar estoque da filial:', err);
    } finally {
      setLoadingEstoque(false);
    }
  };

  const carregarTransferencias = async () => {
    try {
      const [resTransf, resProds] = await Promise.all([
        api.get<SolicitacaoTransferencia[]>('/transferencias'),
        api.get<ProdutoItem[]>('/produtos')
      ]);
      setTransferencias(resTransf);
      setProdutosDisponiveis(resProds);
      if (resProds.length > 0 && !formTransf.produtoId) {
        setFormTransf(prev => ({ ...prev, produtoId: resProds[0].id }));
      }
    } catch (err: any) {
      console.error('Erro ao carregar transferências:', err);
    }
  };

  const carregarConsolidado = async () => {
    try {
      const res = await api.get<RelatorioConsolidado>('/filiais/relatorio-consolidado');
      setRelatorioConsolidado(res);
    } catch (err: any) {
      console.error('Erro ao carregar consolidado:', err);
    }
  };

  useEffect(() => {
    carregarFiliais();
  }, []);

  useEffect(() => {
    if (tabAtiva === 'estoque' && filialSelecionada) {
      carregarEstoque(filialSelecionada);
    } else if (tabAtiva === 'transferencias') {
      carregarTransferencias();
    } else if (tabAtiva === 'consolidado') {
      carregarConsolidado();
    }
  }, [tabAtiva, filialSelecionada]);

  const handleSalvarFilial = async (e: React.FormEvent) => {
    e.preventDefault();
    try {
      await api.post('/filiais', formFilial);
      alert('Filial cadastrada com sucesso!');
      setModalNovaFilial(false);
      setFormFilial({
        codigo: '',
        nome: '',
        cnpj: '',
        endereco: '',
        cidade: '',
        uf: '',
        telefone: '',
        email: '',
        responsavel: ''
      });
      await carregarFiliais();
    } catch (err: any) {
      alert('Erro ao salvar filial: ' + (err.response?.data?.erro || err.message));
    }
  };

  const handleCriarTransferencia = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!formTransf.origemId || !formTransf.destinoId || !formTransf.produtoId) {
      alert('Selecione filial de origem, destino e o produto a transferir.');
      return;
    }
    if (formTransf.origemId === formTransf.destinoId) {
      alert('A filial de origem deve ser diferente da filial de destino.');
      return;
    }
    try {
      await api.post('/transferencias', {
        filialOrigemId: formTransf.origemId,
        filialDestinoId: formTransf.destinoId,
        observacoes: formTransf.observacoes,
        itens: [
          { produtoId: formTransf.produtoId, quantidade: Number(formTransf.quantidade) }
        ]
      });
      alert('Solicitação de transferência criada com sucesso!');
      setModalNovaTransf(false);
      await carregarTransferencias();
    } catch (err: any) {
      alert('Erro ao criar transferência: ' + (err.response?.data?.erro || err.message));
    }
  };

  const handleMudarStatusTransf = async (id: string, novoStatus: string) => {
    try {
      await api.put(`/transferencias/${id}/status`, {
        novoStatus,
        observacoes: `Status alterado para ${novoStatus}`
      });
      alert(`Transferência marcada como ${novoStatus}!`);
      await carregarTransferencias();
      if (tabAtiva === 'estoque' && filialSelecionada) {
        await carregarEstoque(filialSelecionada);
      }
    } catch (err: any) {
      alert('Erro ao atualizar status: ' + (err.response?.data?.erro || err.message));
    }
  };

  return (
    <div style={{ padding: '24px 32px', maxWidth: '1280px', margin: '0 auto' }}>
      {/* CABEÇALHO */}
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', marginBottom: '24px', flexWrap: 'wrap', gap: '14px' }}>
        <div>
          <div style={{ display: 'flex', alignItems: 'center', gap: '10px' }}>
            <Building2 size={26} style={{ color: 'var(--blue-primary)' }} />
            <h1 style={{ fontSize: '24px', fontWeight: 800, color: 'var(--text-main)', margin: 0 }}>
              Gestão de Múltiplas Filiais
            </h1>
          </div>
          <p style={{ color: 'var(--text-muted)', fontSize: '14px', marginTop: '4px' }}>
            Controle unidades, estoques individualizados por loja, reposições internas e relatórios consolidados da rede.
          </p>
        </div>

        <button
          onClick={() => setModalNovaFilial(true)}
          style={{
            backgroundColor: 'var(--blue-primary)',
            color: '#FFF',
            border: 'none',
            borderRadius: '8px',
            padding: '10px 16px',
            fontWeight: 700,
            fontSize: '13px',
            cursor: 'pointer',
            display: 'flex',
            alignItems: 'center',
            gap: '6px'
          }}
        >
          <Plus size={16} /> Nova Filial
        </button>
      </div>

      {/* TABS NAVEGAÇÃO */}
      <div style={{ display: 'flex', gap: '8px', borderBottom: '1px solid var(--border)', marginBottom: '24px' }}>
        <button
          onClick={() => setTabAtiva('filiais')}
          style={{
            padding: '10px 16px',
            border: 'none',
            borderBottom: tabAtiva === 'filiais' ? '3px solid var(--blue-primary)' : '3px solid transparent',
            backgroundColor: 'transparent',
            fontWeight: tabAtiva === 'filiais' ? 700 : 500,
            color: tabAtiva === 'filiais' ? 'var(--blue-primary)' : 'var(--text-muted)',
            cursor: 'pointer',
            display: 'flex',
            alignItems: 'center',
            gap: '8px'
          }}
        >
          <Store size={16} /> Unidades e Filiais ({filiais.length})
        </button>
        <button
          onClick={() => setTabAtiva('estoque')}
          style={{
            padding: '10px 16px',
            border: 'none',
            borderBottom: tabAtiva === 'estoque' ? '3px solid var(--blue-primary)' : '3px solid transparent',
            backgroundColor: 'transparent',
            fontWeight: tabAtiva === 'estoque' ? 700 : 500,
            color: tabAtiva === 'estoque' ? 'var(--blue-primary)' : 'var(--text-muted)',
            cursor: 'pointer',
            display: 'flex',
            alignItems: 'center',
            gap: '8px'
          }}
        >
          <Layers size={16} /> Estoque Individual por Loja
        </button>
        <button
          onClick={() => setTabAtiva('transferencias')}
          style={{
            padding: '10px 16px',
            border: 'none',
            borderBottom: tabAtiva === 'transferencias' ? '3px solid var(--blue-primary)' : '3px solid transparent',
            backgroundColor: 'transparent',
            fontWeight: tabAtiva === 'transferencias' ? 700 : 500,
            color: tabAtiva === 'transferencias' ? 'var(--blue-primary)' : 'var(--text-muted)',
            cursor: 'pointer',
            display: 'flex',
            alignItems: 'center',
            gap: '8px'
          }}
        >
          <ArrowRightLeft size={16} /> Transferências Internas
        </button>
        <button
          onClick={() => setTabAtiva('consolidado')}
          style={{
            padding: '10px 16px',
            border: 'none',
            borderBottom: tabAtiva === 'consolidado' ? '3px solid var(--blue-primary)' : '3px solid transparent',
            backgroundColor: 'transparent',
            fontWeight: tabAtiva === 'consolidado' ? 700 : 500,
            color: tabAtiva === 'consolidado' ? 'var(--blue-primary)' : 'var(--text-muted)',
            cursor: 'pointer',
            display: 'flex',
            alignItems: 'center',
            gap: '8px'
          }}
        >
          <BarChart3 size={16} /> Relatório Consolidado da Rede
        </button>
      </div>

      {/* TAB 1: LISTA DE FILIAIS */}
      {tabAtiva === 'filiais' && (
        <div style={{ backgroundColor: '#FFFFFF', border: '1px solid var(--border)', borderRadius: '12px', overflow: 'hidden' }}>
          {filiais.length === 0 ? (
            <div style={{ padding: '40px', textAlign: 'center', color: 'var(--text-muted)' }}>
              Nenhuma filial cadastrada além da unidade matriz padrão.
            </div>
          ) : (
            <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: '13.5px' }}>
              <thead>
                <tr style={{ backgroundColor: '#F8FAFC', borderBottom: '1px solid var(--border)', textAlign: 'left', color: 'var(--text-muted)' }}>
                  <th style={{ padding: '12px 16px' }}>CÓDIGO</th>
                  <th style={{ padding: '12px 16px' }}>NOME DA FILIAL / UNIDADE</th>
                  <th style={{ padding: '12px 16px' }}>CNPJ</th>
                  <th style={{ padding: '12px 16px' }}>TOKEN SINc</th>
                  <th style={{ padding: '12px 16px' }}>LOCALIZAÇÃO</th>
                  <th style={{ padding: '12px 16px' }}>CONEXÃO / AGENTE</th>
                  <th style={{ padding: '12px 16px', textAlign: 'center' }}>SITUAÇÃO</th>
                </tr>
              </thead>
              <tbody>
                {filiais.map(f => (
                  <tr key={f.id} style={{ borderBottom: '1px solid var(--border-light)' }}>
                    <td style={{ padding: '12px 16px', fontWeight: 700 }}>
                      <span style={{ backgroundColor: '#EEF2FF', color: 'var(--blue-primary)', padding: '2px 8px', borderRadius: '4px' }}>
                        {f.codigo}
                      </span>
                    </td>
                    <td style={{ padding: '12px 16px', fontWeight: 600 }}>{f.nome}</td>
                    <td style={{ padding: '12px 16px' }}>{f.cnpj || '—'}</td>
                    <td style={{ padding: '12px 16px' }}>
                      {f.token ? (
                        <div style={{ display: 'flex', alignItems: 'center', gap: '6px' }}>
                          <code style={{ fontSize: '11px', backgroundColor: '#F1F5F9', padding: '3px 6px', borderRadius: '4px', fontFamily: 'monospace' }}>
                            {f.token}
                          </code>
                          <button
                            onClick={() => copiarToken(f.token!)}
                            title="Copiar Token desta filial para o sincronizador SINc"
                            style={{
                              border: 'none',
                              background: 'none',
                              cursor: 'pointer',
                              padding: '2px',
                              display: 'flex',
                              alignItems: 'center',
                              color: copiadoToken === f.token ? '#16A34A' : 'var(--blue-primary)'
                            }}
                          >
                            {copiadoToken === f.token ? <Check size={14} /> : <Copy size={14} />}
                          </button>
                        </div>
                      ) : (
                        <span style={{ color: 'var(--text-muted)', fontSize: '12px' }}>—</span>
                      )}
                    </td>
                    <td style={{ padding: '12px 16px' }}>{f.cidade ? `${f.cidade} / ${f.uf}` : '—'}</td>
                    <td style={{ padding: '12px 16px' }}>
                      {f.agenteNome ? (
                        <span style={{ display: 'flex', alignItems: 'center', gap: '4px', color: '#059669', fontSize: '12px', fontWeight: 600 }}>
                          <Server size={13} /> {f.agenteNome}
                        </span>
                      ) : (
                        <span style={{ color: 'var(--text-muted)', fontSize: '12px' }}>Nexo Cloud Integrado</span>
                      )}
                    </td>
                    <td style={{ padding: '12px 16px', textAlign: 'center' }}>
                      <span style={{ backgroundColor: f.ativa ? '#DCFCE7' : '#FEE2E2', color: f.ativa ? '#166534' : '#991B1B', padding: '3px 8px', borderRadius: '12px', fontSize: '11px', fontWeight: 700 }}>
                        {f.ativa ? '● ATIVA' : '● INATIVA'}
                      </span>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </div>
      )}

      {/* TAB 2: ESTOQUE INDIVIDUAL POR FILIAL */}
      {tabAtiva === 'estoque' && (
        <div>
          {/* SELETOR DE FILIAL */}
          <div style={{ display: 'flex', alignItems: 'center', gap: '12px', marginBottom: '16px' }}>
            <span style={{ fontSize: '13px', fontWeight: 700, color: 'var(--text-main)' }}>Filial Selecionada:</span>
            <select
              value={filialSelecionada}
              onChange={e => setFilialSelecionada(e.target.value)}
              style={{
                padding: '8px 12px',
                borderRadius: '8px',
                border: '1px solid var(--border)',
                fontSize: '13px',
                minWidth: '240px',
                backgroundColor: '#FFF'
              }}
            >
              {filiais.map(f => (
                <option key={f.id} value={f.id}>
                  {f.codigo} — {f.nome}
                </option>
              ))}
            </select>
          </div>

          <div style={{ backgroundColor: '#FFFFFF', border: '1px solid var(--border)', borderRadius: '12px', overflow: 'hidden' }}>
            {loadingEstoque ? (
              <div style={{ padding: '30px', textAlign: 'center', color: 'var(--text-muted)' }}>
                <RefreshCw size={24} className="animate-spin" style={{ margin: '0 auto 8px' }} />
                Consultando estoque da filial selecionada...
              </div>
            ) : estoqueFilial.length === 0 ? (
              <div style={{ padding: '40px', textAlign: 'center', color: 'var(--text-muted)' }}>
                Nenhum item com estoque registrado especificamente nesta filial ainda.
              </div>
            ) : (
              <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: '13px' }}>
                <thead>
                  <tr style={{ backgroundColor: '#F8FAFC', borderBottom: '1px solid var(--border)', textAlign: 'left', color: 'var(--text-muted)' }}>
                    <th style={{ padding: '12px 16px' }}>CÓDIGO ERP</th>
                    <th style={{ padding: '12px 16px' }}>DESCRIÇÃO DO PRODUTO</th>
                    <th style={{ padding: '12px 16px' }}>UNIDADE</th>
                    <th style={{ padding: '12px 16px' }}>ESTOQUE FÍSICO</th>
                    <th style={{ padding: '12px 16px' }}>ESTOQUE MÍNIMO</th>
                    <th style={{ padding: '12px 16px' }}>CUSTO UNIT.</th>
                    <th style={{ padding: '12px 16px' }}>VALOR TOTAL ESTIMADO</th>
                  </tr>
                </thead>
                <tbody>
                  {estoqueFilial.map(item => (
                    <tr key={item.produtoId} style={{ borderBottom: '1px solid var(--border-light)' }}>
                      <td style={{ padding: '12px 16px', fontWeight: 600 }}>{item.codigoErp || '—'}</td>
                      <td style={{ padding: '12px 16px' }}>{item.descricao}</td>
                      <td style={{ padding: '12px 16px' }}>{item.unidade}</td>
                      <td style={{ padding: '12px 16px', fontWeight: 700, color: item.estoqueAtual <= item.estoqueMinimo ? '#DC2626' : 'var(--text-main)' }}>
                        {item.estoqueAtual}
                      </td>
                      <td style={{ padding: '12px 16px', color: 'var(--text-muted)' }}>{item.estoqueMinimo}</td>
                      <td style={{ padding: '12px 16px' }}>R$ {item.custoAtual.toFixed(2)}</td>
                      <td style={{ padding: '12px 16px', fontWeight: 700 }}>
                        R$ {(item.estoqueAtual * item.custoAtual).toFixed(2)}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            )}
          </div>
        </div>
      )}

      {/* TAB 3: TRANSFERÊNCIAS INTERNAS */}
      {tabAtiva === 'transferencias' && (
        <div>
          <div style={{ display: 'flex', justifyContent: 'flex-end', marginBottom: '14px' }}>
            <button
              onClick={() => {
                if (filiais.length < 2) {
                  alert('Você precisa ter pelo menos 2 filiais cadastradas para realizar transferências.');
                  return;
                }
                setFormTransf(prev => ({
                  ...prev,
                  origemId: filiais[0].id,
                  destinoId: filiais[1].id
                }));
                setModalNovaTransf(true);
              }}
              style={{
                backgroundColor: 'var(--blue-primary)',
                color: '#FFF',
                border: 'none',
                borderRadius: '8px',
                padding: '8px 14px',
                fontSize: '13px',
                fontWeight: 600,
                cursor: 'pointer',
                display: 'flex',
                alignItems: 'center',
                gap: '6px'
              }}
            >
              <Plus size={15} /> Solicitar Reposição / Transferência
            </button>
          </div>

          <div style={{ backgroundColor: '#FFFFFF', border: '1px solid var(--border)', borderRadius: '12px', overflow: 'hidden' }}>
            {transferencias.length === 0 ? (
              <div style={{ padding: '40px', textAlign: 'center', color: 'var(--text-muted)' }}>
                Nenhuma transferência interna solicitada até o momento.
              </div>
            ) : (
              <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: '13px' }}>
                <thead>
                  <tr style={{ backgroundColor: '#F8FAFC', borderBottom: '1px solid var(--border)', textAlign: 'left', color: 'var(--text-muted)' }}>
                    <th style={{ padding: '12px 16px' }}>NÚMERO</th>
                    <th style={{ padding: '12px 16px' }}>ORIGEM</th>
                    <th style={{ padding: '12px 16px' }}>DESTINO</th>
                    <th style={{ padding: '12px 16px' }}>ITENS</th>
                    <th style={{ padding: '12px 16px' }}>STATUS</th>
                    <th style={{ padding: '12px 16px' }}>SOLICITANTE</th>
                    <th style={{ padding: '12px 16px', textAlign: 'right' }}>AÇÕES DE FLUXO</th>
                  </tr>
                </thead>
                <tbody>
                  {transferencias.map(t => (
                    <tr key={t.id} style={{ borderBottom: '1px solid var(--border-light)' }}>
                      <td style={{ padding: '12px 16px', fontWeight: 700 }}>{t.numero}</td>
                      <td style={{ padding: '12px 16px' }}>{t.filialOrigemNome}</td>
                      <td style={{ padding: '12px 16px' }}>{t.filialDestinoNome}</td>
                      <td style={{ padding: '12px 16px' }}>
                        {t.itens.map(i => `${i.quantidadeSolicitada}x ${i.descricaoProduto}`).join(', ')}
                      </td>
                      <td style={{ padding: '12px 16px' }}>
                        <span
                          style={{
                            backgroundColor: t.status === 'Concluida' ? '#DCFCE7' : t.status === 'EmTransito' ? '#FEF3C7' : '#EFF6FF',
                            color: t.status === 'Concluida' ? '#166534' : t.status === 'EmTransito' ? '#92400E' : '#1E40AF',
                            padding: '3px 8px',
                            borderRadius: '12px',
                            fontSize: '11px',
                            fontWeight: 700
                          }}
                        >
                          {t.status.toUpperCase()}
                        </span>
                      </td>
                      <td style={{ padding: '12px 16px', color: 'var(--text-muted)' }}>{t.solicitadoPorNome}</td>
                      <td style={{ padding: '12px 16px', textAlign: 'right' }}>
                        {t.status === 'Pendente' && (
                          <button
                            onClick={() => handleMudarStatusTransf(t.id, 'Aprovada')}
                            style={{
                              backgroundColor: 'var(--blue-primary)',
                              color: '#FFF',
                              border: 'none',
                              padding: '5px 10px',
                              borderRadius: '6px',
                              fontSize: '12px',
                              cursor: 'pointer'
                            }}
                          >
                            Aprovar
                          </button>
                        )}
                        {t.status === 'Aprovada' && (
                          <button
                            onClick={() => handleMudarStatusTransf(t.id, 'EmTransito')}
                            style={{
                              backgroundColor: '#D97706',
                              color: '#FFF',
                              border: 'none',
                              padding: '5px 10px',
                              borderRadius: '6px',
                              fontSize: '12px',
                              cursor: 'pointer'
                            }}
                          >
                            Despachar (Em Trânsito)
                          </button>
                        )}
                        {t.status === 'EmTransito' && (
                          <button
                            onClick={() => handleMudarStatusTransf(t.id, 'Concluida')}
                            style={{
                              backgroundColor: '#059669',
                              color: '#FFF',
                              border: 'none',
                              padding: '5px 10px',
                              borderRadius: '6px',
                              fontSize: '12px',
                              cursor: 'pointer'
                            }}
                          >
                            Confirmar Recebimento
                          </button>
                        )}
                        {t.status === 'Concluida' && (
                          <span style={{ color: '#059669', fontSize: '12px', fontWeight: 600 }}>
                            ✓ Finalizada
                          </span>
                        )}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            )}
          </div>
        </div>
      )}

      {/* TAB 4: RELATÓRIO CONSOLIDADO */}
      {tabAtiva === 'consolidado' && relatorioConsolidado && (
        <div>
          {/* CARDS INDICADORES GLOBAIS */}
          <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(240px, 1fr))', gap: '16px', marginBottom: '24px' }}>
            <div style={{ backgroundColor: '#FFFFFF', border: '1px solid var(--border)', borderRadius: '12px', padding: '20px' }}>
              <div style={{ color: 'var(--text-muted)', fontSize: '13px', fontWeight: 600 }}>Total de Unidades / Filiais</div>
              <div style={{ fontSize: '28px', fontWeight: 800, color: 'var(--blue-primary)', marginTop: '4px' }}>
                {relatorioConsolidado.totalFiliais}
              </div>
            </div>

            <div style={{ backgroundColor: '#FFFFFF', border: '1px solid var(--border)', borderRadius: '12px', padding: '20px' }}>
              <div style={{ color: 'var(--text-muted)', fontSize: '13px', fontWeight: 600 }}>Mix Total de Itens Ativos</div>
              <div style={{ fontSize: '28px', fontWeight: 800, color: '#10B981', marginTop: '4px' }}>
                {relatorioConsolidado.totalProdutos}
              </div>
            </div>

            <div style={{ backgroundColor: '#FFFFFF', border: '1px solid var(--border)', borderRadius: '12px', padding: '20px' }}>
              <div style={{ color: 'var(--text-muted)', fontSize: '13px', fontWeight: 600 }}>Valor Total do Estoque em Rede</div>
              <div style={{ fontSize: '28px', fontWeight: 800, color: '#8B5CF6', marginTop: '4px' }}>
                R$ {relatorioConsolidado.valorEstoqueTotal.toFixed(2)}
              </div>
            </div>
          </div>

          {/* TABELA DE COMPARAÇÃO POR FILIAL */}
          <div style={{ backgroundColor: '#FFFFFF', border: '1px solid var(--border)', borderRadius: '12px', overflow: 'hidden' }}>
            <div style={{ padding: '16px 20px', borderBottom: '1px solid var(--border-light)', fontWeight: 700 }}>
              Comparativo de Desempenho e Estoque por Loja
            </div>
            <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: '13px' }}>
              <thead>
                <tr style={{ backgroundColor: '#F8FAFC', borderBottom: '1px solid var(--border)', textAlign: 'left', color: 'var(--text-muted)' }}>
                  <th style={{ padding: '12px 16px' }}>UNIDADE</th>
                  <th style={{ padding: '12px 16px' }}>LOCALIZAÇÃO</th>
                  <th style={{ padding: '12px 16px' }}>ITENS EM ESTOQUE</th>
                  <th style={{ padding: '12px 16px' }}>VOLUME TOTAL FÍSICO</th>
                  <th style={{ padding: '12px 16px' }}>VALOR TOTAL ESTIMADO</th>
                  <th style={{ padding: '12px 16px' }}>SINCRONIZAÇÃO</th>
                </tr>
              </thead>
              <tbody>
                {relatorioConsolidado.filiais.map(f => (
                  <tr key={f.filialId} style={{ borderBottom: '1px solid var(--border-light)' }}>
                    <td style={{ padding: '12px 16px', fontWeight: 700 }}>{f.codigo} — {f.nome}</td>
                    <td style={{ padding: '12px 16px' }}>{f.cidade ? `${f.cidade}/${f.uf}` : '—'}</td>
                    <td style={{ padding: '12px 16px' }}>{f.totalItensEstoque} produtos</td>
                    <td style={{ padding: '12px 16px', fontWeight: 600 }}>{f.estoqueFisicoTotal} un.</td>
                    <td style={{ padding: '12px 16px', fontWeight: 700, color: '#059669' }}>
                      R$ {f.valorEstoqueEstimado.toFixed(2)}
                    </td>
                    <td style={{ padding: '12px 16px' }}>
                      <span style={{ color: '#059669', fontSize: '12px', fontWeight: 600 }}>
                        ● {f.statusSincronizacao}
                      </span>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      )}

      {/* MODAL NOVA FILIAL */}
      {modalNovaFilial && (
        <div style={{ position: 'fixed', inset: 0, backgroundColor: 'rgba(0,0,0,0.5)', display: 'flex', alignItems: 'center', justifyContent: 'center', zIndex: 100 }}>
          <div style={{ backgroundColor: '#FFFFFF', borderRadius: '16px', width: '90%', maxWidth: '520px', padding: '24px', maxHeight: '90vh', overflowY: 'auto' }}>
            <h3 style={{ fontSize: '18px', fontWeight: 800, color: 'var(--text-main)', margin: '0 0 16px' }}>
              Cadastrar Nova Filial
            </h3>
            <form onSubmit={handleSalvarFilial}>
              <div style={{ display: 'grid', gridTemplateColumns: '1fr 2fr', gap: '12px', marginBottom: '12px' }}>
                <div>
                  <label style={{ fontSize: '12px', fontWeight: 700, color: 'var(--text-muted)' }}>CÓDIGO *</label>
                  <input
                    required
                    placeholder="ex: FIL-02"
                    value={formFilial.codigo}
                    onChange={e => setFormFilial(prev => ({ ...prev, codigo: e.target.value }))}
                    style={{ width: '100%', padding: '8px', borderRadius: '6px', border: '1px solid var(--border)', fontSize: '13px', marginTop: '4px' }}
                  />
                </div>
                <div>
                  <label style={{ fontSize: '12px', fontWeight: 700, color: 'var(--text-muted)' }}>NOME DA FILIAL *</label>
                  <input
                    required
                    placeholder="ex: SuperMix Filial Boa Viagem"
                    value={formFilial.nome}
                    onChange={e => setFormFilial(prev => ({ ...prev, nome: e.target.value }))}
                    style={{ width: '100%', padding: '8px', borderRadius: '6px', border: '1px solid var(--border)', fontSize: '13px', marginTop: '4px' }}
                  />
                </div>
              </div>

              <div style={{ marginBottom: '12px' }}>
                <label style={{ fontSize: '12px', fontWeight: 700, color: 'var(--text-muted)' }}>CNPJ DA FILIAL</label>
                <input
                  placeholder="00.000.000/0002-00"
                  value={formFilial.cnpj}
                  onChange={e => setFormFilial(prev => ({ ...prev, cnpj: e.target.value }))}
                  style={{ width: '100%', padding: '8px', borderRadius: '6px', border: '1px solid var(--border)', fontSize: '13px', marginTop: '4px' }}
                />
              </div>

              <div style={{ marginBottom: '12px' }}>
                <label style={{ fontSize: '12px', fontWeight: 700, color: 'var(--text-muted)' }}>ENDEREÇO</label>
                <input
                  placeholder="Av. Engenheiro Domingos Ferreira, 1200"
                  value={formFilial.endereco}
                  onChange={e => setFormFilial(prev => ({ ...prev, endereco: e.target.value }))}
                  style={{ width: '100%', padding: '8px', borderRadius: '6px', border: '1px solid var(--border)', fontSize: '13px', marginTop: '4px' }}
                />
              </div>

              <div style={{ display: 'grid', gridTemplateColumns: '2fr 1fr', gap: '12px', marginBottom: '12px' }}>
                <div>
                  <label style={{ fontSize: '12px', fontWeight: 700, color: 'var(--text-muted)' }}>CIDADE</label>
                  <input
                    placeholder="Recife"
                    value={formFilial.cidade}
                    onChange={e => setFormFilial(prev => ({ ...prev, cidade: e.target.value }))}
                    style={{ width: '100%', padding: '8px', borderRadius: '6px', border: '1px solid var(--border)', fontSize: '13px', marginTop: '4px' }}
                  />
                </div>
                <div>
                  <label style={{ fontSize: '12px', fontWeight: 700, color: 'var(--text-muted)' }}>UF</label>
                  <input
                    maxLength={2}
                    placeholder="PE"
                    value={formFilial.uf}
                    onChange={e => setFormFilial(prev => ({ ...prev, uf: e.target.value }))}
                    style={{ width: '100%', padding: '8px', borderRadius: '6px', border: '1px solid var(--border)', fontSize: '13px', marginTop: '4px' }}
                  />
                </div>
              </div>

              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '12px', marginBottom: '16px' }}>
                <div>
                  <label style={{ fontSize: '12px', fontWeight: 700, color: 'var(--text-muted)' }}>RESPONSÁVEL</label>
                  <input
                    placeholder="Nome do gerente"
                    value={formFilial.responsavel}
                    onChange={e => setFormFilial(prev => ({ ...prev, responsavel: e.target.value }))}
                    style={{ width: '100%', padding: '8px', borderRadius: '6px', border: '1px solid var(--border)', fontSize: '13px', marginTop: '4px' }}
                  />
                </div>
                <div>
                  <label style={{ fontSize: '12px', fontWeight: 700, color: 'var(--text-muted)' }}>TELEFONE</label>
                  <input
                    placeholder="(81) 3333-0000"
                    value={formFilial.telefone}
                    onChange={e => setFormFilial(prev => ({ ...prev, telefone: e.target.value }))}
                    style={{ width: '100%', padding: '8px', borderRadius: '6px', border: '1px solid var(--border)', fontSize: '13px', marginTop: '4px' }}
                  />
                </div>
              </div>

              <div style={{ display: 'flex', gap: '10px' }}>
                <button
                  type="button"
                  onClick={() => setModalNovaFilial(false)}
                  style={{ flex: 1, padding: '10px', borderRadius: '8px', border: '1px solid var(--border)', backgroundColor: '#FFF', cursor: 'pointer' }}
                >
                  Cancelar
                </button>
                <button
                  type="submit"
                  style={{ flex: 1, padding: '10px', borderRadius: '8px', border: 'none', backgroundColor: 'var(--blue-primary)', color: '#FFF', fontWeight: 700, cursor: 'pointer' }}
                >
                  Salvar Filial
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* MODAL NOVA TRANSFERÊNCIA */}
      {modalNovaTransf && (
        <div style={{ position: 'fixed', inset: 0, backgroundColor: 'rgba(0,0,0,0.5)', display: 'flex', alignItems: 'center', justifyContent: 'center', zIndex: 100 }}>
          <div style={{ backgroundColor: '#FFFFFF', borderRadius: '16px', width: '90%', maxWidth: '480px', padding: '24px' }}>
            <h3 style={{ fontSize: '18px', fontWeight: 800, color: 'var(--text-main)', margin: '0 0 16px' }}>
              Solicitar Transferência Interna
            </h3>
            <form onSubmit={handleCriarTransferencia}>
              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '12px', marginBottom: '12px' }}>
                <div>
                  <label style={{ fontSize: '12px', fontWeight: 700, color: 'var(--text-muted)' }}>ORIGEM (FORNECE)</label>
                  <select
                    value={formTransf.origemId}
                    onChange={e => setFormTransf(prev => ({ ...prev, origemId: e.target.value }))}
                    style={{ width: '100%', padding: '8px', borderRadius: '6px', border: '1px solid var(--border)', fontSize: '13px', marginTop: '4px' }}
                  >
                    {filiais.map(f => (
                      <option key={f.id} value={f.id}>{f.codigo} — {f.nome}</option>
                    ))}
                  </select>
                </div>
                <div>
                  <label style={{ fontSize: '12px', fontWeight: 700, color: 'var(--text-muted)' }}>DESTINO (RECEBE)</label>
                  <select
                    value={formTransf.destinoId}
                    onChange={e => setFormTransf(prev => ({ ...prev, destinoId: e.target.value }))}
                    style={{ width: '100%', padding: '8px', borderRadius: '6px', border: '1px solid var(--border)', fontSize: '13px', marginTop: '4px' }}
                  >
                    {filiais.map(f => (
                      <option key={f.id} value={f.id}>{f.codigo} — {f.nome}</option>
                    ))}
                  </select>
                </div>
              </div>

              <div style={{ marginBottom: '12px' }}>
                <label style={{ fontSize: '12px', fontWeight: 700, color: 'var(--text-muted)' }}>PRODUTO A TRANSFERIR</label>
                <select
                  value={formTransf.produtoId}
                  onChange={e => setFormTransf(prev => ({ ...prev, produtoId: e.target.value }))}
                  style={{ width: '100%', padding: '8px', borderRadius: '6px', border: '1px solid var(--border)', fontSize: '13px', marginTop: '4px' }}
                >
                  {produtosDisponiveis.map(p => (
                    <option key={p.id} value={p.id}>{p.descricao} ({p.unidade})</option>
                  ))}
                </select>
              </div>

              <div style={{ marginBottom: '12px' }}>
                <label style={{ fontSize: '12px', fontWeight: 700, color: 'var(--text-muted)' }}>QUANTIDADE</label>
                <input
                  type="number"
                  min={1}
                  required
                  value={formTransf.quantidade}
                  onChange={e => setFormTransf(prev => ({ ...prev, quantidade: Number(e.target.value) }))}
                  style={{ width: '100%', padding: '8px', borderRadius: '6px', border: '1px solid var(--border)', fontSize: '13px', marginTop: '4px' }}
                />
              </div>

              <div style={{ marginBottom: '16px' }}>
                <label style={{ fontSize: '12px', fontWeight: 700, color: 'var(--text-muted)' }}>MOTIVO / OBSERVAÇÕES</label>
                <textarea
                  placeholder="Ex.: Reposição urgente para suprir alta demanda no fim de semana."
                  value={formTransf.observacoes}
                  onChange={e => setFormTransf(prev => ({ ...prev, observacoes: e.target.value }))}
                  rows={2}
                  style={{ width: '100%', padding: '8px', borderRadius: '6px', border: '1px solid var(--border)', fontSize: '13px', marginTop: '4px' }}
                />
              </div>

              <div style={{ display: 'flex', gap: '10px' }}>
                <button
                  type="button"
                  onClick={() => setModalNovaTransf(false)}
                  style={{ flex: 1, padding: '10px', borderRadius: '8px', border: '1px solid var(--border)', backgroundColor: '#FFF', cursor: 'pointer' }}
                >
                  Cancelar
                </button>
                <button
                  type="submit"
                  style={{ flex: 1, padding: '10px', borderRadius: '8px', border: 'none', backgroundColor: 'var(--blue-primary)', color: '#FFF', fontWeight: 700, cursor: 'pointer' }}
                >
                  Enviar Solicitação
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
};

