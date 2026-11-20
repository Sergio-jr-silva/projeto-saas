@echo off
chcp 65001 >nul
title Nexo Compras — Encerramento de Serviços

echo ==============================================================================
echo           NEXO COMPRAS — ENCERRANDO TODOS OS SERVIÇOS
echo ==============================================================================
echo.

echo [1/3] Encerrando Sincronizador SINc...
taskkill /f /im SINc.exe 2>nul
if %errorlevel% equ 0 (echo       SINc finalizado.) else (echo       SINc nao estava em execucao.)

echo [2/3] Encerrando Backend API (Nexo.Api.exe)...
taskkill /f /im Nexo.Api.exe 2>nul
if %errorlevel% equ 0 (echo       Backend finalizado.) else (echo       Backend nao estava em execucao.)

echo [3/3] Encerrando servidor do Frontend Web (porta 5173)...
for /f "tokens=5" %%a in ('netstat -aon ^| findstr :5173 ^| findstr LISTENING 2^>nul') do (
    taskkill /f /pid %%a >nul 2>&1
)
echo       Porta 5173 liberada.

echo.
echo ==============================================================================
echo   TODOS OS SERVIÇOS FORAM ENCERRADOS COM SUCESSO.
echo ==============================================================================
echo.
pause
