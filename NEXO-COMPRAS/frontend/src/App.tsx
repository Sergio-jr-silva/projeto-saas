import React from 'react';
import { BrowserRouter, Routes, Route, Navigate } from 'react-router-dom';
import { AuthProvider, useAuth } from './contexts/AuthContext';
import { AppLayout } from './layouts/AppLayout';
import { Login } from './pages/Auth/Login';
import { RegisterTenant } from './pages/Auth/RegisterTenant';
import { AdminDashboard } from './pages/Admin/AdminDashboard';
import { CadastrarEmpresaWizard } from './pages/Admin/CadastrarEmpresaWizard';
import { AdminFiliais } from './pages/Admin/AdminFiliais';
import { AdminUsuarios } from './pages/Admin/AdminUsuarios';
import { AdminSincronizadores } from './pages/Admin/AdminSincronizadores';
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
import { PlaceholderPage } from './pages/Tenant/PlaceholderPage';

const ProtectedRoute: React.FC<{ children: React.ReactNode; adminOnly?: boolean }> = ({
  children,
  adminOnly = false,
}) => {
  const { user, loading } = useAuth();

  if (loading) {
    return <div style={{ padding: '60px', textAlign: 'center' }}>Iniciando ambiente seguro Nexo Compras...</div>;
  }

  if (!user) {
    return <Navigate to="/login" replace />;
  }

  if (adminOnly && user.perfil !== 'PlatformAdmin') {
    return <Navigate to="/dashboard" replace />;
  }

  return <>{children}</>;
};

const RootRedirect: React.FC = () => {
  const { user } = useAuth();
  if (user?.perfil === 'PlatformAdmin') {
    return <Navigate to="/admin" replace />;
  }
  return <Navigate to="/dashboard" replace />;
};

export const AppRoutes: React.FC = () => {
  return (
    <Routes>
      {/* Rotas Públicas */}
      <Route path="/login" element={<Login />} />
      <Route path="/cadastro" element={<RegisterTenant />} />

      {/* Rota Pública do Portal do Fornecedor (Fase 4 - Acesso com Token + Código) */}
      <Route path="/cotacao/:token" element={<PortalCotacaoPage />} />
      <Route path="/portal-servico/:token" element={<PortalServicoCotacaoPage />} />

      {/* Rotas Autenticadas dentro do Layout SaaS */}
      <Route
        path="/"
        element={
          <ProtectedRoute>
            <AppLayout />
          </ProtectedRoute>
        }
      >
        <Route index element={<RootRedirect />} />

        {/* Módulo Tenant (Empresa) */}
        <Route path="dashboard" element={<TenantDashboard />} />
        <Route path="filiais" element={<Filiais />} />
        <Route path="produtos" element={<Produtos />} />
        <Route path="fornecedores" element={<Fornecedores />} />
        <Route path="integracao" element={<Integracao />} />
        <Route path="sincronizacao" element={<Integracao />} />
        <Route path="usuarios" element={<Usuarios />} />
        <Route path="assinatura" element={<Assinatura />} />

        {/* Módulo de Cotações e Compras (Fases 4 e 5) */}
        <Route path="cotacoes" element={<CotacoesEmAndamento />} />
        <Route path="cotacoes/nova" element={<NovaCotacao />} />
        <Route path="cotacoes/:id" element={<CotacaoDetalhePage />} />
        <Route path="cotacoes/:id/comparacao" element={<ComparacaoCotacaoPage />} />
        <Route path="comparacao" element={<ComparacaoLista />} />

        {/* Módulo de Cotação e Contratação de Serviços */}
        <Route path="servicos" element={<CotacoesServicoLista />} />
        <Route path="servicos/nova" element={<NovaCotacaoServico />} />
        <Route path="servicos/:id" element={<CotacaoServicoDetalhePage />} />
        <Route path="servicos/:id/comparacao" element={<ComparacaoServicoPage />} />
        <Route path="servicos/ordens" element={<OrdensServicoLista />} />

        {/* Configurações & Identidade Visual */}
        <Route path="configuracoes" element={<Configuracoes />} />
        <Route path="pedidos" element={<Pedidos />} />
        <Route path="historico" element={<Historico />} />
        <Route path="relatorios" element={<Relatorios />} />
        <Route path="economia" element={<Relatorios />} />
        <Route path="analise-fornecedores" element={<Relatorios />} />

        {/* Módulo Exclusivo Super Admin SaaS */}
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
          path="admin/plans"
          element={
            <ProtectedRoute adminOnly>
              <AdminDashboard />
            </ProtectedRoute>
          }
        />
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
