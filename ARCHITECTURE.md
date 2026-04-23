# ARCHITECTURE.md — AutoCo

## 🌍 Entorns

### Local
- Windows
- PowerShell

### Producció
- Proxmox LXC
- Ubuntu Server 24
- Docker Compose

## 🔄 Flux real

1. Desenvolupament local
2. Commit + push GitHub
3. Deploy manual via script

## 🐳 Deploy script

El fitxer clau és:

`deploy/server-update.sh`

Aquest fa:
- git pull
- docker compose down
- build
- up

## 🧱 Components

- Backend (.NET API)
- Frontend (Razor / Blazor)
- Docker infra

## ⚠️ Regles

- Scripts locals → PowerShell
- Scripts servidor → Bash
- No deploy directe des de Windows
