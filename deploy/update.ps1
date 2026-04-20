# =============================================================================
# AutoCo - Genera carpeta d'actualitzacio per copiar al servidor
# =============================================================================
# Us:
#   .\deploy\update.ps1
#   .\deploy\update.ps1 -Dest "C:\ruta\personalitzada"
#
# Genera una carpeta amb tots els fitxers necessaris per actualitzar el
# servidor. Exclou: .env i nginx/ssl (certificats).
# Copia la carpeta resultant al servidor i executa: bash update.sh
# =============================================================================

param(
    [string]$Dest = ""
)

$ErrorActionPreference = "Stop"

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$RepoRoot  = Split-Path -Parent $ScriptDir

if (-not $Dest) {
    $Dest = Join-Path $ScriptDir ("autoco-update-" + (Get-Date -Format "yyyyMMdd"))
}

Write-Host ""
Write-Host "========================================================"
Write-Host "  AutoCo - Generant paquet d'actualitzacio"
Write-Host "  Origen : $RepoRoot"
Write-Host "  Desti  : $Dest"
Write-Host "========================================================"

# Netejar / crear directori de desti
if (Test-Path $Dest) {
    Write-Host ""
    Write-Host "  [!] El directori ja existeix. S'esborrar."
    Remove-Item -Recurse -Force $Dest
}
New-Item -ItemType Directory -Path $Dest | Out-Null

# Funcions auxiliars
function Copy-File($relSrc, $relDest) {
    $src  = Join-Path $RepoRoot $relSrc
    $dest = Join-Path $Dest $relDest
    if (-not (Test-Path $src)) { Write-Warning "  No trobat: $relSrc (saltat)"; return }
    New-Item -ItemType Directory -Path (Split-Path $dest -Parent) -Force | Out-Null
    Copy-Item $src -Destination $dest -Force
}

function Copy-Dir-Clean($relSrc, $relDest) {
    # Usa robocopy per excloure obj/, bin/ i .vs/ (contenen rutes Windows que trenquen Docker a Linux)
    $src  = Join-Path $RepoRoot $relSrc
    $dest = Join-Path $Dest $relDest
    if (-not (Test-Path $src)) { Write-Warning "  No trobat: $relSrc (saltat)"; return }
    New-Item -ItemType Directory -Path $dest -Force | Out-Null
    # /E = subdirectoris, /XD = exclou directoris, /NFL /NDL /NJH /NJS = sense log verbos
    robocopy $src $dest /E /XD obj bin .vs .git /NFL /NDL /NJH /NJS | Out-Null
    # robocopy retorna 0-3 en cas d'exit normal (no es un error)
    if ($LASTEXITCODE -gt 7) { Write-Warning "  Error copiant ${relSrc} (codi $LASTEXITCODE)" }
}

# =============================================================================
# Codi font (sense obj/ ni bin/ — rutes Windows incompatibles amb Docker Linux)
# =============================================================================
Write-Host ""
Write-Host "  Copiant codi font (sense obj/ i bin/)..."

Copy-Dir-Clean "api"     "api"
Copy-Dir-Clean "web"     "web"
Copy-Dir-Clean "shared"  "shared"

# =============================================================================
# Docker i nginx (sense ssl/)
# =============================================================================
Write-Host "  Copiant configuracio Docker i nginx..."

Copy-File ".dockerignore"         ".dockerignore"
Copy-File "docker-compose.yml"    "docker-compose.yml"
Copy-File "nginx/Dockerfile"      "nginx/Dockerfile"
Copy-File "nginx/nginx.conf"      "nginx/nginx.conf"
Copy-File "nginx/entrypoint.sh"   "nginx/entrypoint.sh"

# Creem la carpeta ssl buida perque nginx sapi que existeix
New-Item -ItemType Directory -Path (Join-Path $Dest "nginx/ssl") -Force | Out-Null

# =============================================================================
# Scripts del servidor i referencia .env
# =============================================================================
Write-Host "  Copiant scripts i referencia..."

Copy-File ".env.example"  ".env.example"

