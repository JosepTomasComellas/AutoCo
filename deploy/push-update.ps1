# =============================================================================
# AutoCo – Actualitzacio remota al servidor Linux
# =============================================================================
# Us:
#   .\push-update.ps1                          # servidor per defecte
#   .\push-update.ps1 -Servidor 192.168.1.10   # servidor alternatiu
#   .\push-update.ps1 -ConfigurarClau          # instal·la la clau SSH (1a vegada)
#
# Requisits Windows: OpenSSH client (ssh + scp) — inclòs a Win10/11 per defecte
# Requisits servidor: Docker Engine >= 24, Docker Compose Plugin >= 2.20
# =============================================================================

param(
    [string]$Servidor      = "ct-AutoCo.tute.int",
    [string]$Usuari        = "root",
    [string]$Port          = "22",
    [string]$RutaServidor  = "/docker/AutoCo",
    [switch]$ConfigurarClau   # passa aquest flag la primera vegada per instal·lar la clau SSH
)

$ErrorActionPreference = "Stop"

$ScriptDir  = Split-Path -Parent $MyInvocation.MyCommand.Path
$RepoRoot   = Split-Path -Parent $ScriptDir
$sshTarget  = "${Usuari}@${Servidor}"

# Fitxer de clau SSH a usar (ed25519 preferit, rsa com a alternativa)
$KeyFile = "$env:USERPROFILE\.ssh\id_ed25519"
if (-not (Test-Path $KeyFile)) { $KeyFile = "$env:USERPROFILE\.ssh\id_rsa" }

# Arguments SSH comuns (clau + no StrictHostKeyChecking la primera vegada)
function Get-SshArgs {
    $args = @("-p", $Port)
    if (Test-Path $KeyFile) { $args += @("-i", $KeyFile) }
    $args += @("-o", "BatchMode=yes", "-o", "ConnectTimeout=10")
    return $args
}

Write-Host ""
Write-Host "========================================================"
Write-Host "  AutoCo - Actualitzacio remota"
Write-Host "  Servidor : $sshTarget  (port $Port)"
Write-Host "  Ruta     : $RutaServidor"
Write-Host "========================================================"

# =============================================================================
# PAS 0 (opcional): Instal·lar clau SSH al servidor
# =============================================================================
if ($ConfigurarClau) {
    Write-Host ""
    Write-Host "[SSH] Configurant autenticacio per clau..."

    # Generar clau si no existeix
    if (-not (Test-Path $KeyFile)) {
        Write-Host "    Generant clau ed25519 nova..."
        $sshDir = "$env:USERPROFILE\.ssh"
        if (-not (Test-Path $sshDir)) { New-Item -ItemType Directory -Path $sshDir | Out-Null }
        & ssh-keygen -t ed25519 -f "$env:USERPROFILE\.ssh\id_ed25519" -N '""' -C "autoco-deploy"
        Write-Host "    Clau generada: $env:USERPROFILE\.ssh\id_ed25519"
        $KeyFile = "$env:USERPROFILE\.ssh\id_ed25519"
    } else {
        Write-Host "    Clau ja existeix: $KeyFile"
    }

    $pubKeyFile = "$KeyFile.pub"
    if (-not (Test-Path $pubKeyFile)) {
        Write-Error "  No s'ha trobat la clau publica: $pubKeyFile"
        exit 1
    }

    $pubKey = (Get-Content $pubKeyFile -Raw).Trim()
    Write-Host "    Clau publica: $pubKey"
    Write-Host ""
    Write-Host "    S'instal·lara la clau al servidor (demanarà la contrasenya una sola vegada)..."

    # Instal·lar clau al servidor (equivalent a ssh-copy-id)
    $remoteCmd = "mkdir -p ~/.ssh && chmod 700 ~/.ssh && echo '$pubKey' >> ~/.ssh/authorized_keys && sort -u ~/.ssh/authorized_keys -o ~/.ssh/authorized_keys && chmod 600 ~/.ssh/authorized_keys && echo 'Clau instal·lada OK'"
    & ssh -p $Port -o ConnectTimeout=15 $sshTarget $remoteCmd

    if ($LASTEXITCODE -ne 0) {
        Write-Error "  Error instal·lant la clau. Comprova les credencials."
        exit 1
    }

    Write-Host ""
    Write-Host "  Clau SSH instal·lada correctament."
    Write-Host "  A partir d'ara pots executar el script sense -ConfigurarClau ni contrasenya."
    Write-Host ""
    $resp = Read-Host "  Vols continuar amb l'actualitzacio ara? [S/n]"
    if ($resp -match "^[nN]$") { exit 0 }
}

# =============================================================================
# PAS 1: Verificar connexio SSH sense contrasenya
# =============================================================================
Write-Host ""
Write-Host "[1/4] Verificant connexio SSH..."

