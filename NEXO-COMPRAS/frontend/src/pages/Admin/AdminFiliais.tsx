import React, { useState, useEffect } from 'react';
import { api } from '../../services/api';
import { AdminFilialItem } from '../../types';
import { Store, Search, Copy, Check, Building2, Key } from 'lucide-react';

export const AdminFiliais: React.FC = () => {
  const [filiais, setFiliais] = useState<AdminFilialItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [busca, setBusca] = useState('');
  const [copiadoToken, setCopiadoToken] = useState<string | null>(null);

  const carregarFiliais = async () => {
    try {
      const res = await api.get<AdminFilialItem[]>('/admin/filiais');
      setFiliais(res);
    } catch (err: any) {
      alert('Erro ao carregar filiais: ' + err.message);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    carregarFiliais();
  }, []);

  const copiarToken = (token: string) => {
    navigator.clipboard.writeText(token);
    setCopiadoToken(token);
    setTimeout(() => setCopiadoToken(null), 2000);
  };

  const filiaisFiltradas = filiais.filter(
    (f) =>
      f.nome.toLowerCase().includes(busca.toLowerCase()) ||
      f.empresaNome?.toLowerCase().includes(busca.toLowerCase()) ||
      f.codigo.toLowerCase().includes(busca.toLowerCase()) ||
      f.cnpj.includes(busca)
  );

  return (
    <div>
      <div style={{ marginBottom: '24px' }}>
        <h1 style={{ fontSize: '24px', fontWeight: 800, color: 'var(--text-main)' }}>
          Filiais Globais Cadastradas
        </h1>
        <p style={{ fontSize: '14px', color: 'var(--text-muted)' }}>
          Visão unificada de todas as filiais e lojas físicas de clientes, seus tokens de integração e códigos de sincronização.
        </p>
      </div>

      <div className="card">
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '16px', flexWrap: 'wrap', gap: '12px' }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
            <Store size={18} color="var(--blue-primary)" />
            <span style={{ fontWeight: 700, fontSize: '15px' }}>Total: {filiais.length} filiais registradas</span>
          </div>

          <div style={{ width: '300px', position: 'relative' }}>
            <Search size={16} style={{ position: 'absolute', left: '10px', top: '11px', color: 'var(--text-muted)' }} />
            <input
              placeholder="Buscar por filial, empresa ou CNPJ..."
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
                <th>Filial / Código</th>
                <th>Empresa Cliente</th>
                <th>CNPJ / Localização</th>
                <th>Token da Filial (FLK)</th>
                <th>Status</th>
              </tr>
            </thead>
            <tbody>
              {loading ? (
                <tr>
                  <td colSpan={5} style={{ textAlign: 'center', padding: '30px', color: 'var(--text-muted)' }}>
                    Carregando filiais...
                  </td>
                </tr>
              ) : filiaisFiltradas.length === 0 ? (
                <tr>
                  <td colSpan={5} style={{ textAlign: 'center', padding: '30px', color: 'var(--text-muted)' }}>
                    {filiais.length === 0
                      ? 'Nenhuma filial cadastrada na plataforma ainda. Use o cadastro de empresas para provisionar novas lojas.'
                      : 'Nenhuma filial encontrada com os termos de busca informados.'}
                  </td>
                </tr>
              ) : (
                filiaisFiltradas.map((f) => (
                  <tr key={f.id}>
                    <td>
                      <div style={{ fontWeight: 600, color: 'var(--text-main)' }}>{f.nome}</div>
                      <div style={{ fontSize: '12px', color: 'var(--text-muted)' }}>
                        Código ERP: <span style={{ fontFamily: 'monospace', fontWeight: 600 }}>{f.codigo}</span>
                      </div>
                    </td>
                    <td>
                      <div style={{ display: 'flex', alignItems: 'center', gap: '6px', fontWeight: 500 }}>
                        <Building2 size={14} color="var(--blue-primary)" />
                        {f.empresaNome || 'Empresa'}
                      </div>
                    </td>
                    <td>
                      <div>{f.cnpj || '—'}</div>
                      {(f.cidade || f.uf) && (
                        <div style={{ fontSize: '11px', color: 'var(--text-muted)' }}>
                          {f.cidade} / {f.uf}
                        </div>
                      )}
                    </td>
                    <td>
                      <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
                        <code style={{ fontSize: '12px', backgroundColor: '#F1F5F9', padding: '3px 6px', borderRadius: '4px' }}>
                          {f.token}
                        </code>
                        <button
                          type="button"
                          onClick={() => copiarToken(f.token)}
                          title="Copiar token"
                          style={{ background: 'none', border: 'none', cursor: 'pointer', color: 'var(--text-muted)', padding: '4px' }}
                        >
                          {copiadoToken === f.token ? <Check size={14} color="var(--green)" /> : <Copy size={14} />}
                        </button>
                      </div>
                    </td>
                    <td>
                      <span className={`badge ${f.ativa ? 'badge-green' : 'badge-red'}`}>
                        {f.ativa ? 'Ativa' : 'Inativa'}
                      </span>
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

