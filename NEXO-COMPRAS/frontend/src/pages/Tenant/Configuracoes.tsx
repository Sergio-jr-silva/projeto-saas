import React, { useState, useEffect, useRef } from 'react';
import { api } from '../../services/api';
import { useAuth } from '../../contexts/AuthContext';
import {
  Settings,
  Image,
  Upload,
  Trash2,
  RefreshCw,
  CheckCircle2,
  AlertCircle,
  Building2,
  FileCheck,
  Clock,
  Shield,
  Percent,
  Sliders,
  HelpCircle,
  Info
} from 'lucide-react';

interface TenantInfo {
  id: string;
  razaoSocial: string;
  nomeFantasia: string;
  cnpj: string;
  email: string;
  telefone: string;
  status: string;
  inscricaoEstadual?: string;
  inscricaoMunicipal?: string;
  endereco?: string;
  numero?: string;
  complemento?: string;
  bairro?: string;
  cidade?: string;
  uf?: string;
  cep?: string;
  logoUrl?: string;
  logoAtualizadaEm?: string;
  plano?: {
    id: string;
    nome: string;
    maxUsuarios: number;
    maxFiliais: number;
    maxFornecedores: number;
    maxCotacoesMes: number;
  };
  totalUsuarios: number;
  totalFiliais: number;
  totalAgentes: number;
}

interface TenantConfig {
  uf: string;
  regimeTributario: string;
  creditaIcms: boolean;
  creditaIpi: boolean;
  percCreditoPisCofins: number;
  leadTimePadraoDias: number;
  diasSeguranca: number;
  diasCobertura: number;
  validadeCotacaoDias: number;
}

