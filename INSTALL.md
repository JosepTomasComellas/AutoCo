# AutoCo — Guia d'Instal·lació i Configuració

> Torna al [README del projecte](README.md)

---

## Requisits

- **Docker Desktop** (Windows / macOS) o **Docker Engine + Compose plugin** (Linux)
- Git

---

## Modes de base de dades

AutoCo suporta tres configuracions de BD, seleccionables des del `.env`:

| Mode | `COMPOSE_FILE` | `DB_PROVIDER` | Descripció |
|------|---------------|---------------|------------|
| MSSQL intern | `docker-compose.yml:docker-compose.db.yml` | `SqlServer` (per defecte) | SQL Server 2022 Express en contenidor Docker |
| MSSQL extern | *(absent)* | `SqlServer` (per defecte) | SQL Server existent fora del Docker |
| PostgreSQL | *(absent o intern)* | `PostgreSQL` | PostgreSQL intern o extern |

---

## Instal·lació inicial

### Opció A — Entorn local (desenvolupament)

```bash
git clone https://github.com/JosepTomasComellas/AutoCo.git
cd AutoCo
cp .env.example .env   # ajusta les variables (veure secció Configuració)
docker compose up --build
```

Accedeix a **https://localhost** (accepta l'avís del certificat auto-signat).

---

### Opció B — Servidor de producció (recomanada)

**Configuració inicial (una sola vegada):**

```bash
cd /docker
git clone https://github.com/JosepTomasComellas/AutoCo.git
cd AutoCo
cp .env.example .env
nano .env              # edita les variables obligatòries
```

**Per aplicar qualsevol actualització posterior:**

```bash
bash /docker/AutoCo/deploy/server-update.sh
```

El script fa `git pull`, **valida el `.env`** (variables obligatòries, formats, SMTP) i reconstrueix les imatges. Si la validació falla, els contenidors actuals **no s'aturen**.

---

### Opció C — Actualització del servidor des de Windows

```powershell
# Genera el paquet de fitxers i copia al servidor
.\deploy\update.ps1
scp -r "deploy\autoco-update-YYYYMMDD" root@servidor:/docker/AutoCo-new
```

```bash
# Al servidor
rsync -a --exclude='.env' --exclude='nginx/ssl' /docker/AutoCo-new/ /docker/AutoCo/
bash /docker/AutoCo/deploy/server-update.sh
```

---

## Serveis Docker

| Servei | Imatge | Port | Descripció |
|--------|--------|------|------------|
| `db` | SQL Server 2022 Express | intern | MSSQL intern (opcional; activat per `docker-compose.db.yml`) |
| `redis` | Redis 7 Alpine | intern | Caché de resultats + backplane SignalR + OTP |
| `api` | ASP.NET Core 10 | intern | API REST + JWT |
| `web` | ASP.NET Core 10 | intern | Blazor Server + MudBlazor |
| `nginx` | nginx Alpine | 80 / 443 | Proxy SSL, WebSocket per Blazor |

---

## Configuració (`.env`)

Copia `.env.example` a `.env` i ajusta els valors. Consulta `.env.example` per a exemples de cadenes de connexió per a cada mode.

### Variables de base de dades

| Variable | Descripció | Obligatori |
|----------|------------|:----------:|
| `COMPOSE_FILE` | `docker-compose.yml:docker-compose.db.yml` per MSSQL intern; absent per MSSQL extern | MSSQL intern |
| `DB_CONNECTION` | Cadena de connexió completa (MSSQL o PostgreSQL) | ✓ |
| `MSSQL_SA_PASSWORD` | Contrasenya SQL Server (mínim 8 car., majúsc., número i símbol) | MSSQL intern |
| `DB_PROVIDER` | Motor de BD: `SqlServer` (per defecte) o `PostgreSQL` | |

**Exemples de `DB_CONNECTION`:**

```ini
# MSSQL intern (apunta al contenidor "db")
DB_CONNECTION=Server=db;Database=AutoCoAvaluacio;User Id=sa;Password=LaContrasenya;TrustServerCertificate=True

# MSSQL extern
DB_CONNECTION=Server=el-teu-servidor\INSTANCIA;Database=AutoCoAvaluacio;User Id=sa;Password=LaContrasenya;TrustServerCertificate=True

# PostgreSQL
DB_CONNECTION=Host=el-teu-servidor;Database=AutoCoAvaluacio;Username=postgres;Password=LaContrasenya
```

### Variables d'autenticació i administrador

| Variable | Descripció | Obligatori |
|----------|------------|:----------:|
| `JWT_SECRET` | Secret JWT (mínim 32 caràcters) | ✓ |
| `JWT_EXPIRY_HOURS` | Durada del token en hores (per defecte: `8`) | |
| `ADMIN_EMAIL` | Correu de l'administrador inicial | ✓ |
| `ADMIN_PASSWORD` | Contrasenya de l'administrador inicial | ✓ |
| `ADMIN_NOM` | Nom de l'administrador | ✓ |
| `ADMIN_COGNOMS` | Cognoms de l'administrador | |

