# NEXO COMPRAS — MANUAL TÉCNICO DE CONFIGURAÇÃO

Este guia detalha todos os parâmetros de configuração de cada módulo da solução Nexo Compras para facilitar a implantação em diferentes topologias de rede.

---

## 1. Configurações da API Backend (`BACKEND\Publicacao\appsettings.json`)

O arquivo `appsettings.json` na pasta de publicação controla a inicialização da API:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "Microsoft.EntityFrameworkCore.Database.Command": "Warning"
    }
  },
  "Urls": "http://0.0.0.0:5000",
  "AllowedHosts": "*",
  "ConnectionStrings": {
    "DefaultConnection": "Host=127.0.0.1;Port=5433;Database=nexo;Username=postgres;Password=postgres"
  },
  "Jwt": {
    "Secret": "NexoComprasSaaSSecretKey2026SuperSecureKeyWith32CharsMin!",
    "Issuer": "https://nexocompras.com.br",
    "Audience": "https://app.nexocompras.com.br",
    "ExpirationHours": 24
  },
  "Portal": {
    "BaseUrl": "http://localhost:5173"
  }
}
```

### Explicação dos Parâmetros:
- **`Urls`:** `"http://0.0.0.0:5000"` vincula o Kestrel a todas as placas de rede do computador. Para restringir a uma interface específica, utilize o IP desejado (ex.: `"http://192.168.1.50:5000"`).
- **`ConnectionStrings:DefaultConnection`:** String de conexão ADO.NET Npgsql para o PostgreSQL. Ajuste `Host`, `Port`, `Database`, `Username` e `Password` conforme seu banco.
- **`Portal:BaseUrl`:** Endereço base utilizado para montar os links públicos de cotação enviados aos fornecedores (`/portal/cotacao/{token}`). Ao hospedar em rede, altere para `http://<IP_OU_DOMINIO>:5173`.
- **`Jwt:ExpirationHours`:** Tempo de expiração do token de sessão do usuário (padrão: 24 horas).

---

## 2. Configurações do Frontend Web (`FRONTEND\dist\config.js`)

O frontend foi desenhado com um mecanismo flexível de resolução de URL da API:

```javascript
window.__NEXO_CONFIG__ = {
  // Deixe vazio ("") para auto-detecção via mesmo host na porta 5000.
  // Ou informe explicitamente a URL completa da API:
  API_URL: ""
};
```

### Ordem de Prioridade na Resolução da URL da API:
1. **`window.__NEXO_CONFIG__.API_URL`:** Se preenchido no `config.js`, tem prioridade absoluta.
2. **`localStorage.getItem('nexo_api_url')`:** Permite que qualquer operador ou testador altere o endereço da API via console do navegador em tempo de execução:
   ```javascript
   localStorage.setItem('nexo_api_url', 'http://192.168.1.100:5000/api');
   ```
3. **Detecção Automática:** Quando acessado via rede (ex.: `http://192.168.1.50:5173`), o frontend detecta que não está em `localhost` e automaticamente direciona as chamadas para `http://192.168.1.50:5000/api`.
4. **Fallback Local:** `http://localhost:5000/api` caso esteja acessando de `localhost`.

---

## 3. Configurações do Sincronizador SINc (`SINc\appsettings.json`)

O arquivo `appsettings.json` na pasta do `SINc.exe` define os parâmetros iniciais:

```json
{
  "NexoApi": {
    "BaseUrl": "http://localhost:5000",
    "AgentKey": "",
    "TimeoutSeconds": 30
  },
  "Database": {
    "Type": "Firebird"
  },
  "Firebird": {
    "Host": "127.0.0.1",
    "Port": 3050,
    "Database": "C:\\Users\\sergi\\OneDrive\\Desktop\\NEXO_COMPRAS_TESTE\\BANCO_EXEMPLO\\BM.FDB",
    "User": "SYSDBA",
    "Password": "masterkey",
    "Charset": "NONE",
    "Dialect": 3
  },
  "PostgreSql": {
    "Host": "127.0.0.1",
    "Port": 5432,
    "Database": "sistema_comercial",
    "User": "postgres",
    "Password": "",
    "SslMode": "Prefer",
    "Schema": "public"
  },
  "Sync": {
    "IntervaloEstoqueMin": 5,
    "IntervaloProdutosMin": 30,
    "IntervaloFornecedoresMin": 30,
    "IntervaloPrecosMin": 15,
    "IntervaloHeartbeatSeg": 60
  }
}
```

### Principais Opções:
- **`NexoApi:BaseUrl`:** Endereço da API central. Pode ser configurado pelo arquivo ou diretamente na interface visual do SINc (aba "🏢 Identificação & Token").
- **`Database:Type`:** `Firebird` ou `PostgreSQL`.
- **`Firebird:Database`:** Caminho absoluto para o arquivo `.FDB` do ERP.
- **`Sync:IntervaloEstoqueMin`:** Intervalo em minutos entre coletas automáticas de estoque em segundo plano.
- **`Sync:IntervaloHeartbeatSeg`:** Frequência de sinal de vida (heartbeat) enviado à API (padrão: 60 segundos).

