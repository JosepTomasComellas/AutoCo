# =============================================================================
# AutoCo – Script de generació del paquet de desplegament per a Linux + Docker
# =============================================================================
# Ús: .\generar-deploy.ps1 [-Dest "C:\ruta\sortida"]
# Per defecte crea: .\autoco-deploy-YYYYMMDD
# =============================================================================

param(
    [string]$Dest = ""
)

$ErrorActionPreference = "Stop"

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$RepoRoot  = Split-Path -Parent $ScriptDir

if (-not $Dest) {
    $Dest = Join-Path $ScriptDir ("autoco-deploy-" + (Get-Date -Format "yyyyMMdd"))
}

Write-Host ""
Write-Host "========================================================"
Write-Host "  AutoCo - Generació del paquet de desplegament"
Write-Host "  Origen : $RepoRoot"
Write-Host "  Destí  : $Dest"
Write-Host "========================================================"

# ── Netejar / crear directori de destí ────────────────────────────────────────
if (Test-Path $Dest) {
    Write-Host ""
    Write-Host "[!] El directori ja existeix. S'esborrarà."
    Remove-Item -Recurse -Force $Dest
}
New-Item -ItemType Directory -Path $Dest | Out-Null

# ── Funció d'ajuda ────────────────────────────────────────────────────────────
function Copy-Item-Ensure($src, $destDir) {
    $full = Join-Path $RepoRoot $src
    if (-not (Test-Path $full)) {
        Write-Warning "  No trobat: $src (saltat)"
        return
    }
    $target = Join-Path $Dest $destDir
    New-Item -ItemType Directory -Path $target -Force | Out-Null
    Copy-Item $full -Destination $target -Force
}

function Copy-Dir-Ensure($src, $destRel) {
    $full = Join-Path $RepoRoot $src
    if (-not (Test-Path $full)) {
        Write-Warning "  No trobat: $src (saltat)"
        return
    }
    $target = Join-Path $Dest $destRel
    New-Item -ItemType Directory -Path (Split-Path $target -Parent) -Force | Out-Null
    Copy-Item $full -Destination $target -Recurse -Force
}

# =============================================================================
# 1. FITXERS ARREL
# =============================================================================
Write-Host ""
Write-Host "[1/7] Copiant fitxers arrel..."
Copy-Item-Ensure "docker-compose.yml" "."
Copy-Item-Ensure ".env.example"       "."
Copy-Item-Ensure "AutoCo.sln"         "."

# =============================================================================
# 2. PROJECTE COMPARTIT (shared)
# =============================================================================
Write-Host "[2/7] Copiant projecte shared..."
Copy-Dir-Ensure "shared" "shared"

# =============================================================================
# 3. API
# =============================================================================
Write-Host "[3/7] Copiant API..."
New-Item -ItemType Directory -Path (Join-Path $Dest "api") -Force | Out-Null

Copy-Item-Ensure "api/Dockerfile"        "api"
Copy-Item-Ensure "api/AutoCo.Api.csproj" "api"
Copy-Item-Ensure "api/Program.cs"        "api"
Copy-Item-Ensure "api/.dockerignore"     "api"

Copy-Dir-Ensure "api/Data"       "api/Data"
Copy-Dir-Ensure "api/Services"   "api/Services"
Copy-Dir-Ensure "api/Migrations" "api/Migrations"

# =============================================================================
# 4. WEB
# =============================================================================
Write-Host "[4/7] Copiant Web..."
New-Item -ItemType Directory -Path (Join-Path $Dest "web") -Force | Out-Null

Copy-Item-Ensure "web/Dockerfile"        "web"
Copy-Item-Ensure "web/AutoCo.Web.csproj" "web"
Copy-Item-Ensure "web/Program.cs"        "web"
Copy-Item-Ensure "web/appsettings.json"  "web"
Copy-Item-Ensure "web/.dockerignore"     "web"

Copy-Dir-Ensure "web/Components" "web/Components"
Copy-Dir-Ensure "web/Services"   "web/Services"
Copy-Dir-Ensure "web/wwwroot"    "web/wwwroot"

# =============================================================================
# 5. NGINX
# =============================================================================
Write-Host "[5/7] Copiant nginx..."
New-Item -ItemType Directory -Path (Join-Path $Dest "nginx/ssl") -Force | Out-Null

Copy-Item-Ensure "nginx/Dockerfile"    "nginx"
Copy-Item-Ensure "nginx/nginx.conf"    "nginx"
Copy-Item-Ensure "nginx/entrypoint.sh" "nginx"

$certPath = Join-Path $RepoRoot "nginx/ssl/server.crt"
$keyPath  = Join-Path $RepoRoot "nginx/ssl/server.key"
$sslDest  = Join-Path $Dest "nginx/ssl"

