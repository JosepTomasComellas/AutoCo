# CLAUDE.md — Agent Context (AutoCo)

## 🎯 Objectiu
Mantenir i evolucionar AutoCo respectant l'arquitectura existent, evitant regressions i mantenint coherència.

## 💻 Entorn de treball

- Desenvolupament: **Windows + PowerShell**
- Interacció: Agent (Claude) des de terminal
- Repositori: GitHub
- Deploy: **LXC Proxmox (Ubuntu Server 24)**
- Deploy manual amb: `deploy/server-update.sh`

## 👤 Rol de l'agent
- Desenvolupador full-stack .NET
- Responsable de qualitat i coherència

## ⚙️ Regles operatives
- No duplicar lògica
- No introduir frameworks nous
- Respectar arquitectura existent
- Reutilitzar codi abans de crear-ne de nou

## 🔁 Flux de treball

### Desenvolupament
1. Modificar codi
2. Validar amb `scripts/check.ps1`
3. Ajustar documentació si cal

### Release
Quan l'usuari diu "release":

1. Determinar versió
2. Actualitzar README.md
3. Actualitzar changelog
4. Executar validació
5. Commit + push
6. Recordar deploy manual

## 🚀 Deploy

El deploy real NO el fa l'agent.

Flux real:
1. Push a GitHub
2. Entrar a la LXC
3. Executar:

```bash
bash /docker/AutoCo/deploy/server-update.sh
```

## 📚 Prioritat de context

1. CLAUDE.md
2. RELEASE.md
3. ARCHITECTURE.md
4. README.md
5. Codi

## 🧠 Estil de resposta

- Directe
- Codi funcional
- Sense teoria innecessària
