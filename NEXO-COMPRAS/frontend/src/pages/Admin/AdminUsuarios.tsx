import React, { useState, useEffect } from 'react';
import { api } from '../../services/api';
import { AdminUsuarioItem } from '../../types';
import { Users, Search, Key, CheckCircle, XCircle, Building2, Copy, Check, RefreshCw } from 'lucide-react';

export const AdminUsuarios: React.FC = () => {
  const [usuarios, setUsuarios] = useState<AdminUsuarioItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [busca, setBusca] = useState('');
  const [senhaRedefinida, setSenhaRedefinida] = useState<{ email: string; novaSenha: string } | null>(null);
  const [copiado, setCopiado] = useState(false);

  const carregarUsuarios = async () => {
    try {
      const res = await api.get<AdminUsuarioItem[]>('/admin/usuarios');
      setUsuarios(res);
    } catch (err: any) {
      alert('Erro ao carregar usuários: ' + err.message);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    carregarUsuarios();
  }, []);

  const handleToggleStatus = async (user: AdminUsuarioItem) => {
    const novoStatus = user.ativo ? 'Inativo' : 'Ativo';
    if (!confirm(`Deseja alterar o status do usuário "${user.email}" para ${novoStatus}?`)) {
      return;
    }

    try {
      await api.put(`/admin/usuarios/${user.id}/status`, { status: novoStatus });
      carregarUsuarios();
    } catch (err: any) {
      alert('Erro ao alterar status: ' + err.message);
    }
  };

  const handleResetarSenha = async (user: AdminUsuarioItem) => {
    if (!confirm(`Deseja redefinir a senha do usuário "${user.email}"? Uma nova senha temporária será gerada imediatamente.`)) {
      return;
    }

    try {
      const res = await api.post<{ usuarioId: string; email: string; novaSenhaTemporaria: string }>(
        `/admin/usuarios/${user.id}/resetar-senha`
      );
      setSenhaRedefinida({
        email: res.email,
        novaSenha: res.novaSenhaTemporaria,
      });
    } catch (err: any) {
      alert('Erro ao redefinir senha: ' + err.message);
    }
  };

  const copiarSenha = () => {
    if (!senhaRedefinida) return;
    navigator.clipboard.writeText(senhaRedefinida.novaSenha);
    setCopiado(true);
    setTimeout(() => setCopiado(false), 2000);
  };

  const usuariosFiltrados = usuarios.filter(
    (u) =>
      u.nome.toLowerCase().includes(busca.toLowerCase()) ||
      u.email.toLowerCase().includes(busca.toLowerCase()) ||
      u.empresaNome?.toLowerCase().includes(busca.toLowerCase())
  );

  return (
    <div>
      <div style={{ marginBottom: '24px' }}>
        <h1 style={{ fontSize: '24px', fontWeight: 800, color: 'var(--text-main)' }}>
          Usuários e Credenciais de Clientes
        </h1>
        <p style={{ fontSize: '14px', color: 'var(--text-muted)' }}>
          Gestão centralizada de contas de operadores, compradores e administradores de todas as empresas clientes.
        </p>
      </div>

      {senhaRedefinida && (
        <div style={{
          marginBottom: '20px',
          padding: '16px 20px',
          backgroundColor: '#F0FDF4',
          border: '1px solid #BBF7D0',
          borderRadius: 'var(--radius-md)',
          display: 'flex',
          justifyContent: 'space-between',
          alignItems: 'center',
          flexWrap: 'wrap',
          gap: '12px'
        }}>
          <div>
            <div style={{ fontWeight: 700, fontSize: '14px', color: '#166534' }}>
              Senha Redefinida com Sucesso!
            </div>
            <div style={{ fontSize: '13px', color: '#14532D', marginTop: '2px' }}>
              Usuário: <strong>{senhaRedefinida.email}</strong> • Nova Senha Provisória: <code style={{ fontWeight: 800, fontSize: '14px', backgroundColor: '#DCFCE7', padding: '2px 8px', borderRadius: '4px' }}>{senhaRedefinida.novaSenha}</code>
            </div>
          </div>
          <div style={{ display: 'flex', gap: '8px' }}>
            <button
              onClick={copiarSenha}
              className="btn btn-green"
              style={{ padding: '6px 14px', fontSize: '12px' }}
            >
              {copiado ? <Check size={14} /> : <Copy size={14} />} {copiado ? 'Copiada!' : 'Copiar Senha'}
            </button>
            <button
              onClick={() => setSenhaRedefinida(null)}
              className="btn btn-secondary"
              style={{ padding: '6px 12px', fontSize: '12px' }}
            >
              Fechar
            </button>
          </div>
        </div>
      )}

      <div className="card">
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '16px', flexWrap: 'wrap', gap: '12px' }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
            <Users size={18} color="var(--blue-primary)" />
            <span style={{ fontWeight: 700, fontSize: '15px' }}>Total: {usuarios.length} usuários</span>
          </div>

          <div style={{ width: '300px', position: 'relative' }}>
            <Search size={16} style={{ position: 'absolute', left: '10px', top: '11px', color: 'var(--text-muted)' }} />
            <input
              placeholder="Buscar por nome, email ou empresa..."
              value={busca}
              onChange={(e) => setBusca(e.target.value)}
              style={{ paddingLeft: '34px' }}
            />
          </div>
        </div>

        <div className="table-container">
          <table>
            <thead>
              <tr>
                <th>Usuário / E-mail</th>
                <th>Empresa Vinculada</th>
                <th>Perfil de Acesso</th>
                <th>Status</th>
                <th>Último Login</th>
                <th style={{ textAlign: 'right' }}>Ações</th>
              </tr>
            </thead>
            <tbody>
              {loading ? (
                <tr>
                  <td colSpan={6} style={{ textAlign: 'center', padding: '30px', color: 'var(--text-muted)' }}>
                    Carregando usuários...
                  </td>
                </tr>
              ) : usuariosFiltrados.length === 0 ? (
                <tr>
                  <td colSpan={6} style={{ textAlign: 'center', padding: '30px', color: 'var(--text-muted)' }}>
                    Nenhum usuário encontrado.
                  </td>
                </tr>
              ) : (
                usuariosFiltrados.map((u) => (
                  <tr key={u.id}>
                    <td>
                      <div style={{ fontWeight: 600, color: 'var(--text-main)' }}>{u.nome}</div>
                      <div style={{ fontSize: '12px', color: 'var(--text-muted)' }}>{u.email}</div>
                    </td>
                    <td>
                      <div style={{ display: 'flex', alignItems: 'center', gap: '6px', fontWeight: 500 }}>
                        <Building2 size={14} color="var(--blue-primary)" />
                        {u.empresaNome}
                      </div>
                    </td>
                    <td>
                      <span className={`badge ${u.perfil === 'PlatformAdmin' ? 'badge-yellow' : 'badge-blue'}`}>
                        {u.perfil === 'PlatformAdmin' ? 'Super Admin SaaS' : u.perfil === 'TenantAdmin' ? 'Administrador Loja' : u.perfil}
                      </span>
                    </td>
                    <td>
                      <span className={`badge ${u.ativo ? 'badge-green' : 'badge-red'}`}>
                        {u.ativo ? 'Ativo' : 'Inativo'}
                      </span>
                    </td>
                    <td>
                      <div style={{ fontSize: '12px', color: 'var(--text-muted)' }}>
                        {u.ultimoLogin ? new Date(u.ultimoLogin).toLocaleString('pt-BR') : 'Nunca acessou'}
                      </div>
                    </td>
                    <td style={{ textAlign: 'right' }}>
                      <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '6px' }}>
                        <button
                          type="button"
                          onClick={() => handleResetarSenha(u)}
                          className="btn btn-secondary"
                          title="Redefinir senha do usuário"
                          style={{ padding: '4px 8px', fontSize: '12px' }}
                        >
                          <Key size={13} /> Resetar Senha
                        </button>
                        {u.perfil !== 'PlatformAdmin' && (
                          <button
                            type="button"
                            onClick={() => handleToggleStatus(u)}
                            className={`btn ${u.ativo ? 'btn-danger' : 'btn-green'}`}
                            style={{ padding: '4px 8px', fontSize: '12px' }}
                          >
                            {u.ativo ? <XCircle size={13} /> : <CheckCircle size={13} />}
                          </button>
                        )}
                      </div>
                    </td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>
      </div>
    </div>
  );
};

