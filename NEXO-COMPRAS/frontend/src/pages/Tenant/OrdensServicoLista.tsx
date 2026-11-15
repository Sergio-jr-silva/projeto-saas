import React, { useState, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { api } from '../../services/api';
import { OrdemServicoItem } from '../../types/service';
import {
  FileCheck,
  Search,
  Filter,
  Calendar,
  Building2,
  Printer,
  ChevronRight,
  Clock,
  CheckCircle2,
  AlertTriangle
} from 'lucide-react';

export const OrdensServicoLista: React.FC = () => {
  const navigate = useNavigate();
  const [ordens, setOrdens] = useState<OrdemServicoItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [filtroStatus, setFiltroStatus] = useState<string>('todos');
  const [busca, setBusca] = useState('');

  const carregarOrdens = async () => {
    setLoading(true);
    try {
      let url = '/servicos/ordens';
      if (filtroStatus !== 'todos') url += `?status=${filtroStatus}`;
      const res = await api.get<any>(url);
      const lista = Array.isArray(res) ? res : (Array.isArray(res?.itens) ? res.itens : []);
      setOrdens(lista);
    } catch (err: any) {
      console.error('Erro ao carregar ordens de serviço:', err);
      setOrdens([]);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    carregarOrdens();
  }, [filtroStatus]);

  const abrirPdf = (id: string, e: React.MouseEvent) => {
    e.stopPropagation();
    const baseUrl = api.getBaseUrl();
    window.open(`${baseUrl}/servicos/ordens/${id}/pdf`, '_blank');
  };

  const getStatusBadge = (status: string) => {
    switch (status) {
      case 'Criada':
        return <span className="badge badge-blue">Criada</span>;
      case 'AguardandoAssinatura':
        return <span className="badge badge-yellow">Aguardando Assinatura</span>;
      case 'EmExecucao':
        return <span className="badge badge-purple">Em Execução</span>;
      case 'Concluida':
        return <span className="badge badge-green">Concluída</span>;
      case 'Cancelada':
        return <span className="badge badge-red">Cancelada</span>;
      default:
        return <span className="badge badge-gray">{status}</span>;
    }
  };

  const listaOrdens = Array.isArray(ordens) ? ordens : [];
  const ordensFiltradas = listaOrdens.filter((o) => {
    if (!busca.trim()) return true;
    const b = busca.toLowerCase();
    return (
      o.numero.toLowerCase().includes(b) ||
      o.cotacaoNumero.toLowerCase().includes(b) ||
      o.cotacaoTitulo.toLowerCase().includes(b) ||
      o.fornecedorRazaoSocial.toLowerCase().includes(b)
    );
  });

  const totalGeral = ordensFiltradas.reduce((acc, o) => acc + (o.valorTotal || 0), 0);

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: '24px' }}>
      {/* HEADER */}
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <div>
          <h1 style={{ fontSize: '24px', fontWeight: 800, color: 'var(--text-main)', margin: '0 0 4px 0', display: 'flex', alignItems: 'center', gap: '10px' }}>
            <FileCheck size={26} color="var(--blue-primary)" />
            Ordens de Serviço e Contratos
          </h1>
          <p style={{ margin: 0, fontSize: '14px', color: 'var(--text-muted)' }}>
            Acompanhamento das ordens de serviço contratadas, prazos de execução e termos oficiais em formato ProSyst.
          </p>
        </div>
        <button
          onClick={() => navigate('/servicos')}
          className="btn btn-secondary"
        >
          Ver Cotações de Serviços
        </button>
      </div>

      {/* FILTROS E BUSCA */}
      <div className="card" style={{ padding: '16px 20px', display: 'flex', flexWrap: 'wrap', gap: '16px', alignItems: 'center', justifyContent: 'space-between' }}>
        <div style={{ position: 'relative', flex: 1, minWidth: '280px', maxWidth: '460px' }}>
          <Search size={18} style={{ position: 'absolute', left: '12px', top: '50%', transform: 'translateY(-50%)', color: 'var(--text-muted)' }} />
          <input
            type="text"
            className="input"
            placeholder="Buscar por número da OS, cotação ou fornecedor..."
            value={busca}
            onChange={(e) => setBusca(e.target.value)}
            style={{ paddingLeft: '38px' }}
          />
        </div>

        <div style={{ display: 'flex', alignItems: 'center', gap: '6px', fontSize: '13px' }}>
          <Filter size={15} color="var(--text-muted)" />
          <span style={{ fontWeight: 600 }}>Status da OS:</span>
          <select
            className="input"
            value={filtroStatus}
            onChange={(e) => setFiltroStatus(e.target.value)}
            style={{ width: 'auto', padding: '6px 12px' }}
          >
            <option value="todos">Todos os status</option>
            <option value="Criada">Criada</option>
            <option value="AguardandoAssinatura">Aguardando Assinatura</option>
            <option value="EmExecucao">Em Execução</option>
            <option value="Concluida">Concluída</option>
            <option value="Cancelada">Cancelada</option>
          </select>
        </div>
      </div>

      {/* TABELA DE ORDENS DE SERVIÇO */}
      <div className="card" style={{ padding: 0, overflow: 'hidden' }}>
        {loading ? (
          <div style={{ padding: '60px', textAlign: 'center', color: 'var(--text-muted)' }}>
            Carregando ordens de serviço...
          </div>
        ) : ordensFiltradas.length === 0 ? (
          <div style={{ padding: '60px', textAlign: 'center' }}>
            <FileCheck size={42} style={{ color: 'var(--text-muted)', opacity: 0.4, marginBottom: '12px' }} />
            <div style={{ fontSize: '16px', fontWeight: 600, color: 'var(--text-main)' }}>
              Nenhuma ordem de serviço contratada encontrada
            </div>
            <p style={{ fontSize: '13px', color: 'var(--text-muted)', maxWidth: '420px', margin: '6px auto 16px' }}>
              Ao aprovar uma cotação de serviços na matriz comparativa, a Ordem de Serviço correspondente será gerada automaticamente aqui.
            </p>
            <button onClick={() => navigate('/servicos')} className="btn btn-primary">
              Ir para Cotações de Serviços
            </button>
          </div>
        ) : (
          <table className="table" style={{ width: '100%', borderCollapse: 'collapse' }}>
            <thead>
              <tr style={{ background: '#f8fafc', borderBottom: '1px solid var(--border)' }}>
                <th style={{ padding: '12px 16px', textAlign: 'left', fontSize: '12px', fontWeight: 700, color: 'var(--text-muted)' }}>NÚMERO DA OS</th>
                <th style={{ padding: '12px 16px', textAlign: 'left', fontSize: '12px', fontWeight: 700, color: 'var(--text-muted)' }}>COTAÇÃO DE ORIGEM</th>
                <th style={{ padding: '12px 16px', textAlign: 'left', fontSize: '12px', fontWeight: 700, color: 'var(--text-muted)' }}>FORNECEDOR CONTRATADO</th>
                <th style={{ padding: '12px 16px', textAlign: 'center', fontSize: '12px', fontWeight: 700, color: 'var(--text-muted)' }}>STATUS</th>
                <th style={{ padding: '12px 16px', textAlign: 'center', fontSize: '12px', fontWeight: 700, color: 'var(--text-muted)' }}>EXECUÇÃO</th>
                <th style={{ padding: '12px 16px', textAlign: 'right', fontSize: '12px', fontWeight: 700, color: 'var(--text-muted)' }}>VALOR TOTAL</th>
                <th style={{ padding: '12px 16px', textAlign: 'center', fontSize: '12px', fontWeight: 700, color: 'var(--text-muted)' }}>AÇÕES</th>
              </tr>
            </thead>
            <tbody>
              {ordensFiltradas.map((o) => (
                <tr
                  key={o.id}
                  style={{ borderBottom: '1px solid var(--border)', transition: 'background-color 0.15s' }}
                >
                  <td style={{ padding: '14px 16px' }}>
                    <div style={{ fontWeight: 800, color: 'var(--blue-primary)', fontSize: '14px' }}>
                      {o.numero}
                    </div>
                    <div style={{ fontSize: '11px', color: 'var(--text-muted)', marginTop: '2px' }}>
                      Criada em: {new Date(o.dataCriacao).toLocaleDateString('pt-BR')}
                    </div>
                  </td>
                  <td style={{ padding: '14px 16px' }}>
                    <div style={{ fontWeight: 700, fontSize: '13px', color: 'var(--text-main)' }}>
                      {o.cotacaoNumero}
                    </div>
                    <div style={{ fontSize: '11.5px', color: 'var(--text-muted)', marginTop: '2px' }}>
                      {o.cotacaoTitulo}
                    </div>
                  </td>
                  <td style={{ padding: '14px 16px' }}>
                    <div style={{ fontWeight: 700, fontSize: '13.5px', color: 'var(--text-main)' }}>
                      {o.fornecedorRazaoSocial}
                    </div>
                    <div style={{ fontSize: '11px', color: 'var(--text-muted)', marginTop: '2px' }}>
                      Garantia: {o.garantiaMeses} meses
                    </div>
                  </td>
                  <td style={{ padding: '14px 16px', textAlign: 'center' }}>
                    {getStatusBadge(o.status)}
                  </td>
                  <td style={{ padding: '14px 16px', textAlign: 'center', fontSize: '13px' }}>
                    <div style={{ fontWeight: 600 }}>{o.prazoExecucaoDias} dias</div>
                    {o.previsaoTermino && (
                      <div style={{ fontSize: '11px', color: 'var(--text-muted)' }}>
                        Até {new Date(o.previsaoTermino).toLocaleDateString('pt-BR')}
                      </div>
                    )}
                  </td>
                  <td style={{ padding: '14px 16px', textAlign: 'right' }}>
                    <div style={{ fontWeight: 900, fontSize: '15px', color: '#16a34a' }}>
                      R$ {o.valorTotal.toLocaleString('pt-BR', { minimumFractionDigits: 2 })}
                    </div>
                  </td>
                  <td style={{ padding: '14px 16px', textAlign: 'center' }}>
                    <button
                      onClick={(e) => abrirPdf(o.id, e)}
                      className="btn btn-secondary"
                      style={{ padding: '6px 12px', fontSize: '12px', display: 'inline-flex', alignItems: 'center', gap: '6px' }}
                      title="Imprimir Ordem de Serviço PDF Oficial (ProSyst)"
                    >
                      <Printer size={14} />
                      <span>Imprimir PDF</span>
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>
    </div>
  );
};

