import React, { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { useAuth } from '../../contexts/AuthContext';
import { api } from '../../services/api';
import {
  TrendingDown,
  Package,
  Users,
  AlertTriangle,
  PlusCircle,
  Clock
} from 'lucide-react';

export const TenantDashboard: React.FC = () => {
  const { tenant } = useAuth();
  const [produtosCount, setProdutosCount] = useState(0);
  const [estoqueBaixoCount, setEstoqueBaixoCount] = useState(0);
  const [fornecedoresCount, setFornecedoresCount] = useState(0);
  const [agentesOnline, setAgentesOnline] = useState(0);

  useEffect(() => {
    // Carrega contagens do tenant atual
    api.get<{ total: number }>('/produtos?size=1').then((res) => setProdutosCount(res.total)).catch(() => {});
    api.get<{ total: number }>('/produtos?estoqueBaixo=true&size=1').then((res) => setEstoqueBaixoCount(res.total)).catch(() => {});
    api.get<any[]>('/fornecedores').then((res) => setFornecedoresCount(res.length)).catch(() => {});
    api.get<any[]>('/integration/agents').then((res) => {
      setAgentesOnline(res.filter((a) => a.status === 'Online').length);
    }).catch(() => {});
  }, []);

  return (
    <div>
      {/* BOAS-VINDAS */}
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '24px', flexWrap: 'wrap', gap: '16px' }}>
        <div>
          <h1 style={{ fontSize: '24px', fontWeight: 800, color: 'var(--text-main)' }}>
            Olá! Seja bem-vindo ao Nexo Compras
          </h1>
          <p style={{ fontSize: '14px', color: 'var(--text-muted)' }}>
            Operação central de compras para <strong>{tenant?.nomeFantasia}</strong>.
          </p>
        </div>

        <div style={{ display: 'flex', gap: '10px' }}>
          <Link to="/cotacoes/nova" className="btn btn-primary">
            <PlusCircle size={16} /> Nova Cotação
          </Link>
        </div>
      </div>

      {/* CARDS DE INDICADORES (KPIS) */}
      <div className="grid-cols-4" style={{ marginBottom: '24px' }}>
        <div className="card" style={{ borderTop: '4px solid var(--green)' }}>
          <div style={{ fontSize: '12px', fontWeight: 700, color: 'var(--text-muted)', textTransform: 'uppercase' }}>
            ECONOMIA REALIZADA
          </div>
          <div style={{ fontSize: '26px', fontWeight: 800, color: 'var(--green)', margin: '6px 0' }}>
            R$ 0,00
          </div>
          <div style={{ fontSize: '12px', color: 'var(--text-muted)' }}>
            Negociações fechadas pelo app
          </div>
        </div>

        <div className="card" style={{ borderTop: '4px solid var(--blue-primary)' }}>
          <div style={{ fontSize: '12px', fontWeight: 700, color: 'var(--text-muted)', textTransform: 'uppercase' }}>
            PRODUTOS NO CATÁLOGO
          </div>
          <div style={{ fontSize: '26px', fontWeight: 800, color: 'var(--text-main)', margin: '6px 0' }}>
            {produtosCount}
          </div>
          <div style={{ fontSize: '12px', color: 'var(--text-muted)' }}>
            Sincronizados via ERP / manuais
          </div>
        </div>

        <div className="card" style={{ borderTop: '4px solid var(--red)' }}>
          <div style={{ fontSize: '12px', fontWeight: 700, color: 'var(--text-muted)', textTransform: 'uppercase' }}>
            ALERTAS DE ESTOQUE
          </div>
          <div style={{ fontSize: '26px', fontWeight: 800, color: 'var(--red)', margin: '6px 0' }}>
            {estoqueBaixoCount}
          </div>
          <div style={{ fontSize: '12px', color: 'var(--text-muted)' }}>
            Produtos com estoque baixo ou zerado
          </div>
        </div>

        <div className="card" style={{ borderTop: '4px solid var(--yellow)' }}>
          <div style={{ fontSize: '12px', fontWeight: 700, color: 'var(--text-muted)', textTransform: 'uppercase' }}>
            FORNECEDORES ATIVOS
          </div>
          <div style={{ fontSize: '26px', fontWeight: 800, color: 'var(--text-main)', margin: '6px 0' }}>
            {fornecedoresCount}
          </div>
          <div style={{ fontSize: '12px', color: 'var(--text-muted)' }}>
            Cadastrados para cotação
          </div>
        </div>
      </div>

      {/* GUIA DE INÍCIO RÁPIDO */}
      <div className="grid-cols-2">
        <div className="card">
          <h2 style={{ fontSize: '16px', fontWeight: 700, marginBottom: '10px' }}>
            Como funciona o Nexo Compras
          </h2>
          <ol style={{ paddingLeft: '20px', fontSize: '13.5px', color: 'var(--text-muted)', lineHeight: 1.8 }}>
            <li><strong>Catálogo e Estoque:</strong> Acompanhe os produtos e níveis de estoque unificados em tempo real.</li>
            <li><strong>Alertas de Reposição:</strong> Identifique automaticamente itens com estoque baixo baseados no consumo.</li>
            <li><strong>Disparo de Cotação:</strong> Selecione os produtos e envie links protegidos exclusivos para os fornecedores.</li>
            <li><strong>Comparação Inteligente:</strong> O sistema calcula o custo efetivo com tributos (ICMS, ST, IPI) e aponta a campeã para gerar o pedido.</li>
          </ol>
        </div>

        <div className="card">
          <h2 style={{ fontSize: '16px', fontWeight: 700, marginBottom: '10px' }}>
            Ações Rápidas
          </h2>
          <div style={{ display: 'flex', flexDirection: 'column', gap: '10px' }}>
            <Link to="/produtos" className="btn btn-secondary" style={{ justifyContent: 'flex-start' }}>
              <Package size={16} /> Consultar Catálogo de Produtos e Estoque
            </Link>
            <Link to="/fornecedores" className="btn btn-secondary" style={{ justifyContent: 'flex-start' }}>
              <Users size={16} /> Gerenciar Base de Fornecedores e Contatos
            </Link>
            <Link to="/usuarios" className="btn btn-secondary" style={{ justifyContent: 'flex-start' }}>
              <Users size={16} /> Convidar Compradores da Equipe
            </Link>
          </div>
        </div>
      </div>
    </div>
  );
};

