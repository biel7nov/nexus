@echo off
chcp 65001 >nul
title Nexus TI — Desinstalador do Agente Remoto
color 0C

net session >nul 2>&1
if %errorlevel% neq 0 (
    powershell -NoProfile -Command "Start-Process '%~f0' -Verb RunAs"
    exit /b
)

echo.
echo ====================================================================
echo   NEXUS TI — DESINSTALACAO DO AGENTE REMOTO
echo ====================================================================
echo.
echo  Encerrando servicos e removendo configuracoes...
echo.

taskkill /f /im NexusRemoteAgent.exe >nul 2>&1
taskkill /f /im FSCRemoteAgent.exe >nul 2>&1

reg delete "HKCU\Software\Microsoft\Windows\CurrentVersion\Run" /v "NexusRemoteAgent" /f >nul 2>&1
reg delete "HKCU\Software\Microsoft\Windows\CurrentVersion\Run" /v "FSCRemoteAgent" /f >nul 2>&1

netsh advfirewall firewall delete rule name="NexusTI Remote Agent" >nul 2>&1
netsh advfirewall firewall delete rule name="FSC Remote Agent" >nul 2>&1

del /f /q "%USERPROFILE%\Desktop\Abrir Chamado T.I*.lnk" >nul 2>&1
del /f /q "%USERPROFILE%\Desktop\Abrir Chamado T.I*.url" >nul 2>&1

if exist "C:\Nexus-Agent" (
    rmdir /s /q "C:\Nexus-Agent" >nul 2>&1
)

echo.
echo ====================================================================
echo   OK: Nexus TI Agent desinstalado com sucesso!
echo ====================================================================
echo.
pause
