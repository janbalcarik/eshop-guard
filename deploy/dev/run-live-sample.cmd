@echo off
rem Ziva ukazka jednim prikazem (krok 3.12 v LOKALNI-OVERENI.md). Z cmd v korenu repozitare:
rem   deploy\dev\run-live-sample.cmd
rem   deploy\dev\run-live-sample.cmd -Mock      (zkouska zdarma s falesnym Jevem a OpenAI)
rem Parametry se predaji skriptu run-live-sample.ps1 (PowerShell 7).
setlocal
where pwsh >nul 2>nul
if errorlevel 1 goto nopwsh
pwsh -NoProfile -ExecutionPolicy Bypass -File "%~dp0run-live-sample.ps1" %*
set "code=%errorlevel%"
echo.
pause
exit /b %code%

:nopwsh
echo Chybi PowerShell 7 (pwsh), skript ho potrebuje.
choice /c AN /m "Nainstalovat ho ted pres winget"
if errorlevel 2 exit /b 1
winget install --id Microsoft.PowerShell --source winget
echo.
echo Po instalaci zavrete toto okno, otevrete nove a spustte skript znovu.
pause
exit /b 1
