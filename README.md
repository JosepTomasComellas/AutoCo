# AutoCo — Sistema d'Avaluació entre Iguals · v1.6.2

Aplicació web per gestionar **autoavaluació** i **coavaluació** d'alumnes en activitats de grup, pensada per a entorns educatius de cicles formatius i batxillerat.

---

## Funcionalitats

### Professor / Administrador

**Gestió de l'estructura docent**
- Gestió de **classes**, **alumnes** i **mòduls** (UF/MP), amb edició inline
- **Avatars** d'alumne amb inicials i color per número de llista
- **Mou alumnes** entre classes (elimina participació anterior i reassigna)
- **Exclusions per mòdul**: alumnes que no participen en un mòdul concret
- Enviament de **credencials per correu** als alumnes (SMTP configurable)
- **Codis QR** per a cada classe: genera un QR que porta directament a la pàgina de login de l'alumne

**Gestió d'activitats**
- Creació d'**activitats** d'avaluació per mòdul, amb obertura i tancament manual des del tauler
- **Criteris personalitzats** per activitat (afegir, reordenar, eliminar) o usar els globals
- **Plantilles d'activitat**: desa configuració (nom, descripció, criteris) i reutilitza-la en noves activitats
- Configuració de **grups** per **arrossegar i deixar anar** (drag & drop)
- Importació/exportació de grups per CSV
- **Duplicació d'activitats** reutilitzant la configuració de grups
- **Duplicació creuada** d'activitats a una altra classe i mòdul
- **Indicador de participació** en temps real a cada targeta (Redis pub/sub, instantani)
- **Recordatoris per correu** als alumnes que no han omplert l'avaluació
- **Notificació automàtica al professor** quan el 100% de l'activitat s'ha completat
- **Desfer eliminació** — finestra de 5 s per cancel·lar eliminació d'activitats i alumnes
- **Registre d'activitat** per cada activitat: qui ha obert, tancat o enviat avaluació i quan
- **Ordre de grups persistent** (▲▼) i criteris d'avaluació editables inline a la pàgina de grups

**Resultats i informes**
- Taula de **resultats** amb capçalera fixa, paginació (25 files), puntuació per criteri (Auto / Co) i notes globals acolorides per rang (verd/taronja/vermell)
- **Filtres avançats**: per grup i per rang de nota (alta ≥8 / mitjana 5–7.9 / baixa <5 / sense coavaluació)
- **Notes del professor** per alumne: camp editable inline a la taula de resultats
- **Gràfiques comparatives** per grup (Auto vs. Co, desglossament per criteri) amb Chart.js
- **Informe PDF individual per alumne**: pàgina optimitzada per a impressió/PDF amb dades, notes globals, detall per criteri i comentaris
- **Exportació CSV i Excel (.xlsx)** de resultats amb format i color per rang de nota

**Perfil i autenticació**
- **Pàgina de perfil** del professor: canvi de nom, cognoms i contrasenya des de la barra de navegació
- **Restabliment de contrasenya per email**: OTP de 6 dígits vàlid 15 minuts (Redis)

**Multi-idioma (i18n)**
- **Selector d'idioma** a la barra de navegació: català (per defecte) i castellà
- Infraestructura `IStringLocalizer` amb fitxers `.resx` — extensible a qualsevol cultura
- Pàgines de login i navegació principal ja localitzades; resta de pàgines extensibles amb el mateix patró

**Administració**
- Gestió de **professors** i permisos d'administrador (exclusiu rol Admin)
- **Còpies de seguretat**: exportació/importació JSON de tota la base de dades
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
- **Barra de progrés** d'avaluació completada en temps real (X/Y criteris puntuats)

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
├── api/          # API REST — ASP.NET Core 9 Minimal API
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
| `api` | ASP.NET Core 9 | intern | API REST + JWT |
| `web` | ASP.NET Core 9 | intern | Blazor Server + MudBlazor |
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

- **Backend:** C# / ASP.NET Core 9 · Entity Framework Core 9 · SQL Server 2022
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

### Opció B — Actualització del servidor (des de Windows)

```powershell
# Genera el paquet de fitxers a copiar
.\deploy\update.ps1

# O envía directament al servidor via SSH (una sola comanda)
.\deploy\push-update.ps1                  # requereix clau SSH configurada
.\deploy\push-update.ps1 -ConfigurarClau  # primera vegada: instal·la la clau SSH
```

El paquet preserva automàticament el `.env` i els certificats SSL del servidor.

### Comandes útils al servidor

```bash
docker compose up --build   # Reconstruir i aixecar (aplicar canvis)
docker compose down         # Aturar (dades preservades)
docker compose down -v      # Aturar i esborrar totes les dades
docker compose logs -f      # Logs en temps real
bash /docker/AutoCo/backup.sh  # Backup manual de la BD
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
