import React from 'react';
import { BrowserRouter, Routes, Route, Navigate, useParams } from 'react-router-dom';
import { AuthProvider, useAuth } from './contexts/AuthContext';
import { AppLayout } from './layouts/AppLayout';
import { Login } from './pages/Auth/Login';
import { RegisterTenant } from './pages/Auth/RegisterTenant';
import { AdminDashboard } from './pages/Admin/AdminDashboard';
import { CadastrarEmpresaWizard } from './pages/Admin/CadastrarEmpresaWizard';
import { AdminFiliais } from './pages/Admin/AdminFiliais';
import { AdminUsuarios } from './pages/Admin/AdminUsuarios';
import { AdminSincronizadores } from './pages/Admin/AdminSincronizadores';
import { AdminPrecificacao } from './pages/Admin/AdminPrecificacao';
import { TenantDashboard } from './pages/Tenant/Dashboard';
import { Produtos } from './pages/Tenant/Produtos';
import { Fornecedores } from './pages/Tenant/Fornecedores';
import { Integracao } from './pages/Tenant/Integracao';
import { Usuarios } from './pages/Tenant/Usuarios';
import { NovaCotacao } from './pages/Tenant/NovaCotacao';
import { CotacoesEmAndamento } from './pages/Tenant/CotacoesEmAndamento';
import { CotacaoDetalhePage } from './pages/Tenant/CotacaoDetalhe';
import { ComparacaoCotacaoPage } from './pages/Tenant/ComparacaoCotacao';
import { ComparacaoLista } from './pages/Tenant/ComparacaoLista';
import { PortalCotacaoPage } from './pages/Portal/PortalCotacao';
import { PortalServicoCotacaoPage } from './pages/Portal/PortalServicoCotacao';
import { CotacoesServicoLista } from './pages/Tenant/CotacoesServicoLista';
import { NovaCotacaoServico } from './pages/Tenant/NovaCotacaoServico';
import { CotacaoServicoDetalhePage } from './pages/Tenant/CotacaoServicoDetalhe';
import { ComparacaoServicoPage } from './pages/Tenant/ComparacaoServico';
import { OrdensServicoLista } from './pages/Tenant/OrdensServicoLista';
import { Pedidos } from './pages/Tenant/Pedidos';
import { Historico } from './pages/Tenant/Historico';
import { Filiais } from './pages/Tenant/Filiais';
import { Assinatura } from './pages/Tenant/Assinatura';
import { Relatorios } from './pages/Tenant/Relatorios';
import { Configuracoes } from './pages/Tenant/Configuracoes';

const ProtectedRoute: React.FC<{ children: React.ReactNode; adminOnly?: boolean }> = ({
  children,
  adminOnly = false,
}) => {
  const { user, tenant, loading } = useAuth();
  const { slug } = useParams<{ slug?: string }>();

  if (loading) {
    return <div style={{ padding: '60px', textAlign: 'center' }}>Iniciando ambiente seguro Nexo Compras...</div>;
  }

  if (!user) {
    if (slug) {
      return <Login />;
    }
    return <Navigate to="/login" replace />;
  }

  const isMasterAdmin = user.perfil === 'PlatformAdmin' || user.perfil === 'AdministradorGeral';

  if (adminOnly && !isMasterAdmin) {
    const dest = tenant?.slug ? `/${tenant.slug}/dashboard` : '/dashboard';
    return <Navigate to={dest} replace />;
  }

  // Isolamento multi-tenant por URL (/slug): impede que usuário da Empresa A navegue na URL da Empresa B
  if (slug && !isMasterAdmin && tenant?.slug && tenant.slug.toLowerCase() !== slug.toLowerCase()) {
    return (
      <div style={{ minHeight: '100vh', display: 'flex', alignItems: 'center', justifyContent: 'center', backgroundColor: 'var(--bg-page)', padding: '20px' }}>
        <div className="card" style={{ maxWidth: '460px', textAlign: 'center', padding: '32px 24px' }}>
          <h2 style={{ fontSize: '20px', fontWeight: 800, color: '#DC2626', marginBottom: '8px' }}>
            Acesso Não Autorizado (403)
          </h2>
          <p style={{ fontSize: '14px', color: 'var(--text-muted)', lineHeight: 1.6, marginBottom: '20px' }}>
            Sua sessão pertence à empresa <strong>{tenant.nomeFantasia}</strong> (/{tenant.slug}) e não possui permissão para acessar os dados de <strong>/{slug}</strong>.
          </p>
          <a href={`/${tenant.slug}/dashboard`} className="btn btn-primary" style={{ display: 'inline-block', padding: '10px 20px' }}>
            Retornar ao Meu Ambiente (/{tenant.slug})
          </a>
        </div>
      </div>
    );
  }

  return <>{children}</>;
};

