#!/usr/bin/env bash
# =============================================================================
# AutoCo – Script de generació del paquet de desplegament per a Linux + Docker
# =============================================================================
# Ús: bash generar-deploy.sh [directori_de_sortida]
# Per defecte crea: ./autoco-deploy-YYYYMMDD
# =============================================================================
set -euo pipefail

# ── Configuració ──────────────────────────────────────────────────────────────
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
DEST="${1:-"$SCRIPT_DIR/autoco-deploy-$(date +%Y%m%d)"}"

echo "========================================================"
echo "  AutoCo – Generació del paquet de desplegament"
echo "  Origen : $REPO_ROOT"
echo "  Destí  : $DEST"
echo "========================================================"

# ── Netejar / crear directori de destí ────────────────────────────────────────
if [ -d "$DEST" ]; then
    echo "[!] El directori '$DEST' ja existeix. S'esborrarà."
    rm -rf "$DEST"
fi
mkdir -p "$DEST"

# ── Funció d'ajuda per copiar preservant l'estructura ─────────────────────────
copy_file() {
    local src="$1"
    local dest_dir="$DEST/$2"
    mkdir -p "$dest_dir"
    cp "$REPO_ROOT/$src" "$dest_dir/"
}

copy_dir() {
    local src="$1"
    local dest_dir="$DEST/$2"
    mkdir -p "$(dirname "$dest_dir")"
    cp -r "$REPO_ROOT/$src" "$dest_dir"
}

# ═══════════════════════════════════════════════════════════════════════════════
# 1. FITXERS ARREL
# ═══════════════════════════════════════════════════════════════════════════════
echo ""
echo "[1/7] Copiant fitxers arrel..."
copy_file "docker-compose.yml"  "."
copy_file ".env.example"        "."
copy_file "AutoCo.sln"          "."

# ═══════════════════════════════════════════════════════════════════════════════
# 2. PROJECTE COMPARTIT (shared)
# ═══════════════════════════════════════════════════════════════════════════════
echo "[2/7] Copiant projecte shared..."
copy_dir "shared" "shared"

# ═══════════════════════════════════════════════════════════════════════════════
# 3. API (api/)
# ═══════════════════════════════════════════════════════════════════════════════
echo "[3/7] Copiant API..."
mkdir -p "$DEST/api"

copy_file "api/Dockerfile"          "api"
copy_file "api/AutoCo.Api.csproj"   "api"
copy_file "api/Program.cs"          "api"
copy_file "api/.dockerignore"       "api"

# Data
copy_dir "api/Data"       "api/Data"

# Services
copy_dir "api/Services"   "api/Services"

# Migrations
copy_dir "api/Migrations" "api/Migrations"

# ═══════════════════════════════════════════════════════════════════════════════
# 4. WEB (web/)
# ═══════════════════════════════════════════════════════════════════════════════
echo "[4/7] Copiant Web..."
mkdir -p "$DEST/web"

copy_file "web/Dockerfile"          "web"
copy_file "web/AutoCo.Web.csproj"   "web"
copy_file "web/Program.cs"          "web"
copy_file "web/appsettings.json"    "web"
copy_file "web/.dockerignore"       "web"

# Components
copy_dir "web/Components" "web/Components"

# Services
copy_dir "web/Services"   "web/Services"

# wwwroot (CSS, JS, imatges, plantilles)
copy_dir "web/wwwroot"    "web/wwwroot"

# ═══════════════════════════════════════════════════════════════════════════════
# 5. NGINX
# ═══════════════════════════════════════════════════════════════════════════════
echo "[5/7] Copiant nginx..."
mkdir -p "$DEST/nginx/ssl"

copy_file "nginx/Dockerfile"    "nginx"
copy_file "nginx/nginx.conf"    "nginx"
copy_file "nginx/entrypoint.sh" "nginx"

# Copiar certificat SSL si existeix (no auto-signat generat localment)
if [ -f "$REPO_ROOT/nginx/ssl/server.crt" ] && \
   [ "$(wc -c < "$REPO_ROOT/nginx/ssl/server.crt")" -gt 64 ]; then
    echo "    → Copiant certificat SSL existent..."
    cp "$REPO_ROOT/nginx/ssl/server.crt" "$DEST/nginx/ssl/"
    cp "$REPO_ROOT/nginx/ssl/server.key" "$DEST/nginx/ssl/"
else
    echo "    → Sense certificat SSL vàlid; nginx generarà un d'autosignat."
    touch "$DEST/nginx/ssl/.gitkeep"
fi

