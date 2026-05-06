# AutoCo — Sistema d'Avaluació entre Iguals · v2.5.12

Aplicació web per gestionar **autoavaluació** i **coavaluació** d'alumnes en activitats de grup, pensada per a entorns educatius de cicles formatius i batxillerat.

---

## Funcionalitats

### Professor / Administrador

**Gestió de l'estructura docent**
- Gestió de **classes**, **alumnes** i **mòduls** (UF/MP), amb edició inline
- **Camp DNI** als alumnes, extret automàticament en importar des de l'EPSS
- **Fotos d'alumnes**: upload individual per botó càmera (a la taula o a la fitxa d'edició) o **importació massiva en ZIP** (fotos nomenades per DNI, format EPSS)
- **Fotos de professors**: upload manual des de la pàgina de perfil o des de la fitxa d'admin; visible a la barra de navegació
- **Importació massiva d'alumnes**: si el correu ja existeix a la mateixa classe, **actualitza** les dades (Nom, Cognoms, NumLlista, DNI) sense tocar la contrasenya
- **Avatars** d'alumne amb inicials i color per número de llista (fallback si no hi ha foto)
- **Mou alumnes** entre classes (elimina participació anterior i reassigna)
- **Exclusions per mòdul**: alumnes que no participen en un mòdul concret
- Enviament de **credencials per correu** als alumnes (SMTP configurable)
- **Codis QR** per a cada classe: genera un QR que porta directament a la pàgina de login de l'alumne

**Gestió d'activitats**
- Creació d'**activitats** d'avaluació per mòdul, amb obertura i tancament manual des del tauler
- **Criteris personalitzats** per activitat (afegir, reordenar, eliminar) o usar els globals
- **Plantilles d'activitat**: desa configuració (nom, descripció, criteris) i reutilitza-la en noves activitats
- Configuració de **grups** per **arrossegar i deixar anar** (drag & drop)
- **Edició del nom del grup inline** (botó llapis directament a la capçalera del grup)
- Importació/exportació de grups per CSV
- **Duplicació d'activitats** reutilitzant la configuració de grups
- **Duplicació creuada** d'activitats a una altra classe i mòdul
- **Indicador de participació** en temps real a cada targeta (Redis pub/sub, instantani)
- **Botó «Convidar a participar»** directament visible a la targeta quan l'activitat és oberta — envia correu a tots els alumnes del grup amb barra de progrés en temps real
- **Recordatoris per correu** als alumnes que no han omplert l'avaluació
- **Notificació automàtica al professor** quan el 100% de l'activitat s'ha completat
- **Desfer eliminació** — finestra de 5 s per cancel·lar eliminació d'activitats i alumnes
- **Registre d'activitat** per cada activitat: qui ha obert, tancat o enviat avaluació i quan
- **Ordre de grups persistent** (▲▼) i criteris d'avaluació editables inline a la pàgina de grups

**Resultats i informes**
- Taula de **resultats** amb capçalera fixa, paginació (25 files), puntuació per criteri (Auto / Co) i notes globals acolorides per rang (verd/taronja/vermell); quatre blocs **col·lapsables** per defecte (filtres, taula, detall coavaluacions, registre)
- **Filtres avançats**: per **alumne** (cerca per nom/cognoms), per grup i per rang de nota (alta ≥8 / mitjana 5–7.9 / baixa <5 / sense coavaluació)
- **Notes del professor** per alumne: camp editable inline a la taula de resultats
- **Gràfiques comparatives** per grup (Auto vs. Co, desglossament per criteri) amb Chart.js
- **Informe PDF individual per alumne**: pàgina optimitzada per a impressió/PDF amb dades, notes globals, detall per criteri i comentaris
- **Informe PDF complet per activitat** (`/professor/informe-activitat/{id}`): capçalera, taula resum per grup, estadístiques per criteri (distribució A–E), detall individual opcional per alumne amb salt de pàgina
- **Exportació CSV i Excel (.xlsx)** de resultats amb format, color per rang de nota i columna de comentaris (autoavaluació)

**Perfil i autenticació**
- **Pàgina de perfil** del professor: canvi de nom, cognoms i contrasenya des de la barra de navegació
- **Restabliment de contrasenya per email**: OTP de 6 dígits vàlid 15 minuts (Redis)

**Multi-idioma (i18n)**
- **Selector d'idioma** a la barra de navegació: català (per defecte) i castellà
- Infraestructura `IStringLocalizer` amb fitxers `.resx` — extensible a qualsevol cultura
- Pàgines de login i navegació principal ja localitzades; resta de pàgines extensibles amb el mateix patró

**Administració**
- Gestió de **professors** i permisos d'administrador (exclusiu rol Admin)
- **Còpies de seguretat**: exportació/importació JSON completa (incloent criteris, notes, plantilles i contrasenyes xifrades); **backup automàtic** diari/setmanal configurable via variables d'entorn (`BACKUP_*`)
- **KPIs al tauler**: classes, mòduls, alumnes, activitats, obertes i grups
- **Mode fosc** i **selector de tema de color** (6 opcions) amb preferències desades al navegador

