<#
desbloquear_impresora.ps1 - Deja la PC lista para la térmica de Sabores Wanka.
  1. Quita el bloqueo "descargado de internet" a todos los archivos.
  2. Agrega exclusiones de Windows Defender (carpeta + exe) para que el
     antivirus no frene la app ni el reemplazo de archivos al actualizar.
  3. Activa la cola de impresión y lista las impresoras instaladas.

Uso (clic derecho > Ejecutar con PowerShell, o desde consola admin):
  powershell -ExecutionPolicy Bypass -File .\desbloquear_impresora.ps1
  powershell -ExecutionPolicy Bypass -File .\desbloquear_impresora.ps1 -Carpeta "C:\SaboresWanka"

Requiere administrador (se reabre solo si falta).
#>
param(
  [string]$Carpeta = (Get-Location).Path
)

$ErrorActionPreference = "Stop"

# --- Auto-elevar a administrador ---
$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()
  ).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
  Write-Host "Reabriendo como administrador..."
  Start-Process powershell.exe -Verb RunAs -ArgumentList "-ExecutionPolicy Bypass -File `"$PSCommandPath`" -Carpeta `"$Carpeta`""
  exit
}

if (-not (Test-Path $Carpeta)) { throw "No existe la carpeta: $Carpeta" }
Write-Host "Carpeta: $Carpeta"

# --- 1. Desbloquear archivos descargados ---
Get-ChildItem $Carpeta -Recurse -File -ErrorAction SilentlyContinue | Unblock-File
Write-Host "[OK] Archivos desbloqueados."

# --- 2. Exclusiones de Defender (app + reemplazo en actualizaciones) ---
$exe = Join-Path $Carpeta "sabores wanka.exe"
Add-MpPreference -ExclusionPath $Carpeta
if (Test-Path $exe) {
  Add-MpPreference -ExclusionProcess $exe
  Write-Host "[OK] Exclusiones: carpeta + sabores wanka.exe"
} else {
  Write-Host "[OK] Exclusión: carpeta (exe no encontrado aquí, se excluye al instalar)"
}

# --- 3. Cola de impresión activa ---
$spooler = Get-Service -Name Spooler -ErrorAction SilentlyContinue
if ($spooler -and $spooler.Status -ne "Running") {
  Start-Service -Name Spooler
  Write-Host "[OK] Cola de impresión iniciada."
} else {
  Write-Host "[OK] Cola de impresión activa."
}

# --- 4. Impresoras instaladas ---
Write-Host ""
Write-Host "Impresoras instaladas:"
$impresoras = Get-Printer -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Name
if ($impresoras) { $impresoras | ForEach-Object { Write-Host "  - $_" } }
else { Write-Host "  (ninguna) Instala tu térmica en Configuración > Impresoras y elígela en el POS (tab Impresoras)." }

Write-Host ""
Write-Host "Listo. Si usas otro antivirus (ESET, Avast, etc.), agrega la misma"
Write-Host "carpeta a sus exclusiones manualmente: solo Defender se configura aquí."
