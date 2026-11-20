# Nexo Compras — Relatório Técnico de Validação e Testes

Data: 20/11/2026  
Ambiente: Windows 11 64-bit / .NET 9.0 SDK & Runtime / React 19 & Vite 8  
Status Geral: **APROVADO COM SUCESSO**

---

## 1. Responsividade Completa (Celulares, Tablets e Monitores)

### Auditoria e Alterações Realizadas
- **Menu Lateral e Drawer Mobile:**
  - Implementado drawer retrátil com transição CSS suave (`transform: translateX(-100%)` para `0`).
  - Adicionado botão hamburger de alternância na Topbar (`.mobile-nav-toggle`), com ícones dinâmicos `Menu` e `X`.
  - Adicionado backdrop escurecido com desfoque (`backdrop-filter: blur(2px)`) para fechar o menu ao tocar fora.
  - Fechamento automático do drawer mobile ao navegar entre rotas.
- **Tabelas Responsivas:**
  - Adicionado invólucro `.table-container` com rolagem horizontal controlada e aceleração por hardware (`-webkit-overflow-scrolling: touch; width: 100%; overflow-x: auto`).
  - Largura mínima de 580px em telas compactas para evitar esmagamento de colunas financeiras e botões de ação.
- **Modais e Pop-ups:**
  - Substituídos layouts com posições e larguras fixas por `.modal-overlay` e `.modal-content-responsive`.
  - Altura máxima restrita a `90vh` com rolagem vertical automática (`overflow-y: auto`) para que modais caibam em telas de smartphones.
- **Alvos de Toque e Formulários:**
  - Botões e inputs configurados com altura mínima de 42-44px em telas `<= 640px`.
  - Tamanho de fonte base em inputs ajustado para 15px em dispositivos móveis, impedindo zoom automático do iOS Safari.

### Resumo dos Testes de Resolução
| Dispositivo / Viewport | Resolução | Comportamento Observado | Resultado |
|---|---|---|---|
| Celular Pequeno | 320px | Drawer recolhido, tabela com scroll lateral suave, formulários em coluna única, sem quebra do layout | APROVADO |
| Smartphone Padrão | 375px / 390px / 430px | Header compacto, badges organizados, botões confortáveis para o toque, modais contidos | APROVADO |
| Tablet Portrait | 768px | Drawer acessível via hamburger, cards organizados em 2 colunas, sem rolagem horizontal na página | APROVADO |
| Tablet Landscape / Laptop | 1024px / 1366px | Sidebar expandida automaticamente, navegação completa visível, grids fluidos | APROVADO |
| Desktop & Widescreen | 1920px | Conteúdo centralizado com largura máxima de 1400px, dashboard expandido sem distorção | APROVADO |

---

## 2. Campo e Validação de CNPJ

### Auditoria e Correções
- **Causa Raiz Identificada:**
  - O campo de CNPJ em alguns formulários permitia digitação livre de caracteres alfa, utilizava formatação frágil por regex e permitia envio sem cálculo dos dígitos verificadores.
- **Componentes Criados e Integrados:**
  - `frontend/src/utils/cnpj.ts`:
    - `cleanCnpj(v)`: Remove todos os caracteres não numéricos.
    - `maskCnpj(v)`: Aplica máscara progressiva estrita `00.000.000/0000-00` em até 14 dígitos.
    - `validarCnpj(v)`: Implementa o algoritmo oficial de Módulo 11 da Receita Federal (rejeita sequências de dígitos iguais e valida os 2 dígitos verificadores).
  - `frontend/src/components/CnpjInput.tsx`: Componente visual reutilizável com bloqueio de teclas não numéricas, tratamento de colagem (paste) e feedback de erro em tempo real.
  - `backend/Security/CnpjValidator.cs`: Validação equivalente no backend para garantir integridade e rejeitar payloads maliciosos ou corrompidos.
  - Persistência padronizada: os CNPJs são persistidos no banco de dados exclusivamente limpos (14 dígitos sem pontuação).

