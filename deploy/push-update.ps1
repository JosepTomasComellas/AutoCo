# =============================================================================
# AutoCo – Actualitzacio remota al servidor Linux
# =============================================================================
# Us:
#   .\push-update.ps1                    # actualitza (requereix clau SSH prèvia)
#   .\push-update.ps1 -ConfigurarClau   # primera vegada: instal·la la clau SSH
#
# Requisits Windows: OpenSSH client (ssh + scp) — inclòs a Win10/11 per defecte
# =============================================================================

param(
    [string]$Servidor     = "ct-autoco.tute.int",
    [string]$Usuari       = "root",
    [string]$Port         = "22",
    [string]$RutaServidor = "/docker/AutoCo",
    [switch]$ConfigurarClau
)

$ErrorActionPreference = "Stop"

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$RepoRoot  = Split-Path -Parent $ScriptDir
$sshTarget = "${Usuari}@${Servidor}"
$r         = $RutaServidor   # alias curt per construir comandes bash

# Clau SSH local a usar
$KeyFile    = "$env:USERPROFILE\.ssh\id_ed25519"
$KeyFilePub = "$KeyFile.pub"
if (-not (Test-Path $KeyFile)) {
    $KeyFile    = "$env:USERPROFILE\.ssh\id_rsa"
    $KeyFilePub = "$KeyFile.pub"
}
$TenimClau = (Test-Path $KeyFile) -and (Test-Path $KeyFilePub)

# Arguments SSH reutilitzables (sense contrassenya, accepta nou host)
$sshOpts = @("-p", $Port, "-o", "BatchMode=yes", "-o", "ConnectTimeout=10", "-o", "StrictHostKeyChecking=accept-new")
if ($TenimClau) { $sshOpts += @("-i", $KeyFile) }

# Arguments SCP reutilitzables
$scpOpts = @("-P", $Port, "-o", "StrictHostKeyChecking=accept-new")
if ($TenimClau) { $scpOpts += @("-i", $KeyFile) }

Write-Host ""
Write-Host "========================================================"
Write-Host "  AutoCo - Actualitzacio remota"
Write-Host "  Servidor : $sshTarget  (port $Port)"
Write-Host "  Ruta     : $r"
Write-Host "========================================================"

