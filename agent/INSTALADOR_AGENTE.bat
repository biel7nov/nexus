@echo off
chcp 65001 >nul
title NexusTI - Instalador do Agente Remoto
cd /d "%~dp0"

echo ====================================================================
echo        NEXUSTI — INSTALADOR DO AGENTE REMOTO & SUPORTE
echo ====================================================================
echo.
echo  Iniciando assistente oficial de instalacao NexusTI...
echo.

if exist "%~dp0Instalador-Nexus-Agent.exe" (
    start "" "%~dp0Instalador-Nexus-Agent.exe"
    exit /b 0
)

if exist "%~dp0agent\Instalador-Nexus-Agent.exe" (
    start "" "%~dp0agent\Instalador-Nexus-Agent.exe"
    exit /b 0
)

echo [!] Instalador-Nexus-Agent.exe nao encontrado nesta pasta.
pause
