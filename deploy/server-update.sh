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

# ── Helpers ───────────────────────────────────────────────────────────────────
# Llegeix el valor d'una variable del .env (ignora comentaris inline i cometes)
get_env_val() {
    grep -E "^${1}=" .env 2>/dev/null \
        | tail -1 \
        | sed 's/^[^=]*=//' \
        | sed 's/[[:space:]]*#.*//' \
        | sed 's/^[[:space:]]*//;s/[[:space:]]*$//' \
        | sed "s/^['\"]//;s/['\"]$//"
}

# ── Validació del fitxer .env ─────────────────────────────────────────────────
validate_env() {
    local errors=0
    local warnings=0
    local val

    echo ""
    echo "  Validant configuració del .env..."

    # Comprova que una variable no és buida ni té el valor d'exemple
    _check_required() {
        local var="$1" example="$2"
        val=$(get_env_val "$var")
        if [ -z "$val" ]; then
            echo "  [ERROR] $var és buit o no definit."
            return 1
        elif [ "$val" = "$example" ]; then
            echo "  [ERROR] $var té el valor d'exemple sense personalitzar."
            return 1
        fi
        return 0
    }

    # Variables obligatòries
    _check_required "MSSQL_SA_PASSWORD" "CanviaAquestaContrasenya_2024!"          || errors=$((errors+1))
    _check_required "JWT_SECRET"        "CanviaAquestSecret_MiniM32Caracters_2024" || errors=$((errors+1))
    _check_required "ADMIN_EMAIL"       "CorreuElectronic@domini"                 || errors=$((errors+1))
    _check_required "ADMIN_PASSWORD"    "CanviaAquestaContrasenya123!"            || errors=$((errors+1))
    _check_required "ADMIN_NOM"         "Nom"                                     || errors=$((errors+1))
    _check_required "ADMIN_COGNOMS"     "Cognoms"                                 || errors=$((errors+1))

    # JWT_SECRET: longitud mínima 32 caràcters
    val=$(get_env_val "JWT_SECRET")
    if [ -n "$val" ] && [ "${#val}" -lt 32 ]; then
        echo "  [ERROR] JWT_SECRET massa curt: ${#val} caràcters (mínim 32)."
        errors=$((errors+1))
    fi

    # ADMIN_EMAIL: ha de contenir @
    val=$(get_env_val "ADMIN_EMAIL")
    if [ -n "$val" ] && [[ "$val" != *"@"* ]]; then
        echo "  [ERROR] ADMIN_EMAIL no sembla una adreça vàlida: $val"
        errors=$((errors+1))
    fi

    # BACKUP_DAILY_HOUR: enter entre 0 i 23
    val=$(get_env_val "BACKUP_DAILY_HOUR")
    if [ -n "$val" ]; then
        if ! [[ "$val" =~ ^[0-9]+$ ]] || [ "$val" -gt 23 ]; then
            echo "  [ERROR] BACKUP_DAILY_HOUR ha de ser entre 0 i 23 (actual: '$val')."
            errors=$((errors+1))
        fi
    fi

    # BACKUP_WEEKLY_DAY: enter entre 0 i 6
    val=$(get_env_val "BACKUP_WEEKLY_DAY")
    if [ -n "$val" ]; then
        if ! [[ "$val" =~ ^[0-9]+$ ]] || [ "$val" -gt 6 ]; then
            echo "  [ERROR] BACKUP_WEEKLY_DAY ha de ser entre 0 i 6 (actual: '$val')."
            errors=$((errors+1))
        fi
    fi

    # SMTP: avís si no configurat; si ho està, comprova completesa
    val=$(get_env_val "SMTP_PASSWORD")
    if [ -z "$val" ] || [ "$val" = "contrasenya-aplicacio-gmail" ]; then
        echo "  [AVIS]  SMTP no configurat — no s'enviaran correus de recordatori."
        warnings=$((warnings+1))
    else
        for smtp_var in SMTP_HOST SMTP_PORT SMTP_USERNAME SMTP_FROM_ADDRESS SMTP_FROM_NAME; do
            local smtp_val
            smtp_val=$(get_env_val "$smtp_var")
            if [ -z "$smtp_val" ]; then
                echo "  [ERROR] SMTP_PASSWORD configurat però $smtp_var és buit."
                errors=$((errors+1))
            fi
        done
    fi

    # APP_WEB_URL: avís si és localhost
    val=$(get_env_val "APP_WEB_URL")
    if [ -z "$val" ]; then
        echo "  [ERROR] APP_WEB_URL és buit o no definit."
        errors=$((errors+1))
    elif [ "$val" = "https://localhost" ]; then
        echo "  [AVIS]  APP_WEB_URL és 'https://localhost'. Recomanable canviar-ho en producció."
        warnings=$((warnings+1))
    fi

    # Resultat
    echo ""
    if [ "$errors" -gt 0 ]; then
        echo "  ✗ $errors error(s) trobat(s). Corregeix .env i torna a executar l'script."
        echo "    Els contenidors actuals NO s'han aturat."
        echo ""
        exit 1
    elif [ "$warnings" -gt 0 ]; then
        echo "  ✓ Configuració vàlida ($warnings avís/avisos). Continuant..."
    else
        echo "  ✓ Configuració vàlida."
    fi
}

# ── Versions: actual i nova ───────────────────────────────────────────────────
extract_version() {
    grep 'Current' "$1" 2>/dev/null | sed 's/.*"\(.*\)".*/\1/' | tr -d '[:space:]'
}

VERSIO_ACTUAL=$(extract_version "shared/AppVersion.cs")

echo ""
echo "[1/5] Descarregant canvis de GitHub..."
git fetch --tags origin

VERSIO_NOVA=$(git show origin/main:shared/AppVersion.cs 2>/dev/null \
    | grep 'Current' | sed 's/.*"\(.*\)".*/\1/' | tr -d '[:space:]')

echo ""
echo "  Versió actual desplegada : ${VERSIO_ACTUAL:-desconeguda}"
echo "  Versió al repositori     : ${VERSIO_NOVA:-desconeguda}"

# Mostra els commits nous si n'hi ha
COMMITS_NOUS=$(git log HEAD..origin/main --oneline 2>/dev/null)
if [ -n "$COMMITS_NOUS" ]; then
    echo ""
    echo "  Commits nous:"
    git log HEAD..origin/main --oneline | sed 's/^/    /'
else
    echo ""
    echo "  Ja és la versió més recent. Continuant igualment..."
fi

echo ""
git pull --ff-only

VERSIO_DESPLEGADA=$(extract_version "shared/AppVersion.cs")

echo ""
echo "[2/5] Validant configuració del .env..."
validate_env

echo ""
echo "[3/5] Aturant contenidors..."
docker compose down

echo ""
echo "[4/5] Reconstruint imatges Docker..."
docker compose build --no-cache

echo ""
echo "[5/5] Arrencant contenidors..."
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
if [ "$VERSIO_ACTUAL" != "$VERSIO_DESPLEGADA" ]; then
    echo "  Actualització completada: v$VERSIO_ACTUAL → v$VERSIO_DESPLEGADA"
else
    echo "  Actualització completada: v$VERSIO_DESPLEGADA (sense canvi de versió)"
fi
echo ""
docker compose ps
echo ""
echo "  Logs en temps real: docker compose logs -f"
echo "========================================================"
