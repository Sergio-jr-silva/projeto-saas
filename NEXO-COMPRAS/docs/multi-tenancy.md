# Arquitetura Multi-Tenant — Isolamento de Dados

## 1. Princípio Fundamental

O Nexo Compras utiliza a estratégia de **banco de dados compartilhado com isolamento por Tenant ID** (Shared Database, Shared Schema).

Nenhum dado é misturado ou visível entre empresas diferentes. Uma empresa `A` jamais conseguirá consultar, alterar ou excluir dados de uma empresa `B`.

---

## 2. Camadas de Proteção

### Camada 1: Identificação Confiável via Token JWT
- O `tenant_id` é gravado no payload do token JWT assinado digitalmente com chave secreta HMAC-SHA256.
- O frontend **não** tem permissão de determinar ou enviar livremente o `tenant_id` nas requisições.
- O `TenantContextMiddleware` lê a claim `tenant_id` do usuário autenticado e injeta a instância de `ITenantContext` (Scoped) no pipeline da requisição.

### Camada 2: Global Query Filters no Entity Framework Core
Todas as entidades operacionais implementam a interface `ITenantEntity` (`Guid TenantId { get; set; }`):
- `Produto`
- `Fornecedor`
- `Cotacao`
- `Assinatura`
- `Filial`
- `Agente`

No `NexoDbContext.OnModelCreating`, configuramos:
```csharp
modelBuilder.Entity<Produto>().HasQueryFilter(e => 
    _tenantContext.CurrentTenantId == null || e.TenantId == _tenantContext.CurrentTenantId);
```
Qualquer `SELECT` executado por controladores da empresa aplica automaticamente a cláusula `WHERE tenant_id = @CurrentTenantId`.

### Camada 3: Atribuição Automática no `SaveChangesAsync`
Ao inserir uma nova entidade operacional:
```csharp
if (tenantEntity.TenantId == Guid.Empty)
{
    if (_tenantContext.CurrentTenantId.HasValue)
        tenantEntity.TenantId = _tenantContext.CurrentTenantId.Value;
    else
        throw new InvalidOperationException("Não é permitido salvar entidades operacionais sem TenantId associado.");
}
```
Isso impede que registros sejam gravados sem `TenantId` ou com `TenantId` forjado por payload externo.

### Camada 4: Row-Level Security (RLS) no PostgreSQL
No banco PostgreSQL, políticas de RLS garantem que mesmo em consultas SQL manuais, conexões usando o papel `nexo_app` só visualizem registros cujo `tenant_id` coincida com a variável de sessão `app.current_tenant_id`.

---

## 3. Testes Automatizados de Isolamento

A suíte de testes xUnit (`Nexo.Api.Tests.MultiTenancyIsolationTests`) valida:
1. Dois tenants registrados independentemente (`Mercado A` e `Mercado B`).
2. `Mercado A` cria produtos exclusivos.
3. `Mercado B` executa listagem geral e **não** recebe nenhum produto de `Mercado A`.
4. `Mercado B` tenta buscar o produto de `Mercado A` pelo ID direto e recebe **404 Not Found**.
5. Usuários normais de tenant recebem **403 Forbidden** ao tentar acessar rotas da administração global do SaaS.

