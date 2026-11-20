import React, { useState, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { api } from '../../services/api';
import { AdminDashboard as IAdminDashboard, Plano, TenantSummary } from '../../types';
import {
  Building2,
  DollarSign,
  Users,
  ShieldAlert,
  CheckCircle,
  XCircle,
  Search,
  PlusCircle,
  Store,
  RefreshCw,
  AlertTriangle,
  Server,
  Key
} from 'lucide-react';

export const AdminDashboard: React.FC = () => {
  const navigate = useNavigate();
  const [data, setData] = useState<IAdminDashboard | null>(null);
  const [tenants, setTenants] = useState<TenantSummary[]>([]);
  const [planos, setPlanos] = useState<Plano[]>([]);
  const [busca, setBusca] = useState('');
  const [loading, setLoading] = useState(true);

  const loadData = async () => {
    try {
      const [dash, listTenants, listPlans] = await Promise.all([
        api.get<IAdminDashboard>('/admin/dashboard'),
        api.get<TenantSummary[]>('/admin/tenants'),
        api.get<Plano[]>('/admin/plans'),
      ]);
      setData(dash);
      setTenants(listTenants);
      setPlanos(listPlans);
    } catch (err: any) {
      alert('Erro ao carregar dados administrativos: ' + err.message);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadData();
  }, []);

  const handleToggleStatus = async (tenant: TenantSummary) => {
    const novoStatus = tenant.status === 'Ativo' ? 'Bloqueado' : 'Ativo';
    if (!confirm(`Deseja alterar o status da empresa "${tenant.nomeFantasia}" para ${novoStatus}?`)) {
      return;
    }

    try {
      await api.put(`/admin/tenants/${tenant.id}/status`, { status: novoStatus });
      loadData();
    } catch (err: any) {
      alert('Erro ao alterar status: ' + err.message);
    }
  };

  const filteredTenants = tenants.filter(
    (t) =>
      t.razaoSocial.toLowerCase().includes(busca.toLowerCase()) ||
      t.nomeFantasia.toLowerCase().includes(busca.toLowerCase()) ||
      t.cnpj.includes(busca)
  );

  if (loading) {
    return <div style={{ padding: '40px', textAlign: 'center' }}>Carregando métricas globais do SaaS...</div>;
  }

  const totalEmpresas = data?.totalEmpresas ?? data?.totalTenants ?? 0;
  const empresasAtivas = data?.empresasAtivas ?? data?.tenantsAtivos ?? 0;
  const empresasBloqueadas = data?.empresasBloqueadas ?? data?.tenantsBloqueados ?? 0;

  return (
    <div>
      {/* CABEÇALHO COM AÇÃO PRINCIPAL */}
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', marginBottom: '24px', flexWrap: 'wrap', gap: '16px' }}>
        <div>
          <h1 style={{ fontSize: '24px', fontWeight: 800, color: 'var(--text-main)' }}>
            Painel Administrativo do SaaS
          </h1>
          <p style={{ fontSize: '14px', color: 'var(--text-muted)' }}>
            Centro de controle da plataforma Nexo Compras: empresas clientes, filiais, infraestrutura e monitoramento.
          </p>
        </div>

        <button
          onClick={() => navigate('/admin/empresas/nova')}
          className="btn btn-primary"
          style={{ padding: '9px 18px', fontSize: '14px', fontWeight: 700 }}
        >
          <PlusCircle size={16} /> Cadastrar Nova Empresa
        </button>
      </div>

      {/* ALERTA DE FALHAS DE SINCRONIZAÇÃO RECENTES (SE HOUVER) */}
      {data?.falhasRecentes && data.falhasRecentes.length > 0 && (
        <div style={{
          backgroundColor: '#FFFBEB',
          border: '1px solid #FDE68A',
          borderRadius: 'var(--radius-md)',
          padding: '16px 20px',
          marginBottom: '24px'
        }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: '8px', color: '#B45309', fontWeight: 700, fontSize: '14px', marginBottom: '8px' }}>
            <AlertTriangle size={18} />
            Falhas Recentes de Sincronização Local (SINc)
          </div>
          <div style={{ fontSize: '13px', color: '#92400E', lineHeight: 1.6 }}>
            {data.falhasRecentes.map((f) => (
              <div key={f.id} style={{ display: 'flex', justifyContent: 'space-between', padding: '4px 0', borderBottom: '1px solid #FEF3C7' }}>
                <span><strong>[{f.origem}]</strong> {f.mensagem}</span>
                <span style={{ fontSize: '11px', opacity: 0.8 }}>{new Date(f.dataCriacao).toLocaleTimeString('pt-BR')}</span>
              </div>
            ))}
          </div>
        </div>
      )}

      {/* METRICAS KPI */}
      <div className="grid-cols-4" style={{ marginBottom: '16px', gap: '16px' }}>
        <div className="card">
          <div style={{ display: 'flex', alignItems: 'center', gap: '8px', color: 'var(--text-muted)', fontSize: '13px', fontWeight: 600 }}>
            <Building2 size={16} color="var(--blue-primary)" />
            EMPRESAS CADASTRADAS
          </div>
          <div style={{ fontSize: '28px', fontWeight: 800, color: 'var(--text-main)', marginTop: '8px' }}>
            {totalEmpresas}
          </div>
          <div style={{ fontSize: '12px', color: 'var(--text-muted)', marginTop: '4px' }}>
            Clientes ativos: <strong>{empresasAtivas}</strong> • Suspensas: {empresasBloqueadas}
          </div>
        </div>

        <div className="card">
          <div style={{ display: 'flex', alignItems: 'center', gap: '8px', color: 'var(--text-muted)', fontSize: '13px', fontWeight: 600 }}>
            <Store size={16} color="var(--blue-primary)" />
            FILIAIS CADASTRADAS
          </div>
          <div style={{ fontSize: '28px', fontWeight: 800, color: 'var(--text-main)', marginTop: '8px' }}>
            {data?.totalFiliais ?? 0}
          </div>
          <div style={{ fontSize: '12px', color: 'var(--text-muted)', marginTop: '4px' }}>
            Lojas e centros de distribuição
          </div>
        </div>

        <div className="card">
          <div style={{ display: 'flex', alignItems: 'center', gap: '8px', color: 'var(--text-muted)', fontSize: '13px', fontWeight: 600 }}>
            <RefreshCw size={16} color="var(--green)" />
            SINCRONIZADORES ATIVOS
          </div>
          <div style={{ fontSize: '28px', fontWeight: 800, color: 'var(--text-main)', marginTop: '8px' }}>
            {data?.totalSincronizadores ?? 0}
          </div>
          <div style={{ fontSize: '12px', color: 'var(--text-muted)', marginTop: '4px' }}>
            <span style={{ color: 'var(--green)', fontWeight: 600 }}>{data?.sincronizadoresOnline ?? 0} online</span> • {data?.sincronizadoresOffline ?? 0} offline • {data?.sincronizadoresPendentes ?? 0} pendentes
          </div>
        </div>

        <div className="card">
          <div style={{ display: 'flex', alignItems: 'center', gap: '8px', color: 'var(--text-muted)', fontSize: '13px', fontWeight: 600 }}>
            <DollarSign size={16} color="var(--green)" />
            MRR ESTIMADO
          </div>
          <div style={{ fontSize: '28px', fontWeight: 800, color: 'var(--green)', marginTop: '8px' }}>
            R$ {(data?.mrrEstimado ?? 0).toFixed(2).replace('.', ',')}
          </div>
          <div style={{ fontSize: '12px', color: 'var(--text-muted)', marginTop: '4px' }}>
            Receita Mensal Recorrente
          </div>
        </div>
      </div>

      {/* SEGUNDA LINHA DE INDICADORES REAIS DO BANCO (FORNECEDORES, COTAÇÕES, PEDIDOS E OS) */}
      <div className="grid-cols-3" style={{ marginBottom: '28px', gap: '16px' }}>
        <div className="card" style={{ padding: '14px 18px' }}>
          <div style={{ fontSize: '11.5px', fontWeight: 700, color: 'var(--text-muted)', textTransform: 'uppercase' }}>
            Fornecedores Sincronizados
          </div>
          <div style={{ fontSize: '22px', fontWeight: 800, color: 'var(--text-main)', marginTop: '4px' }}>
            {data?.totalFornecedores ?? 0}
          </div>
        </div>

        <div className="card" style={{ padding: '14px 18px' }}>
          <div style={{ fontSize: '11.5px', fontWeight: 700, color: 'var(--text-muted)', textTransform: 'uppercase' }}>
            Cotações Realizadas
          </div>
          <div style={{ fontSize: '22px', fontWeight: 800, color: 'var(--text-main)', marginTop: '4px' }}>
            {data?.totalCotacoes ?? 0}
          </div>
        </div>

        <div className="card" style={{ padding: '14px 18px' }}>
          <div style={{ fontSize: '11.5px', fontWeight: 700, color: 'var(--text-muted)', textTransform: 'uppercase' }}>
            Pedidos & Ordens de Serviço
          </div>
          <div style={{ fontSize: '22px', fontWeight: 800, color: 'var(--text-main)', marginTop: '4px' }}>
            {data?.totalPedidos ?? 0} ped. • {data?.totalOrdensServico ?? 0} OS
          </div>
        </div>
      </div>

      {/* EMPRESAS PENDENTES DE SINCRONIZAÇÃO */}
      <div className="card" style={{ marginBottom: '28px' }}>
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '16px', flexWrap: 'wrap', gap: '12px' }}>
          <div>
            <h2 style={{ fontSize: '17px', fontWeight: 700, color: 'var(--text-main)', display: 'flex', alignItems: 'center', gap: '8px' }}>
              <RefreshCw size={18} style={{ color: 'var(--blue-primary)' }} />
              Empresas pendentes de sincronização
            </h2>
            <p style={{ fontSize: '13px', color: 'var(--text-muted)' }}>
              Empresas e filiais que ainda não completaram a sincronização inicial de produtos ou aguardam ativação do SINc.
            </p>
          </div>
          <span className="badge badge-yellow" style={{ fontSize: '12px', padding: '4px 10px' }}>
            {data?.empresasPendentesSincronizacao?.length ?? 0} {((data?.empresasPendentesSincronizacao?.length ?? 0) === 1) ? 'pendência' : 'pendências'}
          </span>
        </div>

        <div style={{ overflowX: 'auto' }}>
          <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: '13.5px' }}>
            <thead>
              <tr style={{ backgroundColor: '#F8FAFC', textAlign: 'left', borderBottom: '1px solid var(--border)' }}>
                <th style={{ padding: '10px 12px' }}>Nome da Empresa</th>
                <th style={{ padding: '10px 12px' }}>CNPJ</th>
                <th style={{ padding: '10px 12px' }}>Filial</th>
                <th style={{ padding: '10px 12px' }}>Status da Sincronização</th>
                <th style={{ padding: '10px 12px' }}>Última Comunicação</th>
                <th style={{ padding: '10px 12px' }}>Motivo da Pendência</th>
              </tr>
            </thead>
            <tbody>
              {(!data?.empresasPendentesSincronizacao || data.empresasPendentesSincronizacao.length === 0) ? (
                <tr>
                  <td colSpan={6} style={{ textAlign: 'center', padding: '24px', color: 'var(--text-muted)' }}>
                    <CheckCircle size={20} style={{ color: 'var(--green)', verticalAlign: 'middle', marginRight: '6px' }} />
                    Todas as empresas cadastradas estão com a sincronização inicial concluída com sucesso!
                  </td>
                </tr>
              ) : (
                data.empresasPendentesSincronizacao.map((p, idx) => {
                  const getStatusBadge = (status: string) => {
                    switch (status) {
                      case 'Aguardando ativação':
                        return 'badge badge-yellow';
                      case 'Nunca sincronizada':
                        return 'badge badge-gray';
                      case 'Sincronização inicial em andamento':
                        return 'badge badge-blue';
                      case 'Sincronização inicial com falha':
                        return 'badge badge-red';
                      default:
                        return 'badge';
                    }
                  };

                  const formatCnpj = (val: string) => {
                    const dig = val.replace(/\D/g, '');
                    if (dig.length === 14) {
                      return dig.replace(/^(\d{2})(\d{3})(\d{3})(\d{4})(\d{2})$/, '$1.$2.$3/$4-$5');
                    }
                    return val || '—';
                  };

                  return (
                    <tr key={`${p.tenantId}-${p.filialCodigo || idx}`} style={{ borderBottom: '1px solid var(--border-light)' }}>
                      <td style={{ padding: '12px', fontWeight: 600 }}>{p.nomeEmpresa}</td>
                      <td style={{ padding: '12px', fontFamily: 'monospace' }}>{formatCnpj(p.cnpj)}</td>
                      <td style={{ padding: '12px' }}>
                        {p.filialNome ? `${p.filialNome} (${p.filialCodigo || '001'})` : 'Matriz'}
                      </td>
                      <td style={{ padding: '12px' }}>
                        <span className={getStatusBadge(p.statusSincronizacao)}>
                          {p.statusSincronizacao}
                        </span>
                      </td>
                      <td style={{ padding: '12px', fontSize: '12.5px' }}>
                        {p.ultimaComunicacao
                          ? new Date(p.ultimaComunicacao).toLocaleString('pt-BR')
                          : <span style={{ color: 'var(--text-muted)' }}>Nunca comunicou</span>}
                      </td>
                      <td style={{ padding: '12px', color: p.statusSincronizacao.includes('falha') ? 'var(--red)' : 'var(--text-muted)' }}>
                        {p.motivoPendencia}
                      </td>
                    </tr>
                  );
                })
              )}
            </tbody>
          </table>
        </div>
      </div>

      {/* GESTÃO DE EMPRESAS CADASTRADAS */}
      <div className="card" style={{ marginBottom: '28px' }}>
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '16px', flexWrap: 'wrap', gap: '12px' }}>
          <div>
            <h2 style={{ fontSize: '17px', fontWeight: 700 }}>Empresas Clientes Cadastradas</h2>
            <p style={{ fontSize: '13px', color: 'var(--text-muted)' }}>
              Cada cliente possui seu Tenant ID e isolamento completo no PostgreSQL via Row-Level Security (RLS).
            </p>
          </div>

          <div style={{ width: '280px', position: 'relative' }}>
            <Search size={16} style={{ position: 'absolute', left: '10px', top: '11px', color: 'var(--text-muted)' }} />
            <input
              placeholder="Buscar por razão, fantasia ou CNPJ..."
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
                <th>Empresa (Tenant)</th>
                <th>CNPJ</th>
                <th>Plano Contratado</th>
                <th>Status</th>
                <th style={{ textAlign: 'right' }}>Ações</th>
              </tr>
            </thead>
            <tbody>
              {filteredTenants.length === 0 ? (
                <tr>
                  <td colSpan={5} style={{ textAlign: 'center', padding: '36px', color: 'var(--text-muted)' }}>
                    {tenants.length === 0 ? (
                      <div>
                        <div style={{ fontSize: '15px', fontWeight: 600, color: 'var(--text-main)', marginBottom: '6px' }}>
                          Nenhuma empresa cliente cadastrada no sistema.
                        </div>
                        <div style={{ fontSize: '13px', marginBottom: '16px' }}>
                          Clique no botão abaixo para iniciar o provisionamento do seu primeiro cliente real.
                        </div>
                        <button
                          onClick={() => navigate('/admin/empresas/nova')}
                          className="btn btn-primary"
                          style={{ fontSize: '13px' }}
                        >
                          <PlusCircle size={15} /> Cadastrar Primeira Empresa
                        </button>
                      </div>
                    ) : (
                      'Nenhuma empresa encontrada com os filtros informados.'
                    )}
                  </td>
                </tr>
              ) : (
                filteredTenants.map((t) => (
                  <tr key={t.id}>
                    <td>
                      <div style={{ fontWeight: 600, color: 'var(--text-main)' }}>{t.nomeFantasia}</div>
                      <div style={{ fontSize: '12px', color: 'var(--text-muted)' }}>{t.razaoSocial}</div>
                      <div style={{ fontSize: '11px', color: 'var(--text-light)', fontFamily: 'monospace' }}>
                        Tenant ID: {t.id}
                      </div>
                    </td>
                    <td>
                      {t.cnpj.replace(/(\d{2})(\d{3})(\d{3})(\d{4})(\d{2})/, "$1.$2.$3/$4-$5")}
                    </td>
                    <td>
                      <span className="badge badge-blue">
                        {t.planoNome || 'Padrão'}
                      </span>
                    </td>
                    <td>
                      <span className={`badge ${t.status === 'Ativo' ? 'badge-green' : 'badge-red'}`}>
                        {t.status}
                      </span>
                    </td>
                    <td style={{ textAlign: 'right' }}>
                      <button
                        onClick={() => handleToggleStatus(t)}
                        className={`btn ${t.status === 'Ativo' ? 'btn-danger' : 'btn-green'}`}
                        style={{ padding: '4px 10px', fontSize: '12px' }}
                      >
                        {t.status === 'Ativo' ? (
                          <>
                            <XCircle size={13} /> Bloquear
                          </>
                        ) : (
                          <>
                            <CheckCircle size={13} /> Ativar
                          </>
                        )}
                      </button>
                    </td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>
      </div>

      {/* PLANOS COMERCIAIS */}
      <div className="card">
        <h2 style={{ fontSize: '17px', fontWeight: 700, marginBottom: '6px' }}>
          Planos Comerciais Disponíveis
        </h2>
        <p style={{ fontSize: '13px', color: 'var(--text-muted)', marginBottom: '16px' }}>
          Configuração de limites e mensalidades vigentes para a plataforma Nexo Compras SaaS.
        </p>

        <div className="grid-cols-3">
          {planos.map((p) => (
            <div
              key={p.id}
              style={{
                border: '1px solid var(--border)',
                borderRadius: 'var(--radius-md)',
                padding: '16px',
                backgroundColor: '#FAFAFA'
              }}
            >
              <div style={{ fontWeight: 700, fontSize: '16px', color: 'var(--blue-primary)' }}>
                {p.nome}
              </div>
              <div style={{ fontSize: '22px', fontWeight: 800, margin: '6px 0', color: 'var(--text-main)' }}>
                R$ {p.mensalidade.toFixed(2).replace('.', ',')}
                <span style={{ fontSize: '13px', fontWeight: 500, color: 'var(--text-muted)' }}>/mês</span>
              </div>
              <ul style={{ fontSize: '13px', color: 'var(--text-muted)', listStyle: 'none', lineHeight: 1.8 }}>
                <li>👥 Limite de {p.maxUsuarios} usuários</li>
                <li>🏢 Até {p.maxFiliais} {p.maxFiliais === 1 ? 'filial' : 'filiais'}</li>
                <li>🏭 Até {p.maxFornecedores} fornecedores cadastrados</li>
                <li>📦 Até {p.maxCotacoesMes} cotações/mês</li>
              </ul>
            </div>
          ))}
        </div>
      </div>
    </div>
  );
};
