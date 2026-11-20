# NEXO COMPRAS — GUIA COMPLETO DE DEPLOY E OPERAÇÃO (DOCKER & RAILWAY)

Este documento descreve detalhadamente o processo de execução local via **Docker Compose** e o procedimento passo a passo para implantação em produção na nuvem **Railway**, incluindo banco de dados PostgreSQL gerenciado, backend ASP.NET Core 9, frontend React/Vite com Nginx e isolamento Multi-Tenant.

---

## 1. VISÃO GERAL DA ARQUITETURA

```
                                +-----------------------------------+
                                |            USUÁRIOS               |
                                +-----------------------------------+
                                                  |
                                                  | HTTPS (Porta 443)
                                                  v
                 +-----------------------------------------------------------------+
                 |                    NEXO COMPRAS — FRONTEND                      |
                 |               (React + Vite + Tailwind + Nginx)                 |
                 |      Railway Domain: https://nexo-frontend.up.railway.app       |
                 +-----------------------------------------------------------------+
                                                  |
                                                  | REST API / JSON
                                                  v
                 +-----------------------------------------------------------------+
                 |                     NEXO COMPRAS — BACKEND                      |
                 |                     (ASP.NET Core 9 Web API)                    |
                 |       Railway Domain: https://nexo-api.up.railway.app           |
                 +-----------------------------------------------------------------+
                                                  |
                                                  | Npgsql / ADO.NET (SSL Mode)
                                                  v
                 +-----------------------------------------------------------------+
                 |                   BANCO POSTGRESQL GERENCIADO                   |
                 |                      (PostgreSQL 16 Engine)                     |
                 |             Railway Plugin / Docker Volume Persistente          |
                 +-----------------------------------------------------------------+
```

---

## 2. EXECUÇÃO LOCAL COM DOCKER COMPOSE

Para rodar todo o ecossistema (PostgreSQL, Backend e Frontend) em seu computador de desenvolvimento sem precisar instalar .NET SDK ou Node.js localmente:

