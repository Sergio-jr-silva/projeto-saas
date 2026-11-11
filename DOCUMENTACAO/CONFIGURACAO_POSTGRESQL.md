# Nexo Compras — Guia de Configuração e Diagnóstico PostgreSQL

Este documento orienta os técnicos de implantação e administradores de TI sobre como conectar e diagnosticar o banco de dados PostgreSQL utilizado pelo ERP do cliente ao **SINc (Sincronizador Local do Nexo Compras)**.

---

## 1. Visão Geral da Integração PostgreSQL

O SINc suporta bancos de dados PostgreSQL (versões 10 até 18+) como origem dos dados de produtos, estoque e fornecedores do ERP local. A comunicação é realizada por meio do conector de alta performance `Npgsql` de modo **estritamente de leitura** (`SELECT`).

### 🔒 Diretrizes de Segurança
- **Leitura Estrita**: O SINc nunca executa comandos de alteração estrutural (`ALTER`, `DROP`, `CREATE`) ou manipulação de dados (`INSERT`, `UPDATE`, `DELETE`) no banco de dados do ERP.
- **Proteção de Senha**: Credenciais de banco e tokens SaaS são armazenados com criptografia nativa **Windows DPAPI** (Data Protection API), atrelada ao usuário/máquina do cliente.
- **Zero Vazamento de Senhas**: As senhas nunca são exibidas em texto claro na tela de diagnóstico, em relatórios técnicos ou em logs compartilháveis.

---

## 2. Parâmetros de Conexão no SINc

No painel de configurações do SINc (**Aba: 🗄️ Banco de Dados ERP**), selecione `PostgreSQL` e preencha os seguintes parâmetros:

| Campo | Descrição | Exemplo | Padrão |
| :--- | :--- | :--- | :---: |
| **Host / IP** | Endereço IP ou hostname do servidor PostgreSQL | `127.0.0.1` ou `192.168.1.100` | `127.0.0.1` |
| **Porta** | Porta TCP configurada no PostgreSQL | `5432` ou `5433` | `5432` |
| **Nome do Banco** | Nome da base de dados do ERP | `erp_comercial` | — |
| **Schema** | Schema padrão das tabelas do ERP | `public` | `public` |
| **Usuário** | Usuário com permissão de `SELECT` | `postgres` ou `nexo_leitura` | `postgres` |
| **Senha** | Senha do usuário de leitura | `••••••••` | — |
| **Modo SSL** | Modo de negociação SSL/TLS | `Prefer`, `Require`, `Disable`, `VerifyCA`, `VerifyFull` | `Prefer` |
| **Timeout (s)** | Tempo limite em segundos para cada comando | `5` a `300` | `15` |

---

## 3. O Diagnóstico Técnico de Conectividade (7 Etapas)

Ao clicar no botão **🔌 Executar Diagnóstico de Conectividade PostgreSQL**, o SINc executa automaticamente uma validação em 7 fases:

1. **Validação de Campos Obrigatórios**: Verifica o preenchimento de Host, Porta, Banco e Usuário.
2. **Validação de Formato e Caracteres**: Garante que o host não contenha protocolos indevidos (`http://`) ou barras.
3. **Resolução de DNS**: Testa a resolução de nomes de domínio da rede local para endereços IPv4/IPv6 válidos.
4. **Sonda TCP da Porta**: Abre um socket de rede TCP na porta indicada para verificar se o serviço está escutando e se o firewall permite a conexão.
5. **Handshake e Autenticação Npgsql**: Conecta ao driver e negocia o protocolo e credenciais com o servidor PostgreSQL.
6. **Consulta de Integridade Leve (`SELECT 1;`)**: Confirma que o banco aceita consultas SQL em tempo de resposta aceitável.
7. **Consulta da Versão do Motor (`SELECT version();`)**: Identifica o build oficial do PostgreSQL em execução no cliente.

---

## 4. Classificação de Status & Badges

O resultado é exibido com um status badge visual e tempo de resposta em milissegundos:

- 🟢 **Conectado**: Conexão validada com sucesso, leitura aprovada e versão identificada.
- 🟡 **Servidor acessível**: O servidor e porta foram alcançados, mas houve restrição de autorização (ex: `pg_hba.conf`).
- 🔴 **Falha de conexão**: Servidor inacessível, host incorreto, firewall bloqueando ou porta TCP fechada.
- 🔴 **Falha de autenticação**: Usuário inexistente ou senha incorreta (`28P01: password authentication failed`).
- 🔴 **Banco indisponível**: O nome do banco informado não existe no servidor (`3D000: database does not exist`).
- 🟠 **Problema SSL**: Falha na negociação SSL/TLS ou certificado inválido.
- 🟡 **Campos inválidos**: Parâmetros faltando ou com formato incorreto.

---

## 5. Resolução de Problemas Frequentes (Troubleshooting)

### A. Erro: `Connection refused` (Conexão recusada)
- **Causa**: O serviço PostgreSQL está parado ou a porta configurada no SINc é diferente da configurada no `postgresql.conf`.
- **Solução**:
  1. Abra o `services.msc` no Windows do servidor e inicie o serviço `postgresql-x64-XX`.
  2. Verifique o arquivo `postgresql.conf` (linha `port = 5432`) e o parâmetro `listen_addresses = '*'`.

### B. Erro: `no pg_hba.conf entry for host`
- **Causa**: O PostgreSQL não autoriza conexões vindas do IP do computador onde o SINc está instalado.
- **Solução**:
  1. No servidor, abra o arquivo `pg_hba.conf` localizado no diretório de dados do PostgreSQL.
  2. Adicione a linha:
     ```text
     host    all             all             192.168.1.0/24          scram-sha-256
     ```
     *(Substitua `192.168.1.0/24` pela faixa de IP da rede local do cliente ou use `all` para redes confiáveis)*.
  3. Recarregue o serviço PostgreSQL (`pg_ctl reload` ou reinicie o serviço no Windows).

### C. Erro: `password authentication failed for user`
- **Causa**: Senha incorreta ou usuário com restrição de acesso.
- **Solução**: Confirme a senha do usuário com a equipe técnica de TI do cliente.

### D. Erro: `database "..." does not exist`
- **Causa**: Nome do banco incorreto ou digitado com diferenças de maiúsculas/minúsculas.
- **Solução**: Verifique o nome exato da base no pgAdmin ou execute `\l` no terminal do PostgreSQL.

### E. Falha de SSL / TLS
- **Causa**: O servidor PostgreSQL local não possui certificados SSL configurados, mas o cliente solicitou modo estrito.
- **Solução**: No SINc, altere o parâmetro **Modo SSL** para `Disable` ou `Prefer`.

