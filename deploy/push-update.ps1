# =============================================================================
# AutoCo – Actualitzacio remota al servidor Linux
# =============================================================================
# Ús:
#   .\push-update.ps1 -Servidor 192.168.1.10
#   .\push-update.ps1 -Servidor autoco.centre.cat -Usuari deploy -Port 2222
#   .\push-update.ps1 -Servidor 192.168.1.10 -RutaServidor /opt/autoco
#
# Requisits Windows: OpenSSH client (ssh + scp) instal·lat (Win10/11 per defecte)
# Requisits servidor: Docker Engine >= 24, Docker Compose Plugin >= 2.20
# =============================================================================

param(
    [Parameter(Mandatory, HelpMessage = "IP o nom de host del servidor Linux")]
    [string]$Servidor,

    [string]$Usuari       = "root",
    [string]$Port         = "22",
    [string]$RutaServidor = "/opt/autoco"
)

$ErrorActionPreference = "Stop"

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$RepoRoot  = Split-Path -Parent $ScriptDir

Write-Host ""
Write-Host "========================================================"
Write-Host "  AutoCo - Actualitzacio remota"
Write-Host "  Servidor : $Usuari@${Servidor}:$Port"
Write-Host "  Ruta     : $RutaServidor"
Write-Host "========================================================"

# =============================================================================
# 1. Generar paquet local
# =============================================================================
Write-Host ""
Write-Host "[1/4] Generant paquet de codi actualitzat..."

$TempDir = Join-Path $ScriptDir ("_push-temp-" + (Get-Date -Format "yyyyMMddHHmmss"))
& "$ScriptDir\generar-deploy.ps1" -Dest $TempDir

if (-not (Test-Path $TempDir)) {
    Write-Error "  Error generant el paquet. Atura't."
    exit 1
}

Write-Host "    Paquet generat: $TempDir"

# =============================================================================
# 2. Verificar connectivitat SSH
# =============================================================================
Write-Host ""
Write-Host "[2/4] Verificant connexio SSH..."

$sshTarget = "${Usuari}@${Servidor}"

try {
    $testOutput = & ssh -p $Port -o ConnectTimeout=10 -o BatchMode=yes $sshTarget "echo OK" 2>&1
    if ($testOutput -ne "OK") {
        throw "Resposta inesperada: $testOutput"
    }
    Write-Host "    Connexio SSH OK"
} catch {
    Write-Host ""
    Write-Host "  [!] No s'ha pogut connectar sense contrassenya."
    Write-Host "      Pot ser que calgui autenticar-se per primera vegada."
    Write-Host "      Si uses clau SSH: ssh-copy-id -p $Port ${sshTarget}"
    Write-Host ""
    $resp = Read-Host "    Vols continuar igualment (s'autenticarà interactivament)? [s/N]"
    if ($resp -notmatch "^[sS]$") { exit 0 }
}

# =============================================================================
# 3. Sincronitzar fitxers al servidor (sense sobreescriure .env ni SSL)
# =============================================================================
Write-Host ""
Write-Host "[3/4] Sincronitzant fitxers al servidor..."
Write-Host "      (es preserven .env i certificats SSL existents)"

# Assegurar que el directori de destí existeix al servidor
& ssh -p $Port $sshTarget "mkdir -p ${RutaServidor}/nginx/ssl"

# Llista de directoris i fitxers a sincronitzar (SRC -> DEST relatiu)
# Excloem: .env (pot tenir credencials de producció), nginx/ssl (certificats)
$itemsToSync = @(
    @{ Src = "api";             Dest = "$RutaServidor/api" },
    @{ Src = "web";             Dest = "$RutaServidor/web" },
    @{ Src = "shared";          Dest = "$RutaServidor/shared" },
    @{ Src = "docker-compose.yml"; Dest = "$RutaServidor/docker-compose.yml" },
    @{ Src = "nginx/Dockerfile";   Dest = "$RutaServidor/nginx/Dockerfile" },
    @{ Src = "nginx/nginx.conf";   Dest = "$RutaServidor/nginx/nginx.conf" },
    @{ Src = "nginx/entrypoint.sh"; Dest = "$RutaServidor/nginx/entrypoint.sh" },
    @{ Src = "install.sh";         Dest = "$RutaServidor/install.sh" },
    @{ Src = "update.sh";          Dest = "$RutaServidor/update.sh" },
    @{ Src = "backup.sh";          Dest = "$RutaServidor/backup.sh" },
    @{ Src = ".env.example";       Dest = "$RutaServidor/.env.example" }
)

$errors = 0
foreach ($item in $itemsToSync) {
    $localPath = Join-Path $TempDir $item.Src
    if (-not (Test-Path $localPath)) {
        Write-Warning "    No trobat localment: $($item.Src) (saltat)"
        continue
    }

    Write-Host "    -> $($item.Src)"
    $scpArgs = @("-P", $Port, "-r", $localPath, "${sshTarget}:$($item.Dest)")
    $result = & scp @scpArgs 2>&1
    if ($LASTEXITCODE -ne 0) {
        Write-Warning "    [!] Error copiant $($item.Src): $result"
        $errors++
    }
}

# Crear .env si no existeix al servidor (primera vegada)
& ssh -p $Port $sshTarget @"
if [ ! -f ${RutaServidor}/.env ]; then
    cp ${RutaServidor}/.env.example ${RutaServidor}/.env
    echo '    -> .env creat a partir de .env.example. EDITA les variables!'
fi
"@

if ($errors -gt 0) {
    Write-Host ""
    Write-Warning "  $errors fitxer(s) no s'han pogut copiar. Revisa la connexio."
    $resp = Read-Host "    Vols continuar amb l'actualitzacio igualment? [s/N]"
    if ($resp -notmatch "^[sS]$") {
        Remove-Item -Recurse -Force $TempDir
        exit 1
    }
}

# =============================================================================
# 4. Executar update.sh al servidor
# =============================================================================
Write-Host ""
Write-Host "[4/4] Executant actualitzacio al servidor..."

& ssh -p $Port -t $sshTarget "bash ${RutaServidor}/update.sh"

if ($LASTEXITCODE -ne 0) {
    Write-Host ""
    Write-Warning "  update.sh ha finalitzat amb errors. Comprova els logs:"
    Write-Host "    ssh -p $Port ${sshTarget} 'docker compose -f ${RutaServidor}/docker-compose.yml logs --tail=50'"
} else {
    Write-Host ""
    Write-Host "========================================================"
    Write-Host "  Actualitzacio completada correctament!"
    Write-Host ""
    Write-Host "  Per veure els logs:"
    Write-Host "    ssh -p $Port ${sshTarget} 'cd ${RutaServidor} && docker compose logs -f'"
    Write-Host "========================================================"
}

# Netejar directori temporal
Remove-Item -Recurse -Force $TempDir
Write-Host ""
Write-Host "  (Directori temporal eliminat)"