**PWA (Progressive Web App)**
- **Instal·lable** com a aplicació nativa en escriptori, Android i iOS
- **Caché d'assets estàtics** (CSS, JS) per càrrega més ràpida
- **Pàgina offline** en català quan no hi ha connexió al servidor

**Qualitat i fiabilitat**
- **Notificació en temps real** de participació via Redis pub/sub (subscripció reactiva, sense polling)
- **Tests unitaris** de `ResultsService`: 15 casos cobreixen càlcul de notes, caché, control d'accés i ordenació
- **Validació CSV millorada**: format d'email, NumLlista duplicat, visualització completa d'errors inline

### Alumne
- Accés amb correu electrònic i contrasenya (o via codi QR de la classe)
- Avaluació de tots els membres del grup (inclosa autoavaluació) per criteri
- Puntuació amb **escala de 5 estrelles** mostrada com a lletra (E / D / C / B / A)
- **Desar parcialment**: l'alumne pot guardar l'avanç sense haver completat tots els criteris
- **Barra de progrés** d'avaluació completada en temps real (X/Y criteris puntuats)
- **Canvi de contrasenya propi** des del dashboard de l'alumne

---

## Criteris d'avaluació globals (per defecte)

| Clau | Descripció |
|------|------------|
| `probitat` | Probitat |
| `autonomia` | Autonomia |
| `responsabilitat` | Responsabilitat i Treball de qualitat |
| `collaboracio` | Col·laboració i treball en equip |
| `comunicacio` | Comunicació |

Cada activitat pot sobreescriure aquests criteris amb la seva pròpia llista personalitzada.

### Escala de puntuació

| Estrelles | Lletra | Valor |
|:---------:|:------:|:-----:|
| ★☆☆☆☆ | E | 1 |
| ★★☆☆☆ | D | 3.5 |
| ★★★☆☆ | C | 5 |
| ★★★★☆ | B | 7.5 |
| ★★★★★ | A | 10 |

---

## Arquitectura

```
AutoCo/
├── api/          # API REST — ASP.NET Core 10 Minimal API
│   ├── Data/     # EF Core DbContext, models i seed
│   │   └── Models/   # Professor, Class, Student, Module, Activity,
│   │                 # Group, Evaluation, ProfessorNote,
│   │                 # ActivityTemplate, ActivityLog...
│   ├── Services/ # Auth, Class, Activity, Evaluation, Results, Email, Backup
│   ├── Migrations/
│   └── Dockerfile
├── web/          # Frontend — Blazor Server + MudBlazor
│   ├── Components/
│   │   ├── Pages/   # Alumne/, Professor/ (Dashboard, Resultats,
│   │   │            #   Grafic, QrCodes, InformeAlumne...), Admin/, Auth/
│   │   ├── Shared/  # ActivityCard, diàlegs reutilitzables
│   │   └── Layout/  # MainLayout (tema de color, mode fosc)
│   ├── Services/    # ApiClient, UserStateService
│   ├── wwwroot/
│   │   ├── css/site.css   # Estils globals, DnD, dark mode, print, informe PDF
│   │   └── js/            # app.js (utilitats), charts.js (Chart.js interop)
│   └── Dockerfile
├── shared/       # DTOs + AppVersion compartits entre api i web
├── nginx/        # Proxy invers amb SSL automàtic
├── deploy/       # Scripts de desplegament (update.ps1, push-update.ps1)
├── AutoCo.Tests/ # Tests unitaris xUnit (ResultsService, EF Core InMemory)
└── docker-compose.yml
```

### Serveis Docker

| Servei | Imatge | Port | Descripció |
|--------|--------|------|------------|
| `db` | SQL Server 2022 Express | intern | Base de dades principal |
| `redis` | Redis 7 Alpine | intern | Caché de resultats + backplane SignalR |
| `api` | ASP.NET Core 10 | intern | API REST + JWT |
| `web` | ASP.NET Core 10 | intern | Blazor Server + MudBlazor |
| `nginx` | nginx Alpine | 80 / 443 | Proxy SSL, WebSocket per Blazor |

### Model de dades

```
Professor ──< Module ──< Activity ──< Group ──< GroupMember (Student)
              │               ├──< ActivityCriteria
              │               ├──< Evaluation ──< EvaluationScore
              │               ├──< ProfessorNote (per alumne)
              │               └──< ActivityLog (registre d'accions)
Class ────────┘
  ├──< Student
  └──< ModuleExclusion
ActivityTemplate (per professor, criteris JSON)
```

---

## Tecnologies

