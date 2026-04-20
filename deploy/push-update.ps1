# =============================================================================
# AutoCo - Actualitzacio remota al servidor Linux
# =============================================================================
# Us:
#   .\push-update.ps1                  # actualitza (requereix clau SSH previa)
#   .\push-update.ps1 -ConfigurarClau  # primera vegada: instal.la la clau SSH
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
$sshTarget = $Usuari + "@" + $Servidor
$r         = $RutaServidor

# Clau SSH local
$KeyFile = $env:USERPROFILE + "\.ssh\id_ed25519"
if (-not (Test-Path $KeyFile)) {
    $KeyFile = $env:USERPROFILE + "\.ssh\id_rsa"
}
$TenimClau = Test-Path $KeyFile

# Construeix llista d'arguments SSH i SCP sense contrassenya
function Get-SshArgs {
    $a = @("-p", $Port, "-o", "ConnectTimeout=10", "-o", "StrictHostKeyChecking=accept-new", "-o", "BatchMode=yes")
    if ($TenimClau) { $a = @("-i", $KeyFile) + $a }
    return $a
}
function Get-ScpArgs {
    $a = @("-P", $Port, "-o", "StrictHostKeyChecking=accept-new")
    if ($TenimClau) { $a = @("-i", $KeyFile) + $a }
    return $a
}

Write-Host ""
Write-Host "========================================================"
Write-Host "  AutoCo - Actualitzacio remota"
Write-Host "    Servidor : $sshTarget"
Write-Host "    Port     : $Port"
Write-Host "    Ruta     : $r"
Write-Host "========================================================"

