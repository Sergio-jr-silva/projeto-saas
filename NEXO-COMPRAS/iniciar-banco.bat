@echo off
title PostgreSQL Server (Porta 5433) - Nexo Compras
chcp 65001 >nul
set "SCRATCH_DIR=C:\Users\sergi\.gemini\antigravity\brain\a0a4efb5-f373-4ffc-9d90-fd2b446eb87d\scratch"
set "PG_BIN=%SCRATCH_DIR%\pg\node_modules\@embedded-postgres\windows-x64\native\bin"
set "PG_DATA=%SCRATCH_DIR%\pgdata"

echo ===================================================================
echo   POSTGRESQL SERVER - PORTA 5433 (NEXO COMPRAS)
echo ===================================================================

if exist "%PG_DATA%\postmaster.pid" (
    tasklist /fi "imagename eq postgres.exe" | findstr /i "postgres.exe" >nul
    if errorlevel 1 (
        del /f /q "%PG_DATA%\postmaster.pid" >nul 2>&1
    )
)

echo Servidor iniciando na porta 5433...
"%PG_BIN%\postgres.exe" -D "%PG_DATA%" -p 5433
pause