if ((Test-Path $certPath) -and (Get-Item $certPath).Length -gt 64) {
    Write-Host "    -> Copiant certificat SSL existent..."
    Copy-Item $certPath -Destination $sslDest -Force
    Copy-Item $keyPath  -Destination $sslDest -Force
} else {
    Write-Host "    -> Sense certificat SSL valid; nginx generara un d'autosignat."
    New-Item -ItemType File -Path (Join-Path $sslDest ".gitkeep") -Force | Out-Null
}

# =============================================================================
# 6. CREAR .env A PARTIR DE .env.example
# =============================================================================
Write-Host "[6/7] Creant .env..."
$envSrc  = Join-Path $Dest ".env.example"
$envDest = Join-Path $Dest ".env"
Copy-Item $envSrc -Destination $envDest -Force

# =============================================================================
# 7. GENERAR SCRIPTS PER AL SERVIDOR LINUX
# =============================================================================
Write-Host "[7/7] Generant scripts Linux..."

# ── install.sh ────────────────────────────────────────────────────────────────
$installSh = @'
#!/usr/bin/env bash
# =============================================================================
# AutoCo - Script d'instal·lació al servidor Linux
# =============================================================================
# Us: sudo bash install.sh
# Requisits: Docker Engine >= 24, Docker Compose Plugin >= 2.20
# =============================================================================
set -euo pipefail

DEPLOY_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

echo "========================================================"
echo "  AutoCo - Instal·lació al servidor"
echo "  Directori: $DEPLOY_DIR"
echo "========================================================"

# ── 1. Comprovar dependències ─────────────────────────────────────────────────
echo ""
echo "[1/5] Comprovant dependencies..."

if ! command -v docker &>/dev/null; then
    echo "[ERROR] Docker no trobat. Instal·la Docker Engine:"
    echo "        https://docs.docker.com/engine/install/"
    exit 1
fi

if ! docker compose version &>/dev/null; then
    echo "[ERROR] Docker Compose plugin no trobat."
    echo "        https://docs.docker.com/compose/install/"
    exit 1
fi

echo "    Docker $(docker --version | grep -oP '\d+\.\d+\.\d+' | head -1)  OK"
echo "    Docker Compose $(docker compose version --short)  OK"

# ── 2. Configurar .env ────────────────────────────────────────────────────────
echo ""
echo "[2/5] Comprovant fitxer .env..."

ENV_FILE="$DEPLOY_DIR/.env"
if [ ! -f "$ENV_FILE" ]; then
    cp "$DEPLOY_DIR/.env.example" "$ENV_FILE"
    echo "    Creat .env a partir de .env.example"
fi

# Verificar variables critiques
WARNINGS=0
check_var() {
    local var="$1"
    local val
    val=$(grep -E "^${var}=" "$ENV_FILE" | cut -d= -f2- | tr -d '"' | tr -d "'")
    if [ -z "$val" ] || echo "$val" | grep -qiE "canvia|example|teu-correu|domini"; then
        echo "    [!] ATENCIO: $var te el valor per defecte. Edita .env!"
        WARNINGS=$((WARNINGS+1))
    fi
}

check_var "MSSQL_SA_PASSWORD"
check_var "JWT_SECRET"
check_var "ADMIN_PASSWORD"
check_var "ADMIN_EMAIL"

if [ "$WARNINGS" -gt 0 ]; then
    echo ""
    echo "    Edita el fitxer .env abans de continuar:"
    echo "       nano $ENV_FILE"
    echo ""
    read -rp "    Vols continuar igualment? [s/N] " resp
    [[ "$resp" =~ ^[sS]$ ]] || { echo "Interromput."; exit 0; }
fi