# =============================================================================
# MODE -ConfigurarClau
# =============================================================================
if ($ConfigurarClau) {
    Write-Host ""
    Write-Host "[SSH] Configurant autenticacio per clau publica..."

    if (-not $TenimClau) {
        Write-Host "    Generant clau ed25519..."
        $sshDir = $env:USERPROFILE + "\.ssh"
        if (-not (Test-Path $sshDir)) { New-Item -ItemType Directory -Path $sshDir | Out-Null }
        $keyPath = $env:USERPROFILE + "\.ssh\id_ed25519"
        # Usem cmd /c perque PowerShell descarta els "" buits i ssh-keygen falla
        $keygenCmd = 'ssh-keygen -t ed25519 -q -f "' + $keyPath + '" -N "" -C autoco-deploy'
        & cmd /c $keygenCmd
        if ($LASTEXITCODE -ne 0) { Write-Error "Error generant la clau."; exit 1 }
        $KeyFile   = $keyPath
        $TenimClau = $true
        Write-Host "    Clau generada: $KeyFile"
    } else {
        Write-Host "    Clau existent: $KeyFile"
    }

    $pubKeyFile = $KeyFile + ".pub"
    $pubKey     = (Get-Content $pubKeyFile -Raw).Trim()
    Write-Host "    Clau publica : $pubKey"
    Write-Host ""
    Write-Host "    Instal.lant clau al servidor (demana contrasenya una sola vegada)..."
    Write-Host ""

    # Enviem la clau publica per stdin per evitar problemes de cometes.
    # El servidor llegeix stdin amb cat i l'afegeix a authorized_keys.
    $remoteSetup = 'mkdir -p ~/.ssh; cat >> ~/.ssh/authorized_keys; sort -u ~/.ssh/authorized_keys -o ~/.ssh/authorized_keys; chmod 600 ~/.ssh/authorized_keys; chmod 700 ~/.ssh; echo CLAU_OK'
    $out = $pubKey | & ssh -p $Port -o StrictHostKeyChecking=accept-new -o ConnectTimeout=15 $sshTarget $remoteSetup 2>&1
    if ($LASTEXITCODE -ne 0 -or "$out" -notmatch "CLAU_OK") {
        Write-Error ("Error instal.lant la clau. Sortida: " + $out)
        exit 1
    }

    Write-Host "  Clau SSH instal.lada correctament."
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
Write-Host "[1/4] Verificant connexio SSH..."

if (-not $TenimClau) {
    Write-Host ""
    Write-Host "  No s'ha trobat cap clau SSH local."
    Write-Host "  Executa primer (demana contrasenya una sola vegada):"
    Write-Host ""
    Write-Host "      .\deploy\push-update.ps1 -ConfigurarClau"
    exit 1
}

$sshArgs = Get-SshArgs
$out = & ssh @sshArgs $sshTarget "echo CONN_OK" 2>&1
if ($LASTEXITCODE -ne 0 -or "$out" -notmatch "CONN_OK") {
    Write-Host ""
    Write-Host "  No s'ha pogut connectar sense contrasenya."
    Write-Host "  Executa primer:"
    Write-Host ""
    Write-Host "      .\deploy\push-update.ps1 -ConfigurarClau"
    exit 1
}
Write-Host "    OK"

# =============================================================================
# PAS 2: Generar paquet local
# =============================================================================
Write-Host ""
Write-Host "[2/4] Generant paquet de codi..."

$TempDir = Join-Path $ScriptDir ("_push-temp-" + (Get-Date -Format "yyyyMMddHHmmss"))
& (Join-Path $ScriptDir "generar-deploy.ps1") -Dest $TempDir
if (-not (Test-Path $TempDir)) { Write-Error "Error generant el paquet."; exit 1 }
Write-Host "    OK"

# =============================================================================
# PAS 3: Sincronitzar fitxers (preserva .env i SSL)
# =============================================================================
Write-Host ""
Write-Host "[3/4] Sincronitzant fitxers al servidor..."
Write-Host "      (.env i certificats SSL es preserven)"

$scpArgs = Get-ScpArgs
$sshArgs = Get-SshArgs

# Assegurar directori base
& ssh @sshArgs $sshTarget ("mkdir -p " + $r + "/nginx/ssl") | Out-Null

# Directoris: esborrem el destí primer per evitar copies niades (scp -r)
$dirs = @("api", "web", "shared")
foreach ($d in $dirs) {
    $localPath = Join-Path $TempDir $d
    if (-not (Test-Path $localPath)) { Write-Warning "No trobat: $d (saltat)"; continue }
    Write-Host "    -> $d"
    & ssh @sshArgs $sshTarget ("rm -rf " + $r + "/" + $d) | Out-Null
    & scp @scpArgs -r $localPath ($sshTarget + ":" + $r + "/" + $d) | Out-Null
    if ($LASTEXITCODE -ne 0) { Write-Warning "Error copiant $d" }
}

# Fitxers individuals (scp sobreescriu directament)
$files = @(
    @{ Src = "docker-compose.yml";  Dest = $r + "/docker-compose.yml" },
    @{ Src = "nginx/Dockerfile";    Dest = $r + "/nginx/Dockerfile" },
    @{ Src = "nginx/nginx.conf";    Dest = $r + "/nginx/nginx.conf" },
    @{ Src = "nginx/entrypoint.sh"; Dest = $r + "/nginx/entrypoint.sh" },
    @{ Src = "install.sh";          Dest = $r + "/install.sh" },
    @{ Src = "update.sh";           Dest = $r + "/update.sh" },
    @{ Src = "backup.sh";           Dest = $r + "/backup.sh" },
    @{ Src = ".env.example";        Dest = $r + "/.env.example" }
)
foreach ($f in $files) {
    $localPath = Join-Path $TempDir $f.Src
    if (-not (Test-Path $localPath)) { Write-Warning "No trobat: $($f.Src) (saltat)"; continue }
    Write-Host ("    -> " + $f.Src)
    & scp @scpArgs $localPath ($sshTarget + ":" + $f.Dest) | Out-Null
    if ($LASTEXITCODE -ne 0) { Write-Warning ("Error copiant " + $f.Src) }
}

# Crear .env si no existeix al servidor (if/then sense && ni ||)
$checkEnv = "if [ ! -f " + $r + "/.env ]; then cp " + $r + "/.env.example " + $r + "/.env; echo 'Nou .env creat. Edita les variables!'; fi"
& ssh @sshArgs $sshTarget $checkEnv

Remove-Item -Recurse -Force $TempDir
Write-Host "    OK"

# =============================================================================
# PAS 4: Executar update.sh al servidor
# =============================================================================
Write-Host ""
Write-Host "[4/4] Executant update.sh al servidor..."
Write-Host ""

$updateCmd = "bash " + $r + "/update.sh"
& ssh -p $Port -i $KeyFile -o StrictHostKeyChecking=accept-new -t $sshTarget $updateCmd

if ($LASTEXITCODE -ne 0) {
    Write-Host ""
    Write-Warning "update.sh ha acabat amb errors."
    Write-Host "  Per veure els logs connecta al servidor i executa:"
    Write-Host ("    cd " + $r)
    Write-Host "    docker compose logs --tail=50"
} else {
    Write-Host ""
    Write-Host "========================================================"
    Write-Host "  Actualitzacio completada correctament!"
    Write-Host ""
    Write-Host "  Per veure els logs connecta al servidor i executa:"
    Write-Host ("    cd " + $r)
    Write-Host "    docker compose logs -f"
    Write-Host "========================================================"
}
