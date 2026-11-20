import React, { useState, useEffect } from 'react';
import { useNavigate, Link } from 'react-router-dom';
import { api } from '../../services/api';
import { useAuth } from '../../contexts/AuthContext';
import { Plano } from '../../types';
import { CnpjInput } from '../../components/CnpjInput';
import { validarCnpj, cleanCnpj } from '../../utils/cnpj';
import { Building2, Check, ArrowRight, ShieldCheck } from 'lucide-react';

export const RegisterTenant: React.FC = () => {
  const [razaoSocial, setRazaoSocial] = useState('');
  const [nomeFantasia, setNomeFantasia] = useState('');
  const [cnpj, setCnpj] = useState('');
  const [emailEmpresa, setEmailEmpresa] = useState('');
  const [telefone, setTelefone] = useState('');

  const [adminNome, setAdminNome] = useState('');
  const [adminEmail, setAdminEmail] = useState('');
  const [adminSenha, setAdminSenha] = useState('');

  const [planos, setPlanos] = useState<Plano[]>([]);
  const [planoId, setPlanoId] = useState<string>('');
  const [loading, setLoading] = useState(false);
  const [erro, setErro] = useState('');

  const { refreshUser } = useAuth();
  const navigate = useNavigate();

  useEffect(() => {
    // Carregar planos disponíveis publicamente
    api.get<Plano[]>('/admin/plans').then((res) => {
      setPlanos(res);
      const prof = res.find((p) => p.nome === 'Profissional') || res[0];
      if (prof) setPlanoId(prof.id);
    }).catch(() => {});
  }, []);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setErro('');
    setLoading(true);

    if (!validarCnpj(cnpj)) {
      setErro('O CNPJ informado é inválido. Verifique os números e dígitos verificadores.');
      setLoading(false);
      return;
    }

    try {
      const res = await api.post<{ token: string }>('/auth/register-tenant', {
        razaoSocial,
        nomeFantasia,
        cnpj: cleanCnpj(cnpj),
        email: emailEmpresa,
        telefone,
        adminNome,
        adminEmail,
        adminSenha,
        planoId: planoId || null,
      });

      api.setToken(res.token);
      await refreshUser();
      navigate('/dashboard');
    } catch (err: any) {
      setErro(err.message || 'Erro ao cadastrar empresa.');
    } finally {
      setLoading(false);
    }
  };

  return (
    <div style={{
      minHeight: '100vh',
      backgroundColor: 'var(--bg-page)',
      padding: '40px 20px',
      display: 'flex',
      justifyContent: 'center'
    }}>
      <div style={{ width: '100%', maxWidth: '800px' }}>
        {/* CABEÇALHO */}
        <div style={{ textAlign: 'center', marginBottom: '32px' }}>
          <div style={{
            width: '46px',
            height: '46px',
            borderRadius: '12px',
            backgroundColor: 'var(--blue-primary)',
            color: '#fff',
            display: 'inline-flex',
            alignItems: 'center',
            justifyContent: 'center',
            fontWeight: 800,
            fontSize: '22px',
            marginBottom: '10px'
          }}>
            N
          </div>
          <h1 style={{ fontSize: '26px', fontWeight: 800, color: 'var(--text-main)' }}>
            Comece seu Teste Grátis no Nexo Compras
          </h1>
          <p style={{ fontSize: '14px', color: 'var(--text-muted)' }}>
            15 dias de acesso completo sem compromisso. Conecte seu ERP e automatize suas cotações.
          </p>
        </div>

        <div className="card">
          {erro && (
            <div style={{
              padding: '12px 16px',
              backgroundColor: 'var(--red-light)',
              color: 'var(--red-text)',
              borderRadius: 'var(--radius-sm)',
              fontSize: '14px',
              marginBottom: '20px'
            }}>
              {erro}
            </div>
          )}

          <form onSubmit={handleSubmit}>
            <h2 style={{ fontSize: '16px', fontWeight: 700, color: 'var(--blue-primary)', borderBottom: '1px solid var(--border-light)', paddingBottom: '8px', marginBottom: '16px' }}>
              1. Dados da Empresa
            </h2>

            <div className="grid-cols-2">
              <div className="form-group">
                <label>Razão Social *</label>
                <input
                  required
                  placeholder="Ex.: Supermercado Bom Preço Ltda"
                  value={razaoSocial}
                  onChange={(e) => setRazaoSocial(e.target.value)}
                />
              </div>

              <div className="form-group">
                <label>Nome Fantasia</label>
                <input
                  placeholder="Ex.: Supermercado Bom Preço"
                  value={nomeFantasia}
                  onChange={(e) => setNomeFantasia(e.target.value)}
                />
              </div>
            </div>

            <div className="grid-cols-3">
              <div className="form-group">
                <CnpjInput
                  label="CNPJ *"
                  required
                  value={cnpj}
                  onChange={setCnpj}
                />
              </div>

              <div className="form-group">
                <label>E-mail da Empresa *</label>
                <input
                  type="email"
                  required
                  placeholder="contato@empresa.com.br"
                  value={emailEmpresa}
                  onChange={(e) => setEmailEmpresa(e.target.value)}
                />
              </div>

              <div className="form-group">
                <label>Telefone / WhatsApp</label>
                <input
                  placeholder="(81) 99999-9999"
                  value={telefone}
                  onChange={(e) => setTelefone(e.target.value)}
                />
              </div>
            </div>

            <h2 style={{ fontSize: '16px', fontWeight: 700, color: 'var(--blue-primary)', borderBottom: '1px solid var(--border-light)', paddingBottom: '8px', marginTop: '24px', marginBottom: '16px' }}>
              2. Administrador da Conta
            </h2>

            <div className="grid-cols-3">
              <div className="form-group">
                <label>Nome Completo *</label>
                <input
                  required
                  placeholder="Seu nome"
                  value={adminNome}
                  onChange={(e) => setAdminNome(e.target.value)}
                />
              </div>

              <div className="form-group">
                <label>E-mail de Login *</label>
                <input
                  type="email"
                  required
                  placeholder="seu.email@empresa.com.br"
                  value={adminEmail}
                  onChange={(e) => setAdminEmail(e.target.value)}
                />
              </div>

              <div className="form-group">
                <label>Criar Senha *</label>
                <input
                  type="password"
                  required
                  placeholder="Mínimo 8 caracteres"
                  value={adminSenha}
                  onChange={(e) => setAdminSenha(e.target.value)}
                />
              </div>
            </div>

            <h2 style={{ fontSize: '16px', fontWeight: 700, color: 'var(--blue-primary)', borderBottom: '1px solid var(--border-light)', paddingBottom: '8px', marginTop: '24px', marginBottom: '16px' }}>
              3. Selecione o Plano de Avaliação
            </h2>

            <div className="grid-cols-3" style={{ marginBottom: '24px' }}>
              {planos.map((p) => {
                const isSelected = planoId === p.id;
                return (
                  <div
                    key={p.id}
                    onClick={() => setPlanoId(p.id)}
                    style={{
                      border: isSelected ? '2px solid var(--blue-primary)' : '1px solid var(--border)',
                      backgroundColor: isSelected ? 'var(--blue-soft)' : '#fff',
                      borderRadius: 'var(--radius-md)',
                      padding: '16px',
                      cursor: 'pointer',
                      transition: 'all 0.15s ease'
                    }}
                  >
                    <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                      <span style={{ fontWeight: 700, fontSize: '15px', color: 'var(--text-main)' }}>
                        {p.nome}
                      </span>
                      {isSelected && (
                        <span className="badge badge-blue">
                          <Check size={12} /> Selecionado
                        </span>
                      )}
                    </div>
                    <div style={{ fontSize: '20px', fontWeight: 800, color: 'var(--blue-primary)', margin: '8px 0' }}>
                      R$ {p.mensalidade.toFixed(2).replace('.', ',')}
                      <span style={{ fontSize: '12px', fontWeight: 500, color: 'var(--text-muted)' }}>/mês</span>
                    </div>
                    <div style={{ fontSize: '12px', color: 'var(--text-muted)', lineHeight: 1.6 }}>
                      • Até {p.maxUsuarios} usuários<br />
                      • {p.maxFiliais} {p.maxFiliais === 1 ? 'filial' : 'filiais'}<br />
                      • {p.maxFornecedores >= 9999 ? 'Fornecedores ilimitados' : `Até ${p.maxFornecedores} fornecedores`}<br />
                      • Integração ERP inclusa
                    </div>
                  </div>
                );
              })}
            </div>

            <button
              type="submit"
              disabled={loading}
              className="btn btn-primary"
              style={{ width: '100%', padding: '12px', fontSize: '16px' }}
            >
              {loading ? 'Criando ambiente da empresa...' : 'Criar Conta e Iniciar Teste Grátis'}
              <ArrowRight size={18} />
            </button>
          </form>

          <div style={{ textAlign: 'center', marginTop: '16px', fontSize: '13px', color: 'var(--text-muted)' }}>
            Já tem uma conta? <Link to="/login" style={{ color: 'var(--blue-primary)', fontWeight: 600 }}>Entrar</Link>
          </div>
        </div>
      </div>
    </div>
  );
};

