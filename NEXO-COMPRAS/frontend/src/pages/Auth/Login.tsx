import React, { useState } from 'react';
import { useNavigate, Link } from 'react-router-dom';
import { useAuth } from '../../contexts/AuthContext';
import { ShieldCheck, Building2, Lock, Mail, ArrowRight } from 'lucide-react';

export const Login: React.FC = () => {
  const [email, setEmail] = useState('');
  const [senha, setSenha] = useState('');
  const [erro, setErro] = useState('');
  const [loading, setLoading] = useState(false);
  const { login } = useAuth();
  const navigate = useNavigate();

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setErro('');
    setLoading(true);

    try {
      const res = await login(email, senha);
      if (res.user.perfil === 'PlatformAdmin') {
        navigate('/admin');
      } else {
        navigate('/dashboard');
      }
    } catch (err: any) {
      setErro(err.message || 'Erro ao efetuar login.');
    } finally {
      setLoading(false);
    }
  };

  const setCredentials = (e: string, s: string) => {
    setEmail(e);
    setSenha(s);
  };

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
            Nexo Compras
          </h1>
          <p style={{ fontSize: '14px', color: 'var(--text-muted)', marginTop: '4px' }}>
            Plataforma SaaS de Gestão Inteligente de Compras
          </p>
        </div>

        {/* CARD DE LOGIN */}
        <div className="card">
          <h2 style={{ fontSize: '18px', fontWeight: 700, marginBottom: '6px' }}>
            Entrar na plataforma
          </h2>
          <p style={{ fontSize: '13px', color: 'var(--text-muted)', marginBottom: '20px' }}>
            Acesse com seu e-mail corporativo ou credencial de administrador
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

          {/* ACESSO ADMINISTRATIVO DA PLATAFORMA */}
          <div style={{ marginTop: '24px', paddingTop: '16px', borderTop: '1px solid var(--border-light)' }}>
            <div style={{ fontSize: '11px', fontWeight: 700, color: 'var(--text-muted)', textTransform: 'uppercase', marginBottom: '8px' }}>
              Acesso Administrativo da Plataforma
            </div>
            <div style={{ display: 'flex', gap: '8px' }}>
              <button
                type="button"
                onClick={() => setCredentials('admin@nexocompras.com.br', 'Admin@Nexo2026!')}
                className="btn btn-secondary"
                style={{ flex: 1, fontSize: '12px', padding: '6px 8px' }}
              >
                <ShieldCheck size={14} style={{ color: 'var(--yellow-text)' }} />
                Administrador Geral (Nexo Admin)
              </button>
            </div>
          </div>
        </div>

        {/* ONBOARDING */}
        <div style={{ textAlign: 'center', marginTop: '20px', fontSize: '14px', color: 'var(--text-muted)' }}>
          Sua empresa ainda não utiliza o Nexo Compras?{' '}
          <Link to="/cadastro" style={{ color: 'var(--blue-primary)', fontWeight: 600 }}>
            Criar conta (Teste Grátis)
          </Link>
        </div>
      </div>
    </div>
  );
};

