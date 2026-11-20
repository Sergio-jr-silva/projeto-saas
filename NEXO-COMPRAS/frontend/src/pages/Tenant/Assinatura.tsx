import React, { useState, useEffect } from 'react';
import { api } from '../../services/api';
import {
  CreditCard,
  QrCode,
  CheckCircle2,
  AlertTriangle,
  Clock,
  Sparkles,
  ShieldCheck,
  FileText,
  RefreshCw,
  XCircle,
  Copy,
  ExternalLink,
  ChevronRight
} from 'lucide-react';

interface Plano {
  id: string;
  nome: string;
  descricao: string;
  mensalidade: number;
  precoAnual: number;
  maxUsuarios: number;
  maxFiliais: number;
  maxFornecedores: number;
  maxCotacoesMes: number;
  diasTeste: number;
  recursosJson: string;
}

interface LimitesUso {
  usuariosUsados: number;
  maxUsuarios: number;
  filiaisUsadas: number;
  maxFiliais: number;
  cotacoesMesUsadas: number;
  maxCotacoesMes: number;
  produtosUsados?: number;
  maxProdutos?: number;
  sincronizadoresUsados?: number;
  maxSincronizadores?: number;
}

interface AssinaturaDetalhe {
  id: string;
  planoId: string;
  planoNome: string;
  status: string;
  ciclo: string;
  valor: number;
  dataInicio: string;
  dataFim?: string;
  dataRenovacao?: string;
  formaPagamento: string;
  diasRestantesTeste: number;
  limites: LimitesUso;
  slug?: string;
  faixaComercial?: string;
  moduloContratado?: string;
  moduloDescricao?: string;
  volumeUtilizacao?: string;
  mensalidadeCalculada?: number;
  descontoComercial?: number;
  ajusteManual?: number;
  mensalidadeFinal?: number;
  situacaoFinanceira?: string;
  proximoVencimento?: string;
}

interface SolicitacaoPlano {
  id: string;
  moduloAtual: string;
  moduloSolicitado: string;
  observacoesCliente: string;
  status: string;
  respostaAdmin: string;
  dataCriacao: string;
}

interface Fatura {
  id: string;
  numero: string;
  descricao: string;
  valor: number;
  status: string;
  dataEmissao: string;
  dataVencimento: string;
  dataPagamento?: string;
  metodoPagamento: string;
  pixQrCode: string;
  pixCopiaECola: string;
  urlBoleto: string;
}

