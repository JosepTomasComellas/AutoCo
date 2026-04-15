# AutoCo — Sistema d'Avaluació entre Iguals

Aplicació web per gestionar **autoavaluació** i **coavaluació** d'alumnes en activitats de grup. Tot el text de la interfície és en **català**.

## Estructura del projecte

```
AutoCo/
├── api/                        # API REST (ASP.NET Core Minimal API)
│   ├── Data/
│   │   ├── AppDbContext.cs     # EF Core DbContext
│   │   ├── Constants.cs        # Criteris d'avaluació fixes
│   │   ├── SeedData.cs         # Seed inicial (admin per defecte)
│   │   └── Models/             # Entitats: Professor, Class, Student, Activity, Group, Evaluation...
│   ├── DTOs/Dtos.cs            # Tots els records de request/response
│   ├── Services/               # Lògica de negoci (Auth, Class, Activity, Evaluation, Results)
│   ├── Program.cs              # Minimal API endpoints + DI
│   └── Dockerfile
├── web/                        # Frontend (ASP.NET Core Razor Pages)
│   ├── Pages/
│   │   ├── Index.cshtml        # Pàgina d'inici (selecció de rol)
│   │   ├── Auth/               # Login professor i alumne
│   │   ├── Alumne/             # Dashboard i formulari d'avaluació
│   │   └── Professor/          # Dashboard, classes, alumnes, activitats, grups, resultats
│   ├── Services/ApiClient.cs   # Client HTTP cap a l'API (tots els endpoints)
│   ├── Pages/SessionHelper.cs  # Helpers de sessió (token JWT, rol, userId)
│   ├── Program.cs              # Configuració web (sessió, HttpClient)
│   └── Dockerfile
└── docker-compose.yml          # Orquestració: db + api + web
```

## Desplegament

```bash
# Construir i aixecar tots els serveis
docker-compose up --build

# Només aixecar (sense rebuild)
docker-compose up

# Aturar
docker-compose down

# Aturar i esborrar dades (volum db)
docker-compose down -v
```

**URLs locals:**
- Web: http://localhost:5000
- API: http://localhost:7000
- Swagger: http://localhost:7000/swagger (només Development)

## Serveis Docker

| Servei | Imatge | Port | Descripció |
|--------|--------|------|------------|
| `db` | SQL Server 2022 Express | 1433 | Base de dades |
| `api` | ASP.NET Core 8 | 7000→8080 | API REST + JWT |
| `web` | ASP.NET Core 8 | 5000→8080 | Razor Pages |

La `api` espera que `db` estigui healthy abans d'arrencar (healthcheck inclòs).

## Configuració (variables d'entorn)

Definides al `docker-compose.yml`. Per a producció real, canviar:
- `JwtSettings__Secret` — secret JWT (mínim 32 caràcters)
- `MSSQL_SA_PASSWORD` — contrasenya SQL Server
- `Admin__Password` — contrasenya de l'administrador inicial

**Admin per defecte:** `admin` / `Admin123!`

## Model de dades

```
Professor ──< Class ──< Student
                  └──< Activity ──< Group ──< GroupMember (Student)
                                        └──< Evaluation (Evaluator→Evaluated)
                                                   └──< EvaluationScore (per criteri)
```

- Un alumne pertany a una `Class`
- Una `Activity` té diversos `Group`s
- Un alumne avalua tots els membres del seu grup (inclòs ell mateix)
- `IsSelf = true` quan avaluador = avaluat (autoavaluació)

## Criteris d'avaluació

Definits a `api/Data/Constants.cs` — fixes per a totes les activitats:

| Key | Label |
|-----|-------|
| `probitat` | Probitat |
| `autonomia` | Autonomia |
| `responsabilitat` | Responsabilitat i Treball de qualitat |
| `collaboracio` | Col·laboració i treball en equip |
| `comunicacio` | Comunicació |

Puntuació: **1–10 per criteri**, interfície d'estreles.

## Rols i autenticació

- **Admin** — professor amb `IsAdmin=true`. Gestiona professors i veu tot.
- **Professor** — veu i gestiona les seves pròpies classes/activitats.
- **Student** — accedeix amb `ClassId` + `StudentId` + PIN de 4 dígits.

JWT inclou: `NameIdentifier` (userId), `role`, `classId` (alumnes).  
La sessió web guarda el token JWT i el rol a la sessió HTTP (8h de timeout).

## Endpoints principals de l'API

```
POST /api/auth/professor          # Login professor
POST /api/auth/student            # Login alumne

GET/POST/PUT/DELETE /api/professors          # Gestió professors (admin)
GET/POST/PUT/DELETE /api/classes             # Gestió classes
GET/POST/PUT/DELETE /api/classes/{id}/students
GET/POST/PUT/DELETE /api/activities
POST /api/activities/{id}/toggle             # Obrir/tancar activitat
GET/POST/DELETE /api/activities/{id}/groups
POST/DELETE /api/activities/{id}/groups/{gid}/members

GET  /api/evaluations/{activityId}           # Formulari d'avaluació (alumne)
POST /api/evaluations/{activityId}           # Guardar avaluació (alumne)
GET  /api/student/activities                 # Dashboard alumne

GET  /api/results/{activityId}               # Resultats (professor)
GET  /api/results/{activityId}/csv           # Exportar CSV (professor)
GET  /api/criteria                           # Llista de criteris
GET  /api/public/classes                     # Classes públiques (per al login alumne)
```

## Convencions

- Tot el text de la UI en **català**
- Noms de fitxers i classes en anglès, text visible en català
- API: Minimal API (no controllers)
- Web: Razor Pages (no MVC, no JavaScript frameworks)
- EF Core amb migracions automàtiques a l'inici (`db.Database.Migrate()`)
- Passwords hashejades amb BCrypt
