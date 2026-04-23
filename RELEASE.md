# RELEASE.md — AutoCo

## 🎯 Objectiu
Definir el procés real de release.

## 🔁 Procés

### Fase 1 — Local (Windows)

1. Revisar canvis
2. Assignar versió
3. Actualitzar README.md
4. Executar:

```powershell
scripts/check.ps1
```

5. Commit:

```powershell
git commit -m "release: vX.Y.Z - resum"
```

6. Push:

```powershell
git push
```

---

### Fase 2 — Deploy (Servidor)

Entrar a la LXC i executar:

```bash
bash /docker/AutoCo/deploy/server-update.sh
```

---

## ⚠️ Regles

- No fer release si no compila
- No inventar changelog
- No saltar versions
- No automatitzar deploy (és manual)

## 🧾 Exemple changelog

```
## vX.Y.Z
- Nova funcionalitat
- Millores
- Bugfixos
```