export const Assinatura: React.FC = () => {
  const [assinatura, setAssinatura] = useState<AssinaturaDetalhe | null>(null);
  const [planos, setPlanos] = useState<Plano[]>([]);
  const [faturas, setFaturas] = useState<Fatura[]>([]);
  const [loading, setLoading] = useState(true);
  const [cicloSelecionado, setCicloSelecionado] = useState<'Mensal' | 'Anual'>('Anual');
  const [processando, setProcessando] = useState(false);

  // Modal Checkout / Pagamento Pix
  const [faturaAberta, setFaturaAberta] = useState<Fatura | null>(null);
  const [copiadoPix, setCopiadoPix] = useState(false);
  const [modalCancelar, setModalCancelar] = useState(false);
  const [motivoCancelamento, setMotivoCancelamento] = useState('');
  const [solicitacoes, setSolicitacoes] = useState<SolicitacaoPlano[]>([]);
  const [moduloSolicitado, setModuloSolicitado] = useState('CompletoServicos');
  const [obsSolicitacao, setObsSolicitacao] = useState('');

  const carregarDados = async () => {
    try {
      setLoading(true);
      const [resAssinatura, resPlanos, resFaturas, resSol] = await Promise.all([
        api.get<AssinaturaDetalhe>('/assinaturas/minha'),
        api.get<Plano[]>('/assinaturas/planos'),
        api.get<Fatura[]>('/assinaturas/faturas'),
        api.get<SolicitacaoPlano[]>('/assinaturas/solicitacoes').catch(() => [])
      ]);
      setAssinatura(resAssinatura);
      setPlanos(resPlanos);
      setFaturas(resFaturas);
      setSolicitacoes(resSol);
    } catch (err: any) {
      console.error('Erro ao carregar dados da assinatura:', err);
    } finally {
      setLoading(false);
    }
  };

  const handleSolicitarAlteracao = async (e: React.FormEvent) => {
    e.preventDefault();
    try {
      setProcessando(true);
      await api.post('/assinaturas/solicitar-alteracao', {
        moduloSolicitado,
        observacoesCliente: obsSolicitacao
      });
      alert('Solicitação de alteração enviada ao Administrador Geral para aprovação!');
      setObsSolicitacao('');
      await carregarDados();
    } catch (err: any) {
      alert('Erro ao solicitar alteração: ' + err.message);
    } finally {
      setProcessando(false);
    }
  };

  useEffect(() => {
    carregarDados();
  }, []);

  const handleCheckout = async (plano: Plano, formaPagamento: 'Pix' | 'Cartao' | 'Boleto') => {
    try {
      setProcessando(true);
      const res = await api.post<Fatura>('/assinaturas/checkout', {
        planoId: plano.id,
        ciclo: cicloSelecionado,
        formaPagamento
      });
      alert(`Plano ${plano.nome} selecionado com sucesso!`);
      setFaturaAberta(res);
      await carregarDados();
    } catch (err: any) {
      alert('Erro ao realizar assinatura: ' + (err.response?.data?.erro || err.message));
    } finally {
      setProcessando(false);
    }
  };

  const handlePagarFatura = async (faturaId: string, metodo: string = 'Pix') => {
    try {
      setProcessando(true);
      await api.post(`/assinaturas/faturas/${faturaId}/pagar`, { metodo });
      alert('Pagamento confirmado com sucesso! Assinatura ativada.');
      setFaturaAberta(null);
      await carregarDados();
    } catch (err: any) {
      alert('Erro ao processar pagamento: ' + (err.response?.data?.erro || err.message));
    } finally {
      setProcessando(false);
    }
  };

  const handleCancelarAssinatura = async () => {
    if (!motivoCancelamento.trim()) {
      alert('Por favor informe o motivo do cancelamento.');
      return;
    }
    try {
      setProcessando(true);
      await api.post('/assinaturas/cancelar', { motivo: motivoCancelamento });
      alert('Assinatura cancelada com sucesso.');
      setModalCancelar(false);
      await carregarDados();
    } catch (err: any) {
      alert('Erro ao cancelar: ' + (err.response?.data?.erro || err.message));
    } finally {
      setProcessando(false);
    }
  };

  const handleReativar = async () => {
    try {
      setProcessando(true);
      await api.post('/assinaturas/reativar', {});
      alert('Assinatura reativada com sucesso!');
      await carregarDados();
    } catch (err: any) {
      alert('Erro ao reativar: ' + (err.response?.data?.erro || err.message));
    } finally {
      setProcessando(false);
    }
  };

  const copiarPix = (texto: string) => {
    navigator.clipboard.writeText(texto);
    setCopiadoPix(true);
    setTimeout(() => setCopiadoPix(false), 2500);
  };

  if (loading) {
    return (
      <div style={{ padding: '40px', textAlign: 'center', color: 'var(--text-muted)' }}>
        <RefreshCw size={28} className="animate-spin" style={{ margin: '0 auto 12px' }} />
        Carregando informações da sua assinatura e planos...
      </div>
    );
  }

  const statusColor: Record<string, string> = {
    Ativa: '#059669',
    Teste: '#2563EB',
    Inadimplente: '#DC2626',
    Suspensa: '#DC2626',
    Cancelada: '#6B7280'
  };

  return (
    <div style={{ padding: '24px 32px', maxWidth: '1200px', margin: '0 auto' }}>
      {/* CABEÇALHO */}
      <div style={{ marginBottom: '28px' }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: '10px' }}>
          <Sparkles size={24} style={{ color: 'var(--blue-primary)' }} />
          <h1 style={{ fontSize: '24px', fontWeight: 800, color: 'var(--text-main)', margin: 0 }}>
            Plano, Assinatura e Pagamentos
          </h1>
        </div>
        <p style={{ color: 'var(--text-muted)', fontSize: '14px', marginTop: '4px' }}>
          Gerencie o plano da sua empresa, consulte limites de uso, emita segundas vias de pagamento e realize upgrades.
        </p>
      </div>

      {/* CARD DO PLANO ATUAL & LIMITES */}
      {assinatura && (
        <div
          style={{
            backgroundColor: '#FFFFFF',
            border: '1px solid var(--border)',
            borderRadius: '12px',
            padding: '24px',
            marginBottom: '32px',
            boxShadow: '0 1px 3px rgba(0,0,0,0.05)'
          }}
        >
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', flexWrap: 'wrap', gap: '16px' }}>
            <div>
              <div style={{ display: 'flex', alignItems: 'center', gap: '10px' }}>
                <span style={{ fontSize: '18px', fontWeight: 700, color: 'var(--text-main)' }}>
                  Plano Atual: <strong>{assinatura.planoNome}</strong>
                </span>
                <span
                  style={{
                    backgroundColor: `${statusColor[assinatura.status] || '#2563EB'}15`,
                    color: statusColor[assinatura.status] || '#2563EB',
                    padding: '4px 10px',
                    borderRadius: '20px',
                    fontSize: '12px',
                    fontWeight: 700
                  }}
                >
                  ● {assinatura.status.toUpperCase()}
                </span>
                {assinatura.diasRestantesTeste > 0 && (
                  <span style={{ fontSize: '12px', color: '#D97706', fontWeight: 600, display: 'flex', alignItems: 'center', gap: '4px' }}>
                    <Clock size={13} /> {assinatura.diasRestantesTeste} dias de teste grátis restantes
                  </span>
                )}
              </div>
              <div style={{ color: 'var(--text-muted)', fontSize: '13px', marginTop: '6px' }}>
                Ciclo: <strong>{assinatura.ciclo}</strong> • Valor: <strong>R$ {assinatura.valor.toFixed(2)}/{assinatura.ciclo === 'Anual' ? 'ano' : 'mês'}</strong> • Pagamento: {assinatura.formaPagamento}
                {assinatura.dataRenovacao && (
                  <span> • Próxima renovação: <strong>{new Date(assinatura.dataRenovacao).toLocaleDateString('pt-BR')}</strong></span>
                )}
              </div>
            </div>

            <div style={{ display: 'flex', gap: '10px' }}>
              {assinatura.status === 'Cancelada' ? (
                <button
                  onClick={handleReativar}
                  disabled={processando}
                  style={{
                    backgroundColor: 'var(--blue-primary)',
                    color: '#FFF',
                    border: 'none',
                    borderRadius: '8px',
                    padding: '8px 16px',
                    fontWeight: 600,
                    cursor: 'pointer'
                  }}
                >
                  Reativar Assinatura
                </button>
              ) : (
                <button
                  onClick={() => setModalCancelar(true)}
                  style={{
                    backgroundColor: 'transparent',
                    color: '#DC2626',
                    border: '1px solid #FCA5A5',
                    borderRadius: '8px',
                    padding: '8px 14px',
                    fontSize: '13px',
                    fontWeight: 600,
                    cursor: 'pointer'
                  }}
                >
                  Cancelar Assinatura
                </button>
              )}
            </div>
          </div>

          {/* LIMITES DE CONSUMO */}
          {assinatura.limites && (
            <div style={{ marginTop: '24px', borderTop: '1px solid var(--border-light)', paddingTop: '20px' }}>
              <div style={{ fontSize: '13px', fontWeight: 700, color: 'var(--text-muted)', textTransform: 'uppercase', marginBottom: '14px' }}>
                Consumo dos Limites do Plano
              </div>
              <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(240px, 1fr))', gap: '16px' }}>
                {/* Usuários */}
                <div style={{ backgroundColor: '#F8FAFC', padding: '14px', borderRadius: '8px', border: '1px solid #E2E8F0' }}>
                  <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: '13px', fontWeight: 600, marginBottom: '6px' }}>
                    <span>Usuários Cadastrados</span>
                    <span>{assinatura.limites.usuariosUsados} / {assinatura.limites.maxUsuarios}</span>
                  </div>
                  <div style={{ height: '8px', backgroundColor: '#CBD5E1', borderRadius: '4px', overflow: 'hidden' }}>
                    <div
                      style={{
                        height: '100%',
                        backgroundColor: (assinatura.limites.usuariosUsados / assinatura.limites.maxUsuarios) >= 1 ? '#EF4444' : 'var(--blue-primary)',
                        width: `${Math.min(100, (assinatura.limites.usuariosUsados / assinatura.limites.maxUsuarios) * 100)}%`
                      }}
                    />
                  </div>
                </div>

                {/* Filiais */}
                <div style={{ backgroundColor: '#F8FAFC', padding: '14px', borderRadius: '8px', border: '1px solid #E2E8F0' }}>
                  <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: '13px', fontWeight: 600, marginBottom: '6px' }}>
                    <span>Filiais Ativas</span>
                    <span>{assinatura.limites.filiaisUsadas} / {assinatura.limites.maxFiliais}</span>
                  </div>
                  <div style={{ height: '8px', backgroundColor: '#CBD5E1', borderRadius: '4px', overflow: 'hidden' }}>
                    <div
                      style={{
                        height: '100%',
                        backgroundColor: (assinatura.limites.filiaisUsadas / assinatura.limites.maxFiliais) >= 1 ? '#EF4444' : '#10B981',
                        width: `${Math.min(100, (assinatura.limites.filiaisUsadas / assinatura.limites.maxFiliais) * 100)}%`
                      }}
                    />
                  </div>
                </div>

                {/* Cotações do Mês */}
                <div style={{ backgroundColor: '#F8FAFC', padding: '14px', borderRadius: '8px', border: '1px solid #E2E8F0' }}>
                  <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: '13px', fontWeight: 600, marginBottom: '6px' }}>
                    <span>Cotações este Mês</span>
                    <span>{assinatura.limites.cotacoesMesUsadas} / {assinatura.limites.maxCotacoesMes}</span>
                  </div>
                  <div style={{ height: '8px', backgroundColor: '#CBD5E1', borderRadius: '4px', overflow: 'hidden' }}>
                    <div
                      style={{
                        height: '100%',
                        backgroundColor: (assinatura.limites.cotacoesMesUsadas / assinatura.limites.maxCotacoesMes) >= 1 ? '#EF4444' : '#8B5CF6',
                        width: `${Math.min(100, (assinatura.limites.cotacoesMesUsadas / assinatura.limites.maxCotacoesMes) * 100)}%`
                      }}
                    />
                  </div>
                </div>

                {/* Produtos Cadastrados */}
                <div style={{ backgroundColor: '#F8FAFC', padding: '14px', borderRadius: '8px', border: '1px solid #E2E8F0' }}>
                  <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: '13px', fontWeight: 600, marginBottom: '6px' }}>
                    <span>Produtos Cadastrados</span>
                    <span>{(assinatura.limites.produtosUsados ?? 0).toLocaleString('pt-BR')} / {(assinatura.limites.maxProdutos ?? 10000).toLocaleString('pt-BR')}</span>
                  </div>
                  <div style={{ fontSize: '11.5px', color: 'var(--text-muted)' }}>
                    Sincronizadores SINc ativos: <strong>{assinatura.limites.sincronizadoresUsados ?? 1} / {assinatura.limites.maxSincronizadores ?? 3}</strong>
                  </div>
                </div>
              </div>
            </div>
          )}

          {/* DETALHE DA PRECIFICAÇÃO DINÂMICA E SOLICITAÇÃO DE ALTERAÇÃO */}
          <div style={{ marginTop: '24px', borderTop: '1px solid var(--border-light)', paddingTop: '20px', display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(300px, 1fr))', gap: '20px' }}>
            <div style={{ backgroundColor: '#EFF6FF', padding: '16px', borderRadius: '10px', border: '1px solid #BFDBFE' }}>
              <div style={{ fontSize: '13px', fontWeight: 700, color: '#1E40AF', marginBottom: '10px', textTransform: 'uppercase' }}>
                Condição Comercial & Mensalidade Vigente
              </div>
              <div style={{ fontSize: '13px', lineHeight: 1.8 }}>
                <div>URL Individual: <strong>nexoscompras.com.br/{assinatura.slug || 'empresa'}</strong></div>
                <div>Faixa Comercial: <strong>{assinatura.faixaComercial || 'Essencial'}</strong></div>
                <div>Recursos Contratados: <strong>{assinatura.moduloDescricao || 'Compras e cotações básicas'}</strong></div>
                <div>Volume de Utilização: <strong>{assinatura.volumeUtilizacao || 'Baixo'}</strong></div>
                <div>Situação Financeira: <strong>{assinatura.situacaoFinanceira || 'Regular'}</strong></div>
                <div>Mensalidade Vigente: <strong style={{ fontSize: '16px', color: 'var(--blue-primary)' }}>R$ {(assinatura.mensalidadeFinal ?? assinatura.valor).toFixed(2)}/mês</strong></div>
              </div>
            </div>

            <form onSubmit={handleSolicitarAlteracao} style={{ backgroundColor: '#F8FAFC', padding: '16px', borderRadius: '10px', border: '1px solid #E2E8F0' }}>
              <div style={{ fontSize: '13px', fontWeight: 700, color: 'var(--text-main)', marginBottom: '8px', textTransform: 'uppercase' }}>
                Solicitar Alteração de Módulos / Plano
              </div>
              <p style={{ fontSize: '12px', color: 'var(--text-muted)', marginBottom: '10px' }}>
                Solicite upgrade ou alteração de módulos. A aprovação e atualização comercial serão analisadas pelo Administrador Geral.
              </p>
              <div className="form-group" style={{ marginBottom: '8px' }}>
                <select
                  value={moduloSolicitado}
                  onChange={(e) => setModuloSolicitado(e.target.value)}
                  style={{ width: '100%', padding: '7px 10px', fontSize: '13px' }}
                >
                  <option value="Basico">Compras e cotações básicas</option>
                  <option value="RelatoriosAvancados">Compras e relatórios avançados</option>
                  <option value="CompletoServicos">Compras, relatórios e serviços</option>
                </select>
              </div>
              <div className="form-group" style={{ marginBottom: '8px' }}>
                <input
                  type="text"
                  placeholder="Observações ou necessidade adicional (ex: mais filiais/usuários)"
                  value={obsSolicitacao}
                  onChange={(e) => setObsSolicitacao(e.target.value)}
                  style={{ width: '100%', padding: '7px 10px', fontSize: '13px' }}
                />
              </div>
              <button type="submit" disabled={processando} className="btn btn-primary" style={{ width: '100%', padding: '8px', fontSize: '13px' }}>
                Enviar Solicitação ao Administrador Geral
              </button>
              {solicitacoes.length > 0 && (
                <div style={{ marginTop: '8px', fontSize: '11.5px', color: 'var(--text-muted)' }}>
                  Última solicitação: <strong>{solicitacoes[0].moduloSolicitado}</strong> ({solicitacoes[0].status})
                </div>
              )}
            </form>
          </div>
        </div>
      )}

      {/* SEÇÃO: TABELA DE PLANOS PARA UPGRADE OU TROCA */}
      <div style={{ marginBottom: '40px' }}>
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '18px', flexWrap: 'wrap', gap: '12px' }}>
          <div>
            <h2 style={{ fontSize: '18px', fontWeight: 800, color: 'var(--text-main)', margin: 0 }}>
              Planos Comerciais Disponíveis
            </h2>
            <p style={{ color: 'var(--text-muted)', fontSize: '13px', margin: 0 }}>
              Escolha o plano ideal para a escala de compras da sua rede.
            </p>
          </div>

          {/* SELETOR MENSAL / ANUAL */}
          <div style={{ display: 'flex', backgroundColor: '#E2E8F0', padding: '3px', borderRadius: '8px' }}>
            <button
              onClick={() => setCicloSelecionado('Mensal')}
              style={{
                border: 'none',
                backgroundColor: cicloSelecionado === 'Mensal' ? '#FFFFFF' : 'transparent',
                color: cicloSelecionado === 'Mensal' ? 'var(--blue-primary)' : 'var(--text-muted)',
                fontWeight: 700,
                fontSize: '12.5px',
                padding: '6px 14px',
                borderRadius: '6px',
                cursor: 'pointer'
              }}
            >
              Mensal
            </button>
            <button
              onClick={() => setCicloSelecionado('Anual')}
              style={{
                border: 'none',
                backgroundColor: cicloSelecionado === 'Anual' ? '#FFFFFF' : 'transparent',
                color: cicloSelecionado === 'Anual' ? 'var(--blue-primary)' : 'var(--text-muted)',
                fontWeight: 700,
                fontSize: '12.5px',
                padding: '6px 14px',
                borderRadius: '6px',
                cursor: 'pointer',
                display: 'flex',
                alignItems: 'center',
                gap: '4px'
              }}
            >
              Anual <span style={{ backgroundColor: '#10B981', color: '#FFF', fontSize: '10px', padding: '1px 5px', borderRadius: '4px' }}>Economize 15%</span>
            </button>
          </div>
        </div>

        {/* GRID DE PLANOS */}
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(280px, 1fr))', gap: '20px' }}>
          {planos.map(plano => {
            const isAtual = assinatura?.planoId === plano.id;
            const preco = cicloSelecionado === 'Anual' ? plano.precoAnual : plano.mensalidade;
            return (
              <div
                key={plano.id}
                style={{
                  backgroundColor: '#FFFFFF',
                  borderRadius: '12px',
                  border: isAtual ? '2px solid var(--blue-primary)' : '1px solid var(--border)',
                  padding: '24px',
                  display: 'flex',
                  flexDirection: 'column',
                  position: 'relative',
                  boxShadow: isAtual ? '0 4px 14px rgba(37,99,235,0.12)' : '0 1px 3px rgba(0,0,0,0.05)'
                }}
              >
                {isAtual && (
                  <div
                    style={{
                      position: 'absolute',
                      top: '-10px',
                      right: '16px',
                      backgroundColor: 'var(--blue-primary)',
                      color: '#FFF',
                      fontSize: '11px',
                      fontWeight: 800,
                      padding: '2px 10px',
                      borderRadius: '12px',
                      textTransform: 'uppercase'
                    }}
                  >
                    Plano Ativo
                  </div>
                )}

                <div style={{ fontSize: '18px', fontWeight: 800, color: 'var(--text-main)' }}>{plano.nome}</div>
                <div style={{ color: 'var(--text-muted)', fontSize: '12.5px', minHeight: '36px', marginTop: '4px' }}>
                  {plano.descricao}
                </div>

                <div style={{ margin: '18px 0 16px' }}>
                  <span style={{ fontSize: '28px', fontWeight: 800, color: 'var(--text-main)' }}>
                    R$ {preco.toFixed(2)}
                  </span>
                  <span style={{ fontSize: '12.5px', color: 'var(--text-muted)' }}>
                    /{cicloSelecionado === 'Anual' ? 'ano' : 'mês'}
                  </span>
                </div>

                {/* BENEFÍCIOS */}
                <div style={{ flex: 1, borderTop: '1px solid var(--border-light)', paddingTop: '16px', fontSize: '13px' }}>
                  <div style={{ display: 'flex', alignItems: 'center', gap: '8px', marginBottom: '8px' }}>
                    <CheckCircle2 size={15} color="#10B981" />
                    <span>Até <strong>{plano.maxFiliais}</strong> filiais integradas</span>
                  </div>
                  <div style={{ display: 'flex', alignItems: 'center', gap: '8px', marginBottom: '8px' }}>
                    <CheckCircle2 size={15} color="#10B981" />
                    <span>Até <strong>{plano.maxUsuarios}</strong> usuários no sistema</span>
                  </div>
                  <div style={{ display: 'flex', alignItems: 'center', gap: '8px', marginBottom: '8px' }}>
                    <CheckCircle2 size={15} color="#10B981" />
                    <span>Até <strong>{plano.maxCotacoesMes}</strong> cotações/mês</span>
                  </div>
                  <div style={{ display: 'flex', alignItems: 'center', gap: '8px', marginBottom: '8px' }}>
                    <CheckCircle2 size={15} color="#10B981" />
                    <span>Fornecedores ilimitados e Portal Web</span>
                  </div>
                  <div style={{ display: 'flex', alignItems: 'center', gap: '8px', marginBottom: '8px' }}>
                    <CheckCircle2 size={15} color="#10B981" />
                    <span>Conector Firebird & PostgreSQL</span>
                  </div>
                </div>

                {/* BOTÃO CONTRATAR */}
                <div style={{ marginTop: '20px', display: 'flex', gap: '8px' }}>
                  <button
                    onClick={() => handleCheckout(plano, 'Pix')}
                    disabled={processando || isAtual}
                    style={{
                      flex: 1,
                      backgroundColor: isAtual ? '#F1F5F9' : 'var(--blue-primary)',
                      color: isAtual ? 'var(--text-muted)' : '#FFF',
                      border: 'none',
                      borderRadius: '8px',
                      padding: '10px',
                      fontWeight: 700,
                      fontSize: '13px',
                      cursor: isAtual ? 'default' : 'pointer'
                    }}
                  >
                    {isAtual ? 'Plano Atual' : 'Contratar via Pix'}
                  </button>
                  {!isAtual && (
                    <button
                      onClick={() => handleCheckout(plano, 'Cartao')}
                      disabled={processando}
                      title="Pagar com Cartão de Crédito"
                      style={{
                        backgroundColor: '#F8FAFC',
                        border: '1px solid var(--border)',
                        color: 'var(--text-main)',
                        borderRadius: '8px',
                        padding: '10px 12px',
                        cursor: 'pointer'
                      }}
                    >
                      <CreditCard size={16} />
                    </button>
                  )}
                </div>
              </div>
            );
          })}
        </div>
      </div>

      {/* SEÇÃO: HISTÓRICO DE FATURAS */}
      <div>
        <div style={{ display: 'flex', alignItems: 'center', gap: '8px', marginBottom: '14px' }}>
          <FileText size={20} style={{ color: 'var(--text-muted)' }} />
          <h2 style={{ fontSize: '18px', fontWeight: 800, color: 'var(--text-main)', margin: 0 }}>
            Histórico Financeiro e Faturas
          </h2>
        </div>

        {faturas.length === 0 ? (
          <div style={{ backgroundColor: '#FFFFFF', border: '1px solid var(--border)', borderRadius: '12px', padding: '30px', textAlign: 'center', color: 'var(--text-muted)' }}>
            Nenhuma fatura emitida até o momento.
          </div>
        ) : (
          <div style={{ backgroundColor: '#FFFFFF', border: '1px solid var(--border)', borderRadius: '12px', overflow: 'hidden' }}>
            <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: '13px' }}>
              <thead>
                <tr style={{ backgroundColor: '#F8FAFC', borderBottom: '1px solid var(--border)', textAlign: 'left', color: 'var(--text-muted)' }}>
                  <th style={{ padding: '12px 16px' }}>NÚMERO</th>
                  <th style={{ padding: '12px 16px' }}>DESCRIÇÃO</th>
                  <th style={{ padding: '12px 16px' }}>VALOR</th>
                  <th style={{ padding: '12px 16px' }}>VENCIMENTO</th>
                  <th style={{ padding: '12px 16px' }}>STATUS</th>
                  <th style={{ padding: '12px 16px', textAlign: 'right' }}>AÇÕES</th>
                </tr>
              </thead>
              <tbody>
                {faturas.map(fat => (
                  <tr key={fat.id} style={{ borderBottom: '1px solid var(--border-light)' }}>
                    <td style={{ padding: '12px 16px', fontWeight: 600 }}>{fat.numero}</td>
                    <td style={{ padding: '12px 16px' }}>{fat.descricao}</td>
                    <td style={{ padding: '12px 16px', fontWeight: 700 }}>R$ {fat.valor.toFixed(2)}</td>
                    <td style={{ padding: '12px 16px' }}>{new Date(fat.dataVencimento).toLocaleDateString('pt-BR')}</td>
                    <td style={{ padding: '12px 16px' }}>
                      <span
                        style={{
                          backgroundColor: fat.status === 'Paga' ? '#DCFCE7' : fat.status === 'Vencida' ? '#FEE2E2' : '#FEF3C7',
                          color: fat.status === 'Paga' ? '#166534' : fat.status === 'Vencida' ? '#991B1B' : '#92400E',
                          padding: '3px 8px',
                          borderRadius: '12px',
                          fontSize: '11px',
                          fontWeight: 700
                        }}
                      >
                        {fat.status.toUpperCase()}
                      </span>
                    </td>
                    <td style={{ padding: '12px 16px', textAlign: 'right' }}>
                      {fat.status === 'Pendente' ? (
                        <button
                          onClick={() => setFaturaAberta(fat)}
                          style={{
                            backgroundColor: 'var(--blue-primary)',
                            color: '#FFF',
                            border: 'none',
                            padding: '6px 12px',
                            borderRadius: '6px',
                            fontSize: '12px',
                            fontWeight: 600,
                            cursor: 'pointer'
                          }}
                        >
                          Pagar Fatura
                        </button>
                      ) : (
                        <button
                          onClick={() => setFaturaAberta(fat)}
                          style={{
                            backgroundColor: 'transparent',
                            color: 'var(--text-muted)',
                            border: '1px solid var(--border)',
                            padding: '6px 10px',
                            borderRadius: '6px',
                            fontSize: '12px',
                            cursor: 'pointer'
                          }}
                        >
                          Ver Detalhes
                        </button>
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>

      {/* MODAL DE PAGAMENTO / DETALHE FATURA */}
      {faturaAberta && (
        <div style={{ position: 'fixed', inset: 0, backgroundColor: 'rgba(0,0,0,0.5)', display: 'flex', alignItems: 'center', justifyContent: 'center', zIndex: 100 }}>
          <div style={{ backgroundColor: '#FFFFFF', borderRadius: '16px', width: '90%', maxWidth: '480px', padding: '24px', boxShadow: '0 10px 25px rgba(0,0,0,0.2)' }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', borderBottom: '1px solid var(--border-light)', paddingBottom: '12px' }}>
              <div style={{ fontWeight: 800, fontSize: '17px', color: 'var(--text-main)' }}>
                Fatura {faturaAberta.numero}
              </div>
              <button onClick={() => setFaturaAberta(null)} style={{ border: 'none', background: 'none', cursor: 'pointer' }}>
                <XCircle size={20} color="var(--text-muted)" />
              </button>
            </div>

            <div style={{ textAlign: 'center', margin: '20px 0' }}>
              <div style={{ fontSize: '13px', color: 'var(--text-muted)' }}>Valor total</div>
              <div style={{ fontSize: '28px', fontWeight: 800, color: 'var(--text-main)' }}>
                R$ {faturaAberta.valor.toFixed(2)}
              </div>
              <div style={{ fontSize: '12px', color: 'var(--text-muted)', marginTop: '4px' }}>
                Vencimento em {new Date(faturaAberta.dataVencimento).toLocaleDateString('pt-BR')}
              </div>
            </div>

            {/* QR CODE PIX */}
            {faturaAberta.status === 'Pendente' && (
              <div>
                <div style={{ textAlign: 'center', marginBottom: '14px' }}>
                  <img
                    src={faturaAberta.pixQrCode}
                    alt="QR Code Pix"
                    style={{ width: '180px', height: '180px', margin: '0 auto', border: '1px solid var(--border)', borderRadius: '8px' }}
                  />
                  <div style={{ fontSize: '12px', color: 'var(--text-muted)', marginTop: '6px' }}>
                    Escaneie pelo aplicativo do seu banco
                  </div>
                </div>

                <div style={{ marginBottom: '16px' }}>
                  <label style={{ fontSize: '11px', fontWeight: 700, color: 'var(--text-muted)', textTransform: 'uppercase' }}>
                    Pix Copia e Cola
                  </label>
                  <div style={{ display: 'flex', gap: '8px', marginTop: '4px' }}>
                    <input
                      readOnly
                      value={faturaAberta.pixCopiaECola}
                      style={{
                        flex: 1,
                        padding: '8px',
                        fontSize: '11px',
                        borderRadius: '6px',
                        border: '1px solid var(--border)',
                        backgroundColor: '#F8FAFC'
                      }}
                    />
                    <button
                      onClick={() => copiarPix(faturaAberta.pixCopiaECola)}
                      style={{
                        backgroundColor: copiadoPix ? '#10B981' : 'var(--blue-primary)',
                        color: '#FFF',
                        border: 'none',
                        borderRadius: '6px',
                        padding: '0 12px',
                        cursor: 'pointer',
                        display: 'flex',
                        alignItems: 'center',
                        gap: '4px',
                        fontSize: '12px'
                      }}
                    >
                      {copiadoPix ? <CheckCircle2 size={14} /> : <Copy size={14} />}
                      {copiadoPix ? 'Copiado!' : 'Copiar'}
                    </button>
                  </div>
                </div>

                {/* BOTÃO SIMULAR PAGAMENTO */}
                <div style={{ display: 'flex', gap: '10px' }}>
                  <button
                    onClick={() => handlePagarFatura(faturaAberta.id, 'Pix')}
                    disabled={processando}
                    style={{
                      flex: 1,
                      backgroundColor: '#10B981',
                      color: '#FFF',
                      border: 'none',
                      padding: '10px',
                      borderRadius: '8px',
                      fontWeight: 700,
                      cursor: 'pointer'
                    }}
                  >
                    Confirmar Pagamento Simulado
                  </button>
                </div>
              </div>
            )}

            {faturaAberta.status === 'Paga' && (
              <div style={{ backgroundColor: '#DCFCE7', padding: '16px', borderRadius: '8px', textAlign: 'center', color: '#166534', fontWeight: 600, fontSize: '14px' }}>
                <CheckCircle2 size={24} style={{ margin: '0 auto 6px' }} />
                Esta fatura foi paga com sucesso em {faturaAberta.dataPagamento ? new Date(faturaAberta.dataPagamento).toLocaleDateString('pt-BR') : 'hoje'}.
              </div>
            )}
          </div>
        </div>
      )}

      {/* MODAL DE CANCELAMENTO */}
      {modalCancelar && (
        <div style={{ position: 'fixed', inset: 0, backgroundColor: 'rgba(0,0,0,0.5)', display: 'flex', alignItems: 'center', justifyContent: 'center', zIndex: 100 }}>
          <div style={{ backgroundColor: '#FFFFFF', borderRadius: '16px', width: '90%', maxWidth: '440px', padding: '24px' }}>
            <h3 style={{ fontSize: '18px', fontWeight: 800, color: '#DC2626', margin: '0 0 10px' }}>
              Confirmar Cancelamento de Assinatura
            </h3>
            <p style={{ color: 'var(--text-muted)', fontSize: '13px', marginBottom: '14px' }}>
              Ao cancelar, você continuará tendo acesso até o fim do período já faturado.
            </p>
            <textarea
              placeholder="Descreva o motivo do cancelamento..."
              rows={3}
              value={motivoCancelamento}
              onChange={e => setMotivoCancelamento(e.target.value)}
              style={{ width: '100%', padding: '10px', borderRadius: '8px', border: '1px solid var(--border)', fontSize: '13px', marginBottom: '16px' }}
            />
            <div style={{ display: 'flex', gap: '10px' }}>
              <button
                onClick={() => setModalCancelar(false)}
                style={{ flex: 1, padding: '10px', borderRadius: '8px', border: '1px solid var(--border)', backgroundColor: '#FFF', cursor: 'pointer' }}
              >
                Voltar
              </button>
              <button
                onClick={handleCancelarAssinatura}
                disabled={processando}
                style={{ flex: 1, padding: '10px', borderRadius: '8px', border: 'none', backgroundColor: '#DC2626', color: '#FFF', fontWeight: 700, cursor: 'pointer' }}
              >
                Confirmar Cancelamento
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
};