export const Configuracoes: React.FC = () => {
  const { user, refreshUser } = useAuth();
  const fileInputRef = useRef<HTMLInputElement>(null);

  const [abaAtiva, setAbaAtiva] = useState<'identidade' | 'fiscal' | 'empresa'>('identidade');
  const [loading, setLoading] = useState(true);
  const [salvando, setSalvando] = useState(false);
  const [mensagemSucesso, setMensagemSucesso] = useState<string | null>(null);
  const [mensagemErro, setMensagemErro] = useState<string | null>(null);

  // Dados da Empresa
  const [tenantInfo, setTenantInfo] = useState<TenantInfo | null>(null);

  // Parâmetros Fiscais & Prazos
  const [config, setConfig] = useState<TenantConfig>({
    uf: 'SC',
    regimeTributario: 'SimplesNacional',
    creditaIcms: false,
    creditaIpi: false,
    percCreditoPisCofins: 0,
    leadTimePadraoDias: 3,
    diasSeguranca: 7,
    diasCobertura: 15,
    validadeCotacaoDias: 7
  });

  // Estado da Logomarca
  const [logoPreview, setLogoPreview] = useState<string | null>(null);
  const [arquivoSelecionado, setArquivoSelecionado] = useState<File | null>(null);
  const [uploadingLogo, setUploadingLogo] = useState(false);
  const [confirmandoRemocao, setConfirmandoRemocao] = useState(false);

  // Edição de Dados Cadastrais da Empresa
  const [editandoEmpresa, setEditandoEmpresa] = useState(false);
  const [salvandoEmpresa, setSalvandoEmpresa] = useState(false);
  const [formEmpresa, setFormEmpresa] = useState({
    razaoSocial: '',
    nomeFantasia: '',
    cnpj: '',
    inscricaoEstadual: '',
    inscricaoMunicipal: '',
    endereco: '',
    numero: '',
    complemento: '',
    bairro: '',
    cidade: '',
    uf: '',
    cep: '',
    telefone: '',
    email: ''
  });

  const carregarDados = async () => {
    setLoading(true);
    setMensagemErro(null);
    try {
      const [info, cfg] = await Promise.all([
        api.get<TenantInfo>('/tenant/info'),
        api.get<TenantConfig>('/tenant/config').catch(() => null)
      ]);

      setTenantInfo(info);
      setFormEmpresa({
        razaoSocial: info.razaoSocial || '',
        nomeFantasia: info.nomeFantasia || '',
        cnpj: info.cnpj || '',
        inscricaoEstadual: info.inscricaoEstadual || '',
        inscricaoMunicipal: info.inscricaoMunicipal || '',
        endereco: info.endereco || '',
        numero: info.numero || '',
        complemento: info.complemento || '',
        bairro: info.bairro || '',
        cidade: info.cidade || '',
        uf: info.uf || '',
        cep: info.cep || '',
        telefone: info.telefone || '',
        email: info.email || ''
      });
      if (cfg) setConfig(cfg);

      if (info.logoUrl) {
        const fullUrl = info.logoUrl.startsWith('http')
          ? info.logoUrl
          : `${api.getBaseUrl()}${info.logoUrl.startsWith('/') ? '' : '/'}${info.logoUrl.replace(/^\/?api\/?/, '')}`;
        setLogoPreview(fullUrl);
      } else {
        setLogoPreview(null);
      }
    } catch (err: any) {
      setMensagemErro(err.message || 'Erro ao carregar dados da empresa.');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    carregarDados();
  }, []);

  const handleArquivoChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    setMensagemErro(null);
    setMensagemSucesso(null);
    const files = e.target.files;
    if (!files || files.length === 0) return;

    const arquivo = files[0];

    // Validação de tipo
    const formatosValidos = ['image/png', 'image/jpeg', 'image/jpg', 'image/webp', 'image/svg+xml'];
    if (!formatosValidos.includes(arquivo.type) && !arquivo.name.match(/\.(png|jpe?g|webp|svg)$/i)) {
      setMensagemErro('Formato inválido. Selecione um arquivo PNG, JPG/JPEG, WEBP ou SVG.');
      return;
    }

    // Validação de tamanho (máximo 3MB)
    if (arquivo.size > 3 * 1024 * 1024) {
      setMensagemErro('Tamanho máximo excedido. A imagem deve ter no máximo 3MB.');
      return;
    }

    setArquivoSelecionado(arquivo);

    // Pré-visualização local imediata
    const reader = new FileReader();
    reader.onload = () => {
      setLogoPreview(reader.result as string);
    };
    reader.readAsDataURL(arquivo);
  };

  const handleUploadLogo = async () => {
    if (!arquivoSelecionado) return;

    setUploadingLogo(true);
    setMensagemErro(null);
    setMensagemSucesso(null);

    try {
      const formData = new FormData();
      formData.append('arquivo', arquivoSelecionado);

      const res = await api.postForm<{ logoUrl: string }>('/tenant/logo', formData);

      const fullUrl = res.logoUrl.startsWith('http')
        ? res.logoUrl
        : `${api.getBaseUrl()}${res.logoUrl.startsWith('/') ? '' : '/'}${res.logoUrl.replace(/^\/?api\/?/, '')}`;

      setLogoPreview(fullUrl);
      setArquivoSelecionado(null);
      setMensagemSucesso('Logomarca da empresa atualizada com sucesso! Documentos e cabeçalhos foram sincronizados.');

      // Atualiza o contexto global do tenant
      await refreshUser();
      await carregarDados();
    } catch (err: any) {
      setMensagemErro(err.message || 'Falha ao realizar upload da logomarca.');
    } finally {
      setUploadingLogo(false);
    }
  };

  const handleRemoverLogo = async () => {
    setUploadingLogo(true);
    setMensagemErro(null);
    setMensagemSucesso(null);
    try {
      await api.delete('/tenant/logo');
      setLogoPreview(null);
      setArquivoSelecionado(null);
      setConfirmandoRemocao(false);
      setMensagemSucesso('Logomarca removida com sucesso. Os documentos voltarão a exibir o espaço limpo sem logomarca.');

      await refreshUser();
      await carregarDados();
    } catch (err: any) {
      setMensagemErro(err.message || 'Erro ao remover logomarca.');
    } finally {
      setUploadingLogo(false);
    }
  };

  const handleSalvarEmpresa = async (e: React.FormEvent) => {
    e.preventDefault();
    setSalvandoEmpresa(true);
    setMensagemErro(null);
    setMensagemSucesso(null);
    try {
      const atualizado = await api.put<TenantInfo>('/tenant/info', formEmpresa);
      setTenantInfo((prev: any) => ({ ...prev, ...atualizado }));
      setEditandoEmpresa(false);
      setMensagemSucesso('Dados cadastrais da empresa atualizados com sucesso! Documentos PDF e relatórios utilizarão as novas informações.');
      await refreshUser();
    } catch (err: any) {
      setMensagemErro(err.response?.data?.erro || err.message || 'Erro ao atualizar dados cadastrais.');
    } finally {
      setSalvandoEmpresa(false);
    }
  };

  const handleSalvarConfigFiscal = async (e: React.FormEvent) => {
    e.preventDefault();
    setSalvando(true);
    setMensagemErro(null);
    setMensagemSucesso(null);

    try {
      await api.put('/tenant/config', config);
      setMensagemSucesso('Parâmetros fiscais e prazos operacionais atualizados com sucesso!');
    } catch (err: any) {
      setMensagemErro(err.message || 'Erro ao salvar configurações fiscais.');
    } finally {
      setSalvando(false);
    }
  };

  if (loading) {
    return (
      <div style={{ padding: '60px', textAlign: 'center', color: 'var(--text-muted)' }}>
        <RefreshCw size={24} className="spin" style={{ marginBottom: '12px' }} />
        <div>Carregando configurações da empresa...</div>
      </div>
    );
  }

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: '24px' }}>
      {/* HEADER DA PÁGINA */}
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <div>
          <h1 style={{ fontSize: '24px', fontWeight: 800, color: 'var(--text-main)', margin: '0 0 4px 0', display: 'flex', alignItems: 'center', gap: '10px' }}>
            <Settings size={26} color="var(--blue-primary)" />
            Configurações da Empresa
          </h1>
          <p style={{ margin: 0, fontSize: '14px', color: 'var(--text-muted)' }}>
            Gerencie a identidade visual da sua empresa, parâmetros fiscais, prazos e dados cadastrais.
          </p>
        </div>
      </div>

      {/* FEEDBACK MENSAGENS */}
      {mensagemSucesso && (
        <div style={{ backgroundColor: '#f0fdf4', border: '1px solid #bbf7d0', padding: '12px 16px', borderRadius: '8px', color: '#166534', display: 'flex', alignItems: 'center', gap: '10px', fontSize: '13.5px' }}>
          <CheckCircle2 size={18} />
          <span>{mensagemSucesso}</span>
        </div>
      )}

      {mensagemErro && (
        <div style={{ backgroundColor: '#fef2f2', border: '1px solid #fecaca', padding: '12px 16px', borderRadius: '8px', color: '#991b1b', display: 'flex', alignItems: 'center', gap: '10px', fontSize: '13.5px' }}>
          <AlertCircle size={18} />
          <span>{mensagemErro}</span>
        </div>
      )}

      {/* NAVEGAÇÃO DE ABAS */}
      <div style={{ display: 'flex', gap: '8px', borderBottom: '1px solid var(--border)' }}>
        <button
          onClick={() => setAbaAtiva('identidade')}
          style={{
            padding: '10px 18px',
            fontWeight: 700,
            fontSize: '13.5px',
            border: 'none',
            background: 'none',
            cursor: 'pointer',
            borderBottom: abaAtiva === 'identidade' ? '2.5px solid var(--blue-primary)' : '2.5px solid transparent',
            color: abaAtiva === 'identidade' ? 'var(--blue-primary)' : 'var(--text-muted)',
            display: 'flex',
            alignItems: 'center',
            gap: '8px',
            transition: 'all 0.15s ease'
          }}
        >
          <Image size={16} />
          Identidade Visual & Logomarca
        </button>

        <button
          onClick={() => setAbaAtiva('fiscal')}
          style={{
            padding: '10px 18px',
            fontWeight: 700,
            fontSize: '13.5px',
            border: 'none',
            background: 'none',
            cursor: 'pointer',
            borderBottom: abaAtiva === 'fiscal' ? '2.5px solid var(--blue-primary)' : '2.5px solid transparent',
            color: abaAtiva === 'fiscal' ? 'var(--blue-primary)' : 'var(--text-muted)',
            display: 'flex',
            alignItems: 'center',
            gap: '8px',
            transition: 'all 0.15s ease'
          }}
        >
          <Sliders size={16} />
          Parâmetros Fiscais & Prazos
        </button>

        <button
          onClick={() => setAbaAtiva('empresa')}
          style={{
            padding: '10px 18px',
            fontWeight: 700,
            fontSize: '13.5px',
            border: 'none',
            background: 'none',
            cursor: 'pointer',
            borderBottom: abaAtiva === 'empresa' ? '2.5px solid var(--blue-primary)' : '2.5px solid transparent',
            color: abaAtiva === 'empresa' ? 'var(--blue-primary)' : 'var(--text-muted)',
            display: 'flex',
            alignItems: 'center',
            gap: '8px',
            transition: 'all 0.15s ease'
          }}
        >
          <Building2 size={16} />
          Dados da Empresa & Assinatura
        </button>
      </div>

      {/* ABA 1: IDENTIDADE VISUAL & LOGOMARCA */}
      {abaAtiva === 'identidade' && (
        <div style={{ display: 'flex', flexDirection: 'column', gap: '20px' }}>
          <div className="card" style={{ padding: '24px' }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', marginBottom: '16px' }}>
              <div>
                <h3 style={{ fontSize: '16px', fontWeight: 700, margin: '0 0 4px 0', display: 'flex', alignItems: 'center', gap: '8px' }}>
                  <Image size={18} color="var(--blue-primary)" />
                  Logomarca Oficial da Empresa
                </h3>
                <p style={{ margin: 0, fontSize: '13px', color: 'var(--text-muted)' }}>
                  A logomarca cadastrada é inserida automaticamente em pedidos de compra, cotações, especificações técnicas de serviços e ordens de serviço.
                </p>
              </div>
            </div>

            <div style={{ display: 'grid', gridTemplateColumns: 'minmax(280px, 1fr) 2fr', gap: '28px', alignItems: 'start' }}>
              {/* ÁREA DE PRÉ-VISUALIZAÇÃO */}
              <div
                style={{
                  border: '1.5px dashed var(--border)',
                  borderRadius: '12px',
                  padding: '24px',
                  backgroundColor: '#f8fafc',
                  textAlign: 'center',
                  minHeight: '220px',
                  display: 'flex',
                  flexDirection: 'column',
                  alignItems: 'center',
                  justifyContent: 'center',
                  position: 'relative'
                }}
              >
                {logoPreview ? (
                  <div style={{ width: '100%' }}>
                    <div
                      style={{
                        padding: '16px',
                        background: '#fff',
                        borderRadius: '8px',
                        border: '1px solid var(--border)',
                        boxShadow: '0 2px 4px rgba(0,0,0,0.05)',
                        display: 'flex',
                        alignItems: 'center',
                        justifyContent: 'center',
                        minHeight: '110px'
                      }}
                    >
                      <img
                        src={logoPreview}
                        alt="Logomarca da Empresa"
                        style={{
                          maxWidth: '220px',
                          maxHeight: '90px',
                          width: 'auto',
                          height: 'auto',
                          objectFit: 'contain',
                          display: 'block'
                        }}
                      />
                    </div>
                    <div style={{ marginTop: '12px', fontSize: '12px', color: '#16a34a', fontWeight: 600 }}>
                      ✓ Logomarca ativa nos documentos
                    </div>
                    {arquivoSelecionado && (
                      <div style={{ marginTop: '4px', fontSize: '11px', color: 'var(--blue-primary)', fontWeight: 600 }}>
                        (Arquivo pronto para salvar: {arquivoSelecionado.name})
                      </div>
                    )}
                  </div>
                ) : (
                  <div style={{ padding: '20px' }}>
                    <div style={{ fontSize: '14px', fontWeight: 600, color: 'var(--text-main)', marginBottom: '6px' }}>
                      Nenhuma logomarca cadastrada
                    </div>
                    <p style={{ fontSize: '12.5px', color: 'var(--text-muted)', margin: 0 }}>
                      Conforme padrão do sistema, o espaço do cabeçalho em PDFs e documentos permanecerá completamente limpo e vazio até o cadastro da imagem.
                    </p>
                  </div>
                )}
              </div>

              {/* CONTROLES E AÇÕES DE UPLOAD */}
              <div style={{ display: 'flex', flexDirection: 'column', gap: '16px' }}>
                <input
                  type="file"
                  ref={fileInputRef}
                  onChange={handleArquivoChange}
                  accept="image/png,image/jpeg,image/jpg,image/webp,image/svg+xml"
                  style={{ display: 'none' }}
                />

                <div>
                  <h4 style={{ fontSize: '14px', fontWeight: 700, margin: '0 0 6px 0', color: 'var(--text-main)' }}>
                    Requisitos do Arquivo
                  </h4>
                  <ul style={{ margin: 0, paddingLeft: '18px', fontSize: '12.5px', color: 'var(--text-muted)', lineHeight: 1.6 }}>
                    <li>Formatos suportados: <strong>PNG</strong>, <strong>JPG/JPEG</strong>, <strong>WEBP</strong> e <strong>SVG</strong>.</li>
                    <li>Tamanho máximo permitido: <strong>3 MB</strong>.</li>
                    <li>Resolução recomendada: formato horizontal ou retangular (proporção 2:1 a 4:1) com fundo transparente ou branco.</li>
                    <li>A proporção original da imagem será preservada sem distorções.</li>
                  </ul>
                </div>

                <div style={{ display: 'flex', gap: '10px', flexWrap: 'wrap', marginTop: '8px' }}>
                  <button
                    type="button"
                    onClick={() => fileInputRef.current?.click()}
                    className="btn btn-secondary"
                    disabled={uploadingLogo}
                    style={{ display: 'flex', alignItems: 'center', gap: '6px' }}
                  >
                    <Upload size={15} />
                    <span>{logoPreview ? 'Substituir Logomarca' : 'Selecionar Arquivo'}</span>
                  </button>

                  {arquivoSelecionado && (
                    <button
                      type="button"
                      onClick={handleUploadLogo}
                      className="btn btn-primary"
                      disabled={uploadingLogo}
                      style={{ display: 'flex', alignItems: 'center', gap: '6px' }}
                    >
                      <CheckCircle2 size={15} />
                      <span>{uploadingLogo ? 'Enviando...' : 'Confirmar e Salvar Logomarca'}</span>
                    </button>
                  )}

                  {tenantInfo?.logoUrl && !arquivoSelecionado && (
                    <>
                      {!confirmandoRemocao ? (
                        <button
                          type="button"
                          onClick={() => setConfirmandoRemocao(true)}
                          className="btn btn-secondary"
                          style={{ color: '#dc2626', borderColor: '#fecaca', display: 'flex', alignItems: 'center', gap: '6px' }}
                        >
                          <Trash2 size={15} />
                          <span>Remover Logomarca</span>
                        </button>
                      ) : (
                        <div style={{ display: 'flex', alignItems: 'center', gap: '8px', padding: '6px 12px', background: '#fef2f2', borderRadius: '6px' }}>
                          <span style={{ fontSize: '12px', color: '#991b1b', fontWeight: 600 }}>
                            Confirmar remoção?
                          </span>
                          <button
                            type="button"
                            onClick={handleRemoverLogo}
                            disabled={uploadingLogo}
                            className="btn btn-primary"
                            style={{ backgroundColor: '#dc2626', padding: '4px 10px', fontSize: '11.5px' }}
                          >
                            Sim, Remover
                          </button>
                          <button
                            type="button"
                            onClick={() => setConfirmandoRemocao(false)}
                            className="btn btn-secondary"
                            style={{ padding: '4px 8px', fontSize: '11.5px' }}
                          >
                            Cancelar
                          </button>
                        </div>
                      )}
                    </>
                  )}
                </div>

                <div style={{ marginTop: '12px', padding: '12px', background: '#eff6ff', borderRadius: '8px', border: '1px solid #bfdbfe', fontSize: '12px', color: '#1e40af', display: 'flex', alignItems: 'flex-start', gap: '8px' }}>
                  <Info size={16} style={{ flexShrink: 0, marginTop: '2px' }} />
                  <div>
                    <strong>Regra de Visualização:</strong> Quando a empresa não possui uma logomarca cadastrada, os pedidos, cotações e relatórios não exibem nenhum substituto ou imagem genérica, mantendo a diagramação elegante e perfeitamente equilibrada.
                  </div>
                </div>
              </div>
            </div>
          </div>
        </div>
      )}

      {/* ABA 2: PARÂMETROS FISCAIS & PRAZOS */}
      {abaAtiva === 'fiscal' && (
        <form onSubmit={handleSalvarConfigFiscal} style={{ display: 'flex', flexDirection: 'column', gap: '20px' }}>
          <div className="card" style={{ padding: '24px' }}>
            <h3 style={{ fontSize: '16px', fontWeight: 700, margin: '0 0 16px 0', display: 'flex', alignItems: 'center', gap: '8px' }}>
              <Sliders size={18} color="var(--blue-primary)" />
              Regime Tributário e Créditos de Impostos
            </h3>

            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(260px, 1fr))', gap: '16px' }}>
              <div>
                <label className="label">UF da Matriz / Operação Principal</label>
                <input
                  type="text"
                  className="input"
                  maxLength={2}
                  value={config.uf}
                  onChange={(e) => setConfig({ ...config, uf: e.target.value.toUpperCase() })}
                />
              </div>

              <div>
                <label className="label">Regime Tributário</label>
                <select
                  className="input"
                  value={config.regimeTributario}
                  onChange={(e) => setConfig({ ...config, regimeTributario: e.target.value })}
                >
                  <option value="SimplesNacional">Simples Nacional</option>
                  <option value="LucroPresumido">Lucro Presumido</option>
                  <option value="LucroReal">Lucro Real</option>
                </select>
              </div>

              <div style={{ display: 'flex', alignItems: 'center', gap: '10px', paddingTop: '24px' }}>
                <input
                  type="checkbox"
                  id="creditaIcms"
                  checked={config.creditaIcms}
                  onChange={(e) => setConfig({ ...config, creditaIcms: e.target.checked })}
                  style={{ width: '18px', height: '18px' }}
                />
                <label htmlFor="creditaIcms" style={{ fontSize: '13px', fontWeight: 600, cursor: 'pointer' }}>
                  Aproveita Crédito de ICMS nas Compras
                </label>
              </div>

              <div style={{ display: 'flex', alignItems: 'center', gap: '10px', paddingTop: '24px' }}>
                <input
                  type="checkbox"
                  id="creditaIpi"
                  checked={config.creditaIpi}
                  onChange={(e) => setConfig({ ...config, creditaIpi: e.target.checked })}
                  style={{ width: '18px', height: '18px' }}
                />
                <label htmlFor="creditaIpi" style={{ fontSize: '13px', fontWeight: 600, cursor: 'pointer' }}>
                  Aproveita Crédito de IPI
                </label>
              </div>

              {config.regimeTributario === 'LucroReal' && (
                <div>
                  <label className="label">% Crédito PIS/COFINS (Não-Cumulativo)</label>
                  <input
                    type="number"
                    step="0.01"
                    className="input"
                    value={config.percCreditoPisCofins}
                    onChange={(e) => setConfig({ ...config, percCreditoPisCofins: Number(e.target.value) || 0 })}
                  />
                  <div style={{ fontSize: '11px', color: 'var(--text-muted)', marginTop: '4px' }}>
                    Alíquota padrão unificada: 9,25% (1,65% PIS + 7,60% COFINS).
                  </div>
                </div>
              )}
            </div>
          </div>

          <div className="card" style={{ padding: '24px' }}>
            <h3 style={{ fontSize: '16px', fontWeight: 700, margin: '0 0 16px 0', display: 'flex', alignItems: 'center', gap: '8px' }}>
              <Clock size={18} color="var(--blue-primary)" />
              Prazos Operacionais e Sugestão de Compras
            </h3>

            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(220px, 1fr))', gap: '16px' }}>
              <div>
                <label className="label">Lead Time Padrão de Entrega (dias)</label>
                <input
                  type="number"
                  className="input"
                  min="0"
                  max="180"
                  value={config.leadTimePadraoDias}
                  onChange={(e) => setConfig({ ...config, leadTimePadraoDias: Number(e.target.value) || 0 })}
                />
              </div>

              <div>
                <label className="label">Estoque de Segurança (dias)</label>
                <input
                  type="number"
                  className="input"
                  min="0"
                  max="180"
                  value={config.diasSeguranca}
                  onChange={(e) => setConfig({ ...config, diasSeguranca: Number(e.target.value) || 0 })}
                />
              </div>

              <div>
                <label className="label">Dias de Cobertura de Estoque</label>
                <input
                  type="number"
                  className="input"
                  min="1"
                  max="365"
                  value={config.diasCobertura}
                  onChange={(e) => setConfig({ ...config, diasCobertura: Number(e.target.value) || 1 })}
                />
              </div>

              <div>
                <label className="label">Validade Padrão de Cotações (dias)</label>
                <input
                  type="number"
                  className="input"
                  min="1"
                  max="90"
                  value={config.validadeCotacaoDias}
                  onChange={(e) => setConfig({ ...config, validadeCotacaoDias: Number(e.target.value) || 1 })}
                />
              </div>
            </div>

            <div style={{ marginTop: '20px', display: 'flex', justifyContent: 'flex-end' }}>
              <button
                type="submit"
                className="btn btn-primary"
                disabled={salvando}
                style={{ display: 'flex', alignItems: 'center', gap: '6px' }}
              >
                <CheckCircle2 size={16} />
                <span>{salvando ? 'Salvando...' : 'Salvar Parâmetros'}</span>
              </button>
            </div>
          </div>
        </form>
      )}

      {/* ABA 3: DADOS DA EMPRESA & ASSINATURA */}
      {abaAtiva === 'empresa' && tenantInfo && (
        <div style={{ display: 'flex', flexDirection: 'column', gap: '20px' }}>
          <div className="card" style={{ padding: '24px' }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '16px', flexWrap: 'wrap', gap: '12px' }}>
              <h3 style={{ fontSize: '16px', fontWeight: 700, margin: 0, display: 'flex', alignItems: 'center', gap: '8px' }}>
                <Building2 size={18} color="var(--blue-primary)" />
                Dados Cadastrais da Empresa (Utilizados em Pedidos, Cotações e PDFs)
              </h3>

              {!editandoEmpresa ? (
                <button
                  type="button"
                  className="btn btn-secondary"
                  onClick={() => setEditandoEmpresa(true)}
                  style={{ display: 'flex', alignItems: 'center', gap: '6px' }}
                >
                  <Sliders size={15} />
                  <span>Editar Dados da Empresa</span>
                </button>
              ) : (
                <button
                  type="button"
                  className="btn btn-secondary"
                  onClick={() => setEditandoEmpresa(false)}
                >
                  Cancelar Edição
                </button>
              )}
            </div>

            {editandoEmpresa ? (
              <form onSubmit={handleSalvarEmpresa}>
                <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(260px, 1fr))', gap: '16px' }}>
                  <div>
                    <label style={{ display: 'block', fontSize: '12px', fontWeight: 600, color: 'var(--text-muted)', marginBottom: '4px' }}>
                      Razão Social *
                    </label>
                    <input
                      type="text"
                      className="input"
                      value={formEmpresa.razaoSocial}
                      onChange={(e) => setFormEmpresa({ ...formEmpresa, razaoSocial: e.target.value })}
                      required
                    />
                  </div>

                  <div>
                    <label style={{ display: 'block', fontSize: '12px', fontWeight: 600, color: 'var(--text-muted)', marginBottom: '4px' }}>
                      Nome Fantasia
                    </label>
                    <input
                      type="text"
                      className="input"
                      value={formEmpresa.nomeFantasia}
                      onChange={(e) => setFormEmpresa({ ...formEmpresa, nomeFantasia: e.target.value })}
                    />
                  </div>

                  <div>
                    <label style={{ display: 'block', fontSize: '12px', fontWeight: 600, color: 'var(--text-muted)', marginBottom: '4px' }}>
                      CNPJ *
                    </label>
                    <input
                      type="text"
                      className="input"
                      value={formEmpresa.cnpj}
                      onChange={(e) => setFormEmpresa({ ...formEmpresa, cnpj: e.target.value })}
                      placeholder="00.000.000/0000-00"
                      required
                    />
                  </div>

                  <div>
                    <label style={{ display: 'block', fontSize: '12px', fontWeight: 600, color: 'var(--text-muted)', marginBottom: '4px' }}>
                      Inscrição Estadual
                    </label>
                    <input
                      type="text"
                      className="input"
                      value={formEmpresa.inscricaoEstadual}
                      onChange={(e) => setFormEmpresa({ ...formEmpresa, inscricaoEstadual: e.target.value })}
                      placeholder="Ex: 256918570 ou ISENTO"
                    />
                  </div>

                  <div>
                    <label style={{ display: 'block', fontSize: '12px', fontWeight: 600, color: 'var(--text-muted)', marginBottom: '4px' }}>
                      Inscrição Municipal
                    </label>
                    <input
                      type="text"
                      className="input"
                      value={formEmpresa.inscricaoMunicipal}
                      onChange={(e) => setFormEmpresa({ ...formEmpresa, inscricaoMunicipal: e.target.value })}
                    />
                  </div>

                  <div>
                    <label style={{ display: 'block', fontSize: '12px', fontWeight: 600, color: 'var(--text-muted)', marginBottom: '4px' }}>
                      Telefone / Celular
                    </label>
                    <input
                      type="text"
                      className="input"
                      value={formEmpresa.telefone}
                      onChange={(e) => setFormEmpresa({ ...formEmpresa, telefone: e.target.value })}
                      placeholder="(00) 0000-0000"
                    />
                  </div>

                  <div>
                    <label style={{ display: 'block', fontSize: '12px', fontWeight: 600, color: 'var(--text-muted)', marginBottom: '4px' }}>
                      E-mail de Contato / Compras
                    </label>
                    <input
                      type="email"
                      className="input"
                      value={formEmpresa.email}
                      onChange={(e) => setFormEmpresa({ ...formEmpresa, email: e.target.value })}
                    />
                  </div>

                  <div>
                    <label style={{ display: 'block', fontSize: '12px', fontWeight: 600, color: 'var(--text-muted)', marginBottom: '4px' }}>
                      CEP
                    </label>
                    <input
                      type="text"
                      className="input"
                      value={formEmpresa.cep}
                      onChange={(e) => setFormEmpresa({ ...formEmpresa, cep: e.target.value })}
                      placeholder="00000-000"
                    />
                  </div>

                  <div style={{ gridColumn: 'span 2' }}>
                    <label style={{ display: 'block', fontSize: '12px', fontWeight: 600, color: 'var(--text-muted)', marginBottom: '4px' }}>
                      Endereço (Logradouro e Número)
                    </label>
                    <div style={{ display: 'grid', gridTemplateColumns: '3fr 1fr', gap: '8px' }}>
                      <input
                        type="text"
                        className="input"
                        placeholder="Rua / Avenida"
                        value={formEmpresa.endereco}
                        onChange={(e) => setFormEmpresa({ ...formEmpresa, endereco: e.target.value })}
                      />
                      <input
                        type="text"
                        className="input"
                        placeholder="Nº"
                        value={formEmpresa.numero}
                        onChange={(e) => setFormEmpresa({ ...formEmpresa, numero: e.target.value })}
                      />
                    </div>
                  </div>

                  <div>
                    <label style={{ display: 'block', fontSize: '12px', fontWeight: 600, color: 'var(--text-muted)', marginBottom: '4px' }}>
                      Complemento
                    </label>
                    <input
                      type="text"
                      className="input"
                      value={formEmpresa.complemento}
                      onChange={(e) => setFormEmpresa({ ...formEmpresa, complemento: e.target.value })}
                      placeholder="Sala, Galpão, Bloco..."
                    />
                  </div>

                  <div>
                    <label style={{ display: 'block', fontSize: '12px', fontWeight: 600, color: 'var(--text-muted)', marginBottom: '4px' }}>
                      Bairro
                    </label>
                    <input
                      type="text"
                      className="input"
                      value={formEmpresa.bairro}
                      onChange={(e) => setFormEmpresa({ ...formEmpresa, bairro: e.target.value })}
                    />
                  </div>

                  <div>
                    <label style={{ display: 'block', fontSize: '12px', fontWeight: 600, color: 'var(--text-muted)', marginBottom: '4px' }}>
                      Cidade
                    </label>
                    <input
                      type="text"
                      className="input"
                      value={formEmpresa.cidade}
                      onChange={(e) => setFormEmpresa({ ...formEmpresa, cidade: e.target.value })}
                    />
                  </div>

                  <div>
                    <label style={{ display: 'block', fontSize: '12px', fontWeight: 600, color: 'var(--text-muted)', marginBottom: '4px' }}>
                      UF
                    </label>
                    <input
                      type="text"
                      className="input"
                      maxLength={2}
                      value={formEmpresa.uf}
                      onChange={(e) => setFormEmpresa({ ...formEmpresa, uf: e.target.value.toUpperCase() })}
                      placeholder="UF"
                    />
                  </div>
                </div>

                <div style={{ marginTop: '20px', display: 'flex', justifyContent: 'flex-end', gap: '10px' }}>
                  <button
                    type="button"
                    className="btn btn-secondary"
                    onClick={() => setEditandoEmpresa(false)}
                  >
                    Cancelar
                  </button>
                  <button
                    type="submit"
                    className="btn btn-primary"
                    disabled={salvandoEmpresa}
                    style={{ display: 'flex', alignItems: 'center', gap: '6px' }}
                  >
                    <CheckCircle2 size={16} />
                    <span>{salvandoEmpresa ? 'Salvando...' : 'Salvar Dados Cadastrais'}</span>
                  </button>
                </div>
              </form>
            ) : (
              <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(260px, 1fr))', gap: '16px' }}>
                <div>
                  <div style={{ fontSize: '12px', color: 'var(--text-muted)' }}>Razão Social</div>
                  <div style={{ fontSize: '14px', fontWeight: 700, color: 'var(--text-main)', marginTop: '2px' }}>
                    {tenantInfo.razaoSocial}
                  </div>
                </div>

                <div>
                  <div style={{ fontSize: '12px', color: 'var(--text-muted)' }}>Nome Fantasia</div>
                  <div style={{ fontSize: '14px', fontWeight: 700, color: 'var(--text-main)', marginTop: '2px' }}>
                    {tenantInfo.nomeFantasia || '-'}
                  </div>
                </div>

                <div>
                  <div style={{ fontSize: '12px', color: 'var(--text-muted)' }}>CNPJ</div>
                  <div style={{ fontSize: '14px', fontWeight: 700, color: 'var(--text-main)', marginTop: '2px' }}>
                    {tenantInfo.cnpj.replace(/(\d{2})(\d{3})(\d{3})(\d{4})(\d{2})/, '$1.$2.$3/$4-$5')}
                  </div>
                </div>

                <div>
                  <div style={{ fontSize: '12px', color: 'var(--text-muted)' }}>Inscrição Estadual</div>
                  <div style={{ fontSize: '14px', fontWeight: 600, color: 'var(--text-main)', marginTop: '2px' }}>
                    {tenantInfo.inscricaoEstadual || '(Não informado)'}
                  </div>
                </div>

                <div>
                  <div style={{ fontSize: '12px', color: 'var(--text-muted)' }}>Inscrição Municipal</div>
                  <div style={{ fontSize: '14px', fontWeight: 600, color: 'var(--text-main)', marginTop: '2px' }}>
                    {tenantInfo.inscricaoMunicipal || '(Não informado)'}
                  </div>
                </div>

                <div>
                  <div style={{ fontSize: '12px', color: 'var(--text-muted)' }}>E-mail de Contato</div>
                  <div style={{ fontSize: '14px', fontWeight: 600, color: 'var(--text-main)', marginTop: '2px' }}>
                    {tenantInfo.email || '-'}
                  </div>
                </div>

                <div>
                  <div style={{ fontSize: '12px', color: 'var(--text-muted)' }}>Telefone</div>
                  <div style={{ fontSize: '14px', fontWeight: 600, color: 'var(--text-main)', marginTop: '2px' }}>
                    {tenantInfo.telefone || '-'}
                  </div>
                </div>

                <div>
                  <div style={{ fontSize: '12px', color: 'var(--text-muted)' }}>Endereço Completo</div>
                  <div style={{ fontSize: '14px', fontWeight: 600, color: 'var(--text-main)', marginTop: '2px' }}>
                    {tenantInfo.endereco
                      ? `${tenantInfo.endereco}${tenantInfo.numero ? `, Nº ${tenantInfo.numero}` : ''}${tenantInfo.complemento ? ` - ${tenantInfo.complemento}` : ''} - ${tenantInfo.bairro || ''} - ${tenantInfo.cidade || ''}/${tenantInfo.uf || ''} - CEP: ${tenantInfo.cep || ''}`
                      : '(Endereço não cadastrado)'}
                  </div>
                </div>

                <div>
                  <div style={{ fontSize: '12px', color: 'var(--text-muted)' }}>Status da Empresa</div>
                  <span className="badge badge-green" style={{ marginTop: '4px' }}>
                    {tenantInfo.status}
                  </span>
                </div>
              </div>
            )}
          </div>

          <div className="card" style={{ padding: '24px' }}>
            <h3 style={{ fontSize: '16px', fontWeight: 700, margin: '0 0 16px 0', display: 'flex', alignItems: 'center', gap: '8px' }}>
              <Shield size={18} color="var(--blue-primary)" />
              Plano de Assinatura e Limites Operacionais
            </h3>

            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(180px, 1fr))', gap: '16px' }}>
              <div style={{ padding: '14px', background: '#f8fafc', borderRadius: '8px', border: '1px solid var(--border)' }}>
                <div style={{ fontSize: '11.5px', color: 'var(--text-muted)' }}>PLANO ATIVO</div>
                <div style={{ fontSize: '18px', fontWeight: 800, color: 'var(--blue-primary)', marginTop: '4px' }}>
                  {tenantInfo.plano?.nome || 'Plano Pro'}
                </div>
              </div>

              <div style={{ padding: '14px', background: '#f8fafc', borderRadius: '8px', border: '1px solid var(--border)' }}>
                <div style={{ fontSize: '11.5px', color: 'var(--text-muted)' }}>USUÁRIOS CADASTRADOS</div>
                <div style={{ fontSize: '18px', fontWeight: 800, color: 'var(--text-main)', marginTop: '4px' }}>
                  {tenantInfo.totalUsuarios} / {tenantInfo.plano?.maxUsuarios || '∞'}
                </div>
              </div>

              <div style={{ padding: '14px', background: '#f8fafc', borderRadius: '8px', border: '1px solid var(--border)' }}>
                <div style={{ fontSize: '11.5px', color: 'var(--text-muted)' }}>FILIAIS ATIVAS</div>
                <div style={{ fontSize: '18px', fontWeight: 800, color: 'var(--text-main)', marginTop: '4px' }}>
                  {tenantInfo.totalFiliais} / {tenantInfo.plano?.maxFiliais || '∞'}
                </div>
              </div>

              <div style={{ padding: '14px', background: '#f8fafc', borderRadius: '8px', border: '1px solid var(--border)' }}>
                <div style={{ fontSize: '11.5px', color: 'var(--text-muted)' }}>SINCRONIZADORES ATIVOS</div>
                <div style={{ fontSize: '18px', fontWeight: 800, color: 'var(--text-main)', marginTop: '4px' }}>
                  {tenantInfo.totalAgentes}
                </div>
              </div>
            </div>
          </div>
        </div>
      )}
    </div>
  );
};

