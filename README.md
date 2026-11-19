# Nexo Compras — Guia Oficial de Arquitetura, Preparação e Implantação (SaaS Multi-Tenant & Railway)

SaaS multiempresa para automação de compras, cotações de produtos e serviços, fornecedores, pedidos, estoque multi-filial e relatórios gerenciais.

> **Estado deste README:** Documento técnico confirmado e sincronizado com a arquitetura real do repositório. Todos os caminhos de projetos, nomes de variáveis de ambiente, portas, Dockerfiles e comandos abaixo correspondem exatamente ao código-fonte em produção.

---

## 1. Visão Geral

O **Nexo Compras** utiliza uma aplicação web única para todas as empresas clientes. Cada cliente acessa seu ambiente corporativo por uma rota própria (slug):

- `https://nexoscompras.com.br/alemdopao`
- `https://nexoscompras.com.br/paonorte`
- `https://nexoscompras.com.br/autopecas`

O frontend é compartilhado. A API autentica o usuário via JWT, resolve a empresa pelo `TenantId` assinado no token e autoriza cada operação. A URL/slug **não é mecanismo de segurança isolado** e jamais concede acesso aos dados de uma empresa sem sessão autenticada e validada pelo backend (`TenantContextMiddleware`).

### Arquitetura Confirmada

| Componente | Tecnologia / Responsabilidade Real no Repositório |
|---|---|
| **Frontend** (`nexo-frontend`) | React 19 + TypeScript 5.9 + Vite 8 + Nginx 1.27 Alpine; interface web única com suporte a `/:slug/*` e fallback SPA |
| **API** (`nexo-api`) | ASP.NET Core 9 (`net9.0`); autenticação JWT multi-esquema (`Bearer`, `Agent`, `Portal`), isolamento multi-tenant, precificação dinâmica (R$ 50 a R$ 250) e geração de PDFs |
| **Banco SaaS** (`nexo-postgres`) | PostgreSQL 16 gerenciado no Railway (conexão interna privada via `DATABASE_URL`) |
| **SINc** (`agent-core` / `agent-desktop` / `agent-service`) | Aplicativo/Serviço Windows instalado no cliente; lê Firebird ou PostgreSQL local em modo somente leitura, controla deltas em SQLite local (`sinc_local.db` WAL) e sincroniza via HTTPS |
| **ERP do Cliente** | Firebird (porta 3050) ou PostgreSQL (porta 5432) na rede local do cliente; **não é exposto à internet** |
| **Domínio Oficial** | `nexoscompras.com.br`, registrado no Registro.br e apontado para o Railway (com suporte a CNAME Flattening via Cloudflare DNS) |

---

## 2. Estrutura Real do Repositório

```text
/
├── README.md                                  # Este guia oficial confirmado
├── DEPLOY_RAILWAY.md                          # Guia passo a passo de operação Docker & Railway
├── DOCUMENTACAO/
│   └── SAAS_RAILWAY_PRECIFICACAO_MULTITENANT.md
└── NEXO-COMPRAS/
    ├── NexoCompras.sln                        # Solution .NET 9
    ├── frontend/                              # Serviço Railway: nexo-frontend
    │   ├── Dockerfile                         # Multi-stage: node:22-alpine -> nginx:1.27-alpine
    │   ├── nginx.conf                         # SPA fallback (try_files) + /health
    │   ├── docker-entrypoint.sh               # Injeção runtime de config.js (VITE_API_URL) e $PORT
    │   ├── package.json                       # React 19, Vite 8, TypeScript 5.9
    │   └── src/
    ├── backend/                               # Serviço Railway: nexo-api
    │   ├── Dockerfile                         # Multi-stage: dotnet/sdk:9.0-alpine -> aspnet:9.0-alpine (usuário não-root 'app')
    │   ├── Nexo.Api.csproj                    # Projeto Web API ASP.NET Core 9
    │   ├── Program.cs                         # Leitura de PORT, DATABASE_URL, CORS e Health Checks
    │   ├── appsettings.json                   # Sanitizado (sem senhas ou connection strings)
    │   ├── Controllers/
    │   ├── Data/                              # NexoDbContext.cs e DbInitializer.cs
    │   ├── DTOs/
    │   ├── Models/
    │   ├── MultiTenancy/                      # TenantContext.cs, TenantContextMiddleware.cs, RLS Interceptor
    │   ├── Security/                          # JwtService.cs, AgentAuthHandler.cs, Secrets.cs
    │   └── Services/                          # PrecificacaoService.cs, AssinaturaService.cs, DatabaseResetService.cs, etc.
    ├── backend.tests/                         # Suíte com 39 testes automatizados de integração (xUnit)
    │   └── Nexo.Api.Tests.csproj
    ├── agent-core/                            # Núcleo do SINc (Firebird/Postgres + SQLite Incremental SHA-256)
    │   └── Nexo.Agent.Core.csproj
    ├── agent-desktop/                         # Interface Windows Forms / System Tray do SINc
    │   └── Nexo.Agent.Desktop.csproj
    └── agent-service/                         # Windows Background Service do SINc
        └── Nexo.Agent.Service.csproj
```