JWT_VAL=$(grep -E "^JWT_SECRET=" "$ENV_FILE" | cut -d= -f2- | tr -d '"' | tr -d "'")
if [ ${#JWT_VAL} -lt 32 ]; then
    echo "[ERROR] JWT_SECRET ha de tenir almenys 32 caracters (actual: ${#JWT_VAL})."
    exit 1
fi

echo "    .env validat  OK"

# ── 3. Crear directoris de dades ──────────────────────────────────────────────
echo ""
echo "[3/5] Creant directoris de dades..."
mkdir -p "$DEPLOY_DIR/backups"
chmod 777 "$DEPLOY_DIR/backups"
echo "    backups/  OK"

# ── 4. Construir i arrencar ───────────────────────────────────────────────────
echo ""
echo "[4/5] Construint i arrencant contenidors..."
cd "$DEPLOY_DIR"

docker compose build --no-cache
docker compose up -d

echo ""
echo "    Esperant que els contenidors estiguin llestos..."
for i in $(seq 1 30); do
    if docker compose ps | grep -q "autoco-nginx.*Up"; then
        break
    fi
    printf "."
    sleep 3
done
echo ""

# ── 5. Resum ──────────────────────────────────────────────────────────────────
echo ""
echo "[5/5] Estat dels contenidors:"
docker compose ps

WEB_URL=$(grep -E "^APP_WEB_URL=" "$ENV_FILE" | cut -d= -f2- | tr -d '"' | tr -d "'" 2>/dev/null || echo "https://localhost")

echo ""
echo "========================================================"
echo "  Instal·lacio completada!"
echo ""
echo "  Aplicacio: $WEB_URL"
echo ""
echo "  Ordres utils:"
echo "    Logs:       docker compose logs -f"
echo "    Aturar:     docker compose down"
echo "    Actualitzar: bash update.sh"
echo "    Backup BD:  bash backup.sh"
echo "========================================================"
'@

# ── update.sh ─────────────────────────────────────────────────────────────────
$updateSh = @'
#!/usr/bin/env bash
# =============================================================================
# AutoCo - Script d'actualitzacio
# =============================================================================
set -euo pipefail
DEPLOY_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

echo "========================================================"
echo "  AutoCo - Actualitzacio"
echo "========================================================"

cd "$DEPLOY_DIR"

echo "[1/3] Aturant contenidors..."
docker compose down

echo "[2/3] Reconstruint imatges..."
docker compose build --no-cache

echo "[3/3] Arrencant..."
docker compose up -d

echo ""
echo "Actualitzacio completada!"
docker compose ps
'@

# ── backup.sh ─────────────────────────────────────────────────────────────────
$backupSh = @'
#!/usr/bin/env bash
# =============================================================================
# AutoCo - Backup manual de la base de dades SQL Server
# =============================================================================
set -euo pipefail
DEPLOY_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ENV_FILE="$DEPLOY_DIR/.env"
TIMESTAMP=$(date +%Y%m%d_%H%M%S)

SA_PASS=$(grep -E "^MSSQL_SA_PASSWORD=" "$ENV_FILE" | cut -d= -f2- | tr -d '"' | tr -d "'")
if [ -z "$SA_PASS" ]; then
    echo "[ERROR] MSSQL_SA_PASSWORD no trobat al .env"
    exit 1
fi

echo "Fent backup de la base de dades..."
mkdir -p "$DEPLOY_DIR/backups"

docker exec autoco-db /opt/mssql-tools18/bin/sqlcmd \
    -S localhost -U sa -P "$SA_PASS" -No \
    -Q "BACKUP DATABASE [AutoCoAvaluacio] TO DISK='/var/opt/mssql/backup/autoco_${TIMESTAMP}.bak' WITH FORMAT, COMPRESSION"

echo "Backup creat: $DEPLOY_DIR/backups/autoco_${TIMESTAMP}.bak"
'@

# Escriure amb endings Unix (LF) per a compatibilitat Linux
function Write-LinuxScript($path, $content) {
    # Convertir CRLF → LF
    $bytes = [System.Text.Encoding]::UTF8.GetBytes($content.Replace("`r`n", "`n").Replace("`r", "`n"))
    [System.IO.File]::WriteAllBytes($path, $bytes)
}

Write-LinuxScript (Join-Path $Dest "install.sh") $installSh
Write-LinuxScript (Join-Path $Dest "update.sh")  $updateSh
Write-LinuxScript (Join-Path $Dest "backup.sh")  $backupSh

# =============================================================================
# RESUM FINAL
# =============================================================================
Write-Host ""
Write-Host "========================================================"
Write-Host "  Paquet generat correctament!"
Write-Host ""
Write-Host "  Directori: $Dest"
Write-Host ""
Write-Host "  Estructura:"

Get-ChildItem -Path $Dest -Recurse |
    Where-Object { $_.FullName -notmatch "\\obj\\" } |
    Sort-Object FullName |
    ForEach-Object {
        $rel   = $_.FullName.Substring($Dest.Length + 1)
        $depth = ($rel.Split([IO.Path]::DirectorySeparatorChar).Count - 1)
        $pad   = "  " * $depth
        $icon  = if ($_.PSIsContainer) { "[+]" } else { "   " }
        Write-Host "  $pad$icon $($_.Name)"
    }

Write-Host ""
Write-Host "  Passos per desplegar al servidor Linux:"
Write-Host ""
Write-Host "  1. Copiar el directori al servidor:"
Write-Host "       scp -r `"$Dest`" usuari@servidor:/opt/autoco"
Write-Host ""
Write-Host "  2. Al servidor, editar les variables:"
Write-Host "       nano /opt/autoco/.env"
Write-Host ""
Write-Host "  3. Executar l'instal·lador:"
Write-Host "       sudo bash /opt/autoco/install.sh"
Write-Host "========================================================"
