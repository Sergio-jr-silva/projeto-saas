import React, { useState, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { api } from '../../services/api';
import { CnpjInput } from '../../components/CnpjInput';
import { validarCnpj, cleanCnpj } from '../../utils/cnpj';
import {
  Building2,
  Store,
  Key,
  CheckCircle2,
  AlertCircle,
  Copy,
  Check,
  ArrowRight,
  ArrowLeft,
  UserCheck,
  RefreshCw,
  Eye,
  EyeOff,
  ShieldCheck,
} from 'lucide-react';

interface Plano {
  id: string;
  nome: string;
  mensalidade: number;
}

interface CadastroCompletoEmpresaResponse {
  tenantId: string;
  razaoSocial: string;
  nomeFantasia: string;
  cnpj: string;
  status: string;
  filialId: string;
  nomeFilial: string;
  codigoFilial: string;
  tokenFilial: string;
  usuarioId: string;
  usuarioNome: string;
  usuarioEmail: string;
  senhaInicial: string;
  agenteId?: string;
  agenteNome?: string;
  codigoAtivacao?: string;
  codigoAtivacaoExpiraEm?: string;
}

export const CadastrarEmpresaWizard: React.FC = () => {
  const navigate = useNavigate();

  // Step state (1 to 4 + 5 for success)
  const [step, setStep] = useState(1);
  const [loading, setLoading] = useState(false);
  const [erro, setErro] = useState('');
  const [planos, setPlanos] = useState<Plano[]>([]);
  const [mostrarSenha, setMostrarSenha] = useState(false);
  const [copiadoCampo, setCopiadoCampo] = useState<string | null>(null);
  const [sucessoCriacao, setSucessoCriacao] = useState<CadastroCompletoEmpresaResponse | null>(null);

  // Form State
  const [formData, setFormData] = useState({
    // Etapa 1: Empresa
    razaoSocial: '',
    nomeFantasia: '',
    slug: '',
    cnpj: '',
    email: '',
    telefone: '',
    planoId: '',
    moduloContratado: 'Basico',

    // Etapa 2: Filial
    nomeFilial: 'Matriz - Centro',
    codigoFilial: '001',
    cnpjFilial: '',

    // Etapa 3: Usuário
    responsavelNome: '',
    emailLogin: '',
    senhaInicial: '',
    perfilAcesso: 'TenantAdmin',

    // Etapa 4: SINc
    gerarCodigoAtivacao: true,
  });

  useEffect(() => {
    // Carregar planos disponíveis
    api.get<Plano[]>('/plans')
      .then((res) => {
        setPlanos(res);
        if (res.length > 0 && !formData.planoId) {
          setFormData((prev) => ({ ...prev, planoId: res[0].id }));
        }
      })
      .catch((err) => console.error('Erro ao buscar planos:', err));

    gerarSenhaAleatoria();
  }, []);

  const gerarSenhaAleatoria = () => {
    const chars = 'ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnpqrstuvwxyz23456789!@#$%';
    let pass = 'Nx';
    for (let i = 0; i < 6; i++) {
      pass += chars.charAt(Math.floor(Math.random() * chars.length));
    }
    pass += '!';
    setFormData((prev) => ({ ...prev, senhaInicial: pass }));
  };

  const formatarCnpj = (v: string) => {
    const digitos = v.replace(/\D/g, '').slice(0, 14);
    if (digitos.length <= 2) return digitos;
    if (digitos.length <= 5) return `${digitos.slice(0, 2)}.${digitos.slice(2)}`;
    if (digitos.length <= 8) return `${digitos.slice(0, 2)}.${digitos.slice(2, 5)}.${digitos.slice(5)}`;
    if (digitos.length <= 12) return `${digitos.slice(0, 2)}.${digitos.slice(2, 5)}.${digitos.slice(5, 8)}/${digitos.slice(8)}`;
    return `${digitos.slice(0, 2)}.${digitos.slice(2, 5)}.${digitos.slice(5, 8)}/${digitos.slice(8, 12)}-${digitos.slice(12)}`;
  };

  const obterCnpjBase = (cnpjStr: string) => {
    const limpo = cnpjStr.replace(/\D/g, '');
    return limpo.length >= 8 ? limpo.slice(0, 8) : limpo;
  };

  const validarEtapa = (etapaAtual: number): boolean => {
    setErro('');
    if (etapaAtual === 1) {
      if (!formData.razaoSocial.trim()) {
        setErro('A Razão Social da empresa é obrigatória.');
        return false;
      }
      const rawCnpj = formData.cnpj.replace(/\D/g, '');
      if (rawCnpj.length !== 14 || !validarCnpj(rawCnpj)) {
        setErro('O CNPJ da empresa é inválido. Digite 14 números com dígitos verificadores corretos.');
        return false;
      }
      if (!formData.email.trim() || !formData.email.includes('@')) {
        setErro('Informe um e-mail corporativo válido.');
        return false;
      }
    } else if (etapaAtual === 2) {
      if (!formData.nomeFilial.trim()) {
        setErro('O Nome da Filial inicial é obrigatório.');
        return false;
      }
      if (!formData.codigoFilial.trim()) {
        setErro('O Código da filial é obrigatório (ex: 001).');
        return false;
      }
      if (formData.cnpjFilial && formData.cnpjFilial.replace(/\D/g, '').length > 0) {
        const rawFilial = formData.cnpjFilial.replace(/\D/g, '');
        if (rawFilial.length !== 14 || !validarCnpj(rawFilial)) {
          setErro('O CNPJ da filial é inválido. Digite 14 números com dígitos verificadores corretos.');
          return false;
        }
      }
    } else if (etapaAtual === 3) {
      if (!formData.responsavelNome.trim()) {
        setErro('O nome do responsável pelo acesso é obrigatório.');
        return false;
      }
      if (!formData.emailLogin.trim() || !formData.emailLogin.includes('@')) {
        setErro('Informe um e-mail de login válido.');
        return false;
      }
      if (!formData.senhaInicial.trim() || formData.senhaInicial.length < 6) {
        setErro('A senha inicial deve conter pelo menos 6 caracteres.');
        return false;
      }
    }
    return true;
  };

  const avancarEtapa = () => {
    if (validarEtapa(step)) {
      setStep((prev) => prev + 1);
    }
  };

  const voltarEtapa = () => {
    setErro('');
    setStep((prev) => Math.max(1, prev - 1));
  };

  const handleFinalizarCadastro = async () => {
    if (!validarEtapa(step)) return;
    setLoading(true);
    setErro('');

    try {
      const payload = {
        razaoSocial: formData.razaoSocial.trim(),
        nomeFantasia: formData.nomeFantasia.trim() || formData.razaoSocial.trim(),
        cnpj: formData.cnpj.replace(/\D/g, ''),
        email: formData.email.trim().toLowerCase(),
        telefone: formData.telefone.trim(),
        planoId: formData.planoId || undefined,
        nomeFilial: formData.nomeFilial.trim(),
        codigoFilial: formData.codigoFilial.trim(),
        cnpjFilial: formData.cnpjFilial ? formData.cnpjFilial.replace(/\D/g, '') : undefined,
        responsavelNome: formData.responsavelNome.trim(),
        emailLogin: formData.emailLogin.trim().toLowerCase(),
        senhaInicial: formData.senhaInicial.trim(),
        perfilAcesso: formData.perfilAcesso,
        gerarCodigoAtivacao: formData.gerarCodigoAtivacao,
      };

      const res = await api.post<CadastroCompletoEmpresaResponse>('/admin/empresas/cadastro-completo', payload);
      setSucessoCriacao(res);
      setStep(5); // Etapa 5: Resumo & Credenciais
    } catch (err: any) {
      setErro(err.message || 'Erro ao realizar cadastro da empresa.');
    } finally {
      setLoading(false);
    }
  };

  const copiarTexto = (texto: string, campo: string) => {
    navigator.clipboard.writeText(texto);
    setCopiadoCampo(campo);
    setTimeout(() => setCopiadoCampo(null), 2500);
  };

  const copiarTodasCredenciais = () => {
    if (!sucessoCriacao) return;
    const cnpjBase = obterCnpjBase(sucessoCriacao.cnpj);
    const texto = `=== CREDENCIAIS DE ACESSO NEXO COMPRAS ===
Empresa: ${sucessoCriacao.nomeFantasia} (${sucessoCriacao.razaoSocial})
CNPJ: ${sucessoCriacao.cnpj}
CNPJ-Base: ${cnpjBase}

--- INTEGRAÇÃO COM SINCRONIZADOR LOCAL (SINc) ---
Filial: ${sucessoCriacao.nomeFilial} (Cód: ${sucessoCriacao.codigoFilial})
Token da Filial: ${sucessoCriacao.tokenFilial}
CNPJ-Base: ${cnpjBase}
Código de Ativação SINc: ${sucessoCriacao.codigoAtivacao || 'Não gerado'}

--- ACESSO AO PAINEL WEB ---
Portal Web: ${window.location.origin}/login
E-mail de Login: ${sucessoCriacao.usuarioEmail}
Senha Inicial: ${sucessoCriacao.senhaInicial}

*No computador onde roda o banco de dados (Firebird/PostgreSQL), abra o SINc, vá em Configurações > Identificação e informe o Token da Filial e o CNPJ-Base acima.`;

    copiarTexto(texto, 'todas');
  };

  return (
    <div style={{ maxWidth: '860px', margin: '0 auto', paddingBottom: '60px' }}>
      {/* CABEÇALHO */}
      <div style={{ marginBottom: '24px' }}>
        <button
          onClick={() => navigate('/admin/tenants')}
          className="btn btn-secondary"
          style={{ marginBottom: '12px', fontSize: '12px', padding: '4px 10px' }}
        >
          <ArrowLeft size={14} /> Voltar para Empresas
        </button>
        <h1 style={{ fontSize: '24px', fontWeight: 800, color: 'var(--text-main)' }}>
          Cadastrar Nova Empresa Cliente
        </h1>
        <p style={{ fontSize: '14px', color: 'var(--text-muted)' }}>
          Provisione empresa, filial matriz, credenciais de login e gere o Token de Integração para o SINc.
        </p>
      </div>

      {/* BARRA DE ETAPAS */}
      <div style={{
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'space-between',
        marginBottom: '28px',
        backgroundColor: '#FFFFFF',
        padding: '16px 20px',
        borderRadius: 'var(--radius-md)',
        border: '1px solid var(--border)',
        overflowX: 'auto'
      }}>
        {[
          { num: 1, label: 'Empresa', icon: Building2 },
          { num: 2, label: 'Filial Matriz', icon: Store },
          { num: 3, label: 'Acesso do Cliente', icon: UserCheck },
          { num: 4, label: 'Token & Revisão', icon: Key },
        ].map((s) => {
          const isAtivo = step === s.num;
          const isConcluido = step > s.num;
          const Icon = s.icon;
          return (
            <div
              key={s.num}
              style={{
                display: 'flex',
                alignItems: 'center',
                gap: '8px',
                opacity: isAtivo || isConcluido ? 1 : 0.45,
                fontWeight: isAtivo ? 700 : 500,
                color: isAtivo ? 'var(--blue-primary)' : isConcluido ? 'var(--green)' : 'var(--text-muted)',
                fontSize: '13px',
                whiteSpace: 'nowrap'
              }}
            >
              <div style={{
                width: '28px',
                height: '28px',
                borderRadius: '50%',
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'center',
                backgroundColor: isAtivo ? 'var(--blue-light)' : isConcluido ? 'var(--green-light)' : '#F3F4F6',
                color: isAtivo ? 'var(--blue-primary)' : isConcluido ? 'var(--green-text)' : 'var(--text-muted)',
                fontWeight: 700,
                fontSize: '12px'
              }}>
                {isConcluido ? <Check size={15} /> : s.num}
              </div>
              <span>{s.label}</span>
            </div>
          );
        })}
      </div>

      {/* MENSAGEM DE ERRO */}
      {erro && (
        <div style={{
          padding: '12px 16px',
          backgroundColor: 'var(--red-light)',
          color: 'var(--red-text)',
          borderRadius: 'var(--radius-md)',
          fontSize: '13.5px',
          fontWeight: 500,
          marginBottom: '20px',
          display: 'flex',
          alignItems: 'center',
          gap: '8px'
        }}>
          <AlertCircle size={18} />
          <span>{erro}</span>
        </div>
      )}

      {/* ETAPA 1: DADOS DA EMPRESA */}
      {step === 1 && (
        <div className="card">
          <div style={{ display: 'flex', alignItems: 'center', gap: '10px', marginBottom: '18px' }}>
            <Building2 size={22} color="var(--blue-primary)" />
            <div>
              <h2 style={{ fontSize: '18px', fontWeight: 700 }}>Etapa 1: Dados Cadastrais da Empresa</h2>
              <p style={{ fontSize: '13px', color: 'var(--text-muted)' }}>
                Informações corporativas da organização cliente.
              </p>
            </div>
          </div>

          <div className="form-group">
            <label>Razão Social da Empresa *</label>
            <input
              placeholder="Ex: Supermercado Pepy Car LTDA"
              value={formData.razaoSocial}
              onChange={(e) => setFormData({ ...formData, razaoSocial: e.target.value })}
            />
          </div>

          <div className="grid-cols-2">
            <div className="form-group">
              <label>Nome Fantasia</label>
              <input
                placeholder="Ex: Pepy Supermercados"
                value={formData.nomeFantasia}
                onChange={(e) => setFormData({ ...formData, nomeFantasia: e.target.value })}
              />
            </div>

            <div className="form-group">
              <CnpjInput
                label="CNPJ *"
                required
                value={formData.cnpj}
                onChange={(cnpj) => setFormData({ ...formData, cnpj })}
              />
            </div>
          </div>

          <div className="grid-cols-2">
            <div className="form-group">
              <label>E-mail Corporativo *</label>
              <input
                type="email"
                placeholder="contato@empresa.com.br"
                value={formData.email}
                onChange={(e) => setFormData({ ...formData, email: e.target.value, emailLogin: formData.emailLogin || e.target.value })}
              />
            </div>

            <div className="form-group">
              <label>Telefone / WhatsApp</label>
              <input
                placeholder="(00) 00000-0000"
                value={formData.telefone}
                onChange={(e) => setFormData({ ...formData, telefone: e.target.value })}
              />
            </div>
          </div>

          <div className="grid-cols-2">
            <div className="form-group">
              <label>Slug da URL Exclusiva (nexoscompras.com.br/slug)</label>
              <input
                placeholder="Ex: alemdopao, paonorte, autopecas"
                value={formData.slug}
                onChange={(e) => setFormData({ ...formData, slug: e.target.value.toLowerCase().replace(/[^a-z0-9-]/g, '') })}
              />
            </div>

            <div className="form-group">
              <label>Módulos Contratados (Precificação Dinâmica R$ 50 – R$ 250)</label>
              <select
                value={formData.moduloContratado}
                onChange={(e) => setFormData({ ...formData, moduloContratado: e.target.value })}
              >
                <option value="Basico">Compras e cotações básicas (Base R$ 50,00)</option>
                <option value="RelatoriosAvancados">Compras e relatórios avançados (+R$ 15,00)</option>
                <option value="CompletoServicos">Compras, relatórios e serviços (+R$ 30,00)</option>
              </select>
            </div>
          </div>

          <div style={{ display: 'flex', justifyContent: 'flex-end', marginTop: '24px' }}>
            <button onClick={avancarEtapa} className="btn btn-primary" style={{ padding: '8px 20px' }}>
              Próximo: Filial Matriz <ArrowRight size={16} />
            </button>
          </div>
        </div>
      )}

      {/* ETAPA 2: FILIAL MATRIZ */}
      {step === 2 && (
        <div className="card">
          <div style={{ display: 'flex', alignItems: 'center', gap: '10px', marginBottom: '18px' }}>
            <Store size={22} color="var(--blue-primary)" />
            <div>
              <h2 style={{ fontSize: '18px', fontWeight: 700 }}>Etapa 2: Filial Matriz da Empresa</h2>
              <p style={{ fontSize: '13px', color: 'var(--text-muted)' }}>
                Identificação da primeira unidade física para associação ao token de integração.
              </p>
            </div>
          </div>

          <div className="form-group">
            <label>Nome da Unidade / Filial *</label>
            <input
              placeholder="Ex: Matriz - Centro"
              value={formData.nomeFilial}
              onChange={(e) => setFormData({ ...formData, nomeFilial: e.target.value })}
            />
          </div>

          <div className="grid-cols-2">
            <div className="form-group">
              <label>Código da Filial no ERP *</label>
              <input
                placeholder="001 ou 1"
                value={formData.codigoFilial}
                onChange={(e) => setFormData({ ...formData, codigoFilial: e.target.value })}
              />
            </div>

            <div className="form-group">
              <CnpjInput
                label="CNPJ da Filial (se houver filial própria)"
                value={formData.cnpjFilial}
                onChange={(cnpj) => setFormData({ ...formData, cnpjFilial: cnpj })}
              />
            </div>
          </div>

          <div style={{ display: 'flex', justifyContent: 'space-between', marginTop: '24px' }}>
            <button onClick={voltarEtapa} className="btn btn-secondary">
              <ArrowLeft size={16} /> Voltar
            </button>
            <button onClick={avancarEtapa} className="btn btn-primary" style={{ padding: '8px 20px' }}>
              Próximo: Acesso do Cliente <ArrowRight size={16} />
            </button>
          </div>
        </div>
      )}

      {/* ETAPA 3: ACESSO DO CLIENTE */}
      {step === 3 && (
        <div className="card">
          <div style={{ display: 'flex', alignItems: 'center', gap: '10px', marginBottom: '18px' }}>
            <UserCheck size={22} color="var(--blue-primary)" />
            <div>
              <h2 style={{ fontSize: '18px', fontWeight: 700 }}>Etapa 3: Credenciais de Acesso do Cliente</h2>
              <p style={{ fontSize: '13px', color: 'var(--text-muted)' }}>
                Crie o login inicial para que o administrador da empresa acesse o Nexo Compras Web.
              </p>
            </div>
          </div>

          <div className="form-group">
            <label>Nome do Responsável / Usuário Inicial *</label>
            <input
              placeholder="Ex: Carlos Silva (Gerente de Compras)"
              value={formData.responsavelNome}
              onChange={(e) => setFormData({ ...formData, responsavelNome: e.target.value })}
            />
          </div>

          <div className="grid-cols-2">
            <div className="form-group">
              <label>E-mail de Login *</label>
              <input
                type="email"
                placeholder="carlos@empresa.com.br"
                value={formData.emailLogin}
                onChange={(e) => setFormData({ ...formData, emailLogin: e.target.value })}
              />
            </div>

            <div className="form-group">
              <label>Perfil de Acesso</label>
              <select
                value={formData.perfilAcesso}
                onChange={(e) => setFormData({ ...formData, perfilAcesso: e.target.value })}
              >
                <option value="TenantAdmin">Administrador da Empresa (Acesso Total)</option>
                <option value="Buyer">Comprador (Cotações e Pedidos)</option>
                <option value="Viewer">Visualizador (Somente Leitura)</option>
              </select>
            </div>
          </div>

          <div className="form-group">
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '6px' }}>
              <label style={{ margin: 0 }}>Senha Inicial de Acesso *</label>
              <button
                type="button"
                onClick={gerarSenhaAleatoria}
                style={{
                  background: 'none',
                  border: 'none',
                  color: 'var(--blue-primary)',
                  fontSize: '12px',
                  fontWeight: 600,
                  cursor: 'pointer',
                  display: 'flex',
                  alignItems: 'center',
                  gap: '4px'
                }}
              >
                <RefreshCw size={12} /> Gerar Nova Senha
              </button>
            </div>
            <div style={{ position: 'relative' }}>
              <input
                type={mostrarSenha ? 'text' : 'password'}
                value={formData.senhaInicial}
                onChange={(e) => setFormData({ ...formData, senhaInicial: e.target.value })}
                style={{ paddingRight: '40px', fontFamily: 'monospace', fontWeight: 600 }}
              />
              <button
                type="button"
                onClick={() => setMostrarSenha(!mostrarSenha)}
                style={{ position: 'absolute', right: '10px', top: '10px', background: 'none', border: 'none', cursor: 'pointer', color: 'var(--text-muted)' }}
              >
                {mostrarSenha ? <EyeOff size={16} /> : <Eye size={16} />}
              </button>
            </div>
            <small style={{ color: 'var(--text-muted)', fontSize: '11px', marginTop: '4px', display: 'block' }}>
              Esta senha será criptografada com hash seguro BCrypt no banco de dados.
            </small>
          </div>

          <div style={{ display: 'flex', justifyContent: 'space-between', marginTop: '24px' }}>
            <button onClick={voltarEtapa} className="btn btn-secondary">
              <ArrowLeft size={16} /> Voltar
            </button>
            <button onClick={avancarEtapa} className="btn btn-primary" style={{ padding: '8px 20px' }}>
              Próximo: Token & Revisão <ArrowRight size={16} />
            </button>
          </div>
        </div>
      )}

      {/* ETAPA 4: REVISÃO & TOKEN DE INTEGRAÇÃO */}
      {step === 4 && (
        <div className="card">
          <div style={{ display: 'flex', alignItems: 'center', gap: '10px', marginBottom: '18px' }}>
            <Key size={22} color="var(--blue-primary)" />
            <div>
              <h2 style={{ fontSize: '18px', fontWeight: 700 }}>Etapa 4: Integração Centralizada por Token</h2>
              <p style={{ fontSize: '13px', color: 'var(--text-muted)' }}>
                O sincronizador Windows SINc utiliza exclusivamente o <strong>Token da Filial</strong> e o <strong>CNPJ-Base</strong> para se autenticar e conectar.
              </p>
            </div>
          </div>

          <div style={{
            padding: '16px',
            backgroundColor: '#F0F9FF',
            border: '1px solid #BAE6FD',
            borderRadius: 'var(--radius-md)',
            marginBottom: '20px'
          }}>
            <div style={{ fontWeight: 700, fontSize: '14px', color: '#0369A1', marginBottom: '6px' }}>
              Fluxo Centralizado de Integração
            </div>
            <div style={{ fontSize: '12.5px', color: '#0C4A6E', lineHeight: 1.6 }}>
              Ao finalizar, será gerado o <strong>Token de Integração da Filial</strong>. No computador local do cliente onde está o banco (Firebird ou PostgreSQL), o aplicativo SINc utilizará este token e os 8 primeiros dígitos do CNPJ para se conectar de forma segura e autônoma.
            </div>
          </div>

          <div style={{
            backgroundColor: '#F8FAFC',
            border: '1px solid var(--border)',
            borderRadius: 'var(--radius-md)',
            padding: '16px',
            fontSize: '13px',
            color: 'var(--text-muted)'
          }}>
            <h4 style={{ fontWeight: 700, color: 'var(--text-main)', marginBottom: '8px' }}>
              Resumo do provisionamento:
            </h4>
            <ul style={{ paddingLeft: '20px', lineHeight: 1.8 }}>
              <li><strong>Empresa:</strong> {formData.razaoSocial} ({formData.nomeFantasia || formData.razaoSocial})</li>
              <li><strong>CNPJ:</strong> {formData.cnpj} (CNPJ-Base: <code>{obterCnpjBase(formData.cnpj)}</code>)</li>
              <li><strong>Filial:</strong> {formData.nomeFilial} (Cód: {formData.codigoFilial})</li>
              <li><strong>Acesso Web do Cliente:</strong> {formData.emailLogin}</li>
              <li><strong>Sincronizador SINc:</strong> Será autorizado pelo Token emitido a seguir</li>
            </ul>
          </div>

          <div style={{ display: 'flex', justifyContent: 'space-between', marginTop: '24px' }}>
            <button onClick={voltarEtapa} className="btn btn-secondary">
              <ArrowLeft size={16} /> Voltar
            </button>
            <button
              onClick={handleFinalizarCadastro}
              disabled={loading}
              className="btn btn-green"
              style={{ padding: '10px 24px', fontSize: '14px', fontWeight: 700 }}
            >
              {loading ? (
                <>
                  <RefreshCw size={16} className="animate-spin" /> Criando Empresa e Gerando Token...
                </>
              ) : (
                <>
                  <CheckCircle2 size={16} /> Finalizar e Gerar Token de Integração
                </>
              )}
            </button>
          </div>
        </div>
      )}

      {/* ETAPA 5: RESUMO FINAL & CREDENCIAIS DE ENVIO AO CLIENTE */}
      {step === 5 && sucessoCriacao && (
        <div className="card" style={{ borderColor: 'var(--green)' }}>
          <div style={{ textAlign: 'center', marginBottom: '24px' }}>
            <div style={{
              width: '56px',
              height: '56px',
              borderRadius: '50%',
              backgroundColor: 'var(--green-light)',
              color: 'var(--green)',
              display: 'inline-flex',
              alignItems: 'center',
              justifyContent: 'center',
              marginBottom: '12px'
            }}>
              <CheckCircle2 size={32} />
            </div>
            <h2 style={{ fontSize: '22px', fontWeight: 800, color: 'var(--text-main)' }}>
              Empresa Cadastrada com Sucesso!
            </h2>
            <p style={{ fontSize: '14px', color: 'var(--text-muted)' }}>
              Copie o <strong>Token da Filial</strong> e o <strong>CNPJ-Base</strong> para configurar o sincronizador SINc no servidor do cliente.
            </p>
          </div>

          {/* PACOTE DE CREDENCIAIS */}
          <div style={{
            backgroundColor: '#F8FAFC',
            border: '1px solid var(--border)',
            borderRadius: 'var(--radius-md)',
            padding: '20px',
            marginBottom: '24px'
          }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '16px' }}>
              <div style={{ fontWeight: 700, fontSize: '15px', color: 'var(--text-main)' }}>
                Credenciais de Integração & Acesso
              </div>
              <button
                type="button"
                onClick={copiarTodasCredenciais}
                className="btn btn-primary"
                style={{ fontSize: '12.5px', padding: '6px 14px' }}
              >
                {copiadoCampo === 'todas' ? (
                  <>
                    <Check size={14} /> Copiado!
                  </>
                ) : (
                  <>
                    <Copy size={14} /> Copiar Todas as Credenciais
                  </>
                )}
              </button>
            </div>

            {/* DESTAQUE SINc: TOKEN E CNPJ-BASE */}
            <div style={{
              backgroundColor: '#EFF6FF',
              border: '2px solid #3B82F6',
              borderRadius: 'var(--radius-md)',
              padding: '16px',
              marginBottom: '16px'
            }}>
              <div style={{ fontWeight: 800, fontSize: '14px', color: '#1E40AF', marginBottom: '12px', display: 'flex', alignItems: 'center', gap: '8px' }}>
                <Key size={18} /> CREDENCIAIS PARA O SINCRONIZADOR LOCAL (SINc)
              </div>

              <div style={{ display: 'grid', gridTemplateColumns: '2fr 1fr', gap: '12px' }}>
                <div style={{ backgroundColor: '#FFFFFF', padding: '12px', borderRadius: 'var(--radius-sm)', border: '1px solid #BFDBFE' }}>
                  <div style={{ fontSize: '11px', color: '#1E40AF', fontWeight: 700 }}>TOKEN DE INTEGRAÇÃO DA FILIAL:</div>
                  <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginTop: '4px' }}>
                    <span style={{ fontSize: '14px', fontFamily: 'monospace', fontWeight: 800, color: '#1E3A8A' }}>
                      {sucessoCriacao.tokenFilial}
                    </span>
                    <button
                      type="button"
                      onClick={() => copiarTexto(sucessoCriacao.tokenFilial, 'token')}
                      className="btn btn-secondary"
                      style={{ padding: '3px 8px', fontSize: '11px' }}
                    >
                      {copiadoCampo === 'token' ? <Check size={12} /> : <Copy size={12} />}
                    </button>
                  </div>
                </div>

                <div style={{ backgroundColor: '#FFFFFF', padding: '12px', borderRadius: 'var(--radius-sm)', border: '1px solid #BFDBFE' }}>
                  <div style={{ fontSize: '11px', color: '#1E40AF', fontWeight: 700 }}>CNPJ-BASE (8 DÍGITOS):</div>
                  <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginTop: '4px' }}>
                    <span style={{ fontSize: '14px', fontFamily: 'monospace', fontWeight: 800, color: '#1E3A8A' }}>
                      {obterCnpjBase(sucessoCriacao.cnpj)}
                    </span>
                    <button
                      type="button"
                      onClick={() => copiarTexto(obterCnpjBase(sucessoCriacao.cnpj), 'cnpjBase')}
                      className="btn btn-secondary"
                      style={{ padding: '3px 8px', fontSize: '11px' }}
                    >
                      {copiadoCampo === 'cnpjBase' ? <Check size={12} /> : <Copy size={12} />}
                    </button>
                  </div>
                </div>
              </div>

              <div style={{ fontSize: '12px', color: '#1E40AF', marginTop: '10px' }}>
                💡 <strong>Como usar:</strong> No aplicativo SINc no Windows, abra <em>Configurações &gt; Identificação</em>, cole o <strong>Token</strong> e o <strong>CNPJ-Base</strong> e clique em <em>Validar e Conectar</em>.
              </div>
            </div>

            {/* ACESSO WEB */}
            <div style={{ backgroundColor: '#FFFFFF', padding: '14px', borderRadius: 'var(--radius-sm)', border: '1px solid var(--border-light)', marginBottom: '14px' }}>
              <div style={{ fontWeight: 700, fontSize: '13px', color: 'var(--blue-primary)', marginBottom: '8px', display: 'flex', alignItems: 'center', gap: '6px' }}>
                <ShieldCheck size={16} /> Acesso ao Painel Nexo Compras Web do Cliente
              </div>
              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '12px' }}>
                <div>
                  <div style={{ fontSize: '11px', color: 'var(--text-muted)' }}>E-MAIL DE LOGIN:</div>
                  <div style={{ fontWeight: 600, fontSize: '13px' }}>{sucessoCriacao.usuarioEmail}</div>
                </div>
                <div>
                  <div style={{ fontSize: '11px', color: 'var(--text-muted)' }}>SENHA INICIAL:</div>
                  <div style={{ fontWeight: 700, fontSize: '13px', color: 'var(--green-text)', fontFamily: 'monospace' }}>
                    {sucessoCriacao.senhaInicial}
                  </div>
                </div>
              </div>
            </div>
          </div>

          {/* BOTÕES DE AÇÃO */}
          <div style={{ display: 'flex', justifyContent: 'space-between', gap: '12px' }}>
            <button
              onClick={() => {
                setStep(1);
                setSucessoCriacao(null);
                setFormData({
                  razaoSocial: '',
                  nomeFantasia: '',
                  slug: '',
                  cnpj: '',
                  email: '',
                  telefone: '',
                  planoId: planos.length > 0 ? planos[0].id : '',
                  moduloContratado: 'Basico',
                  nomeFilial: 'Matriz - Centro',
                  codigoFilial: '001',
                  cnpjFilial: '',
                  responsavelNome: '',
                  emailLogin: '',
                  senhaInicial: '',
                  perfilAcesso: 'TenantAdmin',
                  gerarCodigoAtivacao: true,
                });
                gerarSenhaAleatoria();
              }}
              className="btn btn-secondary"
            >
              + Cadastrar Outra Empresa
            </button>
            <button
              onClick={() => navigate('/admin/tenants')}
              className="btn btn-primary"
              style={{ padding: '10px 20px', fontWeight: 700 }}
            >
              Ir para Lista de Empresas <ArrowRight size={16} />
            </button>
          </div>
        </div>
      )}
    </div>
  );
};
