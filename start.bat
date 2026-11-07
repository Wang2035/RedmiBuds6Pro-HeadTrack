@echo off
setlocal
cd /d "%~dp0"
title headtrack - REDMI Buds 6 Pro

where node >nul 2>nul
if errorlevel 1 (
  echo.
  echo   [!] node.exe not found
  echo       Install Node.js LTS: https://nodejs.org/
  echo.
  pause
  exit /b 1
)

if not exist "HidReader.exe" (
  echo [!] HidReader.exe missing
  pause
  exit /b 1
)
if not exist "KeepAlive.exe" (
  echo [!] KeepAlive.exe missing
  pause
  exit /b 1
)

echo.
echo   REDMI Buds 6 Pro  head tracking
echo   ----------------------------------------
echo   browser will open  http://127.0.0.1:8899/
echo   closing this window stops everything
echo.

rem ---- HID binding self-check: only asks for admin when the binding is wrong ----
copy /y "%~dp0fix-hid.ps1" "%TEMP%\buds-fix.ps1" >nul 2>nul
if exist "%TEMP%\buds-fix.ps1" (
  powershell -NoProfile -ExecutionPolicy Bypass -File "%TEMP%\buds-fix.ps1" --check >nul 2>nul
  if errorlevel 10 goto repair
  if errorlevel 3 echo   [i] buds HID node not present - pair the earbuds first
  goto run
)

:repair
echo   [!] HID node bound to the wrong driver, repairing...
echo       one UAC prompt will appear
powershell -NoProfile -ExecutionPolicy Bypass -Command "Start-Process -FilePath 'powershell.exe' -ArgumentList '-NoProfile','-ExecutionPolicy','Bypass','-File','%TEMP%\buds-fix.ps1','--run' -Verb RunAs -Wait" >nul 2>nul
echo   [i] repair done
echo.

:run
node server.js

echo.
echo [server stopped]
pause