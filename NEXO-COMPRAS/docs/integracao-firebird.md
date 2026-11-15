# Integração com Firebird (BM.FDB) — Nexo Compras Agent

## 1. Desafio Arquitetural

Cada supermercado ou loja cliente possui seu próprio servidor local (físico ou virtual), onde está instalado o ERP BM Informática com o banco Firebird (`BM.FDB`).

**Diretrizes de Segurança Rígidas:**
1. **Nunca abrir a porta 3050 do Firebird para a internet.**
2. **Nunca disponibilizar o arquivo `BM.FDB` publicamente.**
3. **Nunca transmitir o banco inteiro para a nuvem.**
4. O servidor em nuvem **nunca tenta acessar o `localhost` do cliente**.

---

## 2. Solução: O Agente Local (.NET Worker Service)

O `Nexo Compras Agent` é instalado no servidor da loja como serviço do Windows.

### Fluxo de Comunicação
1. O administrador da loja acessa a plataforma em `app.nexocompras.com.br/integracao`.
2. Clica em **"Gerar Nova Chave de Ativação"**. A API gera uma chave segura de uso único no padrão `nxk_<64 caracteres hex>`.
3. O hash BCrypt da chave é armazenado na tabela `agentes` associado ao `tenant_id`.
4. O cliente instala o Agente no Windows Server da loja e informa a chave gerada e o caminho do banco (ex.: `C:\DATACASH\BM.FDB`).
5. O agente conecta localmente via ADO.NET Provider (`FirebirdSql.Data.FirebirdClient`) na porta local `127.0.0.1:3050`.
6. O agente consulta apenas as tabelas necessárias:
   - Produtos (`PRODUTO`)
   - Estoque atual (`PRODUTO_ESTOQUE`)
   - Fornecedores (`PESSOA WHERE FORNECEDOR = 'S'`)
   - Preços e histórico de compras (`ENTRADA`, `ENTRADA_PRODUTO`, `SAIDA_PRODUTO`)
7. O agente envia lotes incrementais criptografados via HTTPS para a API em nuvem (`POST /api/sync/push`).
8. O agente reporta heartbeat a cada 1 minuto (status Online/Offline).

---

## 3. Frequências de Sincronização

| Dado | Frequência Padrão | Modo |
|---|---|---|
| Estoque | 5 minutos | Incremental |
| Produtos | 30 minutos | Incremental |
| Fornecedores | 30 minutos | Incremental |
| Heartbeat / Status | 1 minuto | Ping de disponibilidade |

