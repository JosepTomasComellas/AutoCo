# AutoCo — Sistema d'Avaluació entre Iguals

Aplicació web per gestionar **autoavaluació** i **coavaluació** d'alumnes en activitats de grup. Tot el text de la interfície és en **català**.

## Estructura del projecte

```
AutoCo/
├── api/                        # API REST (ASP.NET Core 10 Minimal API)
│   ├── Data/
│   │   ├── AppDbContext.cs     # EF Core DbContext
│   │   ├── Constants.cs        # Criteris d'avaluació globals per defecte
│   │   ├── SeedData.cs         # Seed inicial (admin per defecte)
│   │   └── Models/             # Entitats: Professor, Class, Student, Module,
│   │                           #           Activity, Group, Evaluation...
│   ├── DTOs/Dtos.cs            # Tots els records de request/response
│   ├── Services/               # Lògica de negoci (Auth, Class, Activity,
│   │                           #   Evaluation, Results, Email, Backup)
│   │   ├── ActivitySchedulerService.cs  # BackgroundService: obre/tanca activitats programades (cada minut)
│   │   └── LogLevelHolder.cs   # Singleton: nivell de log en calent (volatile int)
│   ├── Program.cs              # Minimal API endpoints + DI
│   └── Dockerfile
├── web/                        # Frontend (Blazor Server + MudBlazor)
│   ├── Components/
│   │   ├── App.razor           # Arrel HTML: manifest PWA, service worker, i18n
│   │   ├── Layout/
│   │   │   └── MainLayout.razor  # Navbar, footer, tema de color, mode fosc
│   │   ├── Pages/
│   │   │   ├── Index.razor     # Pàgina d'inici (selecció de rol)
│   │   │   ├── Auth/           # Login professor i alumne
│   │   │   ├── Alumne/         # Dashboard i formulari d'avaluació
│   │   │   └── Professor/      # Dashboard, classes, alumnes, activitats,
│   │   │                       #   grups, resultats, gràfiques, informe PDF
│   │   └── Shared/             # ActivityCard, diàlegs reutilitzables
│   ├── Resources/
│   │   └── DictionaryLocalizer.cs  # i18n estàtica (ca/es), evita ResourceManager a Docker
│   ├── Services/
│   │   ├── ApiClient.cs            # Client HTTP cap a l'API (tots els endpoints)
│   │   ├── UserStateService.cs     # Estat de sessió Blazor (substitueix ISession)
│   │   ├── LogLevelHolder.cs       # Singleton: nivell de log en calent (volatile int)
│   │   └── ParticipationNotificationService.cs  # Redis pub/sub → Blazor
│   ├── wwwroot/
│   │   ├── css/site.css        # Estils globals (DnD, dark mode, print, informe PDF)
│   │   ├── js/                 # app.js (utilitats JS), charts.js (Chart.js interop)
│   │   ├── manifest.json       # PWA manifest (theme_color #1e293b)
│   │   ├── service-worker.js   # PWA: caché offline.html+imatges; CSS/JS via HTTP cache (ETag)
│   │   └── offline.html        # Pàgina offline en català
│   ├── Program.cs              # Configuració (Redis, SignalR, MudBlazor, i18n, DataProtection)
│   └── Dockerfile
├── shared/                     # DTOs i AppVersion compartits (api + web)
│   └── AppVersion.cs           # Versió actual de l'aplicació
├── AutoCo.Tests/               # Tests unitaris xUnit (ResultsService, EF Core InMemory)
├── nginx/                      # Proxy invers SSL (auto-signat o certificat propi)
├── deploy/                     # Scripts de desplegament
├── docker-compose.yml          # Orquestració base (redis + api + web + nginx; sense db)
└── docker-compose.db.yml       # Overlay MSSQL intern (afegeix servei db + depends_on)
```

## Desplegament

```bash
# MSSQL intern (per defecte, via COMPOSE_FILE al .env):
docker compose up --build

# MSSQL extern (sense COMPOSE_FILE al .env):
docker compose up --build

# Actualitzar servidor (recomanat; gestiona COMPOSE_FILE automàticament)
bash /docker/AutoCo/deploy/server-update.sh
```

**URLs locals (via nginx):**
- Web: https://localhost

## Serveis Docker

