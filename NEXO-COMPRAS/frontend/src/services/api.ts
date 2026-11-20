export function resolveApiBaseUrl(): string {
  // 1. Runtime override via window.__NEXO_CONFIG__ (from config.js)
  const runtimeConfig = (window as any)?.__NEXO_CONFIG__?.API_URL;
  if (runtimeConfig && typeof runtimeConfig === 'string' && runtimeConfig.trim().length > 0) {
    return runtimeConfig.trim().replace(/\/+$/, '');
  }

  // 2. LocalStorage override (permite alternar IP em tempo de execução no browser)
  const localOverride = localStorage.getItem('nexo_api_url');
  if (localOverride && localOverride.trim().length > 0) {
    return localOverride.trim().replace(/\/+$/, '');
  }

  // 3. Build-time environment variable VITE_API_URL
  if (import.meta.env.VITE_API_URL) {
    return import.meta.env.VITE_API_URL.replace(/\/+$/, '');
  }

  // 4. Detecção automática de domínio de produção (nexoscompras.com.br / Railway) e LAN
  if (typeof window !== 'undefined' && window.location) {
    const { protocol, hostname, origin } = window.location;
    if (hostname && hostname.includes('nexoscompras.com.br')) {
      return 'https://api.nexoscompras.com.br/api';
    }
    if (hostname && hostname.endsWith('.up.railway.app')) {
      return `${origin}/api`;
    }
    if (hostname && hostname !== 'localhost' && hostname !== '127.0.0.1' && protocol.startsWith('http')) {
      return `${protocol}//${hostname}:5000/api`;
    }
  }

  // 5. Fallback padrão local
  return 'http://localhost:5000/api';
}

export const RESERVED_ROUTES = new Set([
  'login', 'admin', 'planos', 'api', 'cadastro', 'recuperar-senha',
  'cotacao', 'portal-servico', 'dashboard', 'produtos', 'fornecedores',
  'cotacoes', 'servicos', 'pedidos', 'relatorios', 'configuracoes',
  'usuarios', 'filiais', 'assinatura', 'integracao', 'sincronizacao',
  'historico', 'comparacao', 'economia', 'analise-fornecedores',
  'health', 'static', 'assets'
]);

export function getActiveCompanySlugFromUrl(): string | null {
  if (typeof window === 'undefined' || !window.location) return null;
  const parts = window.location.pathname.split('/').filter(Boolean);
  if (parts.length === 0) return null;
  const first = parts[0].toLowerCase();
  if (RESERVED_ROUTES.has(first)) return null;
  return first;
}

class ApiService {
  public getBaseUrl(): string {
    return resolveApiBaseUrl();
  }
  private getToken(): string | null {
    return localStorage.getItem('nexo_token');
  }

  public setToken(token: string) {
    localStorage.setItem('nexo_token', token);
  }

  public clearToken() {
    localStorage.removeItem('nexo_token');
  }

  private async request<T>(endpoint: string, options: RequestInit = {}): Promise<T> {
    const token = this.getToken();
    const headers: Record<string, string> = {
      'Content-Type': 'application/json',
      ...(options.headers as Record<string, string>),
    };

    if (token) {
      headers['Authorization'] = `Bearer ${token}`;
    }

    const activeSlug = getActiveCompanySlugFromUrl();
    if (activeSlug) {
      headers['X-Tenant-Slug'] = activeSlug;
    }

    const baseUrl = this.getBaseUrl();
    const response = await fetch(`${baseUrl}${endpoint}`, {
      ...options,
      headers,
    });

    if (response.status === 401 && !endpoint.startsWith('/auth/login')) {
      this.clearToken();
      const loginPath = activeSlug ? `/${activeSlug}/login` : '/login';
      if (window.location.pathname !== loginPath && !window.location.pathname.endsWith('/login')) {
        window.location.href = loginPath;
      }
      throw new Error('Sessão expirada. Faça login novamente.');
    }

    const data = await response.json().catch(() => ({}));

    if (!response.ok) {
      const err: any = new Error(data.erro || `Erro HTTP ${response.status}`);
      err.status = response.status;
      err.codigo = data.codigo;
      err.data = data;
      throw err;
    }

    return data as T;
  }

  public get<T>(endpoint: string) {
    return this.request<T>(endpoint, { method: 'GET' });
  }

  public post<T>(endpoint: string, body?: any) {
    return this.request<T>(endpoint, {
      method: 'POST',
      body: body ? JSON.stringify(body) : undefined,
    });
  }

  public put<T>(endpoint: string, body?: any) {
    return this.request<T>(endpoint, {
      method: 'PUT',
      body: body ? JSON.stringify(body) : undefined,
    });
  }

  public delete<T>(endpoint: string) {
    return this.request<T>(endpoint, { method: 'DELETE' });
  }

  public async postForm<T>(endpoint: string, formData: FormData): Promise<T> {
    const token = this.getToken();
    const headers: Record<string, string> = {};
    if (token) {
      headers['Authorization'] = `Bearer ${token}`;
    }
    const baseUrl = this.getBaseUrl();
    const response = await fetch(`${baseUrl}${endpoint}`, {
      method: 'POST',
      headers,
      body: formData,
    });
    if (response.status === 401) {
      this.clearToken();
      if (!window.location.pathname.startsWith('/login')) {
        window.location.href = '/login';
      }
      throw new Error('Sessão expirada. Faça login novamente.');
    }
    const data = await response.json().catch(() => ({}));
    if (!response.ok) {
      throw new Error(data.erro || `Erro HTTP ${response.status}`);
    }
    return data as T;
  }
}

export const api = new ApiService();

