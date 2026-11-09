================================================================================
NEXO COMPRAS — BASE DE DADOS FIREBIRD DE DEMONSTRAÇÃO
Arquivos: BM.FDB (Banco de Dados Ativo) | BM.FBK (Arquivo de Backup Compactado)
================================================================================

1. DESCRIÇÃO
Esta pasta contém a base Firebird de exemplo para testes e homologação do Sincronizador
SINc e da plataforma Nexo Compras em outros computadores. A base contém cadastros
completos de produtos, grupos, estoque e fornecedores para validação dos fluxos.

2. PARÂMETROS PADRÃO DE CONEXÃO
- Gerenciador (SGBD): Firebird SQL Server (versões 2.5, 3.0, 4.0 ou 5.0)
- Host: 127.0.0.1 (ou o IP do computador onde o Firebird está instalado)
- Porta: 3050 (padrão Firebird)
- Arquivo do Banco: C:\Users\sergi\OneDrive\Desktop\NEXO_COMPRAS_TESTE\BANCO_EXEMPLO\BM.FDB
  (ou o caminho onde a pasta for copiada no computador de destino)
- Usuário: SYSDBA
- Senha: masterkey
- Charset: NONE (ou WIN1252 / ISO-8859-1)
- Dialeto SQL: 3

3. COMO RESTAURAR UMA NOVA BASE A PARTIR DO BM.FBK (SE NECESSÁRIO)
Caso queira restaurar uma cópia limpa do banco a partir do backup:
Utilize o utilitário gbak do Firebird via linha de comando:

  gbak -c -v -user SYSDBA -pas masterkey BM.FBK BM_RESTAURADO.FDB

4. CONFIGURAÇÃO NO SINc.exe
a) Abra o SINc.exe e clique no botão de engrenagem no canto superior direito ("Configurações").
b) Acesse a aba "🗄️ Banco de Dados ERP".
c) Selecione "Firebird".
d) No campo "Caminho do Banco", clique em procurar ou digite o caminho completo para o arquivo BM.FDB.
e) Verifique se Usuário está como SYSDBA e Senha como masterkey.
f) Clique no botão "Testar Conexão". O resultado "Conexão com Firebird estabelecida com sucesso!" será exibido.
g) Clique em "Salvar Configurações".
================================================================================