| Servei | Imatge | Descripció |
|--------|--------|------------|
| `db` | SQL Server 2022 Express | MSSQL intern (opcional; activat per `COMPOSE_FILE=...:docker-compose.db.yml`) |
| `redis` | Redis 7 Alpine | Caché, backplane SignalR, OTP reset contrasenya |
| `api` | ASP.NET Core 10 | API REST + JWT |
| `web` | ASP.NET Core 10 | Blazor Server + MudBlazor |
| `nginx` | nginx Alpine | Proxy SSL, WebSocket Blazor |

La `api` espera `redis` (i `db` si s'usa l'overlay MSSQL intern) healthy abans d'arrencar.

## Configuració (variables d'entorn via .env)

Per a producció real, canviar:
- `COMPOSE_FILE` — `docker-compose.yml:docker-compose.db.yml` per MSSQL intern; absent per MSSQL extern
- `DB_CONNECTION` — cadena de connexió completa (MSSQL o PostgreSQL); **sempre obligatori**
- `MSSQL_SA_PASSWORD` — contrasenya SQL Server (només si s'usa MSSQL intern)
- `JWT_SECRET` — secret JWT (mínim 32 caràcters)
- `ADMIN_EMAIL` / `ADMIN_PASSWORD` — credencials de l'administrador inicial
- `DEFAULT_LANGUAGE` — idioma per defecte de la UI (`ca`, `es`, o qualsevol codi de `config/i18n/`; per defecte `ca`)
- `I18N_PATH` — ruta als fitxers JSON de traducció (per defecte `/app/i18n`, muntat via volum `./config/i18n`)
- `BRAND_APP_NAME` / `BRAND_APP_SHORT_NAME` — nom de l'app al títol i manifest PWA
- `BRAND_ORG_NAME` / `BRAND_ORG_DEPT` — textos del peu de pàgina
- `BRAND_PRIMARY_COLOR` / `BRAND_NAV_COLOR` — colors principals de la UI; `BrandingService` calcula automàticament `PrimaryColorDark` i `NavColorDark` (DarkenHex)
- Logo: fitxer `./config/branding/logo.png` → muntat a `/app/wwwroot/branding/logo.png`; `BrandingService` detecta si existeix
- Imatge de fons: fitxer `./config/branding/background.png` (o `.jpg`) → muntat a `/app/wwwroot/branding/`; `BgImageCssValue` retorna `url(...)` per usar a CSS
- `App.razor` injecta CSS custom properties al `<head>` (`:root { --brand-primary, --brand-primary-dk, --brand-nav, --brand-nav-dk, --brand-bg-image }`) via `<style>` DESPRÉS del `<link site.css>` per sobreescriure; cal `(MarkupString)` per evitar encoding HTML de `url(...)`

## Model de dades

```
Cicle ────< Class ──────< Module ──< Activity ──< Group ──< GroupMember (Student)
                │           │              ├──< ActivityCriteria (criteris per activitat)
                │           │              ├──< Evaluation ──< EvaluationScore (per criteri)
                │           │              ├──< ProfessorNote (per alumne)
                │           │              └──< ActivityLog (registre d'accions)
                │           └──< Professor (via ProfessorClass)
                └──< Student
                └──< ModuleExclusion
ActivityTemplate (per professor, criteris JSON)
```

- Un alumne pertany a una `Class` i autentifica amb email + contrasenya
- Una `Class` pertany a un `Cicle` (FK obligatori; cicle «General» creat per seed)
- Un `Professor` accedeix únicament a les classes que li han estat assignades via `ProfessorClass`
- Una `Activity` pertany a un `Module` (que pertany a una `Class`); té camps `OpenAt?`/`CloseAt?` (UTC) per a programació automàtica
- Un alumne avalua tots els membres del seu grup (inclòs ell mateix)
- `IsSelf = true` quan avaluador = avaluat (autoavaluació)

## Criteris d'avaluació

Definits a `api/Data/Constants.cs` com a globals per defecte. Cada activitat pot sobreescriure'ls via `ActivityCriteria`.

| Key | Label |
|-----|-------|
| `probitat` | Probitat |
| `autonomia` | Autonomia |
| `responsabilitat` | Responsabilitat i Treball de qualitat |
| `collaboracio` | Col·laboració i treball en equip |
| `comunicacio` | Comunicació |

Puntuació: **escala E/D/C/B/A** (estreles 1–5 = valors 1, 3.5, 5, 7.5, 10).

## Rols i autenticació

- **Admin** — professor amb `IsAdmin=true`. Gestiona professors, cicles i veu tot (totes les classes).
- **Gestor** — professor amb `IsGestor=true`. Veu tot (totes les classes, estadístiques, Informe Global), però modifica únicament les classes que té assignades via `ProfessorClass`. JWT role `"Gestor"`. `IsAdminOrGestor` per a lectures; `IsAdmin` per a escritures admin-only.
- **Professor** — veu i gestiona únicament les classes que li han estat assignades via `ProfessorClass`.
- **Alumne** — accedeix amb email + contrasenya. Pot avaluar quan l'activitat és oberta.

JWT (professors) + `ProtectedLocalStorage` (Blazor). Estat global via `UserStateService` (Scoped).

## Endpoints principals de l'API

```
POST /api/auth/professor          # Login professor
POST /api/auth/student            # Login alumne

GET/POST/PUT/DELETE /api/professors                       # Gestió professors (admin)
GET/POST/DELETE    /api/professors/{id}/classes           # Assignació classes a professor (admin)
GET/POST/PUT/DELETE /api/cicles                           # Gestió cicles (admin)
GET/POST/PUT/DELETE /api/classes                          # Gestió classes
GET/POST/PUT/DELETE /api/classes/{id}/students            # Gestió alumnes
POST /api/classes/{id}/students/bulk                      # Importació CSV
POST /api/classes/{id}/students/{sid}/move                # Moure alumne

GET/POST/PUT/DELETE /api/classes/{id}/modules             # Gestió mòduls
GET/POST/DELETE    /api/modules/{id}/exclusions           # Exclusions per mòdul

GET/POST/PUT/DELETE /api/activities                       # Gestió activitats
POST /api/activities/{id}/toggle                          # Obrir/tancar
POST /api/activities/{id}/duplicate                       # Duplicar (mateixa classe)
POST /api/activities/{id}/duplicate-cross                 # Duplicar a una altra classe
GET  /api/activities/{id}/participation                   # Estat de participació
POST /api/activities/{id}/remind                          # Recordatoris per correu
GET  /api/activities/{id}/criteria                        # Criteris de l'activitat
PUT  /api/activities/{id}/criteria                        # Desar criteris personalitzats
GET/POST/DELETE /api/activities/{id}/groups               # Gestió grups
PUT  /api/activities/{id}/groups/{gid}                    # Renomenar grup
POST/DELETE /api/activities/{id}/groups/{gid}/members     # Membres de grup
GET  /api/activities/{id}/log                             # Registre d'activitat

GET  /api/evaluations/{activityId}           # Formulari d'avaluació (alumne)
POST /api/evaluations/{activityId}           # Guardar avaluació
GET  /api/student/activities                 # Dashboard alumne
GET  /api/student/results/{activityId}       # Resultats propis de l'alumne

GET  /api/results/{activityId}               # Resultats (professor)
GET  /api/results/{activityId}/chart         # Dades gràfica
GET  /api/results/{activityId}/csv           # Exportar CSV
GET  /api/results/module/{moduleId}/evolution?studentId=  # Evolució alumne per mòdul

GET  /api/criteria                           # Criteris globals
GET  /api/health                             # Estat DB + Redis (públic, sense auth)

GET  /api/admin/log-level                    # Llegir nivell de log actual (admin)
PUT  /api/admin/log-level                    # Canviar nivell de log en calent (admin)
POST /api/admin/new-year                     # Duplicar estructura classes+mòduls per a nou any acadèmic (admin)

POST /api/auth/refresh                       # Renovar JWT amb refresh token (rotació)
POST /api/auth/logout                        # Invalidar refresh token a Redis
```

## Convencions

- Tot el text de la UI en **català** (o castellà via selector d'idioma)
- Noms de fitxers i classes en anglès, text visible en català
- API: Minimal API (no controllers), ASP.NET Core 10
- Web: **Blazor Server + MudBlazor** (no Razor Pages, no MVC, no JS frameworks)
- i18n: `DictionaryLocalizer` — diccionaris estàtics `Ca`/`Es` + fitxers JSON externs a `I18N_PATH` (`/app/i18n/*.json`); fitxer per a idioma conegut fa override parcial, idioma nou s'afegeix automàticament a `supportedCultures`; fallback al català per claus absents
- Branding: `BrandingService` singleton (variables `BRAND_*`); `manifest.json` és un endpoint dinàmic (fitxer estàtic eliminat); logo via volum `./config/branding/logo.png`; `MainLayout.razor` i `App.razor` injecten `BrandingService`
- EF Core amb migracions automàtiques a l'inici (`db.Database.Migrate()`)
- Passwords hashejades amb BCrypt (work factor 12)
- Caché de resultats: Redis `IDistributedCache`, TTL 5 min, invalidació automàtica
- Temps real: Redis pub/sub → `ParticipationNotificationService` → Blazor
- Nivell de log en calent: `LogLevelHolder` singleton (api + web), `AddFilter` predicate, persistit a Redis `autoco:loglevel`
- Estat de panels a `Resultats.razor`: `sessionStorage` clau `autoco:resultats:{ActivityId}`, carregat a `OnAfterRenderAsync(firstRender)`
- `MudExpansionPanel` (MudBlazor 9.x): usar `@bind-Expanded` + `@bind-Expanded:after` — **no** `IsExpanded`/`IsExpandedChanged` (MUD0002)
- `MudFileUpload` (MudBlazor 9.x): usar `<CustomContent Context="upload">` + `OnClick="@upload.OpenFilePickerAsync"` — **no** `<ActivatorContent>` ni `HtmlTag="label" for=...` (eliminat a v9)
- `IDialogService` (MudBlazor 9.x): `ShowMessageBoxAsync` — **no** `ShowMessageBox` (renomenat a v9)
- `MudChart` (MudBlazor 9.x): usar `BarChartOptions` per a gràfics de barres, `ChartLabels` per a les etiquetes de l'eix X — **no** `ChartOptions` genèric ni `XAxisLabels`
- `MudMenu` (MudBlazor 9.x): `ActivatorContent` ja no connecta el click automàticament. Usar `@ref="_menu"` al component + `OnClick="OpenMenuAsync"` al botó activador, on `Task OpenMenuAsync(MouseEventArgs e) => _menu?.OpenMenuAsync(e, false) ?? Task.CompletedTask`
- Programació d'activitats: `OpenAt`/`CloseAt` en UTC; `ActivitySchedulerService` comprova cada minut i neteja el camp usat; `ToggleOpenAsync` neteja dates passades; UTC→local per a display, local→UTC en desar
- Pesos de criteris: `ActivityCriterion.Weight` (int, default 1); les mitjanes globals es calculen com a `sum(score*weight)/sum(weights)`; `CriteriaHelper.GetForActivityAsync` retorna `(Key, Label, Weight)` tuples
- Còpies de seguretat ZIP: `IBackupService.ExportZipAsync()` retorna `byte[]`; `BackupService` usa `System.IO.Compression.ZipArchive`; compatibilitat enrere amb `.json` antics; `ListFilesAsync` llista `*.zip` i `*.json`
- Resultats alumne: `ShowResultsToStudents` a `Activity`; quan és `true` i activitat tancada, `GET /api/student/results/{id}` retorna `StudentOwnResultDto`; visible a `/alumne/resultats/{id}`
- Renovació de curs: `POST /api/admin/new-year` duplica classes (nou `AcademicYear`) i mòduls; retorna `NewYearResult(ClassesCreated, ModulesCreated)`
- Validació de requests: DataAnnotations als DTOs (`[Required]`, `[MaxLength]`, `[EmailAddress]`, `[Range]`); helper `Validate<T>()` a `Program.cs` retorna `Results.ValidationProblem` (RFC 9457)
- JWT refresh tokens: `StoreRefreshTokenAsync` genera base64url segur, desa a Redis `autoco:refresh:{token}` (TTL 7 dies); rotació en cada refresh; `RefreshFromTokenAsync` a `ApiClient` per a `MainLayout`; `TryRefreshAsync` privat per a reintentar peticions 401
- Paginació del servidor: `PagedResult<T>` als DTOs; paràmetres `?page=1&size=N`; `ApiClient` desempaqueta a `List<T>` (size=500 per defecte = compatible amb codi existent)
- Tests (39): `ResultsServiceTests` (15), `AuthServiceTests` (8), `ActivityServiceTests` (6), `CicleServiceTests` (10); pattern `file sealed class FakePhotoService : IPhotoService` per no usar mocking library
- Rate limiting: SlidingWindow `auth` (5/min), SlidingWindow `remind` (2/min, mass-send), FixedWindow `admin` (20/min); `RejectionStatusCode = 429`
- Compressió fotos: `SixLabors.ImageSharp` a l'API; `SaveImageAsync` redimensiona a 400×400 crop centrat, JPEG Q85; valida content-type (jpeg/png/webp/gif)
- Audit log: model `AdminAuditLog` (sense FK, preservat si s'esborren altres entitats); helper `AuditAsync()` a `Program.cs`; `GET /api/admin/audit` paginat; pàgina `/admin/auditoria` amb colors per tipus d'acció
- Service Worker PWA (`service-worker.js`): **NO caches `site.css`/`app.js`/`charts.js`** — la caché HTTP (ETag) ja ho gestiona i s'invalida automàticament. Només cacha `offline.html` i imatges. `CACHE_NAME` cal actualitzar-lo ÚNICAMENT si canvien els `STATIC_ASSETS` (rarament)
- Cicles: `Cicle` agrupa classes; `ProfessorClass` (join table) assigna professors a classes; admins veuen tot; professors sense classes assignades no veuen res; `GetProfClassIdsAsync` i `HasClassAccessAsync` com a helpers d'autorització; `new-year` hereta `CicleId`; el seed crea un cicle «General» per a classes preexistents (via `EXEC()` per evitar pre-compilació SQL Server)
- Criteris per defecte configurables: taula `DefaultCriteria` a la BD; `GET /api/criteria/defaults` (professors) + `PUT /api/criteria/defaults` (admin); `GET /api/criteria` llegeix BD amb fallback a `Constants.cs`; `SeedDefaultCriteriaAsync` usa BD en comptes de `Constants.cs`; pàgina `/admin/criteris` per editar des de la UI
- Arxivat d'activitats: `Activity.IsArchived` (BIT, default 0); `POST /api/activities/{id}/archive` fa toggle; `GET /api/activities?includeArchived=true` retorna totes; per defecte s'exclouen les arxivades; archivar força tancament (`IsOpen=false`); al Dashboard: switch «Arxivades» carrega sota demanda; `OnArchived` callback a `ActivityCard` mou l'activitat entre llistes sense recarregar
- Backup v2.1: inclou `Cicles` i `ProfessorClasses`; `ImportCoreAsync` esborra `ProfessorClasses` i `Cicles` (en ordre FK), recrea cicles primer, remapeja `CicleId` de les classes, i recrea assignacions amb mapeig d'IDs; backups antics sense `Cicles` creen un cicle «General» automàticament
- Rol Gestor: `Professor.IsGestor` (BIT, default 0); JWT role `"Gestor"`; `IsAdminOrGestor(user)` per a lectures globals (classes, activitats, stats, audit, informe global); `IsAdmin(user)` per a escritures admin-only; `UserStateService`: `IsGestor`, `IsAdminOrGestor`; toggle al formulari d'edició de professor; menú de navegació mostra seccions d'admin a `IsAdminOrGestor`, però les accions destructives (Professors, Cicles, Backup, Sistema) queden sota `IsAdmin`
- Informe Global: `GET /api/results/global` agrega per cicle→classe: modules, activities, students, avg participation; `GET /api/results/global/excel` retorna xlsx (ClosedXML, color-coded); pàgina `/admin/informe-global` amb 6 KPIs + taula per cicle; accessible a Admin i Gestor
- Docker compose split: `docker-compose.yml` base (sense `db`); `docker-compose.db.yml` overlay (afegeix servei MSSQL intern + `api.depends_on.db`); `COMPOSE_FILE=docker-compose.yml:docker-compose.db.yml` al `.env` activa MSSQL intern; `DB_CONNECTION` cadena de connexió explícita (sempre obligatori); `deploy/server-update.sh` auto-migra config anterior a v2.6.23 (detecta `MSSQL_SA_PASSWORD` sense `DB_CONNECTION` i afegeix variables automàticament); `MSSQL_SA_PASSWORD` en validació condicionada a presència de `docker-compose.db.yml` al `COMPOSE_FILE`
- Connexions actives: `OnlinePresenceService` (Scoped, `IAsyncDisposable`) — heartbeat Redis 10 s / TTL 30 s; clau `autoco:online:{prefix}:{userId}:{circuitId}` (GUID 8 chars per circuit → múltiples sessions del mateix usuari visibles); `OnlineUserSnapshot` JSON inclou `IpAddress?` i `CircuitId?`; iniciat via `UserState.OnChange` (captura login en qualsevol moment del circuit, no només primer render) + fallback a `OnAfterRenderAsync` per sessions carregades de localStorage; IP capturada a `MainLayout.OnInitialized()` via `IHttpContextAccessor` (únic moment amb `HttpContext` disponible); `IServer.Keys("autoco:online:*")` per escanejar; pàgina `/admin/connexions` mostra sessions + usuaris únics + badge Multi-sessió + badge «Tu» per sessió pròpia + botó kick per tancar sessions d'altres usuaris (esborra clau online + refresh tokens); accessible a Admin i Gestor; kick disponible només a Admin
- Kick de sessió: publica `autoco:kick:{circuitId}` via Redis pub/sub; `OnlinePresenceService.Start()` subscriu al canal i dispara `OnKicked` event; `MainLayout.HandleKickedAsync` esborra localStorage i navega a `/` (forceLoad:false per evitar JS interop en circuit desconnectat); `CircuitPresenceHandler` (Scoped `CircuitHandler`) implementa `OnConnectionDownAsync` → `StopAsync()` per eliminar la clau Redis en el moment de desconnexió del navegador (evita entrades duplicades en Ctrl+F5)
- Deshabilitar compte professor: `Professor.IsDisabled` (BIT, default 0); migració `20260513000000_AddProfessorIsDisabled`; `ProfessorLoginAsync` i `RefreshAsync` retornen `null` si `IsDisabled`; `POST /api/professors/{id}/toggle-disabled` fa toggle, invalida sessions online i refresh tokens si deshabilitant; no aplicable a admins; pàgina `/admin/professors` mostra chip «Deshabilitat», botó lock/unlock per a no-admins, estil atenuat (`opacity:.65`) per a deshabilitadors
- `X-Forwarded-For` Docker: `ForwardedHeadersOptions.KnownNetworks.Clear()` + `KnownProxies.Clear()` a `web/Program.cs` perquè nginx (172.18.x.x) no és a la llista de confiança per defecte; sense això `RemoteIpAddress` retorna la IP del contenidor nginx en lloc de la IP real del client
- Accés a activitats per professors assignats via `ProfessorClass`: `WithAccess(q, professorId)` helper privat a `ActivityService` i `ModuleService` — filtra per `Module.ProfessorId == professorId OR ProfessorClasses.Any(...)` — usat per ACCIONS (editar, esborrar, grups…); la VISIBILITAT del tauler usa `Module.ProfessorId == professorId OR CreatedByProfessorId == professorId`; `HasActivityAccessAsync` helper a `Program.cs` per a endpoints inline (notes, log)
- `Activity.CreatedByProfessorId` (nullable int): qui ha creat l'activitat, pot diferir de `Module.ProfessorId` si és un professor assignat; `CreatedByProfessor` nav. property per obtenir el nom; `ToDto` usa `CreatedByProfessor ?? Module.Professor`; tots els mètodes de creació (`CreateAsync`, `DuplicateAsync`, `DuplicateCrossAsync`) assignen el camp; SQL patch idempotent amb `EXEC(...)` per al backfill (SQL Server valida tots els noms de columna del batch a temps de compilació — les columnes acabades d'afegir cal referenciar-les via `EXEC()` dins el mateix batch)
- `ActivityDto.CanEdit` (bool, server-computed, default `true`): el backend decideix si l'usuari pot editar; frontend (`ActivityCard`, `Grups`, `Resultats`) usa `Act.CanEdit` en lloc de comparar `UserId == ProfessorId`
- Retry migració MSSQL: `db.Database.Migrate()` fa fins a 10 reintents amb delay de 5 s per si SQL Server extern no és accessible en el moment d'arrencada del contenidor `api`
- Redis shutdown: `app.Run()` al `web/Program.cs` embolcallat en `try/catch (RedisConnectionException)` per evitar excepció no capturada quan Redis s'atura abans que el contenidor web
