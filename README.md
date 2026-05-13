# AutoCo — Sistema d'Avaluació entre Iguals · v2.6.24

Aplicació web per gestionar **autoavaluació** i **coavaluació** d'alumnes en activitats de grup, pensada per a entorns educatius de cicles formatius i batxillerat.

**[→ Guia d'instal·lació i configuració](INSTALL.md)**

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
- **Programació automàtica** d'obertura i tancament: data i hora d'inici i fi opcionals; `ActivitySchedulerService` (background) comprova cada minut i obre/tanca automàticament; xip informatiu a la targeta mostra quan s'obrirà o tancarà
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
- Taula de **resultats** amb capçalera fixa, paginació (25 files), puntuació per criteri (Auto / Co) i notes globals acolorides per rang (verd/taronja/vermell); quatre blocs **col·lapsables** (filtres, taula, detall coavaluacions, registre) amb **estat persistent** entre recàrregues (`sessionStorage` per activitat)
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
- Traduccions externes via fitxers JSON a `./config/i18n/` — extensible sense recompilar
- Override parcial de català/castellà o idiomes nous afegits automàticament

**Administració**
- **Cicles formatius** — agrupació visual de classes per cicle (p.ex. DAM, ASIX, SMX); pàgina `/admin/cicles` amb CRUD complet; les classes es mostren agrupades per cicle a tota la interfície
- **Assignació professors per classe** — un professor accedeix únicament a les classes que li han estat assignades; gestió des de `/admin/professors` (xips per cicle, afegir/treure classes)
- Gestió de **professors** i permisos d'administrador (exclusiu rol Admin)
- **Menú d'administració** a la barra de navegació (icona `AdminPanelSettings`), visible únicament als administradors; dona accés a Professors, Cicles, Còpies de seguretat, Estadístiques, Sistema i Auditoria des de qualsevol pàgina
- **Còpies de seguretat**: backup ZIP complet (dades + fotos + cicles + assignacions professors; remap d'IDs automàtic en restaurar); **backup automàtic** diari/setmanal configurable; compatibilitat enrere amb còpies `.json` antigues
- **Configuració del sistema** (`/admin/sistema`): selector de nivell de log (Error/Warning/Information/Debug/Trace), s'aplica immediatament sense reinici
- **KPIs al tauler**: classes, mòduls, alumnes, activitats, obertes i grups
- **Mode fosc** i **selector de tema de color** (6 opcions) amb preferències desades al navegador

**Branding corporatiu**
- Nom de l'aplicació, organització i departament configurables via variables d'entorn (`BRAND_*`)
- Colors primari i de navegació personalitzables
- Logo personalitzat via fitxer `./config/branding/logo.png`
- Imatge de fons configurable via `./config/branding/background.png` (o `.jpg`)
- Branding aplicat a la UI, pàgines d'autenticació, informes PDF i manifest PWA

**PWA (Progressive Web App)**
- **Instal·lable** com a aplicació nativa en escriptori, Android i iOS
- **Caché d'assets estàtics** (CSS, JS) per càrrega més ràpida
- **Pàgina offline** en català quan no hi ha connexió al servidor

**Qualitat i fiabilitat**
- **Notificació en temps real** de participació via Redis pub/sub (subscripció reactiva, sense polling)
- **Validació de requests a l'API**: DataAnnotations als DTOs; resposta `ValidationProblem` (RFC 9457) automàtica als endpoints POST/PUT
- **JWT refresh tokens**: tokens rotatius desats a Redis (7 dies); renovació automàtica transparent al client
- **Paginació del costat del servidor**: `PagedResult<T>` a l'API amb paràmetres `?page=1&size=N`
- **Tests unitaris** (55 casos, xUnit + EF Core InMemory): `ResultsService` (15), `AuthService` (8), `ActivityService` (6), `CicleService` (10), `ActivityService` arxivat + `DefaultCriteria` (16)
- **Rate limiting**: SlidingWindow per auth (5 req/min) i remind (2 req/min); FixedWindow per admin (20 req/min); 429 automàtic
- **Compressió de fotos automàtica**: redimensionament a 400×400px + JPEG 85% (SixLabors.ImageSharp) en pujar; validació de content-type
- **Audit log d'accions admin**: registre persistent de les accions sensibles; pàgina `/admin/auditoria` filtrable i paginada

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

Cada activitat pot sobreescriure aquests criteris amb la seva pròpia llista personalitzada. Els criteris per defecte són editables des de `/admin/criteris`.

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
│   ├── Services/    # ApiClient, UserStateService, BrandingService
│   ├── wwwroot/
│   │   ├── css/site.css   # Estils globals, DnD, dark mode, print, informe PDF
│   │   └── js/            # app.js (utilitats), charts.js (Chart.js interop)
│   └── Dockerfile
├── shared/                 # DTOs + AppVersion compartits entre api i web
├── nginx/                  # Proxy invers amb SSL automàtic
├── deploy/                 # Scripts de desplegament
├── AutoCo.Tests/           # Tests unitaris xUnit
├── docker-compose.yml      # Base: redis + api + web + nginx
└── docker-compose.db.yml   # Overlay: MSSQL intern (opcional)
```

### Model de dades

```
Cicle ────< Class ──────< Module ──< Activity ──< Group ──< GroupMember (Student)
                │           │              ├──< ActivityCriteria
                │           │              ├──< Evaluation ──< EvaluationScore
                │           │              ├──< ProfessorNote (per alumne)
                │           │              └──< ActivityLog (registre d'accions)
                │           └──< Professor (via ProfessorClass)
                └──< Student
                └──< ModuleExclusion
ActivityTemplate (per professor, criteris JSON)
DefaultCriteria  (criteris globals configurables)
AdminAuditLog    (registre d'accions sensibles, sense FK)
```

---

## Tecnologies

- **Backend:** C# / ASP.NET Core 10 · Entity Framework Core 10 · SQL Server 2022 / PostgreSQL
- **Frontend:** Blazor Server · [MudBlazor 9.4](https://mudblazor.com/) · Chart.js 4
- **QR:** [Net.Codecrete.QrCodeGenerator](https://github.com/manuelbl/QrCodeGenerator) (SVG pur, sense deps)
- **Autenticació:** JWT (professors) · email + contrasenya (alumnes) · `ProtectedLocalStorage`
- **Caché:** Redis (`IDistributedCache`, TTL 5 min, invalidació automàtica)
- **Temps real:** Redis pub/sub → `ParticipationNotificationService` → Blazor (reemplaça polling)
- **Seguretat:** BCrypt (work factor 12) · JWT secret mínim 32 caràcters · Rate limiting
- **Email:** MailKit + SMTP configurable (credencials, recordatoris, notificació de compleció)
- **PWA:** `manifest.json` dinàmic + Service Worker (cache-first assets, pàgina offline)
- **i18n:** `DictionaryLocalizer` + fitxers JSON externs (català per defecte, castellà)
- **Tests:** xUnit + EF Core InMemory (55 casos unitaris)
- **Desplegament:** Docker Compose · nginx (SSL/TLS auto-signat o certificat propi)

---

## Rols

| Rol | Accés |
|-----|-------|
| **Admin** | Tot. Gestiona professors, cicles, veu totes les classes i activitats, còpies de seguretat |
| **Gestor** | Veu totes les classes, activitats, estadístiques i l'Informe Global. Modifica únicament les classes que té assignades |
| **Professor** | Les seves pròpies classes, mòduls, activitats i resultats |
| **Alumne** | Les activitats del seu grup. Pot avaluar mentre l'activitat és oberta |

---

## Changelog

### v2.6.24
- **Connexions actives** (`/admin/connexions`): pantalla d'administració que mostra en temps real els usuaris connectats (heartbeat Redis 10 s / TTL 20 s), agrupats per rol (Admin/Gestor/Professor) i, per als alumnes, per classe; disponible per a Admin i Gestor

### v2.6.23
- **MSSQL extern opcional**: `docker-compose.yml` base sense servei `db`; nou `docker-compose.db.yml` afegeix MSSQL intern; `COMPOSE_FILE` al `.env` activa el mode intern; `DB_CONNECTION` cadena de connexió explícita; `deploy/server-update.sh` auto-migra configuració anterior a v2.6.23

### v2.6.22
- **Dual-engine MSSQL + PostgreSQL**: variable `DB_PROVIDER=SqlServer` (per defecte) o `DB_PROVIDER=PostgreSQL`; MSSQL continua usant `Migrate()` + patches idempotents; PostgreSQL usa `EnsureCreated()` (crea tot l'esquema des del model EF Core); `Npgsql.EntityFrameworkCore.PostgreSQL 10.0.1` afegit

### v2.6.21
- **Backup v2.2 — correccions de completesa**: `Professor.IsGestor` ara s'exporta i es restaura correctament (bug crític: professors Gestors perdien el rol); `Activity.IsArchived` inclòs al backup; `Group.OrderIndex` preservat; `DefaultCriteria` exportada i restaurada; compatibilitat enrere amb backups `"2.1"` i `"2.0"`

### v2.6.20
- **Control d'accés a Resultats i Grups**: `_canEdit = IsAdmin || UserId == Activity.ProfessorId`; els controls d'edició s'amaguen als usuaris sense propietat — gestors i professors externs conserven accés de lectura complet
- **Fix Docker build**: `Microsoft.EntityFrameworkCore.*` fixat a `10.0.7`; evita `NU1103` quan la imatge `sdk:10.0` s'actualitza

### v2.6.19
- **Control d'accés a accions d'activitat**: `ActivityCard` calcula `_canEdit = IsAdmin || UserId == ProfessorId`; botons d'edició s'amaguen si l'usuari no és propietari ni admin

### v2.6.18
- **Fix PDF Informe Global**: vista d'impressió `print-only` amb HTML net; CSS `no-print`/`print-only`; KPIs amb colors per participació

### v2.6.16
- **Rol Gestor** — nou rol intermedi: veu totes les classes, activitats, estadístiques i l'Informe Global; modifica únicament les classes assignades; `IsGestor`, JWT role `"Gestor"`
- **Informe Global** (`/admin/informe-global`) — accessible a Admin i Gestor; KPIs de resum; taula per cicle; exportació Excel i impressió/PDF

### v2.6.15
- **`<PageTitle>` dinàmic** — totes les 20 pàgines reflecteixen `BRAND_APP_NAME` configurable

### v2.6.14
- **Branding complet** — imatge de fons; pàgines d'autenticació; informes PDF; gràfics; CSS custom properties (`:root { --brand-primary, --brand-nav, --brand-bg-image… }`); `BrandingService` ampliada

### v2.6.13
- **Selector d'idioma dinàmic** — llegeix `SupportedUICultures` i mostra tots els idiomes disponibles; endpoint `/i18n/reference.json`

### v2.6.12
- **Branding corporatiu** — `BrandingService` llegeix variables `BRAND_*`; logo via volum; `manifest.json` dinàmic

### v2.6.11
- **Fix crash loop i18n** — `DictionaryLocalizer` valida els noms de fitxer amb `CultureInfo.GetCultureInfo` abans de carregar-los

### v2.6.10
- **Traduccions externes** — fitxers `*.json` a `/app/i18n`; override parcial o idioma nou; fallback al català

### v2.6.9
- **Idioma per defecte configurable** — variable `DEFAULT_LANGUAGE`

### v2.6.8
- **Fix pujada de fotos** — `StreamContent` amb `Content-Type` correcte; null guard als handlers de `MudFileUpload`

### v2.6.7
- **Rendiment EF Core** — `ThenInclude Student`, `AsNoTracking`, `try/finally` a `EvolucioAlumne`

### v2.6.6
- **Auditoria UI** — `@key` als foreach; `StateHasChanged` reduït; comprovació d'accés a l'endpoint d'evolució

### v2.6.5
- **Fix menús navbar** — `MudMenu` v9: `@ref` + `OpenMenuAsync` explícit

### v2.6.4
- **Tests** — 55 casos en total: cobertura de `ArchiveAsync`, `DefaultCriteria`, `GetAllAsync(includeArchived)`

### v2.6.3
- **MudBlazor v9.4.0** — migració completa des de v8.6.0

### v2.6.2
- **Criteris per defecte configurables** — pàgina `/admin/criteris`; persistits a `DefaultCriteria` a la BD

### v2.6.1
- **Arxivat d'activitats** — toggle «Arxivar»; switch «Arxivades» al dashboard; `POST /api/activities/{id}/archive`

### v2.6.0
- **Cicles formatius** — nova capa d'organització; pàgina `/admin/cicles`; classes agrupades per cicle
- **Assignació professors per classe** — `ProfessorClass` join table; accés per classe amb xips a `/admin/professors`
- **Backup v2.1** — inclou cicles i assignacions de professors

### v2.5.x
- **v2.5.23**: Fixes visuals UX + service worker
- **v2.5.22**: Backup ZIP amb fotos + menú admin al navbar
- **v2.5.21**: Menú admin al navbar; dashboard simplificat
- **v2.5.20**: Notificacions in-app; neteja logins periòdica
- **v2.5.19**: Pesos criteris, resultats alumne, evolució, renovació curs, ZIP
- **v2.5.16**: Programació obertura/tancament automàtic d'activitats
- **v2.5.13**: DataProtection persistent; EF Core SplitQuery global
- **v2.5.12**: Nivell de log configurable des de la UI
- **v2.5.11**: Nivell de log configurable via `config/logging.json`
- **v2.5.10**: Logo incrustat als correus; healthcheck API; arrencada seqüencial garantida
- **v2.5.9**: Correus HTML amb estètica de targeta
- **v2.5.8**: Validació de `.env` a l'script de desplegament
- **v2.5.7**: Fix zona horària (TZ=Europe/Madrid, DateTime Kind=Utc)
- **v2.5.6**: Tauler de resultats col·lapsable
- **v2.5.5**: Foto d'alumne editable des de la fitxa d'edició
- **v2.5.4**: Foto de professor editable des de l'admin
- **v2.5.3**: Importació massiva actualitza alumnes existents
- **v2.5.2**: Fix `PendingModelChangesWarning` EF Core 10
- **v2.5.1**: Fotos al formulari d'avaluació, resultats i informes PDF
- **v2.5.0**: Fotos d'alumnes (upload + ZIP massiu) i professors; camp DNI

### v2.4.x i anteriors
- **v2.4.0**: Informe PDF complet per activitat; backup automàtic; backup complet
- **v2.3.0**: Contrasenya xifrada d'alumnes; canvi de contrasenya propi; botó convidar
- **v2.2.x**: Estadístiques d'ús admin; registre de logins
- **v2.1.x**: Edició inline nom grup; valoració parcial; filtre per alumne; correccions
- **v2.0.0**: Migració a .NET 10
- **v1.6.x**: i18n completa ca/es; DictionaryLocalizer; correccions de seguretat IDOR; PWA; temps real
- **v1.5.0**: Perfil professor; restabliment contrasenya; exportació Excel; QR; criteris editables
- **v1.4.0**: Informe PDF individual; gràfiques; duplicació creuada; plantilles; registre d'activitat
- **v1.3.0**: Base — classes, alumnes, mòduls, activitats, grups, autoavaluació, coavaluació, resultats

---

## Llicència

Projecte de codi obert per a ús educatiu — Salesians de Sarrià, Departament d'Informàtica.
