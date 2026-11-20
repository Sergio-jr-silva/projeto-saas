-- Papéis do banco do Nexo Compras (executar como superusuário, uma única vez por ambiente).
-- nexo_migrator: dono do schema; executa as migrations.
-- nexo_app:      usado pela API em runtime. NÃO é superusuário e NÃO ignora RLS.
-- Troque as senhas fora do ambiente de desenvolvimento.
DO $$
BEGIN
  IF NOT EXISTS (SELECT FROM pg_roles WHERE rolname = 'nexo_migrator') THEN
    CREATE ROLE nexo_migrator LOGIN PASSWORD 'migrator_dev' NOSUPERUSER NOBYPASSRLS;
  END IF;
  IF NOT EXISTS (SELECT FROM pg_roles WHERE rolname = 'nexo_app') THEN
    CREATE ROLE nexo_app LOGIN PASSWORD 'app_dev' NOSUPERUSER NOBYPASSRLS;
  END IF;
END $$;

SELECT 'CREATE DATABASE nexo OWNER nexo_migrator ENCODING ''UTF8'''
WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = 'nexo')\gexec