### Variables SMTP (correu)

SMTP és opcional. Si no es configura, les funcions d'email (credencials, recordatoris, restabliment de contrasenya) queden desactivades però l'aplicació funciona amb normalitat.

| Variable | Descripció |
|----------|------------|
| `SMTP_HOST` | Servidor SMTP (p.ex. `smtp.gmail.com`) |
| `SMTP_PORT` | Port SMTP (p.ex. `587`) |
| `SMTP_USERNAME` | Usuari SMTP |
| `SMTP_PASSWORD` | Contrasenya SMTP (o app password de Google) |
| `SMTP_FROM_ADDRESS` | Adreça remitent dels correus |
| `SMTP_FROM_NAME` | Nom remitent dels correus |
| `APP_WEB_URL` | URL pública (p.ex. `https://autoco.centre.cat`) — per als QR i links dels correus |

### Variables de còpies de seguretat automàtiques

| Variable | Descripció | Per defecte |
|----------|------------|:-----------:|
| `BACKUP_ENABLED` | Activa el backup automàtic (`true`/`false`) | `false` |
| `BACKUP_DAILY_HOUR` | Hora del backup diari en hora local Europe/Madrid (0–23) | `2` |
| `BACKUP_WEEKLY_DAY` | Dia de la setmana del backup setmanal (0=Dg … 6=Ds) | `0` |
| `BACKUP_DAILY_RETENTION` | Nombre màxim de backups diaris a conservar | `7` |
| `BACKUP_WEEKLY_RETENTION` | Nombre màxim de backups setmanals a conservar | `4` |

### Variables d'idioma i traduccions

| Variable | Descripció | Per defecte |
|----------|------------|:-----------:|
| `DEFAULT_LANGUAGE` | Idioma per defecte (`ca`, `es`, o qualsevol codi de `config/i18n/`) | `ca` |
| `I18N_PATH` | Ruta als fitxers JSON de traducció (el volum Docker la munta automàticament) | `/app/i18n` |

### Variables de branding corporatiu

| Variable | Descripció | Per defecte |
|----------|------------|:-----------:|
| `BRAND_APP_NAME` | Nom de l'aplicació (títol del navegador, manifest PWA) | `AutoCo Avaluació` |
| `BRAND_APP_SHORT_NAME` | Nom curt PWA | `AutoCo` |
| `BRAND_ORG_NAME` | Nom de l'organització al peu de pàgina | `Salesians de Sarrià` |
| `BRAND_ORG_DEPT` | Departament al peu de pàgina | `Dept. d'Informàtica` |
| `BRAND_PRIMARY_COLOR` | Color primari dels botons i elements destacats | `#CC0000` |
| `BRAND_NAV_COLOR` | Color de la barra de navegació i el peu de pàgina | `#1e293b` |

> **Logo personalitzat:** posa un fitxer `logo.png` a `./config/branding/` per sobreescriure el logo per defecte. El volum Docker el munta automàticament a `/app/wwwroot/branding/logo.png`.

> **Imatge de fons:** posa un fitxer `background.png` (o `.jpg`) a `./config/branding/`. S'aplica com a fons de la pàgina d'inici i autenticació.

---

## Traduccions externes (i18n)

Els fitxers JSON a `./config/i18n/` permeten personalitzar o afegir idiomes sense recompilar:

- **Override parcial** (`ca.json`, `es.json`): sobreescriu únicament les claus que conté; la resta usen el valor integrat.
- **Idioma nou** (`fr.json`, `de.json`...): s'afegeix automàticament als idiomes suportats; les claus absents cauen al català per defecte.

```
config/i18n/
├── ca.json          # override parcial del català (opcional)
├── es.json          # override parcial del castellà (opcional)
└── fr.json          # idioma nou complet (opcional)
```

Format dels fitxers:
```json
{
  "Home_Footer": "El teu Centre · Dept. d'Informàtica",
  "Login_Subtitle": "AutoCo · El teu centre"
}
```

Consulta `config/i18n/ca.override.example.json` i `config/i18n/fr.example.json` com a referència. Els canvis s'apliquen al proper reinici del servei `web`.

Per obtenir la llista completa de claus disponibles, accedeix a `https://el-teu-servidor/i18n/reference.json`.

---

## SSL

- **Sense certificat:** nginx genera automàticament un certificat auto-signat vàlid 10 anys.
- **Amb certificat propi:** col·loca `server.crt` i `server.key` a `nginx/ssl/` abans d'arrencar.

---

## Comandes útils

