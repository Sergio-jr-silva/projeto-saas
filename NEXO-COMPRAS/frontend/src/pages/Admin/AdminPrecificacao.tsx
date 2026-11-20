import React, { useState, useEffect } from 'react';
import { api } from '../../services/api';
import {
  DollarSign,
  Calculator,
  Building2,
  Sliders,
  History,
  CheckCircle2,
  AlertTriangle,
  Edit3,
  RefreshCw,
  Link2,
  ShieldCheck,
  Bell
} from 'lucide-react';

interface SimulacaoResponse {
  versaoTabela: number;
  valorBase: number;
  valorMinimo: number;
  valorMaximo: number;
  adicionalProdutos: number;
  faixaProdutos: string;
  adicionalFiliais: number;
  faixaFiliais: string;
  adicionalUsuarios: number;
  faixaUsuarios: string;
  adicionalSincronizadores: number;
  faixaSincronizadores: string;
  adicionalVolume: number;
  faixaVolume: string;
  adicionalModulos: number;
  faixaModulos: string;
  moduloCodigo: string;
  somaAdicionais: number;
  subtotalBruto: number;
  mensalidadeCalculada: number;
  desconto: number;
  ajusteManual: number;
  mensalidadeFinal: number;
  faixaComercial: string;
}

interface PrecificacaoTenant {
  tenantId: string;
  razaoSocial: string;
  nomeFantasia: string;
  slug: string;
  cnpj: string;
  status: string;
  produtosCadastrados: number;
  filiaisAtivas: number;
  usuariosAtivos: number;
  sincronizadoresAtivos: number;
  cotacoesNoMes: number;
  pedidosNoMes: number;
  sincronizacoesEfetivasNoMes: number;
  sincronizacoesSemAlteracaoIgnoradas: number;
  registrosProcessadosNoMes: number;
  volumeUtilizacao: string;
  volumeSugerido: string;
  moduloContratado: string;
  moduloDescricao: string;
  mensalidadeCalculada: number;
  desconto: number;
  ajusteManual: number;
  mensalidadeFinal: number;
  mensalidadeSimuladaAtual: number;
  faixaComercial: string;
  justificativaPreco: string;
  dataInicioCobranca: string;
  proximoVencimento: string;
  situacaoFinanceira: string;
  maxUsuariosContratado: number;
  maxFiliaisContratado: number;
  maxProdutosContratado: number;
  maxSincronizadoresContratado: number;
  detalheCalculo: SimulacaoResponse;
}

interface ConfigPrecificacao {
  id: string;
  versao: number;
  ativa: boolean;
  mensalidadeBase: number;
  valorMinimo: number;
  valorMaximo: number;
  adicProdFaixa1: number;
  adicProdFaixa2: number;
  adicProdFaixa3: number;
  adicProdFaixa4: number;
  adicFilialFaixa1: number;
  adicFilialFaixa2: number;
  adicFilialFaixa3: number;
  adicFilialFaixa4: number;
  adicUsuarioFaixa1: number;
  adicUsuarioFaixa2: number;
  adicUsuarioFaixa3: number;
  adicUsuarioFaixa4: number;
  adicSincFaixa1: number;
  adicSincFaixa2: number;
  adicSincFaixa3: number;
  adicVolumeBaixo: number;
  adicVolumeMedio: number;
  adicVolumeAlto: number;
  adicModuloBasico: number;
  adicModuloRelatorios: number;
  adicModuloCompleto: number;
  atualizadoPor: string;
  motivoAlteracao: string;
  dataCriacao: string;
}

interface HistoricoComercial {
  id: string;
  tenantId: string;
  tipoAlteracao: string;
  mensalidadeAnterior: number;
  mensalidadeCalculada: number;
  descontoAplicado: number;
  ajusteManual: number;
  mensalidadeFinal: number;
  moduloContratado: string;
  volumeUtilizacao: string;
  justificativa: string;
  alteradoPor: string;
  versaoTabelaPrecos: number;
  dataCriacao: string;
}

interface SolicitacaoPlano {
  id: string;
  tenantId: string;
  empresaNome: string;
  empresaSlug: string;
  solicitadoPorNome: string;
  moduloAtual: string;
  moduloSolicitado: string;
  observacoesCliente: string;
  status: string;
  respostaAdmin: string;
  dataCriacao: string;
}

const fmtCurrency = (v: number) =>
  new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' }).format(v || 0);

const parseCurrency = (val: string | number): number => {
  if (typeof val === 'number') return isNaN(val) ? 0 : val;
  if (!val) return 0;
  const cleaned = val.toString().replace(/[R$\s.]/g, '').replace(',', '.');
  const num = parseFloat(cleaned);
  return isNaN(num) ? 0 : num;
};

