# Nexo Compras — Guia Oficial de Arquitetura, Preparação e Implantação (SaaS Multi-Tenant & Railway)

Consulte o documento principal na raiz do repositório (`../README.md`) e a documentação detalhada em `../DOCUMENTACAO/SAAS_RAILWAY_PRECIFICACAO_MULTITENANT.md`.

## Resumo de Configuração Railway (Monorepo)

| Serviço Railway | Root Directory | Dockerfile | Healthcheck Path |
|---|---|---|---|
| **`nexo-api`** (Backend ASP.NET Core 9) | `NEXO-COMPRAS/backend` | `Dockerfile` | `/health` |
| **`nexo-frontend`** (Frontend React/Vite + Nginx) | `NEXO-COMPRAS/frontend` | `Dockerfile` | `/health` |
| **`nexo-postgres`** (PostgreSQL 16 Gerenciado) | *(Plugin Railway)* | — | Rede Interna Privada |