> **Nota sobre Worker (`nexo-worker`):** A arquitetura real **não necessita** de um quarto serviço worker separado na nuvem. O backend processa as requisições e webhooks diretamente e o sincronizador **SINc** roda localmente nas máquinas Windows das empresas clientes.

---

## 3. Especificações Técnicas Confirmadas (Seção 5.1)

1. **Versões de Runtime e Pacotes**:
   - **.NET**: `.NET 9.0` (`net9.0`, SDK `9.0.201`).
   - **Node.js**: Node `22` com `npm` (`package-lock.json`).
2. **Resolução da URL da API no Frontend (`NEXO-COMPRAS/frontend/src/services/api.ts`)**:
   - **1ª prioridade (Runtime Container)**: `window.__NEXO_CONFIG__?.API_URL` gerado na inicialização do container pelo script `docker-entrypoint.sh` a partir da variável de ambiente `VITE_API_URL` ou `API_URL`. Permite alterar a URL da API no Railway sem recompilar o bundle.
   - **2ª prioridade**: `import.meta.env.VITE_API_URL` (build-time).
   - **3ª prioridade**: Detecção automática de domínio: quando acessado em `nexoscompras.com.br`, aponta automaticamente para `https://api.nexoscompras.com.br/api`.
   - **4ª prioridade (Desenvolvimento Local)**: `http://localhost:5000/api`.
3. **Leitura de Porta e Connection String na API (`NEXO-COMPRAS/backend/Program.cs`)**:
   - **Porta**: Lê a variável `PORT` injetada pelo Railway e faz bind em `http://0.0.0.0:{PORT}` (ou `ASPNETCORE_URLS`).
   - **Banco de Dados**: Lê `DATABASE_URL` (formato URI `postgresql://user:pass@host:port/db` do Railway) ou `ConnectionStrings__DefaultConnection` e converte automaticamente via `Program.ParseConnectionString` para o formato chave-valor do Npgsql.
4. **Migrations e Bootstrap Seguro (`NEXO-COMPRAS/backend/Data/DbInitializer.cs`)**:
   - Executa evolução de schema 100% idempotente e não-destrutiva na inicialização (`CREATE TABLE IF NOT EXISTS` / `ALTER TABLE IF EXISTS ... ADD COLUMN IF NOT EXISTS`).
   - Nunca executa `DROP TABLE`, `DROP DATABASE` ou limpeza automática na inicialização.
   - Preserva/cria de forma idempotente a conta única do **Administrador Geral** (`juniosergio034@gmail.com`, perfil `AdministradorGeral`, hash BCrypt custo 12) e a tabela inicial de **Precificação Dinâmica** (`ConfiguracaoPrecificacao` v1: Base R$ 50,00, Mínimo R$ 50,00, Máximo R$ 250,00).
5. **Armazenamento Persistente de Arquivos (`LocalFileStorage`)**:
   - Logos de empresas e anexos são gravados em `/app/storage` (isolados por subpasta do `TenantId`). No Railway, deve-se montar um **Volume Persistente** no caminho `/app/storage` do serviço `nexo-api`.
6. **Health Checks Reais**:
   - **API (`nexo-api`)**: `GET /health` e `GET /api/health` (retorna HTTP 200 JSON `{"status":"healthy"}`).
   - **Frontend (`nexo-frontend`)**: `GET /health` (retorna HTTP 200 `healthy` via Nginx).

---

## 4. Configuração dos Serviços no Railway (Monorepo)

Crie um projeto no Railway chamado **`Nexo Compras`** (ambiente `production`) com os **3 serviços** abaixo:

### 4.1. Serviço 1: `nexo-postgres`
- Criado via catálogo do Railway (**Provision PostgreSQL**).
- **Rede**: Manter **Public Networking desativado** (acesso restrito à rede interna privada do Railway).

### 4.2. Serviço 2: `nexo-api` (Backend ASP.NET Core 9)
- **Source**: Repositório GitHub `Sergio-jr-silva/projeto-saas`
- **Root Directory**: `NEXO-COMPRAS/backend`
- **Builder**: `Dockerfile` (`NEXO-COMPRAS/backend/Dockerfile`)
- **Healthcheck Path**: `/health`
- **Volume Persistente**: Montar volume em `/app/storage`
- **Variáveis de Ambiente (`Variables`)**:

