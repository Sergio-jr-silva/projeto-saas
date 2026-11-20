import React, { useState, useEffect } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { useAuth } from '../../contexts/AuthContext';
import { api } from '../../services/api';
import { Lock, Mail, ArrowRight, Building2, AlertTriangle, ShieldAlert } from 'lucide-react';

interface ResolvedCompanyInfo {
  id: string;
  slug: string;
  razaoSocial: string;
  nomeFantasia: string;
  status: string;
  logoUrl?: string;
}

export const Login: React.FC = () => {
  const { slug } = useParams<{ slug?: string }>();
  const [email, setEmail] = useState('');
  const [senha, setSenha] = useState('');
  const [erro, setErro] = useState('');
  const [loading, setLoading] = useState(false);

  const [companyInfo, setCompanyInfo] = useState<ResolvedCompanyInfo | null>(null);
  const [resolvingSlug, setResolvingSlug] = useState(false);
  const [slugError, setSlugError] = useState<{ codigo?: string; mensagem: string; nomeFantasia?: string } | null>(null);

  const { login } = useAuth();
  const navigate = useNavigate();

  useEffect(() => {
    if (!slug) {
      setCompanyInfo(null);
      setSlugError(null);
      return;
    }

    let active = true;
    setResolvingSlug(true);
    setSlugError(null);

    api
      .get<ResolvedCompanyInfo>(`/tenants/resolve/${slug}`)
      .then((res) => {
        if (!active) return;
        setCompanyInfo(res);
      })
      .catch((err: any) => {
        if (!active) return;
        setCompanyInfo(null);
        setSlugError({
          codigo: err.codigo || (err.status === 404 ? 'SLUG_INEXISTENTE' : 'ERRO'),
          mensagem: err.message || 'Empresa não encontrada.',
          nomeFantasia: err.data?.nomeFantasia,
        });
      })
      .finally(() => {
        if (active) setResolvingSlug(false);
      });

    return () => {
      active = false;
    };
  }, [slug]);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setErro('');
    setLoading(true);

    try {
      const res = await login(email, senha, slug);
      if (res.user.perfil === 'PlatformAdmin' || res.user.perfil === 'AdministradorGeral') {
        navigate('/admin');
      } else {
        const targetSlug = res.tenant?.slug || slug;
        if (targetSlug) {
          navigate(`/${targetSlug}/dashboard`);
        } else {
          navigate('/dashboard');
        }
      }
    } catch (err: any) {
      setErro(err.message || 'Erro ao efetuar login.');
    } finally {
      setLoading(false);
    }
  };

  if (resolvingSlug) {
    return (
      <div style={{ minHeight: '100vh', display: 'flex', alignItems: 'center', justifyContent: 'center', backgroundColor: 'var(--bg-page)' }}>
        <div className="card" style={{ maxWidth: '420px', textAlign: 'center', padding: '32px' }}>
          Carregando ambiente corporativo <strong>/{slug}</strong>...
        </div>
      </div>
    );
  }

  if (slug && slugError) {
    const isSuspensa = slugError.codigo === 'EMPRESA_SUSPENSA' || slugError.codigo === 'EMPRESA_INATIVA';
    return (
      <div style={{ minHeight: '100vh', display: 'flex', alignItems: 'center', justifyContent: 'center', backgroundColor: 'var(--bg-page)', padding: '20px' }}>
        <div className="card" style={{ maxWidth: '460px', width: '100%', textAlign: 'center', padding: '32px 24px' }}>
          <div style={{
            width: '56px',
            height: '56px',
            borderRadius: '14px',
            backgroundColor: isSuspensa ? '#FEF3C7' : '#FEE2E2',
            color: isSuspensa ? '#D97706' : '#DC2626',
            display: 'inline-flex',
            alignItems: 'center',
            justifyContent: 'center',
            marginBottom: '16px'
          }}>
            {isSuspensa ? <ShieldAlert size={28} /> : <AlertTriangle size={28} />}
          </div>
          <h2 style={{ fontSize: '20px', fontWeight: 800, color: 'var(--text-main)', marginBottom: '8px' }}>
            {isSuspensa
              ? `Acesso Temporariamente Suspenso`
              : `Endereço Não Encontrado (404)`}
          </h2>
          {slugError.nomeFantasia && (
            <div style={{ fontSize: '14px', fontWeight: 700, color: 'var(--blue-primary)', marginBottom: '8px' }}>
              {slugError.nomeFantasia} (/{slug})
            </div>
          )}
          <p style={{ fontSize: '14px', color: 'var(--text-muted)', lineHeight: 1.6, marginBottom: '22px' }}>
            {slugError.mensagem}
          </p>
          <button
            onClick={() => navigate('/login')}
            className="btn btn-primary"
            style={{ width: '100%', padding: '10px', fontSize: '14px' }}
          >
            Ir para o Login Geral
          </button>
        </div>
      </div>
    );
  }

  return (
    <div style={{
      minHeight: '100vh',
      display: 'flex',
      alignItems: 'center',
      justifyContent: 'center',
      backgroundColor: 'var(--bg-page)',
      padding: '20px'
    }}>
      <div style={{ width: '100%', maxWidth: '440px' }}>
        {/* CABEÇALHO */}
        <div style={{ textAlign: 'center', marginBottom: '28px' }}>
          <div style={{
            width: '52px',
            height: '52px',
            borderRadius: '14px',
            backgroundColor: 'var(--blue-primary)',
            color: '#fff',
            display: 'inline-flex',
            alignItems: 'center',
            justifyContent: 'center',
            fontWeight: 800,
            fontSize: '26px',
            boxShadow: 'var(--shadow-md)',
            marginBottom: '12px'
          }}>
            N
          </div>
          <h1 style={{ fontSize: '24px', fontWeight: 800, color: 'var(--text-main)' }}>
            {companyInfo ? companyInfo.nomeFantasia : 'Nexo Compras'}
          </h1>
          <p style={{ fontSize: '14px', color: 'var(--text-muted)', marginTop: '4px' }}>
            {companyInfo
              ? `Ambiente Corporativo Exclusivo • nexoscompras.com.br/${companyInfo.slug}`
              : 'Plataforma SaaS de Gestão Inteligente de Compras'}
          </p>
        </div>

        {/* CARD DE LOGIN */}
        <div className="card">
          {companyInfo && (
            <div style={{
              display: 'flex',
              alignItems: 'center',
              gap: '10px',
              padding: '10px 12px',
              backgroundColor: '#EFF6FF',
              border: '1px solid #BFDBFE',
              borderRadius: 'var(--radius-sm)',
              marginBottom: '16px',
              fontSize: '13px',
              color: '#1E40AF'
            }}>
              <Building2 size={17} />
              <div>
                <div style={{ fontWeight: 700 }}>{companyInfo.razaoSocial}</div>
                <div style={{ fontSize: '11.5px', opacity: 0.85 }}>Rota autenticada: /{companyInfo.slug}</div>
              </div>
            </div>
          )}

          <h2 style={{ fontSize: '18px', fontWeight: 700, marginBottom: '6px' }}>
            Entrar na plataforma
          </h2>
          <p style={{ fontSize: '13px', color: 'var(--text-muted)', marginBottom: '20px' }}>
            Acesse com suas credenciais autorizadas
          </p>

          {erro && (
            <div style={{
              padding: '10px 14px',
              backgroundColor: 'var(--red-light)',
              color: 'var(--red-text)',
              borderRadius: 'var(--radius-sm)',
              fontSize: '13px',
              fontWeight: 500,
              marginBottom: '16px'
            }}>
              {erro}
            </div>
          )}

          <form onSubmit={handleSubmit}>
            <div className="form-group">
              <label>E-mail corporativo</label>
              <div style={{ position: 'relative' }}>
                <Mail size={16} style={{ position: 'absolute', left: '12px', top: '12px', color: 'var(--text-muted)' }} />
                <input
                  type="email"
                  required
                  placeholder="seu.email@empresa.com.br"
                  value={email}
                  onChange={(e) => setEmail(e.target.value)}
                  style={{ paddingLeft: '38px' }}
                />
              </div>
            </div>

            <div className="form-group">
              <label>Senha de acesso</label>
              <div style={{ position: 'relative' }}>
                <Lock size={16} style={{ position: 'absolute', left: '12px', top: '12px', color: 'var(--text-muted)' }} />
                <input
                  type="password"
                  required
                  placeholder="••••••••"
                  value={senha}
                  onChange={(e) => setSenha(e.target.value)}
                  style={{ paddingLeft: '38px' }}
                />
              </div>
            </div>

            <button
              type="submit"
              disabled={loading}
              className="btn btn-primary"
              style={{ width: '100%', padding: '10px', fontSize: '15px', marginTop: '8px' }}
            >
              {loading ? 'Validando credenciais...' : 'Acessar Conta'}
              <ArrowRight size={16} />
            </button>
          </form>
        </div>
      </div>
    </div>
  );
};
