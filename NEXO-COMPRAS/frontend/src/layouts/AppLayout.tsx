import React from 'react';
import { NavLink, Outlet, useLocation } from 'react-router-dom';
import { useAuth } from '../contexts/AuthContext';
import { api } from '../services/api';
import { ErrorBoundary } from '../components/ErrorBoundary';
import {
  LayoutDashboard,
  Package,
  Users,
  PlusCircle,
  Clock,
  GitCompare,
  FileCheck,
  History,
  BarChart3,
  TrendingDown,
  Building2,
  Database,
  RefreshCw,
  ShieldCheck,
  Settings,
  LogOut,
  ShieldAlert,
  Server,
  Store,
  CreditCard,
  Wrench
} from 'lucide-react';

export const AppLayout: React.FC = () => {
  const { user, tenant, logout } = useAuth();
  const location = useLocation();

  const isPlatformAdmin = user?.perfil === 'PlatformAdmin';

  const NavItem = ({ to, icon: Icon, label, end = true }: { to: string; icon: any; label: string; end?: boolean }) => (
    <NavLink
      to={to}
      end={end}
      className={({ isActive }) =>
        `nav-link ${isActive ? 'active' : ''}`
      }
      style={({ isActive }) => ({
        display: 'flex',
        alignItems: 'center',
        gap: '10px',
        padding: '8px 12px',
        borderRadius: 'var(--radius-sm)',
        fontSize: '13.5px',
        fontWeight: isActive ? 600 : 500,
        color: isActive ? '#FFFFFF' : 'var(--text-main)',
        backgroundColor: isActive ? 'var(--blue-primary)' : 'transparent',
        marginBottom: '2px',
        transition: 'all 0.15s ease'
      })}
    >
      <Icon size={17} style={{ opacity: 0.9 }} />
      <span>{label}</span>
    </NavLink>
  );

  const SectionTitle = ({ title }: { title: string }) => (
    <div style={{
      fontSize: '11px',
      fontWeight: 700,
      color: 'var(--text-muted)',
      textTransform: 'uppercase',
      letterSpacing: '0.6px',
      marginTop: '16px',
      marginBottom: '6px',
      paddingLeft: '8px'
    }}>
      {title}
    </div>
  );

  return (
    <div style={{ display: 'flex', minHeight: '100vh', backgroundColor: 'var(--bg-page)' }}>
      {/* SIDEBAR LATERAL FIXA */}
      <aside style={{
        width: '260px',
        backgroundColor: '#FFFFFF',
        borderRight: '1px solid var(--border)',
        display: 'flex',
        flexDirection: 'column',
        position: 'sticky',
        top: 0,
        height: '100vh',
        overflowY: 'auto',
        padding: '16px 12px'
      }}>
        {/* LOGO E IDENTIDADE */}
        <div style={{ display: 'flex', alignItems: 'center', gap: '10px', padding: '6px 8px 18px', borderBottom: '1px solid var(--border-light)' }}>
          <div style={{
            width: '38px',
            height: '38px',
            borderRadius: '10px',
            backgroundColor: 'var(--blue-primary)',
            color: '#fff',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            fontWeight: 800,
            fontSize: '18px'
          }}>
            N
          </div>
          <div>
            <div style={{ fontWeight: 800, fontSize: '16px', color: 'var(--blue-primary)', lineHeight: 1.1 }}>
              NEXO COMPRAS
            </div>
            <div style={{ fontSize: '11px', color: 'var(--text-muted)', fontWeight: 600 }}>
              {isPlatformAdmin ? 'PAINEL SAAS GLOBAL' : 'GESTÃO INTELIGENTE'}
            </div>
          </div>
        </div>

        {/* NAVEGAÇÃO MULTI-SEÇÃO */}
        <nav style={{ flex: 1, paddingTop: '8px' }}>
          {isPlatformAdmin ? (
            /* =======================================================
               AMBIENTE 1: NEXO COMPRAS ADMIN (Exclusivo Super Admin)
               Zero dados ou telas operacionais de clientes
               ======================================================= */
            <>
              <SectionTitle title="ADMINISTRAÇÃO SAAS" />
              <NavItem to="/admin" icon={ShieldAlert} label="Painel Global do SaaS" />
              <NavItem to="/admin/tenants" icon={Building2} label="Empresas Cadastradas" />
              <NavItem to="/admin/empresas/nova" icon={PlusCircle} label="Cadastrar Empresa" />

              <SectionTitle title="INFRAESTRUTURA & ERP" />
              <NavItem to="/admin/filiais" icon={Store} label="Filiais Globais" />
              <NavItem to="/admin/sincronizadores" icon={RefreshCw} label="Sincronizadores SINc" />

              <SectionTitle title="ACESSO & COMERCIAL" />
              <NavItem to="/admin/usuarios" icon={Users} label="Usuários de Clientes" />
              <NavItem to="/admin/plans" icon={Database} label="Planos Comerciais" />
            </>
          ) : (
            /* =======================================================
               AMBIENTE 2: NEXO COMPRAS CLIENTE (Empresas Clientes)
               Telas operacionais do negócio
               ======================================================= */
            <>
              <SectionTitle title="PRINCIPAL" />
              <NavItem to="/dashboard" icon={LayoutDashboard} label="Dashboard" />
              <NavItem to="/filiais" icon={Store} label="Filiais e Unidades" />
              <NavItem to="/produtos" icon={Package} label="Produtos e Estoque" />
              <NavItem to="/fornecedores" icon={Users} label="Fornecedores" />

              <SectionTitle title="COMPRAS" />
              <NavItem to="/cotacoes/nova" icon={PlusCircle} label="Nova Cotação" />
              <NavItem to="/cotacoes" icon={Clock} label="Cotações em Andamento" />
              <NavItem to="/comparacao" icon={GitCompare} label="Comparação de Ofertas" />
              <NavItem to="/pedidos" icon={FileCheck} label="Pedidos de Compra" />
              <NavItem to="/historico" icon={History} label="Histórico de Compras" />

              <SectionTitle title="SERVIÇOS" />
              <NavItem to="/servicos/nova" icon={PlusCircle} label="Nova Cotação de Serviço" />
              <NavItem to="/servicos" icon={Wrench} label="Cotações de Serviços" />
              <NavItem to="/servicos/ordens" icon={FileCheck} label="Ordens de Serviço" />

              <SectionTitle title="ANÁLISE" />
              <NavItem to="/relatorios" icon={BarChart3} label="Relatórios" />
              <NavItem to="/economia" icon={TrendingDown} label="Economia e Indicadores" />
              <NavItem to="/analise-fornecedores" icon={Building2} label="Análise de Fornecedores" />

              <SectionTitle title="SISTEMA" />
              <NavItem to="/usuarios" icon={ShieldCheck} label="Usuários e Permissões" />
              <NavItem to="/assinatura" icon={CreditCard} label="Assinatura e Planos" />
              <NavItem to="/configuracoes" icon={Settings} label="Configurações" />
            </>
          )}
        </nav>

        {/* FOOTER DA SIDEBAR */}
        <div style={{
          paddingTop: '12px',
          borderTop: '1px solid var(--border-light)',
          fontSize: '12px',
          color: 'var(--text-muted)',
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'space-between'
        }}>
          <div>
            <div style={{ fontWeight: 600, color: 'var(--text-main)', fontSize: '12.5px' }}>
              {tenant ? tenant.nomeFantasia : 'Nexo Cloud'}
            </div>
            <div style={{ fontSize: '11px', color: 'var(--text-muted)' }}>
              {tenant?.planoNome ? `Plano ${tenant.planoNome}` : 'Ambiente Central'}
            </div>
          </div>
          <button
            onClick={logout}
            title="Sair da conta"
            style={{
              background: 'none',
              border: 'none',
              cursor: 'pointer',
              color: 'var(--text-muted)',
              padding: '6px'
            }}
          >
            <LogOut size={16} />
          </button>
        </div>
      </aside>

      {/* ÁREA PRINCIPAL COM TOPBAR */}
      <div style={{ flex: 1, display: 'flex', flexDirection: 'column', minWidth: 0 }}>
        {/* TOPBAR */}
        <header style={{
          height: '60px',
          backgroundColor: '#FFFFFF',
          borderBottom: '1px solid var(--border)',
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'space-between',
          padding: '0 28px',
          position: 'sticky',
          top: 0,
          zIndex: 10
        }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: '12px' }}>
            {tenant?.logoUrl && (
              <img
                src={tenant.logoUrl.startsWith('http') ? tenant.logoUrl : `${api.getBaseUrl()}${tenant.logoUrl.startsWith('/') ? '' : '/'}${tenant.logoUrl.replace(/^\/?api\/?/, '')}`}
                alt={tenant.nomeFantasia || 'Logo Empresa'}
                style={{
                  maxHeight: '38px',
                  maxWidth: '130px',
                  objectFit: 'contain',
                  display: 'block'
                }}
                onError={(e) => {
                  // Se falhar o carregamento, não exibir ícone de imagem quebrada
                  (e.target as HTMLElement).style.display = 'none';
                }}
              />
            )}
            {tenant && (
              <span className="badge badge-blue" style={{ fontSize: '12px', padding: '4px 10px' }}>
                <Building2 size={13} style={{ marginRight: '4px' }} />
                {tenant.razaoSocial} ({tenant.cnpj.replace(/(\d{2})(\d{3})(\d{3})(\d{4})(\d{2})/, "$1.$2.$3/$4-$5")})
              </span>
            )}
            {tenant?.status === 'Ativo' && (
              <span className="badge badge-green">Status: Ativo</span>
            )}
            {isPlatformAdmin && (
              <span className="badge badge-yellow">Modo Super Admin</span>
            )}
          </div>

          <div style={{ display: 'flex', alignItems: 'center', gap: '16px' }}>
            <div style={{ textAlign: 'right' }}>
              <div style={{ fontSize: '13px', fontWeight: 600, color: 'var(--text-main)' }}>
                {user?.nome}
              </div>
              <div style={{ fontSize: '11px', color: 'var(--text-muted)' }}>
                {user?.email} • {user?.perfil}
              </div>
            </div>
            <div style={{
              width: '34px',
              height: '34px',
              borderRadius: '50%',
              backgroundColor: 'var(--blue-light)',
              color: 'var(--blue-primary)',
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              fontWeight: 700,
              fontSize: '13px'
            }}>
              {user?.nome.charAt(0).toUpperCase()}
            </div>
          </div>
        </header>

        {/* CONTEÚDO DA PÁGINA */}
        <main style={{ flex: 1, padding: '24px 28px', maxWidth: '1400px', width: '100%', margin: '0 auto' }}>
          <ErrorBoundary>
            <Outlet />
          </ErrorBoundary>
        </main>
      </div>
    </div>
  );
};

