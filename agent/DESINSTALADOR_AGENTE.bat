@echo off
chcp 65001 >nul
title Desinstalador NexusTI Remote Agent

echo ====================================================================
echo      NEXUSTI — DESINSTALAÇÃO DO AGENTE REMOTO & SUPORTE
echo ====================================================================
echo.

:: Verifica permissão de Administrador
net session >nul 2>&1
if %errorlevel% neq 0 (
    echo [AVISO] Solicitando permissões de Administrador...
    powershell -Command "Start-Process '%~0' -Verb RunAs"
    exit /b
)

set "TARGET_DIR=C:\Nexus-Agent"
set "ALT_DIR=C:\FSC-Agent"
set "USER_DIR=%LOCALAPPDATA%\NexusTI\Nexus-Agent"

echo [*] Encerrando processos do NexusTI Remote Agent...
taskkill /f /im NexusRemoteAgent.exe >nul 2>&1
taskkill /f /im FSCRemoteAgent.exe >nul 2>&1
taskkill /f /im FSCRemoteAgent_debug.exe >nul 2>&1

echo [*] Removendo inicialização automática do Windows...
reg delete "HKCU\Software\Microsoft\Windows\CurrentVersion\Run" /v "NexusRemoteAgent" /f >nul 2>&1
reg delete "HKLM\Software\Microsoft\Windows\CurrentVersion\Run" /v "NexusRemoteAgent" /f >nul 2>&1
reg delete "HKCU\Software\Microsoft\Windows\CurrentVersion\Run" /v "FSCRemoteAgent" /f >nul 2>&1
reg delete "HKLM\Software\Microsoft\Windows\CurrentVersion\Run" /v "FSCRemoteAgent" /f >nul 2>&1

echo [*] Removendo registro de programas do Windows...
reg delete "HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\NexusRemoteAgent" /f >nul 2>&1
reg delete "HKLM\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\NexusRemoteAgent" /f >nul 2>&1
reg delete "HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\NexusRemoteAgent" /f >nul 2>&1
reg delete "HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\FSCRemoteAgent" /f >nul 2>&1
reg delete "HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\FSCRemoteAgent" /f >nul 2>&1

echo [*] Removendo regras do Windows Defender Firewall...
netsh advfirewall firewall delete rule name="NexusTI Remote Agent" >nul 2>&1
netsh advfirewall firewall delete rule name="NexusTI Remote Agent Port 8090" >nul 2>&1
netsh advfirewall firewall delete rule name="FSC Remote Agent" >nul 2>&1

echo [*] Removendo atalhos da Área de Trabalho e Menu Iniciar...
del /f /q "%USERPROFILE%\Desktop\NexusTI - Suporte & Chamados.lnk" >nul 2>&1
del /f /q "%USERPROFILE%\Desktop\NexusTI - Abrir Chamado (Web).url" >nul 2>&1
del /f /q "%USERPROFILE%\Desktop\Abrir Chamado T.I.lnk" >nul 2>&1
del /f /q "%USERPROFILE%\Desktop\Abrir Chamado T.I (Web).url" >nul 2>&1
del /f /q "%PUBLIC%\Desktop\NexusTI - Suporte & Chamados.lnk" >nul 2>&1
del /f /q "%PUBLIC%\Desktop\Abrir Chamado T.I.lnk" >nul 2>&1
if exist "%APPDATA%\Microsoft\Windows\Start Menu\Programs\NexusTI" rd /s /q "%APPDATA%\Microsoft\Windows\Start Menu\Programs\NexusTI" >nul 2>&1
if exist "%APPDATA%\Microsoft\Windows\Start Menu\Programs\Fast Solutions Corporate" rd /s /q "%APPDATA%\Microsoft\Windows\Start Menu\Programs\Fast Solutions Corporate" >nul 2>&1

echo [*] Excluindo arquivos da pasta do agente...
if exist "%TARGET_DIR%" rd /s /q "%TARGET_DIR%" >nul 2>&1
if exist "%ALT_DIR%" rd /s /q "%ALT_DIR%" >nul 2>&1
if exist "%USER_DIR%" rd /s /q "%USER_DIR%" >nul 2>&1

echo.
echo ====================================================================
echo   ✓ DESINSTALAÇÃO NEXUSTI CONCLUÍDA!
echo ====================================================================
echo O agente NexusTI foi totalmente removido do sistema.
echo.
pause