# Assegurar permisos d'execució a l'entrypoint
chmod +x "$DEST/nginx/entrypoint.sh"

# ═══════════════════════════════════════════════════════════════════════════════
# 6. CREAR .env A PARTIR DE .env.example
# ═══════════════════════════════════════════════════════════════════════════════
echo "[6/7] Creant .env..."
cp "$DEST/.env.example" "$DEST/.env"

# ═══════════════════════════════════════════════════════════════════════════════
# 7. SCRIPT D'INSTAL·LACIÓ PER AL SERVIDOR LINUX
# ═══════════════════════════════════════════════════════════════════════════════
echo "[7/7] Generant install.sh..."

cat > "$DEST/install.sh" << 'INSTALL_EOF'
#!/usr/bin/env bash
# =============================================================================
# AutoCo – Script d'instal·lació al servidor Linux
# =============================================================================
# Ús: sudo bash install.sh
# Requisits: Docker Engine >= 24, Docker Compose Plugin >= 2.20
# =============================================================================
set -euo pipefail

DEPLOY_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

echo "========================================================"
echo "  AutoCo – Instal·lació al servidor"
echo "  Directori: $DEPLOY_DIR"
echo "========================================================"

# ── 1. Comprovar dependències ──────────────────────────────────────────────────
echo ""
echo "[1/5] Comprovant dependències..."

if ! command -v docker &>/dev/null; then
    echo "[ERROR] Docker no trobat. Instal·la Docker Engine:"
    echo "        https://docs.docker.com/engine/install/"
    exit 1
fi

if ! docker compose version &>/dev/null; then
    echo "[ERROR] Docker Compose plugin no trobat. Instal·la'l:"
    echo "        https://docs.docker.com/compose/install/"
    exit 1
fi

DOCKER_VERSION=$(docker --version | grep -oP '\d+\.\d+' | head -1)
echo "    Docker: $DOCKER_VERSION  ✓"
echo "    Docker Compose: $(docker compose version --short)  ✓"

# ── 2. Configurar .env ────────────────────────────────────────────────────────
echo ""
echo "[2/5] Comprovant fitxer .env..."

ENV_FILE="$DEPLOY_DIR/.env"
if [ ! -f "$ENV_FILE" ]; then
    cp "$DEPLOY_DIR/.env.example" "$ENV_FILE"
    echo "    Creat .env a partir de .env.example"
fi

# Verificar variables crítiques
check_var() {
    local var="$1"
    local val
    val=$(grep -E "^${var}=" "$ENV_FILE" | cut -d= -f2- | tr -d '"' | tr -d "'")
    if [ -z "$val" ] || echo "$val" | grep -qiE "canvia|example|teu-correu|domini"; then
        echo "    [!] ATENCIÓ: $var sembla tenir el valor per defecte. Edita .env!"
        return 1
    fi
    return 0
}

WARNINGS=0
check_var "MSSQL_SA_PASSWORD" || WARNINGS=$((WARNINGS+1))
check_var "JWT_SECRET"        || WARNINGS=$((WARNINGS+1))
check_var "ADMIN_PASSWORD"    || WARNINGS=$((WARNINGS+1))
check_var "ADMIN_EMAIL"       || WARNINGS=$((WARNINGS+1))

if [ "$WARNINGS" -gt 0 ]; then
    echo ""
    echo "    ⚠  Edita el fitxer .env abans de continuar:"
    echo "       nano $ENV_FILE"
    echo ""
    read -rp "    Vols continuar igualment? [s/N] " resp
    [[ "$resp" =~ ^[sS]$ ]] || { echo "Interromput."; exit 0; }
fi