### Casos de Teste Executados
| Entrada do Usuário | Tratamento Frontend | Validação Módulo 11 | Persistência no BD | Resultado |
|---|---|---|---|---|
| `12345678000195` | Mascarado: `12.345.678/0001-95` | Válido (DV correto) | `12345678000195` | APROVADO |
| `12.345.678/0001-95` (colado) | Mantido e formatado | Válido | `12345678000195` | APROVADO |
| `AB12345678000195CD` | Letras bloqueadas na digitação/paste | Válido | `12345678000195` | APROVADO |
| `11.111.111/1111-11` | Mascarado | Inválido (bloqueia envio) | Rejeitado (400) | APROVADO |
| `12.345.678/0001-00` (DV errado) | Exibe alerta "CNPJ inválido" | Inválido (bloqueia envio) | Rejeitado (400) | APROVADO |
| Mais de 14 dígitos | Trava no 14º dígito | Válido (14 dígitos) | Persiste 14 dígitos | APROVADO |

---

## 3. Cadastro e Exibição de Filiais (Multi-Empresa)

### Diagnóstico da Falha Anterior
1. **Atribuição de Tenant no Backend:**
   - No endpoint `POST /api/tenant/branches` (`TenantController.cs`), o objeto `Filial` era instanciado sem a atribuição explícita `f.TenantId = tenantId;`, salvando `Guid.Empty` no banco de dados PostgreSQL.
   - Como a consulta da listagem filtra `Where(f => f.TenantId == tenantId)`, a filial recém-criada ficava "órfã" e invisível.
2. **Resolução de Contexto Multi-Tenant:**
   - No `FiliaisController.cs`, o método `ObterTenantId()` dependia exclusivamente de `ITenantContext.CurrentTenantId`. Em requisições de administração ou sem o cookie de tenant, recaía sobre o primeiro tenant ativo da base. O método foi aprimorado para resolver via cabeçalhos `X-Tenant-Slug`, `X-Tenant-Id`, parâmetro de rota ou claims JWT.
3. **Tratamento de Exceções no Frontend:**
   - Em `AdminFiliais.tsx`, chamadas como `f.cnpj.includes(busca)` geravam erro não tratado `Cannot read properties of null` quando filiais não possuíam CNPJ preenchido, travando toda a tabela. Corrigido com coalescência nula `(f.cnpj || '').includes(busca)`.
4. **Atualização Imediata de Estado (UX):**
   - No `Filiais.tsx`, o salvamento agora atualiza o estado local imediatamente (`setFiliais(prev => [novaFilial, ...prev])`) além de disparar a recarga do backend, garantindo feedback instantâneo na tela.

### Testes de Isolamento e Persistência
| Cenário | Ação | Verificação | Resultado |
|---|---|---|---|
| Cadastro de Nova Filial | Preenchimento do modal na Empresa A | Salva no PostgreSQL com `TenantId` da Empresa A; aparece instantaneamente na listagem | APROVADO |
| Recarga da Página (F5) | Atualização do navegador | A filial permanece listada corretamente | APROVADO |
| Isolamento Multiempresa | Login com usuário da Empresa B | Filiais da Empresa A **não** aparecem para o usuário da Empresa B | APROVADO |
| Painel SuperAdmin | Consulta em `/admin/filiais` | Exibe todas as filiais com o nome da respectiva empresa cliente | APROVADO |

---

## 4. Compilação e Exportação do SINc como OneFile

### Diretrizes de Compilação
- **Projeto:** `NEXO-COMPRAS/agent-desktop/Nexo.Agent.Desktop.csproj`
- **Comando executado:**
  ```powershell
  dotnet publish NEXO-COMPRAS/agent-desktop/Nexo.Agent.Desktop.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o dist/NexoCompras
  ```
- **Binário Gerado:** `dist/NexoCompras/SINc.exe` (52.7 MB)
- **Tipo de Executável:** `WinExe` (Subsystem Windows GUI — sem console preto/CMD).

### Testes do Executável OneFile
1. **Independência de SDK e Pastas de Desenvolvimento:**
   - O executável contém o runtime do .NET 9, as DLLs do Windows Forms e todos os drivers nativos do SQLite e Firebird incorporados em um único arquivo `.exe`.
2. **Persistência Centralizada em `%ProgramData%\NexoCompras\SINc\`:**
   - Configurações (`appsettings.json`), banco de controle delta (`sinc_local.db`), fila offline (`offline_queue.json`) e logs são criados e lidos a partir de `%ProgramData%\NexoCompras\SINc\`.
   - Movimentar o arquivo `SINc.exe` para qualquer outra pasta ou diretório não causa perda de configurações, credenciais ou dados pendentes.
3. **Instância Única (Single-Instance):**
   - Testada abertura simultânea de dois executáveis; a segunda instância detecta o Mutex existente, foca a janela aberta e encerra sem duplicar processos.