- **Backend:** C# / ASP.NET Core 10 · Entity Framework Core 10 · SQL Server 2022
- **Frontend:** Blazor Server · [MudBlazor 8.6](https://mudblazor.com/) · Chart.js 4
- **QR:** [Net.Codecrete.QrCodeGenerator](https://github.com/manuelbl/QrCodeGenerator) (SVG pur, sense deps)
- **Autenticació:** JWT (professors) · email + contrasenya (alumnes) · `ProtectedLocalStorage`
- **Caché:** Redis (`IDistributedCache`, TTL 5 min, invalidació automàtica)
- **Temps real:** Redis pub/sub → `ParticipationNotificationService` → Blazor (reemplaça polling)
- **Seguretat:** BCrypt (work factor 12) · JWT secret mínim 32 caràcters
- **Email:** MailKit + SMTP configurable (credencials, recordatoris, notificació de compleció)
- **PWA:** `manifest.json` + Service Worker (cache-first assets, pàgina offline)
- **i18n:** `IStringLocalizer` + fitxers `.resx` (català per defecte, castellà)
- **Tests:** xUnit + EF Core InMemory (15 tests unitaris de `ResultsService`)
- **Desplegament:** Docker Compose · nginx (SSL/TLS auto-signat o certificat propi)

---

## Rols

| Rol | Accés |
|-----|-------|
| **Admin** | Tot. Gestiona professors, veu totes les classes i activitats, còpies de seguretat |
| **Professor** | Les seves pròpies classes, mòduls, activitats i resultats |
| **Alumne** | Les activitats del seu grup. Pot avaluar mentre l'activitat és oberta |

---

## Desplegament

### Opció A — Entorn local (desenvolupament)

**Requisits:** Docker Desktop

```bash
git clone https://github.com/JosepTomasComellas/AutoCo.git
cd AutoCo
cp .env.example .env        # edita les variables si cal
docker compose up --build
```

Accedeix a **https://localhost** (accepta l'avís del certificat auto-signat).

### Opció B — Actualització directa des del servidor (recomanada)

**Configuració inicial (una sola vegada):**
```bash
cd /docker
git clone https://github.com/JosepTomasComellas/AutoCo.git AutoCo-git
cp AutoCo/.env AutoCo-git/.env
cp -r AutoCo/nginx/ssl AutoCo-git/nginx/ssl
rm -rf AutoCo && mv AutoCo-git AutoCo
```

**A partir d'ara, per aplicar qualsevol actualització:**
```bash
bash /docker/AutoCo/deploy/server-update.sh
```

El script fa `git pull`, **valida la configuració del `.env`** (variables obligatòries, formats, SMTP) i reconstrueix les imatges. Si la validació falla, els contenidors actuals no s'aturen.

### Opció C — Actualització del servidor des de Windows

```powershell
# Genera el paquet de fitxers i copia al servidor
.\deploy\update.ps1
scp -r "deploy\autoco-update-YYYYMMDD" root@servidor:/docker/AutoCo-new
```

```bash
# Al servidor
rsync -a --exclude='.env' --exclude='nginx/ssl' /docker/AutoCo-new/ /docker/AutoCo/
bash /docker/AutoCo/update.sh
```

### Comandes útils al servidor

```bash
bash /docker/AutoCo/deploy/server-update.sh  # Actualitzar des de GitHub
docker compose logs -f           # Logs en temps real
docker compose down              # Aturar (dades preservades)
docker compose down -v           # Aturar i esborrar totes les dades
bash /docker/AutoCo/backup.sh    # Backup manual de la BD
```

---

## Configuració (.env)

Copia `.env.example` a `.env` i ajusta els valors:

| Variable | Descripció | Obligatori |
|----------|------------|:----------:|
| `MSSQL_SA_PASSWORD` | Contrasenya SQL Server (mínim 8 car., majúsc., número i símbol) | ✓ |
| `JWT_SECRET` | Secret JWT (mínim 32 caràcters) | ✓ |
| `JWT_EXPIRY_HOURS` | Durada del token en hores (per defecte: 8) | |
| `ADMIN_EMAIL` | Correu de l'administrador inicial | ✓ |
| `ADMIN_PASSWORD` | Contrasenya de l'administrador inicial | ✓ |
| `ADMIN_NOM` | Nom de l'administrador | ✓ |
| `ADMIN_COGNOMS` | Cognoms de l'administrador | |
| `SMTP_HOST` | Servidor SMTP (p.ex. `smtp.gmail.com`) | |
| `SMTP_PORT` | Port SMTP (p.ex. `587`) | |
| `SMTP_USERNAME` | Usuari SMTP | |
| `SMTP_PASSWORD` | Contrasenya SMTP (o app password) | |
| `SMTP_FROM_ADDRESS` | Adreça remitent dels correus | |
| `SMTP_FROM_NAME` | Nom remitent dels correus | |
| `APP_WEB_URL` | URL pública (p.ex. `https://autoco.centre.cat`) — per als QR i links dels correus | |
| `BACKUP_ENABLED` | Activa el backup automàtic (`true`/`false`, per defecte `false`) | |
| `BACKUP_DAILY_HOUR` | Hora del backup diari en hora local Europe/Madrid (0–23, per defecte `2`) | |
| `BACKUP_WEEKLY_DAY` | Dia de la setmana del backup setmanal (0=Dg, 1=Dl … 6=Ds, per defecte `0`) | |
| `BACKUP_DAILY_RETENTION` | Nombre màxim de backups diaris a conservar (per defecte `7`) | |
| `BACKUP_WEEKLY_RETENTION` | Nombre màxim de backups setmanals a conservar (per defecte `4`) | |

> SMTP és opcional. Si no es configura, les funcions d'email queden desactivades però l'aplicació funciona amb normalitat.

### SSL

- **Sense certificat:** nginx genera automàticament un certificat auto-signat vàlid 10 anys.
- **Amb certificat propi:** col·loca `server.crt` i `server.key` a `nginx/ssl/` abans d'arrencar.

---

## Endpoints principals de l'API

```
POST /api/auth/professor                              # Login professor/admin
POST /api/auth/student                                # Login alumne

GET/POST/PUT/DELETE /api/professors                   # Gestió professors (admin)
GET/POST/PUT/DELETE /api/classes                      # Gestió classes
GET/POST/PUT/DELETE /api/classes/{id}/students        # Gestió alumnes
POST /api/classes/{id}/students/bulk                  # Importació massiva CSV
POST /api/classes/{id}/students/{sid}/reset-password  # Reset contrasenya
POST /api/classes/{id}/students/{sid}/send-password   # Enviar credencials per correu
POST /api/classes/{id}/students/send-all-passwords    # Enviar credencials a tots
POST /api/classes/{id}/students/{sid}/move            # Moure alumne a una altra classe
GET/POST/PUT/DELETE /api/classes/{id}/modules         # Gestió mòduls
GET/POST/DELETE    /api/modules/{id}/exclusions       # Exclusions per mòdul

GET/POST/PUT/DELETE /api/activities                   # Gestió activitats
POST /api/activities/{id}/toggle                      # Obrir/tancar activitat
POST /api/activities/{id}/duplicate                   # Duplicar (mateixa classe)
POST /api/activities/{id}/duplicate-cross             # Duplicar a una altra classe
GET  /api/activities/{id}/participation               # Estat de participació
POST /api/activities/{id}/remind                      # Enviar recordatoris
GET  /api/activities/{id}/criteria                    # Obtenir criteris
PUT  /api/activities/{id}/criteria                    # Desar criteris personalitzats
GET  /api/activities/{id}/groups/export               # Exportar grups (CSV)
POST /api/activities/{id}/groups/import               # Importar grups (CSV)
GET/POST/DELETE /api/activities/{id}/groups           # Gestió grups
PUT /api/activities/{id}/groups/{gid}                 # Renomenar grup
POST/DELETE /api/activities/{id}/groups/{gid}/members # Membres de grup
GET  /api/activities/{id}/log                         # Registre d'activitat

GET  /api/notes/{activityId}                          # Notes del professor per activitat
GET  /api/notes/{activityId}/{studentId}              # Nota d'un alumne concret
PUT  /api/notes/{activityId}/{studentId}              # Desar nota

GET  /api/templates                                   # Llistar plantilles del professor
POST /api/templates                                   # Crear plantilla
DELETE /api/templates/{id}                            # Eliminar plantilla

GET  /api/evaluations/{activityId}                    # Formulari d'avaluació (alumne)
POST /api/evaluations/{activityId}                    # Guardar avaluació
GET  /api/student/activities                          # Dashboard alumne

GET  /api/results/{activityId}                        # Resultats (professor)
GET  /api/results/{activityId}/chart                  # Dades gràfica
GET  /api/results/{activityId}/csv                    # Exportar CSV

GET/POST /api/admin/backup/files                      # Backups al servidor
GET/DELETE /api/admin/backup/files/{name}             # Descarregar/eliminar backup
POST /api/admin/backup/files/{name}/restore           # Restaurar backup
GET  /api/admin/backup/export                         # Exportar backup JSON complet
POST /api/admin/backup/import                         # Importar backup JSON

GET  /api/health                                      # Estat DB + Redis
GET  /api/criteria                                    # Llista de criteris globals
```

---

## Changelog

### v2.5.12
- **Nivell de log configurable des de la UI** — pàgina `/admin/sistema` (accessible des del tauler admin) amb selector de 5 nivells (Error, Warning, Information, Debug, Trace); el canvi s'aplica immediatament a l'API i al web sense reinici i persisteix entre reinicis via Redis (`autoco:loglevel`); tècnicament: `LogLevelHolder` singleton capturat per un filtre `AddFilter` en calent, inicialitzat des de Redis a l'arrencada

### v2.5.11
- **Nivell de log configurable sense reinici** — `config/logging.json` muntat com a volum Docker (`:ro`) a `/app/logging.json` en els contenidors `api` i `web`; editar el fitxer al servidor propaga el canvi en pocs segons gràcies a `reloadOnChange: true` i `DOTNET_USE_POLLING_FILE_WATCHER=true`; per defecte silencia els espais de noms sorollosos (`System.Net.Http`, `Microsoft.AspNetCore`, EF Core, SignalR, Redis) i manté `Information` per als logs d'AutoCo

### v2.5.10
- **Logo incrustat als correus** — `logo2.png` copiat al contenidor API i adjuntat via CID (`LinkedResources`); es mostra sense dependre de cap URL pública ni de la web
- **Healthcheck de l'API** — `docker-compose.yml` afegeix `healthcheck` al servei `api` (`curl /api/health`, interval 10s, start_period 60s); `/api/health` ara és públic (sense `RequireAuthorization`)
- **Arrencada seqüencial garantida** — `web` ara espera `api: condition: service_healthy` (i `redis: service_healthy`) abans d'arrencar, evitant el `Connection refused` durant les migracions EF Core
- **Dockerfile API** — instal·la `curl` (necessari per al healthcheck) i copia el logo a `/app/resources/logo2.png`

### v2.5.9
- **Correus HTML amb estètica de targeta** — tots els correus transaccionals (credencials, recordatori, convit, compleció, reset de contrasenya) s'envien ara en format HTML + text pla (fallback `multipart/alternative`); la plantilla reprodueix l'estil de la pantalla de login: capçalera fosca `#1e293b`, bloc de dades amb fons `#f8fafc` i contorn, contrasenya destacada en vermell monospace, botó CTA vermell `#CC0000` i peu de pàgina gris

### v2.5.8
- **Validació de `.env` a l'script de desplegament** — `server-update.sh` comprova ara que totes les variables obligatòries (`MSSQL_SA_PASSWORD`, `JWT_SECRET`, `ADMIN_*`) estiguin personalitzades, que `JWT_SECRET` tingui mínim 32 caràcters, que `ADMIN_EMAIL` sigui vàlid, i que `BACKUP_DAILY_HOUR`/`BACKUP_WEEKLY_DAY` estiguin en rang; avisa si SMTP no és configurat o si `APP_WEB_URL` és `localhost`; en cas d'error, atura el desplegament **sense** aturar els contenidors actuals
- **Fix `.env.example`** — eliminada la variable `ADMIN_USERNAME` (no usada); comentari de `BACKUP_DAILY_HOUR` corregit a "hora local Europe/Madrid"

### v2.5.7
- **Fix zona horària** — contenidors `api` i `web` configurats amb `TZ=Europe/Madrid`; EF Core ara retorna tots els `DateTime` amb `Kind=Utc` (value converter) perquè `ToLocalTime()` converteixi correctament a CET/CEST en tots els registres i timestamps de l'aplicació

### v2.5.6
- **Tauler de resultats col·lapsable** — els quatre blocs (filtres avançats, taula de resultats, detall coavaluacions, registre d'activitat) ara estan agrupats en un `MudExpansionPanels` i col·lapsats per defecte; es poden desplegar independentment

### v2.5.5
- **Foto d'alumne editable des de la fitxa d'edició** — quan es clica el botó llapis d'un alumne, el formulari mostra un avatar, botó d'upload i botó d'eliminar foto; la llista i la fitxa es sincronitzen al moment

### v2.5.4
- **Foto de professor editable des de l'admin** — la fitxa d'edició de cada professor a `/admin/professors` inclou ara una secció de foto (avatar 64px, upload, eliminació); l'avatar de la llista de professors mostra la foto si en té; si l'admin s'edita a si mateix, la navbar s'actualitza immediatament

### v2.5.3
- **Importació massiva: actualitza alumnes existents** — si un correu ja existeix a la mateixa classe, actualitza Nom, Cognoms, NumLlista i DNI sense tocar la contrasenya; si pertany a una altra classe, s'omés amb missatge; el resum mostra `X creats, Y actualitzats, Z omesos`

### v2.5.2
- **Fix `PendingModelChangesWarning`** — afegit `HasMaxLength(30)` per a `Student.Dni` a `OnModelCreating`; propietat reposicionada alfabèticament al snapshot; `ConfigureWarnings` per compatibilitat entre snapshot EF Core 9 i runtime EF Core 10

### v2.5.1
- **Foto al formulari d'avaluació** — l'alumne veu la foto de cada company/a quan omple l'avaluació
- **Foto als resultats** — la foto apareix a la taula de resultats i als panells d'expansió de coavaluadors
- **Foto als informes PDF** — informe individual i informe d'activitat mostren la foto de l'alumne (compatible amb impressió)

### v2.5.0
- **Fotos d'alumnes** — upload individual (botó càmera per alumne) i **importació massiva en ZIP** (fotos nomenades per la part numèrica del DNI, compatible amb el format EPSS); les fotos es serveixen com a fitxers estàtics via volum Docker compartit (`fotos-data`)
- **Fotos de professors** — upload manual des de la pàgina de perfil; l'avatar apareix a la barra de navegació; persistit a la sessió
- **Camp DNI als alumnes** — nou camp opcional `Dni`; s'extreu automàticament en importar des del format HTML/XLS de l'EPSS (columna 3: `Num|Cognoms|Nom|DNI|…`); és la clau per associar fotos del ZIP
- **Arquitectura de fotos** — volum Docker `fotos-data` muntat a `/app/fotos` (API, escriptura) i `/app/wwwroot/fotos` (Web, servei HTTP); `PhotoService` centralitza la lògica de persistència i URL

### v2.4.1
- **Fix**: atributs `[DbContext]` i `[Migration]` afegits a la migració `AddStudentEncryptedPassword` — sense ells EF Core no descobria la migració per reflexió i la columna `PlainPasswordEncrypted` no es creava en desplegaments existents
- **UX**: botó «Convidar a participar» (campana) mogut del menú desplegable a les accions visibles de la targeta d'activitat, al costat del botó de gràfica; visible directament quan l'activitat és oberta

### v2.4.0
- **Informe PDF complet per activitat** — nova pàgina `/professor/informe-activitat/{id}`: capçalera amb dades del mòdul i classe, taula resum per grup (nota auto/co, comentari, nombre d'avaluadors), estadístiques per criteri (mitjana auto/co, distribució A–E amb badges de color), detall individual opcional per alumne (salt de pàgina entre alumnes, inclou avaluacions rebudes i nota del professor)
- **Backup complet** — `BackupService` ampliats per incloure `ActivityCriteria`, `ProfessorNotes`, `ActivityTemplates` i camp `PlainPasswordEncrypted`; `ImportAsync` restaura correctament les taules noves
- **Backup automàtic** (`BackupHostedService`) — servei en background que executa backups diaris i setmanals a l'hora configurada; retenció configurable via `BACKUP_*` variables d'entorn
- **Columna «Comentari»** a l'exportació Excel (.xlsx): autoavaluació de l'alumne inclosa com a columna addicional

### v2.3.0
- **Contrasenya xifrada d'alumnes** — camp `PlainPasswordEncrypted` (AES-GCM) a la taula `Students`; permet al professor consultar la contrasenya en text pla a la pantalla de gestió d'alumnes
- **Canvi de contrasenya propi per a alumnes** — el dashboard de l'alumne inclou un formulari per canviar la seva pròpia contrasenya
- **Botó «Convidar a participar»** — disponible des de la targeta d'activitat quan és oberta; envia correu a tots els alumnes del grup amb barra de progrés d'enviament en temps real
- **Refactorització de recordatoris** — lògica unificada entre «Convidar» (tots els alumnes) i «Recordar» (alumnes que no han avaluat)
- **Ordenació de columnes al DataGrid** de resultats per Nom i Coavaluació

### v2.2.3
- **Fix estadístiques**: participació mitjana capada al 100% — valors superiors indicaven alumnes que van avaluar i després van ser moguts o eliminats del grup

### v2.2.2
- **Estadístiques: reiniciar registre d'accessos** — botó (icona paperera) a la capçalera de la pàgina d'estadístiques; demana confirmació i esborra tots els registres de `ProfessorLogins`; nou endpoint `DELETE /api/admin/stats/logins`

### v2.2.1
- **Fix**: migració formal `AddProfessorLogins` per resoldre `PendingModelChangesWarning` d'EF Core 10
- **Fix**: query `GroupBy` a `/api/admin/stats` ara usa tipus anònim per evitar error de traducció SQL

### v2.2.0
- **Estadístiques d'ús (admin)** — nova pàgina `/admin/estadistiques` accessible només per administradors: KPIs globals (accessos 30d, professors actius, activitats creades), taula per professor (accessos, activitats, participació mitjana, darrer accés amb codi de color) i gràfica de tendència mensual (últims 6 mesos)
- **Registre de logins** — cada accés de professor es desa a la taula `ProfessorLogins` per alimentar les estadístiques; sense impacte en rendiment ni en privacitat (dades internes)
- **Nou endpoint** `GET /api/admin/stats` — retorna estadístiques agregades per professor i tendència mensual

### v2.1.3
- **Peu de pàgina simplificat** — «AutoCo Avaluació © any» → «AutoCo © any» arreu (footer, informes PDF) per evitar salt de línia en mòbil

### v2.1.2
- **Correcció login: missatge d'error** — les credencials incorrectes ja mostren el missatge d'error en comptes de retornar silenciosament a la selecció de rol; nou `PostLoginAsync` separa la lògica de 401-credencials de 401-sessió expirada
- **Barra de progrés sticky** — `height: 100vh; overflow: hidden` al `.mud-layout` i `overflow-y: auto; min-height: 0` al `.mud-main-content` perquè `position: sticky` funcioni dins del contenidor de scroll de MudBlazor
- **`server-update.sh`: mostra versions** — el script de desplegament ara mostra la versió actual desplegada, la versió nova al repositori i la llista de commits nous abans d'actualitzar

### v2.1.1
- **Correcció login: canvi de rol** — `OnInitialized` de les pàgines de login ja no redirigeix qualsevol usuari autenticat, sinó únicament l'usuari del rol corresponent; permet canviar de rol per fer proves sense quedar-se atrapat

### v2.1.0
- **Grups: edició inline del nom** — botó llapis a la capçalera de cada grup, camp inline amb confirm/cancel; nou endpoint `PUT /api/activities/{id}/groups/{gid}`
- **Valoració parcial** — l'alumne pot desar l'avaluació sense tenir tots els criteris omplerts; es mostra un avís però no es bloqueja
- **Resultats: filtre per alumne** — camp de cerca per nom/cognoms a la secció de filtres avançats
- **Correcció gràfics** — Chart.js carregat des d'`App.razor` (no `<HeadContent>`) per garantir que la llibreria estigui disponible en qualsevol navegació
- **Correcció indicador de participació** — `GetParticipationAsync` ara filtra per `IsSelf = true`, consistent amb el senyal en temps real de Redis; ja no compta avaluacions parcials com a completes
- **Barra de progrés sticky** — `overflow-y: auto` al `.mud-main-content` perquè `position: sticky` funcioni correctament dins del contenidor de scroll de MudBlazor
- **Scroll a la llista de grups** — la columna d'alumnes sense assignar té `max-height: 400px` i scroll intern; el botó de llançar-hi és sempre visible
- **Activitats tancades** — fons vermell clarament visible en mode clar (`#fecaca`) i fosc (`#450a0a`) via classe CSS
- **Footer** — «Departament» abreujat a «Dept.» arreu (footer, informes PDF)

### v2.0.0
- **Migració a .NET 10**: tots els projectes (`api`, `web`, `shared`, `AutoCo.Tests`) actualitzats a `net10.0`; imatges Docker `aspnet:10.0` / `sdk:10.0`; paquets Microsoft `10.0.*`
- **UI alumne**: llegenda de puntuació eliminada del formulari d'avaluació — la correspondència estreles ↔ lletra ja es mostra inline a cada criteri
- **UI professor**: botons de les targetes d'activitat agrupats en un desplegable — les quatre primeres accions (Resultats, Grups, Editar, Gràfica) resten visibles directament
- **PWA**: `theme_color` del manifest i meta tag corregit (`#1976d2` → `#1e293b`); colors de la pàgina offline alineats amb el tema de l'aplicació
- **Qualitat**: 0 warnings de compilació — CS8604 (`NomComplet` nullable) i EF1002 (pragma) resolts

### v1.6.8
- **i18n completa**: tota la UI traduïda al català i castellà; `DictionaryLocalizer` cobreix totes les cadenes de professors, alumnes i administrador
- **Bugfix**: taules `ActivityCriteria`, `ActivityLogs`, `ProfessorNotes`, `ActivityTemplates` no es creaven en alguns entorns — afegida migració explícita
- **Bugfix**: botons d'`ActivityCard` feien wrap en pantalles petites — afegit `flex-wrap:wrap`

### v1.6.6
- **DictionaryLocalizer estàtic**: bypass del `ResourceManager` per evitar problemes de resolució de recursos embeguts a Docker; totes les traduccions ara en diccionaris C# compilats

### v1.6.5
- **Bugfix i18n**: selector d'idioma no feia res en fer clic — `InvokeVoidAsync` al `OnClick` de `MudMenuItem` retornava `ValueTask` sense ser awaited i l'excepció era silenciosa; canviat a `async () => await JS.InvokeVoidAsync(...)`
- **Bugfix i18n**: recursos de localització no trobats — registre explícit de `IStringLocalizer<SharedResources>` via `factory.Create("AutoCo.Web.Resources.SharedResources", "AutoCo.Web")` que apunta directament al recurs embedded correcte; afegit using `Microsoft.Extensions.Localization`

### v1.6.4
- **Bugfix i18n crític**: les claus de traducció es mostraven literalment (p.ex. `Lang_Catalan`) perquè `ResourceManagerStringLocalizerFactory` no podia calcular el nom del recurs embedded sense `[RootNamespace]`; afegit `[assembly: RootNamespaceAttribute("AutoCo.Web")]` a `Program.cs`
- **Bugfix i18n Blazor Server**: `RequestLocalizationMiddleware` fixava la cultura al thread HTTP però els threads del circuit Blazor no l'heretaven; `App.razor` ara llegeix la cultura del `HttpContext` via `IRequestCultureFeature` i la propaga via `CultureInfo.DefaultThreadCurrentCulture/UICulture`
- **Redis**: el warning `Memory overcommit must be enabled` és una advertència del host Linux — veure les instruccions de configuració al servidor

### v1.6.3
- **Rendiment crític**: `BackupService.ImportAsync` passava de N+1 `SaveChangesAsync` (centenars de crides per backup gran) a exactament 10 crides independentment del volum de dades
- **Seguretat**: codi OTP de restabliment de contrasenya usat `new Random()` (predictible) — substituït per `RandomNumberGenerator.GetInt32()` (criptogràficament segur)
- **Robustesa**: `ModuleService.CreateAsync` usava l'operador `!` null-forgiving sense validació — ara llança `InvalidOperationException` si el professor no existeix
- **Logging**: `catch {}` al log de toggle d'activitat substituït per `logger.LogWarning` amb context
- **Robustesa**: `GetUserId` usava `int.Parse` (pot llançar `FormatException`) — substituït per `int.TryParse` amb fallback segur

### v1.6.2
- **Seguretat crítica**: IDOR a `CreateGroupAsync`, `DeleteGroupAsync`, `AddMemberAsync`, `RemoveMemberAsync` — qualsevol professor podia gestionar grups i membres d'activitats alienes; tots ara validen propietat de l'activitat
- **Robustesa**: `ReorderGroupsAsync` usa `Dictionary.TryGetValue` en lloc de `.First()` per evitar `InvalidOperationException` en condicions de carrera
- **Rendiment**: refactorització de `ImportGroupsAsync` en 3 fases (parse → crear grups → crear membres) eliminant tots els `SaveChangesAsync` dins del loop; es garanteix coherència i s'eviten duplicats en importacions simultànies
- **Logging**: `EvaluationService` ara registra warns detallats en lloc de `catch { }` silent als `Task.Run` (notificació Redis, log d'activitat, notificació de compleció)
- **Rendiment**: `ProfessorService.SendAllCredentialsAsync` N+1 corregit (un sol `SaveChangesAsync` per a tots els professors)
- **Seguretat**: `/api/health` ara requereix autenticació per evitar information disclosure
- **DoS**: `PUT /api/activities/{id}/criteria` limitat a 50 criteris màxim per activitat
- **Seguretat**: `BackupService.ImportAsync` retorna missatge genèric en errors (sense detalls interns)

### v1.6.1
- **Seguretat**: IDOR a `GetGroupsAsync` i `ReorderGroupsAsync` — qualsevol professor autenticat podia llegir/modificar grups d'activitats alienes; afegida validació de propietat
- **Seguretat**: `ReorderGroupsAsync` ara ignora IDs de grups que no pertanyen a l'activitat
- **Rendiment**: `SendAllPasswordsAsync` tenia N+1 `SaveChangesAsync` dins el bucle — ara desa tots els hashes d'un cop
- **Robustesa**: `ImportGroupsAsync` afegit límit de 5 MB i 5.000 línies per prevenir DoS

### v1.6.0
- **PWA**: `manifest.json`, service worker (cache-first d'assets, pàgina offline)
- **Temps real**: Redis pub/sub substitueix el polling de 30 s a l'indicador de participació
- **Validació CSV**: regex email, NumLlista duplicat, llista d'errors inline completa
- **Tests unitaris**: 15 casos xUnit per a `ResultsService` (EF Core InMemory)
- **i18n**: `IStringLocalizer` + fitxers `.resx` (ca/es), selector d'idioma a la navbar
- **Bugfix**: caché de resultats no s'invalidava en canviar criteris, obrir/tancar o editar activitats
- **Bugfix**: `ActivityCard` no es subscrivia a temps real si l'activitat s'obria post-render

### v1.5.0
- Perfil professor (nom, cognoms, contrasenya) des de la barra de navegació
- Restabliment de contrasenya per email (OTP 6 dígits, Redis, 15 min)
- Exportació Excel (.xlsx) amb color per rang de nota
- Criteris d'avaluació editables inline a la pàgina de grups
- Ordre de grups persistent (▲▼)
- Paginació de la taula de resultats (25 files)
- Seguretat: comprovació de propietat en endpoints de notes i log

### v1.4.0
- Notes del professor per alumne (editables inline)
- Plantilles d'activitat (desa i reutilitza configuració + criteris)
- Gràfiques comparatives Auto vs. Co per grup i per criteri (Chart.js)
- Codis QR per classe (SVG, imprimibles)
- Informe PDF individual per alumne
- Duplicació creuada d'activitats entre classes
- Registre d'activitat (qui ha obert, tancat, avaluat i quan)

### v1.3.0 i anteriors
- Gestió de classes, alumnes, mòduls i activitats
- Grups per drag & drop, importació/exportació CSV
- Autoavaluació i coavaluació per escala d'estrelles
- Resultats amb filtres, exportació CSV
- Mode fosc, selector de tema de color
- Còpies de seguretat JSON

---

## Llicència

Projecte de codi obert per a ús educatiu — Salesians de Sarrià, Departament d'Informàtica.
