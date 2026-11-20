import React, { useState, useEffect } from 'react';
import { api } from '../../services/api';
import { User, TenantSummary } from '../../types';
import { useAuth } from '../../contexts/AuthContext';
import { Users, Plus, ShieldCheck, Mail, Lock } from 'lucide-react';

export const Usuarios: React.FC = () => {
  const { tenant } = useAuth();
  const [users, setUsers] = useState<User[]>([]);
  const [loading, setLoading] = useState(true);
  const [showModal, setShowModal] = useState(false);

  // Form
  const [nome, setNome] = useState('');
  const [email, setEmail] = useState('');
  const [senha, setSenha] = useState('');
  const [perfil, setPerfil] = useState('Buyer');
  const [erro, setErro] = useState('');

  const loadUsers = async () => {
    try {
      const res = await api.get<User[]>('/tenant/users');
      setUsers(res);
    } catch (err: any) {
      alert('Erro ao carregar usuários: ' + err.message);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadUsers();
  }, []);

  const handleCreate = async (e: React.FormEvent) => {
    e.preventDefault();
    setErro('');
    try {
      await api.post('/tenant/users', {
        nome,
        email,
        senha,
        perfil,
      });

      setShowModal(false);
      setNome('');
      setEmail('');
      setSenha('');
      loadUsers();
    } catch (err: any) {
      setErro(err.message || 'Erro ao adicionar usuário.');
    }
  };

  return (
    <div>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '20px', flexWrap: 'wrap', gap: '12px' }}>
        <div>
          <h1 style={{ fontSize: '24px', fontWeight: 800, color: 'var(--text-main)' }}>
            Usuários e Permissões
          </h1>
          <p style={{ fontSize: '14px', color: 'var(--text-muted)' }}>
            Equipe autorizada a acessar o ambiente de compras da empresa ({users.length} usuários).
          </p>
        </div>

        <button onClick={() => setShowModal(true)} className="btn btn-primary">
          <Plus size={16} /> Adicionar Novo Usuário
        </button>
      </div>

      <div className="card">
        <div className="table-container">
          <table>
            <thead>
              <tr>
                <th>Nome Completo</th>
                <th>E-mail de Acesso</th>
                <th>Perfil de Acesso</th>
                <th>Status</th>
              </tr>
            </thead>
            <tbody>
              {loading ? (
                <tr>
                  <td colSpan={4} style={{ textAlign: 'center', padding: '24px', color: 'var(--text-muted)' }}>
                    Carregando usuários...
                  </td>
                </tr>
              ) : (
                users.map((u) => (
                  <tr key={u.id}>
                    <td>
                      <div style={{ fontWeight: 600 }}>{u.nome}</div>
                    </td>
                    <td>{u.email}</td>
                    <td>
                      <span className={`badge ${u.perfil === 'TenantAdmin' ? 'badge-blue' : u.perfil === 'Buyer' ? 'badge-green' : 'badge-gray'}`}>
                        {u.perfil === 'TenantAdmin' ? 'Administrador da Loja' : u.perfil === 'Buyer' ? 'Comprador' : 'Visualizador'}
                      </span>
                    </td>
                    <td>
                      <span className="badge badge-green">Ativo</span>
                    </td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>
      </div>

      {showModal && (
        <div style={{
          position: 'fixed',
          inset: 0,
          backgroundColor: 'rgba(30, 41, 59, 0.4)',
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'center',
          zIndex: 50,
          padding: '20px'
        }}>
          <div className="card" style={{ width: '100%', maxWidth: '500px' }}>
            <h2 style={{ fontSize: '18px', fontWeight: 700, marginBottom: '16px' }}>
              Adicionar Usuário à Equipe
            </h2>

            {erro && (
              <div style={{
                padding: '10px 14px',
                backgroundColor: 'var(--red-light)',
                color: 'var(--red-text)',
                borderRadius: 'var(--radius-sm)',
                fontSize: '13px',
                marginBottom: '16px'
              }}>
                {erro}
              </div>
            )}

            <form onSubmit={handleCreate}>
              <div className="form-group">
                <label>Nome Completo *</label>
                <input
                  required
                  placeholder="Ex.: Maria Souza"
                  value={nome}
                  onChange={(e) => setNome(e.target.value)}
                />
              </div>

              <div className="form-group">
                <label>E-mail *</label>
                <input
                  type="email"
                  required
                  placeholder="maria.souza@empresa.com.br"
                  value={email}
                  onChange={(e) => setEmail(e.target.value)}
                />
              </div>

              <div className="form-group">
                <label>Senha Inicial *</label>
                <input
                  type="password"
                  required
                  placeholder="••••••••"
                  value={senha}
                  onChange={(e) => setSenha(e.target.value)}
                />
              </div>

              <div className="form-group">
                <label>Perfil de Acesso</label>
                <select value={perfil} onChange={(e) => setPerfil(e.target.value)}>
                  <option value="Buyer">Comprador (Cria cotações e negocia com fornecedores)</option>
                  <option value="Viewer">Visualizador (Apenas consulta relatórios e cotações)</option>
                  <option value="TenantAdmin">Administrador (Acesso total às configurações)</option>
                </select>
              </div>

              <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '10px', marginTop: '20px' }}>
                <button type="button" onClick={() => setShowModal(false)} className="btn btn-secondary">
                  Cancelar
                </button>
                <button type="submit" className="btn btn-primary">
                  Criar Usuário
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
};