$sshArgs = Get-SshArgs
$testOutput = & ssh @sshArgs $sshTarget "echo OK" 2>&1

if ($LASTEXITCODE -ne 0 -or $testOutput -notmatch "OK") {
    Write-Host ""
    Write-Host "  [!] No s'ha pogut connectar sense contrasenya."
    Write-Host "      Executa primer: .\push-update.ps1 -ConfigurarClau"
    Write-Host "      (installara la clau SSH al servidor demanant la contrasenya una sola vegada)"
    exit 1
}
Write-Host "    Connexio SSH sense contrasenya: OK"

# =============================================================================
# PAS 2: Generar paquet local
# =============================================================================
Write-Host ""
Write-Host "[2/4] Generant paquet de codi actualitzat..."

$TempDir = Join-Path $ScriptDir ("_push-temp-" + (Get-Date -Format "yyyyMMddHHmmss"))
& "$ScriptDir\generar-deploy.ps1" -Dest $TempDir

if (-not (Test-Path $TempDir)) {
    Write-Error "  Error generant el paquet."
    exit 1
}
Write-Host "    Paquet generat OK"

# =============================================================================
# PAS 3: Sincronitzar fitxers (sense sobreescriure .env ni SSL)
# =============================================================================
Write-Host ""
Write-Host "[3/4] Sincronitzant fitxers al servidor..."
Write-Host "      (.env i certificats SSL existents es preserven)"

# Arguments scp (clau + port)
$scpBase = @("-P", $Port)
if (Test-Path $KeyFile) { $scpBase += @("-i", $KeyFile) }

# Assegurar que el directori de destí existeix
& ssh @sshArgs $sshTarget "mkdir -p ${RutaServidor}/nginx/ssl" | Out-Null

$itemsToSync = @(
    @{ Src = "api";                Dest = "$RutaServidor/api" },
    @{ Src = "web";                Dest = "$RutaServidor/web" },
    @{ Src = "shared";             Dest = "$RutaServidor/shared" },
    @{ Src = "docker-compose.yml"; Dest = "$RutaServidor/docker-compose.yml" },
    @{ Src = "nginx/Dockerfile";   Dest = "$RutaServidor/nginx/Dockerfile" },
    @{ Src = "nginx/nginx.conf";   Dest = "$RutaServidor/nginx/nginx.conf" },
    @{ Src = "nginx/entrypoint.sh";Dest = "$RutaServidor/nginx/entrypoint.sh" },
    @{ Src = "install.sh";         Dest = "$RutaServidor/install.sh" },
    @{ Src = "update.sh";          Dest = "$RutaServidor/update.sh" },
    @{ Src = "backup.sh";          Dest = "$RutaServidor/backup.sh" },
    @{ Src = ".env.example";       Dest = "$RutaServidor/.env.example" }
)

$errors = 0
foreach ($item in $itemsToSync) {
    $localPath = Join-Path $TempDir $item.Src
    if (-not (Test-Path $localPath)) {
        Write-Warning "    No trobat: $($item.Src) (saltat)"
        continue
    }
    Write-Host "    -> $($item.Src)"
    & scp @scpBase -r $localPath "${sshTarget}:$($item.Dest)" 2>&1 | Out-Null
    if ($LASTEXITCODE -ne 0) {
        Write-Warning "    [!] Error copiant $($item.Src)"
        $errors++
    }
}

# Crear .env si no existeix al servidor
& ssh @sshArgs $sshTarget "[ -f ${RutaServidor}/.env ] || (cp ${RutaServidor}/.env.example ${RutaServidor}/.env && echo '  -> .env creat des de .env.example. Edita-lo!')"

Remove-Item -Recurse -Force $TempDir

if ($errors -gt 0) {
    Write-Host ""
    Write-Warning "  $errors fitxer(s) no s'han pogut copiar."
    $resp = Read-Host "  Vols continuar igualment? [s/N]"
    if ($resp -notmatch "^[sS]$") { exit 1 }
}

# =============================================================================
# PAS 4: Executar update.sh al servidor
# =============================================================================
Write-Host ""
Write-Host "[4/4] Executant update.sh al servidor..."
Write-Host ""

# -t força pseudo-TTY per veure la sortida en temps real
& ssh -p $Port -i $KeyFile -t $sshTarget "bash ${RutaServidor}/update.sh"

if ($LASTEXITCODE -ne 0) {
    Write-Host ""
    Write-Warning "  update.sh ha acabat amb errors. Logs:"
    Write-Host "    ssh $sshTarget 'cd ${RutaServidor} && docker compose logs --tail=50'"
} else {
    Write-Host ""
    Write-Host "========================================================"
    Write-Host "  Actualitzacio completada correctament!"
    Write-Host ""
    Write-Host "  Logs: ssh $sshTarget 'cd ${RutaServidor} && docker compose logs -f'"
    Write-Host "========================================================"
}