export const AdminPrecificacao: React.FC = () => {
  const [aba, setAba] = useState<'empresas' | 'simulador' | 'regras' | 'solicitacoes'>('empresas');
  const [tenants, setTenants] = useState<PrecificacaoTenant[]>([]);
  const [config, setConfig] = useState<ConfigPrecificacao | null>(null);
  const [versoes, setVersoes] = useState<ConfigPrecificacao[]>([]);
  const [solicitacoes, setSolicitacoes] = useState<SolicitacaoPlano[]>([]);
  const [loading, setLoading] = useState(true);

  // Estado do Simulador
  const [simReq, setSimReq] = useState({
    quantidadeProdutos: 800,
    quantidadeFiliais: 1,
    quantidadeUsuarios: 2,
    quantidadeSincronizadores: 1,
    volumeUtilizacao: 'Baixo',
    moduloContratado: 'Basico',
    desconto: 0,
    ajusteManual: 0
  });
  const [simResult, setSimResult] = useState<SimulacaoResponse | null>(null);

  // Modal de Edição Comercial da Empresa
  const [empresaEditando, setEmpresaEditando] = useState<PrecificacaoTenant | null>(null);
  const [editForm, setEditForm] = useState({
    slug: '',
    moduloContratado: 'Basico',
    volumeUtilizacao: 'Baixo',
    desconto: 0,
    ajusteManual: 0,
    mensalidadeFinal: 0,
    mensalidadeManualInput: '',
    justificativa: '',
    situacaoFinanceira: 'Regular',
    proximoVencimento: '',
    maxUsuariosContratado: 10,
    maxFiliaisContratado: 5
  });
  const [historicoEmpresa, setHistoricoEmpresa] = useState<HistoricoComercial[]>([]);
  const [salvandoEmpresa, setSalvandoEmpresa] = useState(false);

  // Edição de Regras Versionadas
  const [configForm, setConfigForm] = useState<ConfigPrecificacao | null>(null);
  const [motivoNovaVersao, setMotivoNovaVersao] = useState('');
  const [salvandoConfig, setSalvandoConfig] = useState(false);

  const carregarTudo = async () => {
    try {
      setLoading(true);
      const [resTenants, resCfg, resVersoes, resSol] = await Promise.all([
        api.get<PrecificacaoTenant[]>('/admin/precificacao/tenants'),
        api.get<ConfigPrecificacao>('/admin/precificacao/config'),
        api.get<ConfigPrecificacao[]>('/admin/precificacao/versoes'),
        api.get<SolicitacaoPlano[]>('/admin/precificacao/solicitacoes')
      ]);
      setTenants(resTenants);
      setConfig(resCfg);
      setConfigForm(resCfg);
      setVersoes(resVersoes);
      setSolicitacoes(resSol);
    } catch (err: any) {
      alert('Erro ao carregar módulo de precificação: ' + err.message);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    carregarTudo();
  }, []);

  const executarSimulacao = async () => {
    try {
      const res = await api.post<SimulacaoResponse>('/admin/precificacao/simular', simReq);
      setSimResult(res);
    } catch (err: any) {
      alert('Erro ao simular: ' + err.message);
    }
  };

  useEffect(() => {
    executarSimulacao();
  }, [simReq]);

  const abrirEdicaoEmpresa = async (t: PrecificacaoTenant) => {
    setEmpresaEditando(t);
    const mFinal = t.mensalidadeFinal > 0 ? t.mensalidadeFinal : t.mensalidadeCalculada;
    setEditForm({
      slug: t.slug || '',
      moduloContratado: t.moduloContratado || 'Basico',
      volumeUtilizacao: t.volumeUtilizacao || 'Baixo',
      desconto: t.desconto || 0,
      ajusteManual: t.ajusteManual || 0,
      mensalidadeFinal: mFinal,
      mensalidadeManualInput: mFinal.toLocaleString('pt-BR', { minimumFractionDigits: 2, maximumFractionDigits: 2 }),
      justificativa: t.justificativaPreco || 'Revisão comercial pelo Administrador Geral',
      situacaoFinanceira: t.situacaoFinanceira || 'Regular',
      proximoVencimento: t.proximoVencimento ? t.proximoVencimento.slice(0, 10) : '',
      maxUsuariosContratado: t.maxUsuariosContratado || 10,
      maxFiliaisContratado: t.maxFiliaisContratado || 5
    });
    try {
      const hist = await api.get<HistoricoComercial[]>(`/admin/precificacao/tenants/${t.tenantId}/historico`);
      setHistoricoEmpresa(hist);
    } catch {
      setHistoricoEmpresa([]);
    }
  };

  const salvarCondicoesEmpresa = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!empresaEditando) return;
    setSalvandoEmpresa(true);
    try {
      if (editForm.slug && editForm.slug !== empresaEditando.slug) {
        await api.put(`/admin/tenants/${empresaEditando.tenantId}/slug`, {
          slug: editForm.slug,
          justificativa: editForm.justificativa
        });
      }

      await api.put(`/admin/precificacao/tenants/${empresaEditando.tenantId}`, {
        moduloContratado: editForm.moduloContratado,
        volumeUtilizacao: editForm.volumeUtilizacao,
        desconto: Number(editForm.desconto),
        ajusteManual: Number(editForm.ajusteManual),
        mensalidadeManual: Number(editForm.mensalidadeFinal || 0),
        justificativa: editForm.justificativa,
        situacaoFinanceira: editForm.situacaoFinanceira,
        proximoVencimento: editForm.proximoVencimento ? new Date(editForm.proximoVencimento).toISOString() : null,
        maxUsuariosContratado: Number(editForm.maxUsuariosContratado),
        maxFiliaisContratado: Number(editForm.maxFiliaisContratado)
      });

      alert('Condições comerciais e mensalidade atualizadas com sucesso!');
      setEmpresaEditando(null);
      await carregarTudo();
    } catch (err: any) {
      alert('Erro ao salvar condições comerciais: ' + err.message);
    } finally {
      setSalvandoEmpresa(false);
    }
  };

  const salvarNovaVersaoRegras = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!configForm) return;
    setSalvandoConfig(true);
    try {
      await api.put('/admin/precificacao/config', {
        ...configForm,
        motivoAlteracao: motivoNovaVersao || 'Atualização de tabela de preços pelo Administrador Geral'
      });
      alert('Nova versão da tabela de precificação publicada com sucesso!');
      setMotivoNovaVersao('');
      await carregarTudo();
    } catch (err: any) {
      alert('Erro ao atualizar critérios: ' + err.message);
    } finally {
      setSalvandoConfig(false);
    }
  };

  const responderSolicitacao = async (id: string, aprovar: boolean) => {
    const resp = prompt(aprovar ? 'Observação de aprovação para o cliente:' : 'Motivo da recusa:');
    if (resp === null) return;
    try {
      await api.post(`/admin/precificacao/solicitacoes/${id}/responder`, {
        aprovar,
        respostaAdmin: resp
      });
      await carregarTudo();
    } catch (err: any) {
      alert('Erro ao responder solicitação: ' + err.message);
    }
  };

  if (loading) {
    return <div style={{ padding: '40px', textAlign: 'center' }}>Carregando módulo de precificação dinâmica SaaS...</div>;
  }

  const mrrTotal = tenants.filter(t => t.status === 'Ativo').reduce((acc, t) => {
    if (empresaEditando && t.tenantId === empresaEditando.tenantId) {
      return acc + (editForm.mensalidadeFinal || 0);
    }
    return acc + (t.mensalidadeFinal || 0);
  }, 0);

  return (
    <div>
      {/* CABEÇALHO */}
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', marginBottom: '24px', flexWrap: 'wrap', gap: '16px' }}>
        <div>
          <h1 style={{ fontSize: '24px', fontWeight: 800, color: 'var(--text-main)' }}>
            Módulo Comercial & Precificação Flexível SaaS
          </h1>
          <p style={{ fontSize: '14px', color: 'var(--text-muted)' }}>
            Gestão de mensalidades por regras ou valor manual negociado, URLs por cliente (/slug), descontos justificados e regras versionadas.
          </p>
        </div>
        <div style={{ display: 'flex', gap: '10px', alignItems: 'center' }}>
          <div className="badge badge-green" style={{ fontSize: '13.5px', padding: '8px 14px' }}>
            MRR Ativo: <strong>{fmtCurrency(mrrTotal)}/mês</strong>
          </div>
          <div className="badge badge-blue" style={{ fontSize: '13px', padding: '8px 14px' }}>
            Tabela Ativa: <strong>v{config?.versao ?? 1}</strong>
          </div>
        </div>
      </div>

      {/* NAVEGAÇÃO DE ABAS */}
      <div style={{ display: 'flex', gap: '8px', marginBottom: '22px', borderBottom: '1px solid var(--border)', paddingBottom: '10px', flexWrap: 'wrap' }}>
        <button
          onClick={() => setAba('empresas')}
          className={`btn ${aba === 'empresas' ? 'btn-primary' : 'btn-secondary'}`}
        >
          <Building2 size={16} /> Empresas, Slugs e Mensalidades ({tenants.length})
        </button>
        <button
          onClick={() => setAba('simulador')}
          className={`btn ${aba === 'simulador' ? 'btn-primary' : 'btn-secondary'}`}
        >
          <Calculator size={16} /> Simulador Comercial (R$ 50 – R$ 250)
        </button>
        <button
          onClick={() => setAba('regras')}
          className={`btn ${aba === 'regras' ? 'btn-primary' : 'btn-secondary'}`}
        >
          <Sliders size={16} /> Critérios e Faixas Versionadas (v{config?.versao})
        </button>
        <button
          onClick={() => setAba('solicitacoes')}
          className={`btn ${aba === 'solicitacoes' ? 'btn-primary' : 'btn-secondary'}`}
        >
          <Bell size={16} /> Solicitações de Clientes ({solicitacoes.filter(s => s.status === 'Pendente').length})
        </button>
      </div>

      {/* ABA 1: TABELA DE EMPRESAS E PRECIFICAÇÃO */}
      {aba === 'empresas' && (
        <div className="card" style={{ padding: 0, overflow: 'hidden' }}>
          <div style={{ padding: '18px 22px', borderBottom: '1px solid var(--border)', display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
            <div>
              <h3 style={{ fontSize: '16px', fontWeight: 700 }}>Empresas Clientes — Consumo Real & Mensalidade Dinâmica</h3>
              <p style={{ fontSize: '12.5px', color: 'var(--text-muted)', marginTop: '2px' }}>
                Sincronizações sem alterações no ERP são automaticamente ignoradas no cálculo de volume para não penalizar o cliente.
              </p>
            </div>
            <button onClick={carregarTudo} className="btn btn-secondary" style={{ fontSize: '12.5px' }}>
              <RefreshCw size={14} /> Atualizar Consumo
            </button>
          </div>

          <div style={{ overflowX: 'auto' }}>
            <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: '13px' }}>
              <thead>
                <tr style={{ backgroundColor: '#F8FAFC', textAlign: 'left', borderBottom: '1px solid var(--border)' }}>
                  <th style={{ padding: '12px 14px' }}>Empresa / Slug</th>
                  <th style={{ padding: '12px 10px' }}>Produtos</th>
                  <th style={{ padding: '12px 10px' }}>Filiais</th>
                  <th style={{ padding: '12px 10px' }}>Usuários</th>
                  <th style={{ padding: '12px 10px' }}>SINc</th>
                  <th style={{ padding: '12px 10px' }}>Volume Mensal</th>
                  <th style={{ padding: '12px 10px' }}>Módulos</th>
                  <th style={{ padding: '12px 10px' }}>Calculada</th>
                  <th style={{ padding: '12px 10px' }}>Desc. / Ajuste</th>
                  <th style={{ padding: '12px 10px' }}>Mensalidade Final</th>
                  <th style={{ padding: '12px 10px' }}>Próx. Vencimento</th>
                  <th style={{ padding: '12px 10px' }}>Situação</th>
                  <th style={{ padding: '12px 14px', textAlign: 'right' }}>Ações</th>
                </tr>
              </thead>
              <tbody>
                {tenants.map((t) => (
                  <tr key={t.tenantId} style={{ borderBottom: '1px solid var(--border-light)' }}>
                    <td style={{ padding: '12px 14px' }}>
                      <div style={{ fontWeight: 700, color: 'var(--text-main)' }}>{t.nomeFantasia}</div>
                      <div style={{ fontSize: '11.5px', color: 'var(--blue-primary)', fontFamily: 'monospace', fontWeight: 600 }}>
                        /{t.slug}
                      </div>
                    </td>
                    <td style={{ padding: '12px 10px', fontWeight: 600 }}>{t.produtosCadastrados.toLocaleString('pt-BR')}</td>
                    <td style={{ padding: '12px 10px' }}>{t.filiaisAtivas}</td>
                    <td style={{ padding: '12px 10px' }}>{t.usuariosAtivos}</td>
                    <td style={{ padding: '12px 10px' }}>{t.sincronizadoresAtivos}</td>
                    <td style={{ padding: '12px 10px' }}>
                      <span className="badge badge-blue">{t.volumeUtilizacao}</span>
                      <div style={{ fontSize: '11px', color: 'var(--text-muted)', marginTop: '2px' }}>
                        {t.cotacoesNoMes} cot. • {t.pedidosNoMes} ped. • {t.sincronizacoesEfetivasNoMes} syncs
                      </div>
                    </td>
                    <td style={{ padding: '12px 10px', fontSize: '12px' }}>
                      <div style={{ fontWeight: 600 }}>{t.moduloContratado}</div>
                      <div style={{ fontSize: '11px', color: 'var(--text-muted)' }}>{t.faixaComercial}</div>
                    </td>
                    <td style={{ padding: '12px 10px', fontWeight: 600 }}>
                      {fmtCurrency(t.mensalidadeCalculada)}
                    </td>
                    <td style={{ padding: '12px 10px', fontSize: '12px' }}>
                      {t.desconto > 0 && <div style={{ color: '#16A34A' }}>-{fmtCurrency(t.desconto)}</div>}
                      {t.ajusteManual !== 0 && (
                        <div style={{ color: t.ajusteManual > 0 ? '#D97706' : '#16A34A' }}>
                          {t.ajusteManual > 0 ? '+' : ''}{fmtCurrency(t.ajusteManual)}
                        </div>
                      )}
                      {t.desconto === 0 && t.ajusteManual === 0 && <span style={{ color: 'var(--text-muted)' }}>—</span>}
                    </td>
                    <td style={{ padding: '12px 10px' }}>
                      <span style={{ fontWeight: 800, fontSize: '14.5px', color: 'var(--blue-primary)' }}>
                        {fmtCurrency(t.mensalidadeFinal)}
                      </span>
                    </td>
                    <td style={{ padding: '12px 10px', fontSize: '12px' }}>
                      {new Date(t.proximoVencimento).toLocaleDateString('pt-BR')}
                    </td>
                    <td style={{ padding: '12px 10px' }}>
                      <span className={`badge ${t.situacaoFinanceira === 'Regular' ? 'badge-green' : t.situacaoFinanceira === 'Inadimplente' ? 'badge-red' : 'badge-yellow'}`}>
                        {t.situacaoFinanceira}
                      </span>
                    </td>
                    <td style={{ padding: '12px 14px', textAlign: 'right' }}>
                      <button
                        onClick={() => abrirEdicaoEmpresa(t)}
                        className="btn btn-secondary"
                        style={{ padding: '5px 10px', fontSize: '12px' }}
                      >
                        <Edit3 size={13} /> Gerenciar Preço & Slug
                      </button>
                    </td>
                  </tr>
                ))}
                {tenants.length === 0 && (
                  <tr>
                    <td colSpan={13} style={{ padding: '32px', textAlign: 'center', color: 'var(--text-muted)' }}>
                      Nenhuma empresa cadastrada no momento.
                    </td>
                  </tr>
                )}
              </tbody>
            </table>
          </div>
        </div>
      )}

      {/* ABA 2: SIMULADOR COMERCIAL */}
      {aba === 'simulador' && (
        <div className="grid-cols-2" style={{ gap: '20px' }}>
          <div className="card">
            <h3 style={{ fontSize: '17px', fontWeight: 700, marginBottom: '6px' }}>
              Simulador de Mensalidade Dinâmica
            </h3>
            <p style={{ fontSize: '13px', color: 'var(--text-muted)', marginBottom: '18px' }}>
              Simule o valor exato da assinatura mensal antes de cadastrar ou alterar o contrato de um cliente.
            </p>

            <div className="grid-cols-2" style={{ gap: '12px' }}>
              <div className="form-group">
                <label>Quantidade de Produtos</label>
                <input
                  type="number"
                  min={0}
                  value={simReq.quantidadeProdutos}
                  onChange={(e) => setSimReq({ ...simReq, quantidadeProdutos: Number(e.target.value) })}
                />
              </div>
              <div className="form-group">
                <label>Quantidade de Filiais</label>
                <input
                  type="number"
                  min={1}
                  value={simReq.quantidadeFiliais}
                  onChange={(e) => setSimReq({ ...simReq, quantidadeFiliais: Number(e.target.value) })}
                />
              </div>
              <div className="form-group">
                <label>Usuários Ativos</label>
                <input
                  type="number"
                  min={1}
                  value={simReq.quantidadeUsuarios}
                  onChange={(e) => setSimReq({ ...simReq, quantidadeUsuarios: Number(e.target.value) })}
                />
              </div>
              <div className="form-group">
                <label>Sincronizadores SINc</label>
                <input
                  type="number"
                  min={1}
                  value={simReq.quantidadeSincronizadores}
                  onChange={(e) => setSimReq({ ...simReq, quantidadeSincronizadores: Number(e.target.value) })}
                />
              </div>
              <div className="form-group">
                <label>Volume de Utilização</label>
                <select
                  value={simReq.volumeUtilizacao}
                  onChange={(e) => setSimReq({ ...simReq, volumeUtilizacao: e.target.value })}
                >
                  <option value="Baixo">Baixo (+R$ 0,00)</option>
                  <option value="Medio">Médio (+R$ 10,00)</option>
                  <option value="Alto">Alto (+R$ 25,00)</option>
                </select>
              </div>
              <div className="form-group">
                <label>Módulos Contratados</label>
                <select
                  value={simReq.moduloContratado}
                  onChange={(e) => setSimReq({ ...simReq, moduloContratado: e.target.value })}
                >
                  <option value="Basico">Compras e cotações básicas (+R$ 0,00)</option>
                  <option value="RelatoriosAvancados">Compras e relatórios avançados (+R$ 15,00)</option>
                  <option value="CompletoServicos">Compras, relatórios e serviços (+R$ 30,00)</option>
                </select>
              </div>
              <div className="form-group">
                <label>Desconto Autorizado (R$)</label>
                <input
                  type="number"
                  min={0}
                  step="0.01"
                  value={simReq.desconto}
                  onChange={(e) => setSimReq({ ...simReq, desconto: Number(e.target.value) })}
                />
              </div>
              <div className="form-group">
                <label>Ajuste Manual (R$)</label>
                <input
                  type="number"
                  step="0.01"
                  value={simReq.ajusteManual}
                  onChange={(e) => setSimReq({ ...simReq, ajusteManual: Number(e.target.value) })}
                />
              </div>
            </div>
          </div>

          {/* RESULTADO DA SIMULAÇÃO */}
          {simResult && (
            <div className="card" style={{ backgroundColor: '#F8FAFC', border: '2px solid #BFDBFE' }}>
              <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '16px' }}>
                <span className="badge badge-blue">Faixa Comercial: {simResult.faixaComercial}</span>
                <span style={{ fontSize: '12px', color: 'var(--text-muted)' }}>Tabela v{simResult.versaoTabela}</span>
              </div>

              <div style={{ fontSize: '13.5px', lineHeight: 2 }}>
                <div style={{ display: 'flex', justifyContent: 'space-between', borderBottom: '1px dashed var(--border)' }}>
                  <span>Mensalidade-Base Inicial:</span>
                  <strong>{fmtCurrency(simResult.valorBase)}</strong>
                </div>
                <div style={{ display: 'flex', justifyContent: 'space-between', borderBottom: '1px dashed var(--border)' }}>
                  <span>Produtos ({simResult.faixaProdutos}):</span>
                  <strong>+{fmtCurrency(simResult.adicionalProdutos)}</strong>
                </div>
                <div style={{ display: 'flex', justifyContent: 'space-between', borderBottom: '1px dashed var(--border)' }}>
                  <span>Filiais ({simResult.faixaFiliais}):</span>
                  <strong>+{fmtCurrency(simResult.adicionalFiliais)}</strong>
                </div>
                <div style={{ display: 'flex', justifyContent: 'space-between', borderBottom: '1px dashed var(--border)' }}>
                  <span>Usuários ({simResult.faixaUsuarios}):</span>
                  <strong>+{fmtCurrency(simResult.adicionalUsuarios)}</strong>
                </div>
                <div style={{ display: 'flex', justifyContent: 'space-between', borderBottom: '1px dashed var(--border)' }}>
                  <span>Sincronizadores ({simResult.faixaSincronizadores}):</span>
                  <strong>+{fmtCurrency(simResult.adicionalSincronizadores)}</strong>
                </div>
                <div style={{ display: 'flex', justifyContent: 'space-between', borderBottom: '1px dashed var(--border)' }}>
                  <span>Volume ({simResult.faixaVolume}):</span>
                  <strong>+{fmtCurrency(simResult.adicionalVolume)}</strong>
                </div>
                <div style={{ display: 'flex', justifyContent: 'space-between', borderBottom: '1px dashed var(--border)' }}>
                  <span>Módulos ({simResult.faixaModulos}):</span>
                  <strong>+{fmtCurrency(simResult.adicionalModulos)}</strong>
                </div>
                <div style={{ display: 'flex', justifyContent: 'space-between', marginTop: '8px', color: 'var(--text-muted)' }}>
                  <span>Mensalidade Calculada (Teto R$ 250 / Piso R$ 50):</span>
                  <strong>{fmtCurrency(simResult.mensalidadeCalculada)}</strong>
                </div>
                {simResult.desconto > 0 && (
                  <div style={{ display: 'flex', justifyContent: 'space-between', color: '#16A34A' }}>
                    <span>Desconto Comercial Aplicado:</span>
                    <strong>-{fmtCurrency(simResult.desconto)}</strong>
                  </div>
                )}
                {simResult.ajusteManual !== 0 && (
                  <div style={{ display: 'flex', justifyContent: 'space-between', color: '#D97706' }}>
                    <span>Ajuste Manual:</span>
                    <strong>{simResult.ajusteManual > 0 ? '+' : ''}{fmtCurrency(simResult.ajusteManual)}</strong>
                  </div>
                )}
              </div>

              <div style={{
                marginTop: '18px',
                padding: '16px',
                backgroundColor: '#FFFFFF',
                borderRadius: 'var(--radius-md)',
                border: '1px solid #93C5FD',
                display: 'flex',
                justifyContent: 'space-between',
                alignItems: 'center'
              }}>
                <div>
                  <div style={{ fontSize: '12px', fontWeight: 700, color: 'var(--text-muted)', textTransform: 'uppercase' }}>
                    MENSALIDADE FINAL
                  </div>
                  <div style={{ fontSize: '11.5px', color: 'var(--text-muted)' }}>
                    Limite garantido entre {fmtCurrency(simResult.valorMinimo)} e {fmtCurrency(simResult.valorMaximo)}
                  </div>
                </div>
                <div style={{ fontSize: '28px', fontWeight: 800, color: 'var(--blue-primary)' }}>
                  {fmtCurrency(simResult.mensalidadeFinal)}/mês
                </div>
              </div>
            </div>
          )}
        </div>
      )}

      {/* ABA 3: REGRAS E FAIXAS VERSIONADAS */}
      {aba === 'regras' && configForm && (
        <div className="grid-cols-2" style={{ gap: '20px' }}>
          <form onSubmit={salvarNovaVersaoRegras} className="card">
            <h3 style={{ fontSize: '17px', fontWeight: 700, marginBottom: '6px' }}>
              Configuração da Tabela de Preços (Versão Atual: v{config?.versao})
            </h3>
            <p style={{ fontSize: '12.5px', color: 'var(--text-muted)', marginBottom: '16px' }}>
              Qualquer alteração gera uma nova versão auditada no banco de dados.
            </p>

            <div className="grid-cols-3" style={{ gap: '10px', marginBottom: '12px' }}>
              <div className="form-group">
                <label>Valor-Base (R$)</label>
                <input type="number" step="0.01" value={configForm.mensalidadeBase} onChange={(e) => setConfigForm({ ...configForm, mensalidadeBase: Number(e.target.value) })} />
              </div>
              <div className="form-group">
                <label>Mínimo (R$)</label>
                <input type="number" step="0.01" value={configForm.valorMinimo} onChange={(e) => setConfigForm({ ...configForm, valorMinimo: Number(e.target.value) })} />
              </div>
              <div className="form-group">
                <label>Máximo (R$)</label>
                <input type="number" step="0.01" value={configForm.valorMaximo} onChange={(e) => setConfigForm({ ...configForm, valorMaximo: Number(e.target.value) })} />
              </div>
            </div>

            <h4 style={{ fontSize: '13px', fontWeight: 700, margin: '12px 0 8px' }}>Adicionais por Faixa de Produtos (R$)</h4>
            <div className="grid-cols-4" style={{ gap: '8px' }}>
              <div className="form-group"><label>Até 1.000</label><input type="number" step="0.01" value={configForm.adicProdFaixa1} onChange={(e) => setConfigForm({ ...configForm, adicProdFaixa1: Number(e.target.value) })} /></div>
              <div className="form-group"><label>1.001–3.000</label><input type="number" step="0.01" value={configForm.adicProdFaixa2} onChange={(e) => setConfigForm({ ...configForm, adicProdFaixa2: Number(e.target.value) })} /></div>
              <div className="form-group"><label>3.001–8.000</label><input type="number" step="0.01" value={configForm.adicProdFaixa3} onChange={(e) => setConfigForm({ ...configForm, adicProdFaixa3: Number(e.target.value) })} /></div>
              <div className="form-group"><label>Acima 8.000</label><input type="number" step="0.01" value={configForm.adicProdFaixa4} onChange={(e) => setConfigForm({ ...configForm, adicProdFaixa4: Number(e.target.value) })} /></div>
            </div>

            <h4 style={{ fontSize: '13px', fontWeight: 700, margin: '12px 0 8px' }}>Adicionais por Filiais (R$)</h4>
            <div className="grid-cols-4" style={{ gap: '8px' }}>
              <div className="form-group"><label>1 filial</label><input type="number" step="0.01" value={configForm.adicFilialFaixa1} onChange={(e) => setConfigForm({ ...configForm, adicFilialFaixa1: Number(e.target.value) })} /></div>
              <div className="form-group"><label>2 a 3</label><input type="number" step="0.01" value={configForm.adicFilialFaixa2} onChange={(e) => setConfigForm({ ...configForm, adicFilialFaixa2: Number(e.target.value) })} /></div>
              <div className="form-group"><label>4 a 6</label><input type="number" step="0.01" value={configForm.adicFilialFaixa3} onChange={(e) => setConfigForm({ ...configForm, adicFilialFaixa3: Number(e.target.value) })} /></div>
              <div className="form-group"><label>Acima de 6</label><input type="number" step="0.01" value={configForm.adicFilialFaixa4} onChange={(e) => setConfigForm({ ...configForm, adicFilialFaixa4: Number(e.target.value) })} /></div>
            </div>

            <h4 style={{ fontSize: '13px', fontWeight: 700, margin: '12px 0 8px' }}>Adicionais por Usuários (R$)</h4>
            <div className="grid-cols-4" style={{ gap: '8px' }}>
              <div className="form-group"><label>1 a 2</label><input type="number" step="0.01" value={configForm.adicUsuarioFaixa1} onChange={(e) => setConfigForm({ ...configForm, adicUsuarioFaixa1: Number(e.target.value) })} /></div>
              <div className="form-group"><label>3 a 5</label><input type="number" step="0.01" value={configForm.adicUsuarioFaixa2} onChange={(e) => setConfigForm({ ...configForm, adicUsuarioFaixa2: Number(e.target.value) })} /></div>
              <div className="form-group"><label>6 a 10</label><input type="number" step="0.01" value={configForm.adicUsuarioFaixa3} onChange={(e) => setConfigForm({ ...configForm, adicUsuarioFaixa3: Number(e.target.value) })} /></div>
              <div className="form-group"><label>Acima de 10</label><input type="number" step="0.01" value={configForm.adicUsuarioFaixa4} onChange={(e) => setConfigForm({ ...configForm, adicUsuarioFaixa4: Number(e.target.value) })} /></div>
            </div>

            <h4 style={{ fontSize: '13px', fontWeight: 700, margin: '12px 0 8px' }}>Módulos Contratados (R$)</h4>
            <div className="grid-cols-3" style={{ gap: '8px' }}>
              <div className="form-group"><label>Básico</label><input type="number" step="0.01" value={configForm.adicModuloBasico} onChange={(e) => setConfigForm({ ...configForm, adicModuloBasico: Number(e.target.value) })} /></div>
              <div className="form-group"><label>Relatórios Avanç.</label><input type="number" step="0.01" value={configForm.adicModuloRelatorios} onChange={(e) => setConfigForm({ ...configForm, adicModuloRelatorios: Number(e.target.value) })} /></div>
              <div className="form-group"><label>Completo + Serv.</label><input type="number" step="0.01" value={configForm.adicModuloCompleto} onChange={(e) => setConfigForm({ ...configForm, adicModuloCompleto: Number(e.target.value) })} /></div>
            </div>

            <div className="form-group" style={{ marginTop: '12px' }}>
              <label>Motivo da Nova Versão</label>
              <input
                type="text"
                required
                placeholder="Ex: Reajuste comercial ou ajuste de faixa"
                value={motivoNovaVersao}
                onChange={(e) => setMotivoNovaVersao(e.target.value)}
              />
            </div>

            <button type="submit" disabled={salvandoConfig} className="btn btn-primary" style={{ width: '100%', marginTop: '10px' }}>
              {salvandoConfig ? 'Publicando nova versão...' : 'Salvar e Publicar Nova Versão'}
            </button>
          </form>

          <div className="card">
            <h3 style={{ fontSize: '16px', fontWeight: 700, marginBottom: '12px' }}>
              <History size={16} style={{ marginRight: '6px' }} />
              Histórico de Versões da Tabela de Preços
            </h3>
            {versoes.map((v) => (
              <div key={v.id} style={{ padding: '12px', borderBottom: '1px solid var(--border-light)', fontSize: '13px' }}>
                <div style={{ display: 'flex', justifyContent: 'space-between', fontWeight: 700 }}>
                  <span>Versão v{v.versao} {v.ativa && <span className="badge badge-green">Ativa</span>}</span>
                  <span>Base: {fmtCurrency(v.mensalidadeBase)} ({fmtCurrency(v.valorMinimo)} – {fmtCurrency(v.valorMaximo)})</span>
                </div>
                <div style={{ fontSize: '12px', color: 'var(--text-muted)', marginTop: '4px' }}>
                  {v.motivoAlteracao} • Por {v.atualizadoPor} em {new Date(v.dataCriacao).toLocaleString('pt-BR')}
                </div>
              </div>
            ))}
          </div>
        </div>
      )}

      {/* ABA 4: SOLICITAÇÕES DE CLIENTES */}
      {aba === 'solicitacoes' && (
        <div className="card">
          <h3 style={{ fontSize: '16px', fontWeight: 700, marginBottom: '14px' }}>
            Solicitações de Alteração de Plano / Módulos enviadas pelos Clientes
          </h3>
          {solicitacoes.length === 0 ? (
            <p style={{ color: 'var(--text-muted)', fontSize: '13.5px' }}>Nenhuma solicitação registrada.</p>
          ) : (
            <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: '13px' }}>
              <thead>
                <tr style={{ backgroundColor: '#F8FAFC', textAlign: 'left', borderBottom: '1px solid var(--border)' }}>
                  <th style={{ padding: '10px' }}>Empresa</th>
                  <th style={{ padding: '10px' }}>Solicitante</th>
                  <th style={{ padding: '10px' }}>Módulo Atual → Solicitado</th>
                  <th style={{ padding: '10px' }}>Observações</th>
                  <th style={{ padding: '10px' }}>Status</th>
                  <th style={{ padding: '10px', textAlign: 'right' }}>Decisão Admin Geral</th>
                </tr>
              </thead>
              <tbody>
                {solicitacoes.map((s) => (
                  <tr key={s.id} style={{ borderBottom: '1px solid var(--border-light)' }}>
                    <td style={{ padding: '10px', fontWeight: 700 }}>{s.empresaNome} (/{s.empresaSlug})</td>
                    <td style={{ padding: '10px' }}>{s.solicitadoPorNome}</td>
                    <td style={{ padding: '10px' }}>{s.moduloAtual} → <strong>{s.moduloSolicitado}</strong></td>
                    <td style={{ padding: '10px' }}>{s.observacoesCliente || '—'}</td>
                    <td style={{ padding: '10px' }}>
                      <span className={`badge ${s.status === 'Aprovada' ? 'badge-green' : s.status === 'Recusada' ? 'badge-red' : 'badge-yellow'}`}>
                        {s.status}
                      </span>
                    </td>
                    <td style={{ padding: '10px', textAlign: 'right' }}>
                      {s.status === 'Pendente' && (
                        <div style={{ display: 'flex', gap: '6px', justifyContent: 'flex-end' }}>
                          <button onClick={() => responderSolicitacao(s.id, true)} className="btn btn-primary" style={{ padding: '4px 10px', fontSize: '12px' }}>
                            Aprovar
                          </button>
                          <button onClick={() => responderSolicitacao(s.id, false)} className="btn btn-secondary" style={{ padding: '4px 10px', fontSize: '12px' }}>
                            Recusar
                          </button>
                        </div>
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </div>
      )}

      {/* MODAL DE EDIÇÃO COMERCIAL E HISTÓRICO DA EMPRESA */}
      {empresaEditando && (
        <div style={{
          position: 'fixed',
          inset: 0,
          backgroundColor: 'rgba(15, 23, 42, 0.55)',
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'center',
          zIndex: 1000,
          padding: '20px'
        }}>
          <div className="card" style={{ maxWidth: '820px', width: '100%', maxHeight: '90vh', overflowY: 'auto' }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '16px' }}>
              <div>
                <h3 style={{ fontSize: '18px', fontWeight: 800 }}>
                  Configuração Comercial: {empresaEditando.nomeFantasia}
                </h3>
                <div style={{ fontSize: '12.5px', color: 'var(--text-muted)' }}>
                  URL Oficial: <strong>https://nexoscompras.com.br/{editForm.slug}</strong> • Calculada pelas regras: <strong>{fmtCurrency(empresaEditando.mensalidadeCalculada)}</strong>
                </div>
                <div style={{ fontSize: '13px', color: 'var(--green)', fontWeight: 700, marginTop: '4px' }}>
                  Total MRR da Plataforma (recalculado ao vivo): {fmtCurrency(mrrTotal)}/mês
                </div>
              </div>
              <button onClick={() => setEmpresaEditando(null)} className="btn btn-secondary">Fechar</button>
            </div>

            <form onSubmit={salvarCondicoesEmpresa}>
              <div className="grid-cols-3" style={{ gap: '12px' }}>
                <div className="form-group" style={{ gridColumn: 'span 3' }}>
                  <label style={{ fontWeight: 700, color: 'var(--blue-primary)', display: 'flex', justifyContent: 'space-between' }}>
                    <span>Valor da Mensalidade Negociada (R$)</span>
                    <span style={{ color: 'var(--green)', fontSize: '13px' }}>
                      Mensalidade final a cobrar: {fmtCurrency(editForm.mensalidadeFinal || 0)}
                    </span>
                  </label>
                  <div style={{ position: 'relative' }}>
                    <span style={{ position: 'absolute', left: '12px', top: '10px', fontWeight: 700, color: 'var(--text-muted)' }}>R$</span>
                    <input
                      type="text"
                      style={{ paddingLeft: '38px', fontSize: '16px', fontWeight: 800, color: 'var(--blue-primary)' }}
                      placeholder="Ex: 100,00 • 1.000,00 • 1.250,50 • 2.500,00"
                      value={editForm.mensalidadeManualInput}
                      onChange={(e) => {
                        const raw = e.target.value;
                        const parsed = parseCurrency(raw);
                        setEditForm({
                          ...editForm,
                          mensalidadeManualInput: raw,
                          mensalidadeFinal: parsed
                        });
                      }}
                    />
                  </div>
                  <small style={{ color: 'var(--text-muted)', fontSize: '11.5px', marginTop: '3px', display: 'block' }}>
                    Aceita qualquer valor monetário (sem teto). O valor informado é salvo diretamente no banco, atualiza a assinatura da empresa e recalcula o MRR ao vivo.
                  </small>
                </div>
                <div className="form-group">
                  <label>Slug da URL (/slug)</label>
                  <input
                    type="text"
                    required
                    value={editForm.slug}
                    onChange={(e) => setEditForm({ ...editForm, slug: e.target.value })}
                  />
                </div>
                <div className="form-group">
                  <label>Módulos Contratados</label>
                  <select
                    value={editForm.moduloContratado}
                    onChange={(e) => setEditForm({ ...editForm, moduloContratado: e.target.value })}
                  >
                    <option value="Basico">Compras e cotações básicas (+R$ 0)</option>
                    <option value="RelatoriosAvancados">Compras e relatórios avançados (+R$ 15)</option>
                    <option value="CompletoServicos">Compras, relatórios e serviços (+R$ 30)</option>
                  </select>
                </div>
                <div className="form-group">
                  <label>Volume de Utilização</label>
                  <select
                    value={editForm.volumeUtilizacao}
                    onChange={(e) => setEditForm({ ...editForm, volumeUtilizacao: e.target.value })}
                  >
                    <option value="Baixo">Baixo (+R$ 0)</option>
                    <option value="Medio">Médio (+R$ 10)</option>
                    <option value="Alto">Alto (+R$ 25)</option>
                  </select>
                </div>
                <div className="form-group">
                  <label>Desconto Comercial (R$)</label>
                  <input
                    type="number"
                    min={0}
                    step="0.01"
                    value={editForm.desconto}
                    onChange={(e) => setEditForm({ ...editForm, desconto: Number(e.target.value) })}
                  />
                </div>
                <div className="form-group">
                  <label>Ajuste Manual (R$)</label>
                  <input
                    type="number"
                    step="0.01"
                    value={editForm.ajusteManual}
                    onChange={(e) => setEditForm({ ...editForm, ajusteManual: Number(e.target.value) })}
                  />
                </div>
                <div className="form-group">
                  <label>Situação Financeira</label>
                  <select
                    value={editForm.situacaoFinanceira}
                    onChange={(e) => setEditForm({ ...editForm, situacaoFinanceira: e.target.value })}
                  >
                    <option value="Regular">Regular</option>
                    <option value="Pendente">Pendente</option>
                    <option value="Inadimplente">Inadimplente</option>
                    <option value="Isento">Isento</option>
                  </select>
                </div>
                <div className="form-group">
                  <label>Próximo Vencimento</label>
                  <input
                    type="date"
                    value={editForm.proximoVencimento}
                    onChange={(e) => setEditForm({ ...editForm, proximoVencimento: e.target.value })}
                  />
                </div>
                <div className="form-group">
                  <label>Limite Máx. Usuários</label>
                  <input
                    type="number"
                    min={1}
                    value={editForm.maxUsuariosContratado}
                    onChange={(e) => setEditForm({ ...editForm, maxUsuariosContratado: Number(e.target.value) })}
                  />
                </div>
                <div className="form-group">
                  <label>Limite Máx. Filiais</label>
                  <input
                    type="number"
                    min={1}
                    value={editForm.maxFiliaisContratado}
                    onChange={(e) => setEditForm({ ...editForm, maxFiliaisContratado: Number(e.target.value) })}
                  />
                </div>
              </div>

              <div className="form-group">
                <label>Justificativa Comercial Obrigatória (Auditada)</label>
                <input
                  type="text"
                  required
                  placeholder="Informe o motivo da atualização comercial, desconto ou ajuste"
                  value={editForm.justificativa}
                  onChange={(e) => setEditForm({ ...editForm, justificativa: e.target.value })}
                />
              </div>

              <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '10px', marginTop: '12px' }}>
                <button type="button" onClick={() => setEmpresaEditando(null)} className="btn btn-secondary">
                  Cancelar
                </button>
                <button type="submit" disabled={salvandoEmpresa} className="btn btn-primary">
                  {salvandoEmpresa ? 'Salvando...' : 'Aplicar Mensalidade & Registrar Histórico'}
                </button>
              </div>
            </form>

            {/* HISTÓRICO COMERCIAL DA EMPRESA */}
            <div style={{ marginTop: '24px', borderTop: '1px solid var(--border)', paddingTop: '16px' }}>
              <h4 style={{ fontSize: '14px', fontWeight: 700, marginBottom: '10px' }}>
                Histórico de Alterações Comerciais ({historicoEmpresa.length})
              </h4>
              {historicoEmpresa.map((h) => (
                <div key={h.id} style={{ padding: '8px 10px', borderBottom: '1px solid var(--border-light)', fontSize: '12.5px' }}>
                  <div style={{ display: 'flex', justifyContent: 'space-between', fontWeight: 600 }}>
                    <span>[{h.tipoAlteracao}] {fmtCurrency(h.mensalidadeAnterior)} → {fmtCurrency(h.mensalidadeFinal)} (Calc: {fmtCurrency(h.mensalidadeCalculada)})</span>
                    <span>{new Date(h.dataCriacao).toLocaleString('pt-BR')}</span>
                  </div>
                  <div style={{ color: 'var(--text-muted)', marginTop: '2px' }}>
                    Justificativa: {h.justificativa} • Por: {h.alteradoPor} (Tabela v{h.versaoTabelaPrecos})
                  </div>
                </div>
              ))}
            </div>
          </div>
        </div>
      )}
    </div>
  );
};
