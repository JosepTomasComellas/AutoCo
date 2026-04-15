# AutoCo — Sistema d'Avaluació entre Iguals

Aplicació web per gestionar **autoavaluació** i **coavaluació** d'alumnes en activitats de grup, pensada per a entorns educatius de cicles formatius i batxillerat.

---

## Funcionalitats principals

- **Professors** creen classes, alumnes, activitats i grups
- **Alumnes** avaluen els companys del seu grup i s'autoavaluen, amb puntuació 1–10 per 5 criteris
- **Resultats** en temps real: mitjanes per criteri, gràfiques comparatives auto vs. co-avaluació per grup
- **Exportació CSV** de resultats i llistats d'alumnes
- **Duplicació d'activitats** amb importació/exportació de configuració de grups per correu
- **Enviament automàtic** de credencials i PINs per correu electrònic

### Criteris d'avaluació (fixes)

| Clau | Descripció |
|------|------------|
| `probitat` | Probitat |
| `autonomia` | Autonomia |
| `responsabilitat` | Responsabilitat i Treball de qualitat |
| `collaboracio` | Col·laboració i treball en equip |
| `comunicacio` | Comunicació |

---

## Arquitectura

```
AutoCo/
├── api/          # API REST — ASP.NET Core 9 Minimal API
├── web/          # Frontend — Blazor Server + MudBlazor 8
├── nginx/        # Proxy invers amb SSL
└── docker-compose.yml
```

### Serveis Docker

| Servei | Imatge | Descripció |
|--------|--------|------------|
| `db` | SQL Server 2022 Express | Base de dades principal |
| `redis` | Redis 7 Alpine | Caché de resultats + backplane SignalR |
| `api` | ASP.NET Core 9 | API REST + JWT |
| `web` | ASP.NET Core 9 | Blazor Server + MudBlazor |
| `nginx` | nginx | Proxy SSL, WebSocket per Blazor |

### Model de dades

```
Professor ──< Class ──< Student
                 └──< Activity ──< Group ──< GroupMember (Student)
                                       └──< Evaluation (Evaluator→Evaluated)
                                                  └──< EvaluationScore (per criteri)
```

---

## Tecnologies

- **Backend:** C# / ASP.NET Core 9, Entity Framework Core, SQL Server 2022
- **Frontend:** Blazor Server, [MudBlazor 8](https://mudblazor.com/)
- **Autenticació:** JWT (professors) · PIN de 4 dígits (alumnes)
- **Caché:** Redis (`IDistributedCache`, TTL 5 min, invalidació en guardar)
- **Email:** SMTP (Gmail) per enviar PINs i credencials
- **Desplegament:** Docker Compose, nginx (SSL/TLS)

---

## Desplegament ràpid

### Requisits

- [Docker Desktop](https://www.docker.com/products/docker-desktop/) (Windows/macOS/Linux)

### Passos

```bash
# 1. Clona el repositori
git clone https://github.com/JosepTomasComellas/AutoCo.git
cd AutoCo

# 2. (Opcional) Genera certificats SSL autosignats per a nginx
mkdir -p nginx/ssl
openssl req -x509 -nodes -days 365 -newkey rsa:2048 \
  -keyout nginx/ssl/server.key -out nginx/ssl/server.crt \
  -subj "/CN=localhost"

# 3. Construeix i aixeca tots els serveis
docker-compose up --build

# 4. Accedeix a l'aplicació
#    https://localhost
```

### Credencials per defecte

| Rol | Usuari | Contrasenya |
|-----|--------|-------------|
| Administrador | `admin` | `Admin12345aA.` |

> ⚠️ Per a producció, canvia `JwtSettings__Secret`, `MSSQL_SA_PASSWORD` i `Admin__Password` al `docker-compose.yml`.

### Altres comandes

```bash
docker-compose up           # Aixecar sense reconstruir
docker-compose down         # Aturar
docker-compose down -v      # Aturar i esborrar totes les dades
```

---

## Rols

### Administrador
- Gestiona tots els professors
- Veu i accedeix a totes les classes i activitats
- Envia credencials per correu

### Professor
- Crea i gestiona les seves classes i alumnes
- Crea activitats, configura grups, obre/tanca avaluacions
- Consulta resultats i exporta a CSV
- Duplica activitats i importa/exporta configuració de grups

### Alumne
- Accedeix amb el seu identificador i PIN de 4 dígits
- Avalua tots els membres del seu grup (inclòs ell mateix)
- Veu les activitats disponibles del seu grup

---

## Captures de pantalla

| Dashboard professor | Resultats i gràfiques |
|--------------------|-----------------------|
| *(pendent)* | *(pendent)* |

---

## Llicència

Projecte de codi obert per a ús educatiu.