| Nome da Variável | Valor / Referência no Railway | Finalidade |
|---|---|---|
| `ASPNETCORE_ENVIRONMENT` | `Production` | Ativa políticas de segurança e CORS de produção |
| `DATABASE_URL` | `${{Postgres.DATABASE_URL}}` | Referência direta à conexão privada do PostgreSQL Railway |
| `JWT_SECRET` | `<gerar-segredo-aleatorio-64-chars>` | Chave mestra HMAC-SHA256 para assinatura de tokens JWT |
| `Jwt__Issuer` | `https://nexoscompras.com.br` | Emissor oficial dos tokens JWT |
| `Jwt__Audience` | `https://nexoscompras.com.br` | Audiência oficial dos tokens JWT |
| `CORS_ALLOWED_ORIGINS` | `https://nexoscompras.com.br,https://www.nexoscompras.com.br` | Origens autorizadas no CORS |
| `PAYMENT_WEBHOOK_SECRET` | `<segredo-webhook-gateway>` | Token validado no header `X-Webhook-Token` em `/api/webhooks/pagamentos` |

### 4.3. Serviço 3: `nexo-frontend` (React + Vite + Nginx)
- **Source**: Repositório GitHub `Sergio-jr-silva/projeto-saas`
- **Root Directory**: `NEXO-COMPRAS/frontend`
- **Builder**: `Dockerfile` (`NEXO-COMPRAS/frontend/Dockerfile`)
- **Healthcheck Path**: `/health`
- **Variáveis de Ambiente (`Variables`)**:

| Nome da Variável | Valor no Railway | Finalidade |
|---|---|---|
| `VITE_API_URL` | `https://api.nexoscompras.com.br/api` *(ou URL `.up.railway.app/api` da API no primeiro deploy)* | Injetada no build e em runtime (`config.js`) para consumo da API |

---

## 5. Checklist de Testes de Aceitação Pré-Produção (Seção 12)

### Aplicação
- [x] Frontend build concluído (`tsc && vite build` em `1.63s`, 0 erros).
- [x] API build concluído (`dotnet build` com 0 erros).
- [x] Dockerfiles multi-stage revisados e validados para `nexo-frontend` e `nexo-api`.
- [x] Health check responde em `/health` e `/api/health`.
- [x] API conecta ao PostgreSQL com suporte automático a `DATABASE_URL`.
- [x] Login do Admin Geral (`juniosergio034@gmail.com`) validado em teste de integração real.
- [x] CORS restrito a `nexoscompras.com.br`, subdomínios oficiais e `CORS_ALLOWED_ORIGINS` em produção.
- [x] Rotas internas (`/:slug/*`, `/admin`, `/login`) funcionam ao atualizar a página (F5) via fallback `try_files` do Nginx.
- [x] Logos e anexos utilizam diretório `/app/storage` preparado para Railway Volume.

### Multi-Tenant
- [x] Empresa A vê somente seus produtos e fornecedores (`MultiTenancyIsolationTests` & `SaasMultiTenantSlugAndPricingTests`).
- [x] Empresa B vê somente seus produtos e fornecedores.
- [x] Acesso cruzado por ID (IDOR) e por manipulação de header `X-Tenant-Slug` ou login cruzado em `/:slug/login` é bloqueado com `403/404`.
- [x] Cotações, pedidos, ordens de serviço e relatórios são estritamente isolados por `TenantId`.
- [x] Slug inexistente retorna `404 SLUG_INEXISTENTE`; empresa suspensa/inativa retorna `403 EMPRESA_SUSPENSA` / `403 EMPRESA_INATIVA`; rotas reservadas (`/login`, `/admin`, `/planos`, `/api`, `/cadastro`, `/recuperar-senha`) são protegidas.

### SINc
- [x] Ativação válida por código temporário de uso único (`ACTV-...`).
- [x] Credencial individual exclusiva (`nxk_...`) por instalação/Agent.
- [x] Revogação bloqueia imediatamente novas chamadas com `401 Unauthorized`.
- [x] Primeira sincronização envia a carga inicial completa (testado com 1.000 produtos em `SincronizacaoIncrementalRealTests`).
- [x] Segunda sincronização sem alterações envia **0 produtos** (0 chamadas desnecessárias à API).
- [x] Alteração de produtos envia exclusivamente os itens modificados/novos.
- [x] Queda/falha da API (HTTP 500) não confirma o hash no SQLite local, preservando o item para reenvio na retomada.
- [x] Isolamento multi-tenant no SQLite local via chave composta `(TenantId, BranchId, EntityType, ExternalId)`.

### Produção
- [x] Backup automático JSON pré-limpeza gerado e validado.
- [x] Segredos removidos de `appsettings.json` e fora do Git/bundle.
- [x] Banco de dados limpo: **0 empresas fictícias**, **0 produtos**, **0 cotações**, **0 pedidos**, mantendo exclusivamente o **Administrador Geral**.