# Validar JWT_SECRET (mínim 32 caràcters)
JWT_VAL=$(grep -E "^JWT_SECRET=" "$ENV_FILE" | cut -d= -f2- | tr -d '"' | tr -d "'")
if [ ${#JWT_VAL} -lt 32 ]; then
    echo "[ERROR] JWT_SECRET ha de tenir almenys 32 caràcters (actual: ${#JWT_VAL})."
    exit 1
fi

echo "    .env validat  ✓"

# ── 3. Crear directoris de dades ──────────────────────────────────────────────
echo ""
echo "[3/5] Creant directoris de dades..."
mkdir -p "$DEPLOY_DIR/backups"
chmod 777 "$DEPLOY_DIR/backups"   # SQL Server necessita accés de escriptura
echo "    backups/  ✓"

# ── 4. Construir i arrencar els contenidors ───────────────────────────────────
echo ""
echo "[4/5] Construint i arrencant contenidors..."
cd "$DEPLOY_DIR"

docker compose pull --ignore-pull-failures 2>/dev/null || true
docker compose build --no-cache
docker compose up -d

echo ""
echo "    Esperant que l'aplicació estigui disponible..."
for i in $(seq 1 30); do
    if docker compose ps | grep -q "autoco-api.*Up"; then
        break
    fi
    printf "."
    sleep 2
done
echo ""

# ── 5. Resum final ────────────────────────────────────────────────────────────
echo ""
echo "[5/5] Estat dels contenidors:"
docker compose ps

WEB_URL=$(grep -E "^APP_WEB_URL=" "$ENV_FILE" | cut -d= -f2- | tr -d '"' | tr -d "'" || echo "https://localhost")

echo ""
echo "========================================================"
echo "  Instal·lació completada!"
echo ""
echo "  Aplicació:  $WEB_URL"
echo "  API Swagger (dev only): http://localhost:7000/swagger"
echo ""
echo "  Ordres útils:"
echo "    Logs en temps real:  docker compose logs -f"
echo "    Aturar:              docker compose down"
echo "    Reiniciar:           docker compose restart"
echo "    Actualitzar:         bash update.sh"
echo "========================================================"
INSTALL_EOF

chmod +x "$DEST/install.sh"

# ── Script d'actualització ────────────────────────────────────────────────────
cat > "$DEST/update.sh" << 'UPDATE_EOF'
#!/usr/bin/env bash
# =============================================================================
# AutoCo – Script d'actualització
# =============================================================================
set -euo pipefail
DEPLOY_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

echo "========================================================"
echo "  AutoCo – Actualització"
echo "========================================================"

cd "$DEPLOY_DIR"
echo "[1/3] Aturant contenidors..."
docker compose down

echo "[2/3] Reconstruint imatges..."
docker compose build --no-cache

echo "[3/3] Arrencant..."
docker compose up -d

echo ""
echo "Actualització completada!"
docker compose ps
UPDATE_EOF

chmod +x "$DEST/update.sh"

# ── Script de backup manual ───────────────────────────────────────────────────
cat > "$DEST/backup.sh" << 'BACKUP_EOF'
#!/usr/bin/env bash
# =============================================================================
# AutoCo – Backup manual de la base de dades
# =============================================================================
set -euo pipefail
DEPLOY_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ENV_FILE="$DEPLOY_DIR/.env"
BACKUP_DIR="$DEPLOY_DIR/backups"
TIMESTAMP=$(date +%Y%m%d_%H%M%S)

SA_PASS=$(grep -E "^MSSQL_SA_PASSWORD=" "$ENV_FILE" | cut -d= -f2- | tr -d '"' | tr -d "'")
if [ -z "$SA_PASS" ]; then
    echo "[ERROR] MSSQL_SA_PASSWORD no trobat al .env"
    exit 1
fi

echo "Fent backup de la base de dades..."
mkdir -p "$BACKUP_DIR"

docker exec autoco-db /opt/mssql-tools18/bin/sqlcmd \
    -S localhost -U sa -P "$SA_PASS" -No \
    -Q "BACKUP DATABASE [AutoCoAvaluacio] TO DISK='/var/opt/mssql/backup/autoco_${TIMESTAMP}.bak' WITH FORMAT, COMPRESSION"

echo "Backup creat: $BACKUP_DIR/autoco_${TIMESTAMP}.bak"
BACKUP_EOF

chmod +x "$DEST/backup.sh"

# ─────────────────────────────────────────────────────────────────────────────
# RESUM
# ─────────────────────────────────────────────────────────────────────────────
echo ""
echo "========================================================"
echo "  Paquet generat correctament a:"
echo "  $DEST"
echo ""
echo "  Estructura:"
find "$DEST" -not -path "*/shared/obj/*" -not -path "*/web/obj/*" \
    | sort | sed "s|$DEST||" | sed 's|^/||' | \
    awk '{
        n=split($0,a,"/");
        for(i=1;i<n;i++) printf "  ";
        print (n>1?"├── ":"")a[n]
    }'
echo ""
echo "  Passos per desplegar al servidor Linux:"
echo "  1. Copiar el directori al servidor:"
echo "     scp -r $DEST usuari@servidor:/opt/autoco"
echo ""
echo "  2. Al servidor, editar les variables:"
echo "     nano /opt/autoco/.env"
echo ""
echo "  3. Executar l'instal·lador:"
echo "     sudo bash /opt/autoco/install.sh"
echo "========================================================"
