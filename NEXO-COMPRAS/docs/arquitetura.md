# Arquitetura Geral — Nexo Compras SaaS v2.0

## 1. Visão Geral da Plataforma

O **Nexo Compras** é uma plataforma SaaS multi-empresa (Multi-Tenant) desenvolvida para automatizar a gestão de compras, cotações e negociações com fornecedores para o varejo e atacado.

A arquitetura é dividida em três módulos principais:
1. **Nexo Compras SaaS**: Aplicação central na nuvem (ASP.NET Core 9 + PostgreSQL + React TypeScript).
2. **Nexo Compras Agent**: Serviço local (.NET Worker Service) instalado no Windows Server de cada loja, conectando-se ao Firebird (`BM.FDB`).
3. **Nexo Compras Admin**: Painel administrativo exclusivo do proprietário da plataforma para gerenciar empresas, licenças, planos comerciais e monitorar MRR.

---

## 2. Diagrama de Comunicação

```text
  [ SERVIDOR DA LOJA ]                          [ NUVEM NEXO COMPRAS ]
 ┌──────────────────────┐                    ┌────────────────────────────┐
 │  Firebird Server     │                    │  API ASP.NET Core 9        │
 │  (BM.FDB local)      │                    │  - MultiTenancy Context    │
 │       │              │                    │  - JWT Bearer Auth         │
 │       ▼              │                    │  - Global Query Filters    │
 │  Nexo Compras Agent  │  HTTPS (Porta 443) │       │                    │
 │  (.NET Worker)       │ ─────────────────> │       ▼                    │
 └──────────────────────┘                    │  PostgreSQL Central        │
                                             │  - Isolamento por Tenant   │
                                             │  - Row-Level Security      │
                                             └──────────────┬─────────────┘
                                                            │
                                             ┌──────────────┴─────────────┐
                                             │  Frontend React Vite TS    │
                                             │  (app.nexocompras.com.br)  │
                                             └────────────────────────────┘
```

---

## 3. Pilha Tecnológica Adotada

- **Backend**: C# 13, ASP.NET Core 9, Entity Framework Core 9, Npgsql.
- **Banco de Dados Central**: PostgreSQL 18+ com isolamento lógico por `tenant_id` e Row-Level Security (RLS).
- **Frontend**: React 19, TypeScript, Vite, React Router 7, Lucide Icons.
- **Segurança**: JWT Bearer Tokens, BCrypt para hash de senhas, TenantContextMiddleware com injeção automática de contexto.

