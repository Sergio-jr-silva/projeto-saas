-- Permissões padrão do nexo_app em objetos criados pelo nexo_migrator (executar conectado ao banco "nexo").
GRANT CONNECT ON DATABASE nexo TO nexo_app;
GRANT USAGE ON SCHEMA public TO nexo_app;
ALTER DEFAULT PRIVILEGES FOR ROLE nexo_migrator IN SCHEMA public GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO nexo_app;
ALTER DEFAULT PRIVILEGES FOR ROLE nexo_migrator IN SCHEMA public GRANT USAGE, SELECT ON SEQUENCES TO nexo_app;