### 2.1. Pré-requisitos
- [Docker Desktop](https://www.docker.com/products/docker-desktop/) instalado e ativo.
- Docker Compose v2+.

### 2.2. Passo a Passo

1. **Configurar as Variáveis de Ambiente:**
   Copie o arquivo de exemplo para `.env`:
   ```bash
   cp .env.example .env
   ```

2. **Iniciar os Serviços:**
   Na raiz do repositório, execute:
   ```bash
   docker compose up -d --build
   ```

3. **Verificar a Saúde dos Containers:**
   ```bash
   docker compose ps
   ```
   Você verá os 3 serviços em estado `healthy`:
   - `nexo-postgres` (porta 5432)
   - `nexo-backend` (porta 5000)
   - `nexo-frontend` (porta 5173)

4. **Acessar a Aplicação:**
   - **Frontend (Web App):** [http://localhost:5173](http://localhost:5173)
   - **Backend API (Healthcheck):** [http://localhost:5000/health](http://localhost:5000/health)
   - **API Base:** [http://localhost:5000/api](http://localhost:5000/api)

5. **Parar ou Reiniciar:**
   ```bash
   # Parar os containers mantendo os dados persistidos no banco
   docker compose stop

   # Encerrar e remover containers
   docker compose down

   # Encerrar removendo volumes (atenção: apaga dados do banco local)
   docker compose down -v
   ```

---

## 3. DEPLOY PASSO A PASSO NO RAILWAY

A plataforma [Railway](https://railway.app/) é ideal para aplicações monorepo com PostgreSQL gerenciado e deploy automatizado a partir do GitHub.

### 3.1. Criação do Projeto e Provisionamento do PostgreSQL

1. Acesse o painel do **Railway** ([railway.app](https://railway.app)) e clique em **New Project**.
2. Selecione **Provision PostgreSQL**.
3. O Railway criará o banco PostgreSQL gerenciado com backups automáticos, conexões seguras e métricas.
4. Clique no card do PostgreSQL provisionado, vá na aba **Variables** e verifique que as seguintes variáveis foram criadas:
   - `DATABASE_URL` (formato `postgresql://postgres:senha@host:porta/railway`)
   - `PGHOST`, `PGPORT`, `PGUSER`, `PGPASSWORD`, `PGDATABASE`.

---

### 3.2. Deploy do Serviço de Backend (API ASP.NET Core 9)

1. No mesmo projeto do Railway, clique em **+ New Service** -> **GitHub Repo** e selecione o repositório do Nexo Compras.
2. Nomeie o serviço como `nexo-backend` ou `nexo-api`.
3. Vá na aba **Settings** do serviço:
   - **Root Directory:** configure como `NEXO-COMPRAS/backend`
   - **Builder:** selecione `Dockerfile` (detectará automaticamente `NEXO-COMPRAS/backend/Dockerfile`)
   - **Healthcheck Path:** configure como `/health`
   - **Healthcheck Timeout:** `100` segundos
   - **Restart Policy:** `On Failure` (Max Retries: 10)
4. Vá na aba **Variables** e adicione as seguintes variáveis de ambiente:
   | Variável | Valor Recomendado | Descrição |
   |---|---|---|
   | `DATABASE_URL` | `${{Postgres.DATABASE_URL}}` | Vincula automaticamente à URL interna do Postgres Railway |
   | `ASPNETCORE_ENVIRONMENT` | `Production` | Ativa o modo de produção do ASP.NET Core |
   | `Jwt__Secret` | *(Gerar chave aleatória forte de 32+ caracteres)* | Assinatura dos tokens JWT |
   | `Jwt__Issuer` | `https://nexocompras.com.br` | Emissor dos tokens |
   | `Jwt__Audience` | `https://app.nexocompras.com.br` | Audiência esperada |
   | `CORS_ALLOWED_ORIGINS` | `https://*.up.railway.app` *(ou seu domínio customizado)* | Lista de origens permitidas |
5. Vá na aba **Networking** -> **Public Networking** e clique em **Generate Domain**.
   - Anote a URL gerada (exemplo: `https://nexo-backend-production.up.railway.app`).
   - Teste no navegador: `https://nexo-backend-production.up.railway.app/health`. A resposta será:
     ```json
     {"status":"healthy","timestamp":"...","service":"nexo-compras-api","version":"1.0.0"}
     ```

---

### 3.3. Deploy do Serviço de Frontend (React / Vite / Nginx)

1. No mesmo projeto do Railway, clique em **+ New Service** -> **GitHub Repo** e selecione o repositório do Nexo Compras novamente.
2. Nomeie o serviço como `nexo-frontend`.
3. Vá na aba **Settings**:
   - **Root Directory:** configure como `NEXO-COMPRAS/frontend`
   - **Builder:** selecione `Dockerfile` (detectará `NEXO-COMPRAS/frontend/Dockerfile`)
   - **Healthcheck Path:** configure como `/health`
   - **Restart Policy:** `On Failure`
4. Vá na aba **Variables** e adicione:
   | Variável | Valor | Descrição |
   |---|---|---|
   | `VITE_API_URL` | `https://nexo-backend-production.up.railway.app/api` | URL pública da API do Backend criada no passo 3.2 |
5. Vá na aba **Networking** -> **Public Networking** e clique em **Generate Domain**.
   - Anote a URL do frontend (exemplo: `https://nexo-compras.up.railway.app`).
6. **Atualizar CORS no Backend:**
   - Volte ao serviço `nexo-backend` -> **Variables** e atualize `CORS_ALLOWED_ORIGINS` incluindo a URL gerada do frontend:
     ```
     CORS_ALLOWED_ORIGINS=https://nexo-compras.up.railway.app
     ```

---

### 3.4. Injeção Dinâmica em Tempo de Execução (Runtime Config)

Diferente de SPAs tradicionais que exigem recompilação do Vite para alterar a URL da API, o Nexo Compras possui injeção nativa de configuração via script de inicialização (`docker-entrypoint.sh`).

Ao alterar a variável `VITE_API_URL` no painel do Railway, basta clicar em **Restart** ou **Redeploy** no serviço frontend; a nova URL da API entra em vigor instantaneamente sem recompilar o TypeScript ou o Vite.

---

### 3.5. Migrações e Inicialização do Banco de Dados

O Nexo Compras possui migração automática e idempotente via `DbInitializer.InitializeAsync`:
- Ao iniciar, o backend executa `context.Database.EnsureCreatedAsync()`.
- Em seguida, verifica e aplica todas as alterações incrementais (tabelas de Refresh Token, colunas multi-tenant de filial, logos da empresa, campos de serviço e índices otimizados).
- Se as tabelas já existirem, nenhum dado é apagado ou sobrescrito.
- Cria os planos iniciais e o usuário master administrativo padrão caso ainda não existam.

---

## 4. DOMÍNIOS PERSONALIZADOS E SSL / HTTPS

Tanto o backend quanto o frontend suportam domínios próprios:
1. No painel do Railway, selecione o serviço -> **Settings** -> **Networking** -> **Custom Domain**.
2. Digite seu domínio:
   - Frontend: `app.nexocompras.com.br`
   - Backend: `api.nexocompras.com.br`
3. Crie os apontamentos DNS do tipo `CNAME` no seu provedor de domínio (Cloudflare, GoDaddy, Registro.br, etc.) apontando para o registro informado pelo Railway.
4. O Railway emite certificados SSL Let's Encrypt automaticamente e renova antes do vencimento.

---

## 5. MONITORAMENTO, LOGS E BACKUP

### 5.1. Logs em Tempo Real
No painel do Railway, clique no serviço e acesse a aba **Deployments** -> **View Logs**. O backend emite logs estruturados de:
- Inicialização e verificação de integridade do banco.
- Tentativas de autenticação e bloqueio por força bruta.
- Consultas auditadas com isolamento por Tenant ID.
- Health checks recebidos.

### 5.2. Backup do Banco PostgreSQL
Para gerar backup manual ou automatizado via CLI:
```bash
# Exportar dump do banco em produção
pg_dump -h <HOST_DO_RAILWAY> -p <PORTA> -U postgres -d railway -F c -b -v -f nexo_backup_$(date +%Y%m%d).dump

# Restaurar dump
pg_restore -h <HOST_DESTINO> -p <PORTA> -U postgres -d nexo -v nexo_backup_20261115.dump
```

---

## 6. BOAS PRÁTICAS DE SEGURANÇA EM PRODUÇÃO

1. **Nunca use a chave JWT padrão em produção:**
   - Gere uma chave criptográfica de 64 caracteres:
     ```bash
     openssl rand -base64 48
     ```
   - Configure no Railway em `Jwt__Secret`.
2. **Restringir o CORS:**
   - Nunca deixe `*` em produção. Configure `CORS_ALLOWED_ORIGINS` apenas com seu domínio oficial.
3. **Senhas Seguras:**
   - A aplicação bloqueia senhas fracas no cadastro (exige mínimo de 10 caracteres, letras maiúsculas, minúsculas e números).
4. **Isolamento de Tenants:**
   - O isolamento multi-tenant é garantido no núcleo do Entity Framework Core via filtros globais de consulta (`HasQueryFilter`) validados por tokens JWT assinados.
