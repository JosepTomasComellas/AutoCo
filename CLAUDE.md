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
│   │   └── ParticipationNotificationService.cs  # Redis pub/sub → Blazor
│   ├── wwwroot/
│   │   ├── css/site.css        # Estils globals (DnD, dark mode, print, informe PDF)
│   │   ├── js/                 # app.js (utilitats JS), charts.js (Chart.js interop)
│   │   ├── manifest.json       # PWA manifest (theme_color #1e293b)
│   │   ├── service-worker.js   # PWA: cache-first assets, pàgina offline
│   │   └── offline.html        # Pàgina offline en català
│   ├── Program.cs              # Configuració (Redis, SignalR, MudBlazor, i18n, DataProtection)
│   └── Dockerfile
├── shared/                     # DTOs i AppVersion compartits (api + web)
│   └── AppVersion.cs           # Versió actual de l'aplicació
├── AutoCo.Tests/               # Tests unitaris xUnit (ResultsService, EF Core InMemory)
├── nginx/                      # Proxy invers SSL (auto-signat o certificat propi)
├── deploy/                     # Scripts de desplegament
└── docker-compose.yml          # Orquestració: db + redis + api + web + nginx
```

## Desplegament

```bash
# Construir i aixecar tots els serveis
docker compose up --build

# Actualitzar servidor (recomanat)
bash /docker/AutoCo/deploy/server-update.sh
```

**URLs locals (via nginx):**
- Web: https://localhost

## Serveis Docker

| Servei | Imatge | Descripció |
|--------|--------|------------|
| `db` | SQL Server 2022 Express | Base de dades principal |
| `redis` | Redis 7 Alpine | Caché, backplane SignalR, OTP reset contrasenya |
| `api` | ASP.NET Core 10 | API REST + JWT |
| `web` | ASP.NET Core 10 | Blazor Server + MudBlazor |
| `nginx` | nginx Alpine | Proxy SSL, WebSocket Blazor |

La `api` espera que `db` i `redis` estiguin healthy abans d'arrencar.

## Configuració (variables d'entorn via .env)

Per a producció real, canviar:
- `JWT_SECRET` — secret JWT (mínim 32 caràcters)
- `MSSQL_SA_PASSWORD` — contrasenya SQL Server
- `ADMIN_EMAIL` / `ADMIN_PASSWORD` — credencials de l'administrador inicial

## Model de dades

```
Professor ──< Module ──< Activity ──< Group ──< GroupMember (Student)
              │               ├──< ActivityCriteria (criteris per activitat)
              │               ├──< Evaluation ──< EvaluationScore (per criteri)
              │               ├──< ProfessorNote (per alumne)
              │               └──< ActivityLog (registre d'accions)
Class ────────┘
  ├──< Student
  └──< ModuleExclusion
ActivityTemplate (per professor, criteris JSON)
```

- Un alumne pertany a una `Class` i autentifica amb email + contrasenya
- Una `Activity` pertany a un `Module` (que pertany a una `Class`)
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

- **Admin** — professor amb `IsAdmin=true`. Gestiona professors i veu tot.
- **Professor** — veu i gestiona les seves pròpies classes/activitats.
- **Alumne** — accedeix amb email + contrasenya. Pot avaluar quan l'activitat és oberta.

JWT (professors) + `ProtectedLocalStorage` (Blazor). Estat global via `UserStateService` (Scoped).

## Endpoints principals de l'API

```
POST /api/auth/professor          # Login professor
POST /api/auth/student            # Login alumne

GET/POST/PUT/DELETE /api/professors                       # Gestió professors (admin)
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
POST/DELETE /api/activities/{id}/groups/{gid}/members     # Membres de grup
GET  /api/activities/{id}/log                             # Registre d'activitat

GET  /api/evaluations/{activityId}           # Formulari d'avaluació (alumne)
POST /api/evaluations/{activityId}           # Guardar avaluació
GET  /api/student/activities                 # Dashboard alumne

GET  /api/results/{activityId}               # Resultats (professor)
GET  /api/results/{activityId}/chart         # Dades gràfica
GET  /api/results/{activityId}/csv           # Exportar CSV

GET  /api/criteria                           # Criteris globals
GET  /api/health                             # Estat DB + Redis (autenticat)
```

## Convencions

- Tot el text de la UI en **català** (o castellà via selector d'idioma)
- Noms de fitxers i classes en anglès, text visible en català
- API: Minimal API (no controllers), ASP.NET Core 10
- Web: **Blazor Server + MudBlazor** (no Razor Pages, no MVC, no JS frameworks)
- i18n: `DictionaryLocalizer` estàtic — evita problemes amb `ResourceManager` a Docker
- EF Core amb migracions automàtiques a l'inici (`db.Database.Migrate()`)
- Passwords hashejades amb BCrypt (work factor 12)
- Caché de resultats: Redis `IDistributedCache`, TTL 5 min, invalidació automàtica
- Temps real: Redis pub/sub → `ParticipationNotificationService` → Blazor
