import React, { useState, useEffect } from 'react';
import { api } from '../../services/api';
import { AdminSincronizadorItem } from '../../types';
import { RefreshCw, Search, ShieldCheck, ShieldAlert, Key, Check, Copy, Building2, HardDrive } from 'lucide-react';

export const AdminSincronizadores: React.FC = () => {
  const [agentes, setAgentes] = useState<AdminSincronizadorItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [busca, setBusca] = useState('');
  const [novoCodigo, setNovoCodigo] = useState<{ agenteId: string; codigo: string; expiraEm: string } | null>(null);
  const [copiado, setCopiado] = useState(false);

  const carregarAgentes = async () => {
    try {
      const res = await api.get<AdminSincronizadorItem[]>('/admin/sincronizadores');
      setAgentes(res);
    } catch (err: any) {
      alert('Erro ao carregar sincronizadores: ' + err.message);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    carregarAgentes();
  }, []);

  const handleGerarNovoCodigo = async (agente: AdminSincronizadorItem) => {
    if (!confirm(`Gerar novo código de ativação temporário (48h) para "${agente.nome}"?`)) {
      return;
    }

    try {
      const res = await api.post<{ codigoAtivacao: string; expiraEm: string }>(
        `/admin/sincronizadores/${agente.id}/gerar-codigo-ativacao`
      );
      setNovoCodigo({
        agenteId: agente.id,
        codigo: res.codigoAtivacao,
        expiraEm: res.expiraEm,
      });
      carregarAgentes();
    } catch (err: any) {
      alert('Erro ao gerar código: ' + err.message);
    }
  };

  const handleRevogar = async (agente: AdminSincronizadorItem) => {
    if (!confirm(`Revogar permanentemente as credenciais do sincronizador "${agente.nome}"? O agente deixará de sincronizar imediatamente.`)) {
      return;
    }

    try {
      await api.post(`/admin/sincronizadores/${agente.id}/revogar`);
      // Atualização imediata do estado local sem necessidade de recarregar a página
      setAgentes((prev) =>
        prev.map((a) => (a.id === agente.id ? { ...a, revogado: true, status: 'Revogado' } : a))
      );
      carregarAgentes();
    } catch (err: any) {
      alert('Erro ao revogar sincronizador: ' + (err.response?.data?.erro || err.message));
    }
  };

  const copiarCodigo = () => {
    if (!novoCodigo) return;
    navigator.clipboard.writeText(novoCodigo.codigo);
    setCopiado(true);
    setTimeout(() => setCopiado(false), 2000);
  };

  const agentesFiltrados = agentes.filter(
    (a) =>
      a.nome.toLowerCase().includes(busca.toLowerCase()) ||
      a.empresaNome?.toLowerCase().includes(busca.toLowerCase()) ||
      a.tipoBanco.toLowerCase().includes(busca.toLowerCase()) ||
      a.status.toLowerCase().includes(busca.toLowerCase())
  );

  return (
    <div>
      <div style={{ marginBottom: '24px' }}>
        <h1 style={{ fontSize: '24px', fontWeight: 800, color: 'var(--text-main)' }}>
          Sincronizadores Locais (SINc)
        </h1>
        <p style={{ fontSize: '14px', color: 'var(--text-muted)' }}>
          Monitoramento de status, saúde de comunicação, tokens e emissão de códigos de ativação para os aplicativos desktop dos clientes.
        </p>
      </div>

      {novoCodigo && (
        <div style={{
          marginBottom: '20px',
          padding: '16px 20px',
          backgroundColor: '#F0F9FF',
          border: '1px solid #BAE6FD',
          borderRadius: 'var(--radius-md)',
          display: 'flex',
          justifyContent: 'space-between',
          alignItems: 'center',
          flexWrap: 'wrap',
          gap: '12px'
        }}>
          <div>
            <div style={{ fontWeight: 700, fontSize: '14px', color: '#0369A1' }}>
              Novo Código de Ativação Gerado (Válido por 48 horas)
            </div>
            <div style={{ fontSize: '13px', color: '#0C4A6E', marginTop: '2px' }}>
              Código de Ativação: <code style={{ fontWeight: 800, fontSize: '15px', backgroundColor: '#E0F2FE', padding: '2px 8px', borderRadius: '4px' }}>{novoCodigo.codigo}</code> • Expira em: {new Date(novoCodigo.expiraEm).toLocaleString('pt-BR')}
            </div>
          </div>
          <div style={{ display: 'flex', gap: '8px' }}>
            <button
              onClick={copiarCodigo}
              className="btn btn-primary"
              style={{ padding: '6px 14px', fontSize: '12px' }}
            >
              {copiado ? <Check size={14} /> : <Copy size={14} />} {copiado ? 'Copiado!' : 'Copiar Código'}
            </button>
            <button
              onClick={() => setNovoCodigo(null)}
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
            <RefreshCw size={18} color="var(--blue-primary)" />
            <span style={{ fontWeight: 700, fontSize: '15px' }}>Total: {agentes.length} sincronizadores registrados</span>
          </div>

          <div style={{ width: '300px', position: 'relative' }}>
            <Search size={16} style={{ position: 'absolute', left: '10px', top: '11px', color: 'var(--text-muted)' }} />
            <input
              placeholder="Buscar por sincronizador, empresa ou banco..."
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
                <th>Sincronizador / Instalação</th>
                <th>Empresa Cliente</th>
                <th>Conector ERP</th>
                <th>Status</th>
                <th>Última Comunicação</th>
                <th style={{ textAlign: 'right' }}>Ações</th>
              </tr>
            </thead>
            <tbody>
              {loading ? (
                <tr>
                  <td colSpan={6} style={{ textAlign: 'center', padding: '30px', color: 'var(--text-muted)' }}>
                    Carregando sincronizadores...
                  </td>
                </tr>
              ) : agentesFiltrados.length === 0 ? (
                <tr>
                  <td colSpan={6} style={{ textAlign: 'center', padding: '30px', color: 'var(--text-muted)' }}>
                    Nenhum sincronizador cadastrado na plataforma ainda.
                  </td>
                </tr>
              ) : (
                agentesFiltrados.map((a) => (
                  <tr key={a.id}>
                    <td>
                      <div style={{ fontWeight: 600, color: 'var(--text-main)' }}>{a.nome}</div>
                      <div style={{ fontSize: '11px', color: 'var(--text-muted)' }}>
                        Máquina: {a.nomeMaquina || 'Aguardando primeiro vínculo'} {a.versao ? `• v${a.versao}` : ''}
                      </div>
                    </td>
                    <td>
                      <div style={{ display: 'flex', alignItems: 'center', gap: '6px', fontWeight: 500 }}>
                        <Building2 size={14} color="var(--blue-primary)" />
                        {a.empresaNome}
                      </div>
                    </td>
                    <td>
                      <div style={{ display: 'flex', alignItems: 'center', gap: '6px' }}>
                        <HardDrive size={14} color="var(--text-muted)" />
                        <span style={{ fontWeight: 600 }}>{a.tipoBanco}</span>
                      </div>
                    </td>
                    <td>
                      {a.revogado ? (
                        <span className="badge badge-red">Revogado</span>
                      ) : a.status === 'Online' ? (
                        <span className="badge badge-green">Online</span>
                      ) : a.status === 'PendenteAtivacao' ? (
                        <span className="badge badge-yellow">Pendente Ativação</span>
                      ) : (
                        <span className="badge badge-red">Offline</span>
                      )}
                    </td>
                    <td>
                      <div style={{ fontSize: '12px' }}>
                        {a.ultimoHeartbeat ? new Date(a.ultimoHeartbeat).toLocaleString('pt-BR') : 'Sem registros'}
                      </div>
                      {a.ultimaSincronizacao && (
                        <div style={{ fontSize: '11px', color: 'var(--text-muted)' }}>
                          Sinc: {new Date(a.ultimaSincronizacao).toLocaleString('pt-BR')}
                        </div>
                      )}
                    </td>
                    <td style={{ textAlign: 'right' }}>
                      <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '6px' }}>
                        {!a.revogado && (
                          <>
                            <button
                              type="button"
                              onClick={() => handleGerarNovoCodigo(a)}
                              className="btn btn-secondary"
                              title="Gerar novo código temporário de ativação"
                              style={{ padding: '4px 8px', fontSize: '12px' }}
                            >
                              <Key size={13} /> Gerar Código
                            </button>
                            <button
                              type="button"
                              onClick={() => handleRevogar(a)}
                              className="btn btn-danger"
                              title="Revogar credenciais deste agente"
                              style={{ padding: '4px 8px', fontSize: '12px' }}
                            >
                              Revogar
                            </button>
                          </>
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

