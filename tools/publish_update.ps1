<#
publish_update.ps1 - Publica una nueva version de Sabores Wanka (GitHub Releases, repo PRIVADO).
Patron del proyecto hermano Tiendas Nachis 2, adaptado a Wanka (sin Supabase: la llave de
LECTURA viaja dentro del ZIP como github_update.json, nunca en git).

Uso:
  .\tools\publish_update.ps1                        # patch 1.0.0 -> 1.0.1
  .\tools\publish_update.ps1 -NewVersion 1.1.0      # version exacta
  .\tools\publish_update.ps1 -Draft                 # draft (las PCs no lo ven)
  .\tools\publish_update.ps1 -ZipOnly               # solo genera el ZIP sin subir

Llaves:
  LECTURA (para las PCs, dentro del ZIP): -ReadToken, o env SW_READ_TOKEN,
    o archivo local tools/.github-token (git-ignored). Fine-grained, solo
    Contents: Read sobre el repo privado.
  ESCRITURA (para subir el release): GitHub CLI 'gh' autenticado, o env
    GH_TOKEN con Contents: Read and write.

  Owner/Repo: tools/publish_config.json (sin secretos).
#>
param(
  [string]$NewVersion = "",
  [string]$ReadToken = "",
  [switch]$Draft,
  [switch]$ZipOnly
)

$ErrorActionPreference = "Stop"
$raiz = Split-Path -Parent $PSScriptRoot
$proyecto = Join-Path $raiz "sabores wanka\sabores wanka.csproj"

# --- Config owner/repo (sin secretos) ---
$cfgPath = Join-Path $PSScriptRoot "publish_config.json"
if (-not (Test-Path $cfgPath)) { throw "Falta tools/publish_config.json (owner/repo)." }
$cfg = Get-Content -Raw $cfgPath | ConvertFrom-Json
$owner = $cfg.owner
$repo = $cfg.repo
if ([string]::IsNullOrWhiteSpace($owner) -or [string]::IsNullOrWhiteSpace($repo)) {
  throw "Completa owner/repo en tools/publish_config.json."
}

# --- Llave de LECTURA (solo local, nunca al repo) ---
if ([string]::IsNullOrWhiteSpace($ReadToken)) { $ReadToken = $env:SW_READ_TOKEN }
$tokenFile = Join-Path $PSScriptRoot ".github-token"
if ([string]::IsNullOrWhiteSpace($ReadToken) -and (Test-Path $tokenFile)) {
  $ReadToken = (Get-Content -Raw $tokenFile).Trim()
}
if ([string]::IsNullOrWhiteSpace($ReadToken)) {
  throw "Falta la llave de LECTURA: -ReadToken, env SW_READ_TOKEN o tools/.github-token."
}

# --- Version: bump <Version> del csproj (patch+1 si no se indica) ---
$xml = Get-Content -Raw $proyecto
if ($xml -notmatch "<Version>([^<]*)</Version>") { throw "El csproj no tiene <Version>." }
$actual = $Matches[1].Trim()
if ([string]::IsNullOrWhiteSpace($NewVersion)) {
  $p = $actual.Split(".")
  $NewVersion = "$($p[0]).$($p[1]).$([int]$p[2] + 1)"
}
$xml = $xml -replace "<Version>[^<]*</Version>", "<Version>$NewVersion</Version>"
Set-Content -Path $proyecto -Value $xml -Encoding utf8BOM
Write-Host "Version: $actual -> $NewVersion"

# --- Publish autocontenido win-x64 ---
$stage = Join-Path ([System.IO.Path]::GetTempPath()) ("sw_publish_" + [System.Guid]::NewGuid().ToString("N"))
$appDir = Join-Path $stage "app"
dotnet publish $proyecto -c Release -r win-x64 -p:Platform=x64 -p:SelfContained=true -o $appDir
if ($LASTEXITCODE -ne 0) { throw "dotnet publish fallo." }
$exe = Get-ChildItem $appDir -Filter "*.exe" | Select-Object -First 1
if ($null -eq $exe) { throw "El publish no genero exe en $appDir." }

# --- Inyecta la config de update (con la llave de LECTURA) junto al exe ---
@{ owner = $owner; repo = $repo; token = $ReadToken; enabled = $true } |
  ConvertTo-Json | Set-Content (Join-Path $appDir "github_update.json") -Encoding utf8NoBOM

# --- ZIP + verificacion anti-fugas (sin fuentes ni secretos de dev) ---
$zipPath = Join-Path $PSScriptRoot ("sabores-wanka-update-v" + $NewVersion + ".zip")
if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
Compress-Archive -Path (Join-Path $appDir "*") -DestinationPath $zipPath
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip = [System.IO.Compression.ZipFile]::OpenRead($zipPath)
$fugas = @($zip.Entries | Where-Object { $_.FullName -match "\.(cs|xaml|csproj|sln|md|sql|ps1|pdb|user|suo)$" })
$zip.Dispose()
if ($fugas.Count -gt 0) {
  Write-Host ($fugas | ForEach-Object { $_.FullName })
  throw "El ZIP contiene archivos fuente. Revisa el publish."
}
Write-Host "ZIP OK: $zipPath"

if ($ZipOnly) {
  Write-Host "Modo ZipOnly: no se subio nada. Commit del bump queda pendiente."
  return
}

# --- Subida a GitHub Releases ---
$tag = "v" + $NewVersion
$notas = "Sabores Wanka $tag. Las PCs la detectan al siguiente inicio (o con Buscar actualizaciones)."
$gh = Get-Command gh -ErrorAction SilentlyContinue
if ($gh) {
  $draftFlag = @()
  if ($Draft) { $draftFlag = @("--draft") }
  gh release create $tag $zipPath --repo "$owner/$repo" --title $tag --notes $notas @draftFlag
} else {
  if ([string]::IsNullOrWhiteSpace($env:GH_TOKEN)) { throw "Sin 'gh' se requiere env GH_TOKEN (Contents: Read and write)." }
  $h = @{ Authorization = "Bearer $($env:GH_TOKEN)"; "User-Agent" = "SaboresWanka-Publisher"; Accept = "application/vnd.github+json" }
  $body = @{ tag_name = $tag; name = $tag; body = $notas; draft = [bool]$Draft; prerelease = $false } | ConvertTo-Json
  $rel = Invoke-RestMethod -Method Post -Uri "https://api.github.com/repos/$owner/$repo/releases" -Headers $h -Body $body -ContentType "application/json"
  $up = $rel.upload_url -replace "\{\?name,label\}", "?name=$([System.IO.Path]::GetFileName($zipPath))"
  $h2 = @{ Authorization = "Bearer $($env:GH_TOKEN)"; "User-Agent" = "SaboresWanka-Publisher"; "Content-Type" = "application/zip" }
  Invoke-RestMethod -Method Post -Uri $up -Headers $h2 -InFile $zipPath | Out-Null
}

Remove-Item $zipPath -Force -ErrorAction SilentlyContinue
Remove-Item $stage -Recurse -Force -ErrorAction SilentlyContinue
Write-Host "Publicado $tag. Haz commit del bump de version."
