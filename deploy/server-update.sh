#!/usr/bin/env bash
# =============================================================================
# AutoCo - Actualització directa des de GitHub
# Us: bash /docker/AutoCo/deploy/server-update.sh
# =============================================================================
set -euo pipefail

DEPLOY_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

echo ""
echo "========================================================"
echo "  AutoCo - Actualització des de GitHub"
echo "  Directori: $DEPLOY_DIR"
echo "========================================================"

cd "$DEPLOY_DIR"

# Comprova que és un repositori git
if [ ! -d ".git" ]; then
    echo ""
    echo "[ERROR] $DEPLOY_DIR no és un repositori git."
    echo "        Clona el repo primer:"
    echo "        git clone https://github.com/JosepTomasComellas/AutoCo.git $DEPLOY_DIR"
    exit 1
fi

# Comprova que existeix el .env
if [ ! -f ".env" ]; then
    echo ""
    echo "[ERROR] No s'ha trobat .env a $DEPLOY_DIR"
    echo "        Copia .env.example a .env i edita les variables."
    exit 1
fi

echo ""
echo "[1/4] Descarregant canvis de GitHub..."
git pull --ff-only

echo ""
echo "[2/4] Aturant contenidors..."
docker compose down

echo ""
echo "[3/4] Reconstruint imatges Docker..."
docker compose build --no-cache

echo ""
echo "[4/4] Arrencant contenidors..."
docker compose up -d

echo ""
echo "  Esperant que els serveis arranquin..."
for i in $(seq 1 20); do
    docker compose ps | grep -q "autoco-nginx.*Up" && break
    printf "." && sleep 3
done
echo ""

echo ""
echo "========================================================"
echo "  Actualització completada!"
echo ""
docker compose ps
echo ""
echo "  Versió desplegada:"
grep 'Current' shared/AppVersion.cs | tr -d ' '
echo ""
echo "  Logs en temps real: docker compose logs -f"
echo "========================================================"
