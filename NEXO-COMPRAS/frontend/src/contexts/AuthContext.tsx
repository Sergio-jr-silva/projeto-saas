import React, { createContext, useContext, useState, useEffect } from 'react';
import { User, TenantSummary } from '../types';
import { api } from '../services/api';

interface AuthContextType {
  user: User | null;
  tenant: TenantSummary | null;
  loading: boolean;
  login: (email: string, senha: string) => Promise<{ user: User; tenant: TenantSummary | null }>;
  logout: () => void;
  refreshUser: () => Promise<void>;
}

const AuthContext = createContext<AuthContextType>({} as AuthContextType);

export const AuthProvider: React.FC<{ children: React.ReactNode }> = ({ children }) => {
  const [user, setUser] = useState<User | null>(null);
  const [tenant, setTenant] = useState<TenantSummary | null>(null);
  const [loading, setLoading] = useState(true);

  const refreshUser = async () => {
    try {
      const data = await api.get<{ usuario: User; tenant: TenantSummary | null }>('/auth/me');
      setUser(data.usuario);
      setTenant(data.tenant);
    } catch {
      setUser(null);
      setTenant(null);
      api.clearToken();
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    const token = localStorage.getItem('nexo_token');
    if (token) {
      refreshUser();
    } else {
      setLoading(false);
    }
  }, []);

  const login = async (email: string, senha: string) => {
    const res = await api.post<{ token: string; usuario: User; tenant: TenantSummary | null }>('/auth/login', {
      email,
      senha,
    });
    api.setToken(res.token);
    setUser(res.usuario);
    setTenant(res.tenant);
    return { user: res.usuario, tenant: res.tenant };
  };

  const logout = () => {
    api.clearToken();
    setUser(null);
    setTenant(null);
    window.location.href = '/login';
  };

  return (
    <AuthContext.Provider value={{ user, tenant, loading, login, logout, refreshUser }}>
      {children}
    </AuthContext.Provider>
  );
};

export const useAuth = () => useContext(AuthContext);

