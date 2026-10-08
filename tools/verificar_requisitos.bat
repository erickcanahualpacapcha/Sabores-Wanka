@echo off
REM verificar_requisitos.bat - Revisa que la PC tenga todo para Sabores Wanka.
REM Solo lectura (no instala nada). Si algo FALTA, instala desde el link.
chcp 65001 >nul
setlocal EnableDelayedExpansion
title Sabores Wanka - Verificacion de requisitos

set FALLOS=0
echo ================================================
echo  Sabores Wanka - Verificacion de requisitos
echo ================================================
echo.

REM --- 1. Windows 10 1809+ / 11 (build >= 17763, ideal 19041+) ---
for /f "tokens=3" %%a in ('reg query "HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion" /v CurrentBuildNumber 2^>nul') do set BUILD=%%a
if not defined BUILD ( set BUILD=0 )
if %BUILD% GEQ 19041 (
  echo [OK] Windows build %BUILD% ^(compatible^)
) else (
  if %BUILD% GEQ 17763 (
    echo [OK] Windows build %BUILD% ^(minimo, se recomienda 19041+^)
  ) else (
    echo [FALTA] Windows build %BUILD% - se requiere Windows 10 1809+ o Windows 11
    set /a FALLOS+=1
  )
)

REM --- 2. WebView2 (lo usa la vista de WhatsApp) ---
set WV=0
reg query "HKLM\SOFTWARE\WOW6432Node\Microsoft\EdgeUpdate\ClientState" /s 2>nul | findstr /R /C:"REG_SZ.*[0-9][0-9]*\.[0-9]" >nul && set WV=1
reg query "HKLM\SOFTWARE\Microsoft\EdgeUpdate\ClientState" /s 2>nul | findstr /R /C:"REG_SZ.*[0-9][0-9]*\.[0-9]" >nul && set WV=1
reg query "HKCU\Software\Microsoft\EdgeUpdate\ClientState" /s 2>nul | findstr /R /C:"REG_SZ.*[0-9][0-9]*\.[0-9]" >nul && set WV=1
if %WV%==1 (
  echo [OK] WebView2 Runtime instalado
) else (
  echo [FALTA] WebView2 Runtime - instalar desde:
  echo        https://developer.microsoft.com/microsoft-edge/webview2/
  set /a FALLOS+=1
)

REM --- 3. Visual C++ Redistribuible x64 (librerias nativas) ---
reg query "HKLM\SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\X64" /v Version >nul 2>&1
if %ERRORLEVEL%==0 (
  echo [OK] Visual C++ Redistribuible x64 instalado
) else (
  echo [FALTA] Visual C++ Redistribuible x64 - instalar desde:
  echo        https://aka.ms/vs/17/release/vc_redist.x64.exe
  set /a FALLOS+=1
)

REM --- 4. Cola de impresion (spooler) para la termica ---
sc query Spooler 2>nul | find "RUNNING" >nul
if %ERRORLEVEL%==0 (
  echo [OK] Cola de impresion activa
) else (
  echo [FALTA] Cola de impresion detenida - ejecutar: net start Spooler
  set /a FALLOS+=1
)

REM --- 5. Impresoras instaladas (informativo) ---
echo.
echo Impresoras instaladas:
powershell -NoProfile -Command "Get-Printer | Select-Object -ExpandProperty Name" 2>nul
if %ERRORLEVEL% NEQ 0 echo   ^(no se pudo listar - revisar manual^)

echo.
echo ================================================
if %FALLOS%==0 (
  echo  TODO OK - puedes ejecutar sabores wanka.exe
) else (
  echo  Faltan %FALLOS% requisito^(s^) - instalalos y repite
)
echo ================================================
pause