const RootRedirect: React.FC = () => {
  const { user, tenant } = useAuth();
  if (user?.perfil === 'PlatformAdmin' || user?.perfil === 'AdministradorGeral') {
    return <Navigate to="/admin" replace />;
  }
  if (tenant?.slug) {
    return <Navigate to={`/${tenant.slug}/dashboard`} replace />;
  }
  return <Navigate to="/dashboard" replace />;
};

const SlugRootRedirect: React.FC = () => {
  const { slug } = useParams<{ slug?: string }>();
  return <Navigate to={slug ? `/${slug}/dashboard` : '/dashboard'} replace />;
};

export const AppRoutes: React.FC = () => {
  return (
    <Routes>
      {/* Rotas Públicas Reservadas */}
      <Route path="/login" element={<Login />} />
      <Route path="/recuperar-senha" element={<Login />} />
      <Route path="/cadastro" element={<RegisterTenant />} />
      <Route path="/planos" element={<RegisterTenant />} />
      <Route path="/:slug/login" element={<Login />} />

      {/* Rota Pública do Portal do Fornecedor (Fase 4 - Acesso com Token + Código) */}
      <Route path="/cotacao/:token" element={<PortalCotacaoPage />} />
      <Route path="/portal-servico/:token" element={<PortalServicoCotacaoPage />} />

      {/* Rotas Autenticadas Globais e Super Admin */}
      <Route
        path="/"
        element={
          <ProtectedRoute>
            <AppLayout />
          </ProtectedRoute>
        }
      >
        <Route index element={<RootRedirect />} />

        {/* Módulo Tenant (Rotas Diretas) */}
        <Route path="dashboard" element={<TenantDashboard />} />
        <Route path="filiais" element={<Filiais />} />
        <Route path="produtos" element={<Produtos />} />
        <Route path="fornecedores" element={<Fornecedores />} />
        <Route path="integracao" element={<Integracao />} />
        <Route path="sincronizacao" element={<Integracao />} />
        <Route path="usuarios" element={<Usuarios />} />
        <Route path="assinatura" element={<Assinatura />} />
        <Route path="cotacoes" element={<CotacoesEmAndamento />} />
        <Route path="cotacoes/nova" element={<NovaCotacao />} />
        <Route path="cotacoes/:id" element={<CotacaoDetalhePage />} />
        <Route path="cotacoes/:id/comparacao" element={<ComparacaoCotacaoPage />} />
        <Route path="comparacao" element={<ComparacaoLista />} />
        <Route path="servicos" element={<CotacoesServicoLista />} />
        <Route path="servicos/nova" element={<NovaCotacaoServico />} />
        <Route path="servicos/:id" element={<CotacaoServicoDetalhePage />} />
        <Route path="servicos/:id/comparacao" element={<ComparacaoServicoPage />} />
        <Route path="servicos/ordens" element={<OrdensServicoLista />} />
        <Route path="configuracoes" element={<Configuracoes />} />
        <Route path="pedidos" element={<Pedidos />} />
        <Route path="historico" element={<Historico />} />
        <Route path="relatorios" element={<Relatorios />} />
        <Route path="economia" element={<Relatorios />} />
        <Route path="analise-fornecedores" element={<Relatorios />} />

        {/* Módulo Exclusivo Super Admin SaaS (/admin) */}
        <Route
          path="admin"
          element={
            <ProtectedRoute adminOnly>
              <AdminDashboard />
            </ProtectedRoute>
          }
        />
        <Route
          path="admin/tenants"
          element={
            <ProtectedRoute adminOnly>
              <AdminDashboard />
            </ProtectedRoute>
          }
        />
        <Route
          path="admin/empresas/nova"
          element={
            <ProtectedRoute adminOnly>
              <CadastrarEmpresaWizard />
            </ProtectedRoute>
          }
        />
        <Route
          path="admin/filiais"
          element={
            <ProtectedRoute adminOnly>
              <AdminFiliais />
            </ProtectedRoute>
          }
        />
        <Route
          path="admin/usuarios"
          element={
            <ProtectedRoute adminOnly>
              <AdminUsuarios />
            </ProtectedRoute>
          }
        />
        <Route
          path="admin/sincronizadores"
          element={
            <ProtectedRoute adminOnly>
              <AdminSincronizadores />
            </ProtectedRoute>
          }
        />
        <Route
          path="admin/precificacao"
          element={
            <ProtectedRoute adminOnly>
              <AdminPrecificacao />
            </ProtectedRoute>
          }
        />
        <Route
          path="admin/plans"
          element={
            <ProtectedRoute adminOnly>
              <AdminPrecificacao />
            </ProtectedRoute>
          }
        />
      </Route>

      {/* Rotas Individuais por Empresa via Slug (/:slug e /:slug/*) */}
      <Route
        path="/:slug"
        element={
          <ProtectedRoute>
            <AppLayout />
          </ProtectedRoute>
        }
      >
        <Route index element={<SlugRootRedirect />} />
        <Route path="dashboard" element={<TenantDashboard />} />
        <Route path="filiais" element={<Filiais />} />
        <Route path="produtos" element={<Produtos />} />
        <Route path="fornecedores" element={<Fornecedores />} />
        <Route path="integracao" element={<Integracao />} />
        <Route path="sincronizacao" element={<Integracao />} />
        <Route path="usuarios" element={<Usuarios />} />
        <Route path="assinatura" element={<Assinatura />} />
        <Route path="cotacoes" element={<CotacoesEmAndamento />} />
        <Route path="cotacoes/nova" element={<NovaCotacao />} />
        <Route path="cotacoes/:id" element={<CotacaoDetalhePage />} />
        <Route path="cotacoes/:id/comparacao" element={<ComparacaoCotacaoPage />} />
        <Route path="comparacao" element={<ComparacaoLista />} />
        <Route path="servicos" element={<CotacoesServicoLista />} />
        <Route path="servicos/nova" element={<NovaCotacaoServico />} />
        <Route path="servicos/:id" element={<CotacaoServicoDetalhePage />} />
        <Route path="servicos/:id/comparacao" element={<ComparacaoServicoPage />} />
        <Route path="servicos/ordens" element={<OrdensServicoLista />} />
        <Route path="configuracoes" element={<Configuracoes />} />
        <Route path="pedidos" element={<Pedidos />} />
        <Route path="historico" element={<Historico />} />
        <Route path="relatorios" element={<Relatorios />} />
        <Route path="economia" element={<Relatorios />} />
        <Route path="analise-fornecedores" element={<Relatorios />} />
      </Route>

      <Route path="*" element={<RootRedirect />} />
    </Routes>
  );
};

export default function App() {
  return (
    <AuthProvider>
      <BrowserRouter>
        <AppRoutes />
      </BrowserRouter>
    </AuthProvider>
  );
}