```bash
bash /docker/AutoCo/deploy/server-update.sh  # Actualitzar des de GitHub
docker compose logs -f api                   # Logs de l'API en temps real
docker compose logs -f web                   # Logs del web en temps real
docker compose down                          # Aturar (dades preservades)
docker compose down -v                       # Aturar i esborrar totes les dades
docker compose ps                            # Estat dels contenidors
```

---

## Endpoints principals de l'API

```
POST /api/auth/professor                              # Login professor/admin
POST /api/auth/student                                # Login alumne
POST /api/auth/refresh                                # Renovar JWT
POST /api/auth/logout                                 # Invalidar sessió

GET/POST/PUT/DELETE /api/professors                   # Gestió professors (admin)
GET/POST/DELETE    /api/professors/{id}/classes       # Assignació classes a professor (admin)
GET/POST/PUT/DELETE /api/cicles                       # Gestió cicles (admin)
GET/POST/PUT/DELETE /api/classes                      # Gestió classes
GET/POST/PUT/DELETE /api/classes/{id}/students        # Gestió alumnes
POST /api/classes/{id}/students/bulk                  # Importació massiva CSV
POST /api/classes/{id}/students/{sid}/reset-password  # Reset contrasenya alumne
POST /api/classes/{id}/students/{sid}/send-password   # Enviar credencials per correu
POST /api/classes/{id}/students/send-all-passwords    # Enviar credencials a tots
POST /api/classes/{id}/students/{sid}/move            # Moure alumne a una altra classe
GET/POST/PUT/DELETE /api/classes/{id}/modules         # Gestió mòduls
GET/POST/DELETE    /api/modules/{id}/exclusions       # Exclusions per mòdul

GET/POST/PUT/DELETE /api/activities                   # Gestió activitats
POST /api/activities/{id}/toggle                      # Obrir/tancar activitat
POST /api/activities/{id}/archive                     # Arxivar/desarxivar activitat
POST /api/activities/{id}/duplicate                   # Duplicar (mateixa classe)
POST /api/activities/{id}/duplicate-cross             # Duplicar a una altra classe
GET  /api/activities/{id}/participation               # Estat de participació
POST /api/activities/{id}/remind                      # Enviar recordatoris
GET  /api/activities/{id}/criteria                    # Obtenir criteris
PUT  /api/activities/{id}/criteria                    # Desar criteris personalitzats
GET/POST/DELETE /api/activities/{id}/groups           # Gestió grups
PUT  /api/activities/{id}/groups/{gid}                # Renomenar grup
POST/DELETE /api/activities/{id}/groups/{gid}/members # Membres de grup
GET  /api/activities/{id}/groups/export               # Exportar grups (CSV)
POST /api/activities/{id}/groups/import               # Importar grups (CSV)
GET  /api/activities/{id}/log                         # Registre d'activitat

GET  /api/notes/{activityId}                          # Notes del professor per activitat
PUT  /api/notes/{activityId}/{studentId}              # Desar nota

GET  /api/templates                                   # Llistar plantilles del professor
POST /api/templates                                   # Crear plantilla
DELETE /api/templates/{id}                            # Eliminar plantilla

GET  /api/evaluations/{activityId}                    # Formulari d'avaluació (alumne)
POST /api/evaluations/{activityId}                    # Guardar avaluació
GET  /api/student/activities                          # Dashboard alumne
GET  /api/student/results/{activityId}                # Resultats propis de l'alumne

GET  /api/results/{activityId}                        # Resultats (professor)
GET  /api/results/{activityId}/chart                  # Dades gràfica
GET  /api/results/{activityId}/csv                    # Exportar CSV
GET  /api/results/{activityId}/excel                  # Exportar Excel (.xlsx)
GET  /api/results/module/{moduleId}/evolution         # Evolució alumne per mòdul
GET  /api/results/global                              # Informe global per cicle (admin/gestor)
GET  /api/results/global/excel                        # Informe global Excel (admin/gestor)

GET  /api/criteria                                    # Criteris globals actius
GET  /api/criteria/defaults                           # Criteris per defecte (professors)
PUT  /api/criteria/defaults                           # Editar criteris per defecte (admin)

GET/POST /api/admin/backup/files                      # Backups al servidor
GET/DELETE /api/admin/backup/files/{name}             # Descarregar/eliminar backup
POST /api/admin/backup/files/{name}/restore           # Restaurar backup
GET  /api/admin/backup/export                         # Exportar backup ZIP complet
POST /api/admin/backup/import                         # Importar backup ZIP

GET  /api/admin/audit                                 # Registre d'auditoria (admin)
GET  /api/admin/log-level                             # Llegir nivell de log (admin)
PUT  /api/admin/log-level                             # Canviar nivell de log en calent (admin)
POST /api/admin/new-year                              # Duplicar estructura per a nou curs (admin)
GET  /api/admin/stats                                 # Estadístiques d'ús (admin)

GET  /api/health                                      # Estat DB + Redis (públic)
GET  /i18n/reference.json                             # Totes les claus de traducció en català
```