# Generar update.sh i backup.sh (identics als de generar-deploy.ps1)
function Write-LinuxScript($path, $content) {
    $bytes = [System.Text.Encoding]::UTF8.GetBytes($content.Replace("`r`n", "`n").Replace("`r", "`n"))
    [System.IO.File]::WriteAllBytes($path, $bytes)
}

$updateSh = @'
#!/usr/bin/env bash
set -euo pipefail
DEPLOY_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ENV_FILE="$DEPLOY_DIR/.env"

echo "========================================================"
echo "  AutoCo - Actualitzacio"
echo "  Directori: $DEPLOY_DIR"
echo "========================================================"

if [ ! -f "$ENV_FILE" ]; then
    echo "[ERROR] No s'ha trobat .env a $DEPLOY_DIR"
    echo "        Copia .env.example a .env i edita les variables."
    exit 1
fi

echo ""
echo "[1/3] Aturant contenidors..."
cd "$DEPLOY_DIR"
docker compose down

echo ""
echo "[2/3] Reconstruint imatges Docker..."
docker compose build --no-cache

echo ""
echo "[3/3] Arrencant contenidors..."
docker compose up -d

echo ""
echo "  Esperant que nginx arranqui..."
for i in $(seq 1 20); do
    docker compose ps | grep -q "autoco-nginx.*Up" && break
    printf "." && sleep 3
done
echo ""

echo ""
echo "========================================================"
echo "  Actualitzacio completada!"
docker compose ps
echo ""
echo "  Logs en temps real: docker compose logs -f"
echo "========================================================"
'@

$backupSh = @'
#!/usr/bin/env bash
set -euo pipefail
DEPLOY_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ENV_FILE="$DEPLOY_DIR/.env"
TIMESTAMP=$(date +%Y%m%d_%H%M%S)

SA_PASS=$(grep -E "^MSSQL_SA_PASSWORD=" "$ENV_FILE" | cut -d= -f2- | tr -d '"' | tr -d "'")
if [ -z "$SA_PASS" ]; then echo "[ERROR] MSSQL_SA_PASSWORD no trobat al .env"; exit 1; fi

echo "Fent backup de la base de dades..."
mkdir -p "$DEPLOY_DIR/backups"
docker exec autoco-db /opt/mssql-tools18/bin/sqlcmd \
    -S localhost -U sa -P "$SA_PASS" -No \
    -Q "BACKUP DATABASE [AutoCoAvaluacio] TO DISK='/var/opt/mssql/backup/autoco_${TIMESTAMP}.bak' WITH FORMAT, COMPRESSION"
echo "Backup creat: $DEPLOY_DIR/backups/autoco_${TIMESTAMP}.bak"
'@

Write-LinuxScript (Join-Path $Dest "update.sh")  $updateSh
Write-LinuxScript (Join-Path $Dest "backup.sh")  $backupSh

# =============================================================================
# Resum
# =============================================================================
$mida = (Get-ChildItem $Dest -Recurse -File | Measure-Object -Property Length -Sum).Sum
$midaMB = [math]::Round($mida / 1MB, 1)

Write-Host ""
Write-Host "========================================================"
Write-Host "  Paquet generat correctament!"
Write-Host ""
Write-Host "  Directori : $Dest"
Write-Host "  Mida total: $midaMB MB"
Write-Host ""
Write-Host "  Fitxers exclosos (a preservar al servidor):"
Write-Host "    - .env"
Write-Host "    - nginx/ssl/server.crt i server.key"
Write-Host ""
Write-Host "  Passos per aplicar l'actualitzacio al servidor:"
Write-Host ""
Write-Host "  1. Copia la carpeta al servidor:"
Write-Host "       scp -r `"$Dest`" root@ct-autoco.tute.int:/docker/AutoCo-new"
Write-Host ""
Write-Host "  2. Al servidor, substitueix els fitxers (conserva .env i ssl):"
Write-Host "       rsync -a --exclude='.env' --exclude='nginx/ssl' /docker/AutoCo-new/ /docker/AutoCo/"
Write-Host "       rm -rf /docker/AutoCo-new"
Write-Host ""
Write-Host "  3. Executa l'actualitzacio:"
Write-Host "       bash /docker/AutoCo/update.sh"
Write-Host "========================================================"