# =============================================================================
# MODE -ConfigurarClau : instal·la la clau SSH (demana contrasenya 1 sola vegada)
# =============================================================================
if ($ConfigurarClau) {
    Write-Host ""
    Write-Host "[SSH] Configurant autenticacio per clau publica..."

    # Generar clau si no existeix
    if (-not $TenimClau) {
        Write-Host "    Generant clau ed25519..."
        $sshDir = "$env:USERPROFILE\.ssh"
        if (-not (Test-Path $sshDir)) { New-Item -ItemType Directory -Path $sshDir | Out-Null }
        & ssh-keygen -t ed25519 -f "$env:USERPROFILE\.ssh\id_ed25519" -N '""' -C "autoco-deploy@$env:COMPUTERNAME"
        if ($LASTEXITCODE -ne 0) { Write-Error "Error generant la clau."; exit 1 }
        $KeyFile    = "$env:USERPROFILE\.ssh\id_ed25519"
        $KeyFilePub = "$KeyFile.pub"
        $TenimClau  = $true
        # Recalcular $sshOpts i $scpOpts amb la nova clau
        $sshOpts = @("-p", $Port, "-i", $KeyFile, "-o", "BatchMode=yes", "-o", "ConnectTimeout=10", "-o", "StrictHostKeyChecking=accept-new")
        $scpOpts = @("-P", $Port, "-i", $KeyFile, "-o", "StrictHostKeyChecking=accept-new")
        Write-Host "    Clau generada: $KeyFile"
    } else {
        Write-Host "    Clau existent: $KeyFile"
    }

    $pubKey = (Get-Content $KeyFilePub -Raw).Trim()
    Write-Host "    Clau publica  : $pubKey"
    Write-Host ""
    Write-Host "    Instal·lant clau al servidor (demanarà la contrasenya una sola vegada)..."
    Write-Host ""

    # Construïm la comanda bash amb concatenació (evita que PowerShell interpreti && com operador)
    $remoteCmd = 'mkdir -p ~/.ssh' `
               + ' && chmod 700 ~/.ssh' `
               + ' && echo ' + "'" + $pubKey + "'" + ' >> ~/.ssh/authorized_keys' `
               + ' && sort -u ~/.ssh/authorized_keys -o ~/.ssh/authorized_keys' `
               + ' && chmod 600 ~/.ssh/authorized_keys' `
               + ' && echo OK'

    # Sense BatchMode per permetre la contrasenya interactiva
    $out = & ssh -p $Port -o StrictHostKeyChecking=accept-new -o ConnectTimeout=15 $sshTarget $remoteCmd 2>&1
    if ($LASTEXITCODE -ne 0 -or "$out" -notmatch "OK") {
        Write-Host ""
        Write-Error "  Error instal·lant la clau. Sortida: $out"
        exit 1
    }

    Write-Host ""
    Write-Host "  Clau SSH instal·lada correctament al servidor."
    Write-Host "  A partir d'ara executa simplement:"
    Write-Host "      .\deploy\push-update.ps1"
    Write-Host ""
    $resp = Read-Host "  Vols continuar amb l'actualitzacio ara? [S/n]"
    if ($resp -match "^[nN]$") { exit 0 }
    Write-Host ""
}

# =============================================================================
# PAS 1: Verificar connexio SSH sense contrasenya
# =============================================================================
Write-Host ""
Write-Host "[1/4] Verificant connexio SSH sense contrasenya..."

if (-not $TenimClau) {
    Write-Host ""
    Write-Host "  [!] No s'ha trobat cap clau SSH local."
    Write-Host "      Executa primer per instal·lar-la (demana contrasenya 1 sola vegada):"
    Write-Host ""
    Write-Host "      .\deploy\push-update.ps1 -ConfigurarClau"
    exit 1
}

$out = & ssh @sshOpts $sshTarget "echo OK" 2>&1
if ($LASTEXITCODE -ne 0 -or "$out" -notmatch "OK") {
    Write-Host ""
    Write-Host "  [!] No s'ha pogut connectar sense contrasenya."
    Write-Host "      Pot ser que la clau no estigui instal·lada al servidor."
    Write-Host "      Executa:"
    Write-Host ""
    Write-Host "      .\deploy\push-update.ps1 -ConfigurarClau"
    exit 1
}
Write-Host "    OK — connexio sense contrasenya verificada"

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
# PAS 3: Sincronitzar fitxers (preserva .env i SSL)
# =============================================================================
Write-Host ""
Write-Host "[3/4] Sincronitzant fitxers al servidor..."
Write-Host "      (.env i certificats SSL existents es preserven)"

# Assegurar directori destí
& ssh @sshOpts $sshTarget ('mkdir -p ' + $r + '/nginx/ssl') | Out-Null

$itemsToSync = @(
    @{ Src = "api";                 Dest = "$r/api" },
    @{ Src = "web";                 Dest = "$r/web" },
    @{ Src = "shared";              Dest = "$r/shared" },
    @{ Src = "docker-compose.yml";  Dest = "$r/docker-compose.yml" },
    @{ Src = "nginx/Dockerfile";    Dest = "$r/nginx/Dockerfile" },
    @{ Src = "nginx/nginx.conf";    Dest = "$r/nginx/nginx.conf" },
    @{ Src = "nginx/entrypoint.sh"; Dest = "$r/nginx/entrypoint.sh" },
    @{ Src = "install.sh";          Dest = "$r/install.sh" },
    @{ Src = "update.sh";           Dest = "$r/update.sh" },
    @{ Src = "backup.sh";           Dest = "$r/backup.sh" },
    @{ Src = ".env.example";        Dest = "$r/.env.example" }
)

$errors = 0
foreach ($item in $itemsToSync) {
    $localPath = Join-Path $TempDir $item.Src
    if (-not (Test-Path $localPath)) {
        Write-Warning "    No trobat: $($item.Src) (saltat)"
        continue
    }
    Write-Host "    -> $($item.Src)"
    $out = & scp @scpOpts -r $localPath "${sshTarget}:$($item.Dest)" 2>&1
    if ($LASTEXITCODE -ne 0) {
        Write-Warning "    [!] Error copiant $($item.Src): $out"
        $errors++
    }
}

# Crear .env si no existeix al servidor (construïm la comanda bash per concatenació)
$cmdEnv = 'if [ ! -f ' + $r + '/.env ]; then cp ' + $r + '/.env.example ' + $r + '/.env; echo "-> .env creat. Edita-lo!"; fi'
& ssh @sshOpts $sshTarget $cmdEnv

Remove-Item -Recurse -Force $TempDir

if ($errors -gt 0) {
    Write-Host ""
    Write-Warning "  $($errors) fitxer(s) no s'han pogut copiar."
    $resp = Read-Host "  Vols continuar igualment? [s/N]"
    if ($resp -notmatch "^[sS]$") { exit 1 }
}

# =============================================================================
# PAS 4: Executar update.sh al servidor
# =============================================================================
Write-Host ""
Write-Host "[4/4] Executant update.sh al servidor..."
Write-Host ""

& ssh -p $Port -i $KeyFile -o StrictHostKeyChecking=accept-new -t $sshTarget ('bash ' + $r + '/update.sh')

if ($LASTEXITCODE -ne 0) {
    Write-Host ""
    Write-Warning "  update.sh ha acabat amb errors."
    Write-Host "  Per veure els logs connecta al servidor i executa:"
    Write-Host "    cd $r"
    Write-Host "    docker compose logs --tail=50"
} else {
    Write-Host ""
    Write-Host "========================================================"
    Write-Host "  Actualitzacio completada correctament!"
    Write-Host ""
    Write-Host "  Per veure els logs connecta al servidor i executa:"
    Write-Host "    cd $r"
    Write-Host "    docker compose logs -f"
    Write-Host "========================================================"
}
