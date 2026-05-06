using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Threading.RateLimiting;
using AutoCo.Api.Data;
using AutoCo.Shared.DTOs;
using AutoCo.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using AutoCo.Api.Data.Models;

var builder = WebApplication.CreateBuilder(args);

// logging.json muntat com a volum Docker — recarrega automàticament sense reinici.
builder.Configuration.AddJsonFile("logging.json", optional: true, reloadOnChange: true);

// LogLevelHolder: singleton capturat pel filtre; el nivell es pot canviar en calent des de la UI.
var logHolder = new AutoCo.Api.Services.LogLevelHolder();
builder.Services.AddSingleton(logHolder);
builder.Logging.AddFilter((category, level) => level >= logHolder.Level);

// ── Base de dades ─────────────────────────────────────────────────────────────
builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"),
           sql => sql.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery))
       .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning)));

// ── JWT ───────────────────────────────────────────────────────────────────────
var jwtSecret = builder.Configuration["JwtSettings:Secret"]
    ?? throw new InvalidOperationException("JwtSettings:Secret no configurat.");
if (jwtSecret.Length < 32)
    throw new InvalidOperationException("JwtSettings:Secret ha de tenir almenys 32 caràcters.");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opt =>
    {
        opt.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey         = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            ValidateIssuer           = false,
            ValidateAudience         = false
        };
    });

builder.Services.AddAuthorization();

// ── Serveis ───────────────────────────────────────────────────────────────────
builder.Services.AddSingleton<IPasswordCryptoService>(
    new PasswordCryptoService(jwtSecret));
builder.Services.AddScoped<IAuthService,       AuthService>();
builder.Services.AddScoped<IProfessorService,  ProfessorService>();
builder.Services.AddScoped<IClassService,      ClassService>();
builder.Services.AddScoped<IModuleService,     ModuleService>();
builder.Services.AddScoped<IActivityService,   ActivityService>();
builder.Services.AddScoped<IEvaluationService, EvaluationService>();
builder.Services.AddScoped<IResultsService,    ResultsService>();
builder.Services.AddScoped<IEmailService,      EmailService>();
builder.Services.AddScoped<IBackupService,     BackupService>();
builder.Services.AddScoped<IPhotoService,      PhotoService>();
builder.Services.AddHostedService<BackupHostedService>();
builder.Services.AddHostedService<ActivitySchedulerService>();

// ── Redis (caché de resultats) ─────────────────────────────────────────────────
var redisConn = builder.Configuration["Redis:ConnectionString"] ?? "localhost:6379";
builder.Services.AddSingleton<StackExchange.Redis.IConnectionMultiplexer>(
    StackExchange.Redis.ConnectionMultiplexer.Connect(redisConn));
builder.Services.AddStackExchangeRedisCache(opt =>
{
    opt.Configuration = redisConn;
    opt.InstanceName  = "autoco:";
});

// ── Rate limiting (protecció contra força bruta) ───────────────────────────
builder.Services.AddRateLimiter(opt =>
{
    opt.AddSlidingWindowLimiter("auth", o =>
    {
        o.PermitLimit         = 5;
        o.Window              = TimeSpan.FromMinutes(1);
        o.SegmentsPerWindow   = 3;
        o.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        o.QueueLimit          = 0;
    });
    opt.AddSlidingWindowLimiter("remind", o =>
    {
        o.PermitLimit         = 2;
        o.Window              = TimeSpan.FromMinutes(1);
        o.SegmentsPerWindow   = 2;
        o.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        o.QueueLimit          = 0;
    });
    opt.AddFixedWindowLimiter("admin", o =>
    {
        o.PermitLimit         = 20;
        o.Window              = TimeSpan.FromMinutes(1);
        o.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        o.QueueLimit          = 0;
    });
    opt.RejectionStatusCode = 429;
});

builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();

// ── DataProtection (claus persistents) ───────────────────────────────────────
// L'API usa JWT (no cookies), però ASP.NET Core inicialitza DataProtection de
// totes formes. Persistim les claus al volum Docker per evitar el warning.
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new System.IO.DirectoryInfo("/app/dp-keys"))
    .SetApplicationName("AutoCo.Api");

var app = builder.Build();

// ── Migració i seed automàtics ────────────────────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var db     = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
    db.Database.Migrate();

    // ── Taules afegides al model sense migració formal (crea si no existeix) ──
    // Idempotent: segur d'executar a cada arrencada. Necessari quan el projecte
    // no té fitxers de migració i la BD va ser creada amb EnsureCreated o bé
    // amb una versió anterior del model.
    await db.Database.ExecuteSqlRawAsync("""
        IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'ActivityCriteria')
        BEGIN
            CREATE TABLE [ActivityCriteria] (
                [Id]         INT          NOT NULL IDENTITY(1,1),
                [ActivityId] INT          NOT NULL,
                [Key]        NVARCHAR(50) NOT NULL,
                [Label]      NVARCHAR(200) NOT NULL,
                [OrderIndex] INT          NOT NULL,
                CONSTRAINT [PK_ActivityCriteria] PRIMARY KEY ([Id]),
                CONSTRAINT [FK_ActivityCriteria_Activities_ActivityId]
                    FOREIGN KEY ([ActivityId]) REFERENCES [Activities]([Id]) ON DELETE CASCADE
            );
            CREATE UNIQUE INDEX [IX_ActivityCriteria_ActivityId_Key]
                ON [ActivityCriteria] ([ActivityId], [Key]);
        END

        IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'ProfessorNotes')
        BEGIN
            CREATE TABLE [ProfessorNotes] (
                [Id]         INT           NOT NULL IDENTITY(1,1),
                [ActivityId] INT           NOT NULL,
                [StudentId]  INT           NOT NULL,
                [Note]       NVARCHAR(MAX) NOT NULL,
                [UpdatedAt]  DATETIME2     NOT NULL DEFAULT GETUTCDATE(),
                CONSTRAINT [PK_ProfessorNotes] PRIMARY KEY ([Id]),
                CONSTRAINT [FK_ProfessorNotes_Activities_ActivityId]
                    FOREIGN KEY ([ActivityId]) REFERENCES [Activities]([Id]) ON DELETE CASCADE,
                CONSTRAINT [FK_ProfessorNotes_Students_StudentId]
                    FOREIGN KEY ([StudentId]) REFERENCES [Students]([Id])
            );
            CREATE UNIQUE INDEX [IX_ProfessorNotes_ActivityId_StudentId]
                ON [ProfessorNotes] ([ActivityId], [StudentId]);
        END

        IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'ActivityTemplates')
        BEGIN
            CREATE TABLE [ActivityTemplates] (
                [Id]          INT           NOT NULL IDENTITY(1,1),
                [ProfessorId] INT           NOT NULL,
                [Name]        NVARCHAR(300) NOT NULL,
                [Description] NVARCHAR(MAX) NULL,
                [CriteriaJson] NVARCHAR(MAX) NOT NULL DEFAULT N'[]',
                [CreatedAt]   DATETIME2     NOT NULL DEFAULT GETUTCDATE(),
                CONSTRAINT [PK_ActivityTemplates] PRIMARY KEY ([Id])
            );
            CREATE INDEX [IX_ActivityTemplates_ProfessorId]
                ON [ActivityTemplates] ([ProfessorId]);
        END

        IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'ActivityLogs')
        BEGIN
            CREATE TABLE [ActivityLogs] (
                [Id]           INT           NOT NULL IDENTITY(1,1),
                [ActivityId]   INT           NOT NULL,
                [ActivityName] NVARCHAR(300) NOT NULL,
                [ActorName]    NVARCHAR(300) NULL,
                [Action]       NVARCHAR(50)  NOT NULL,
                [Details]      NVARCHAR(MAX) NULL,
                [CreatedAt]    DATETIME2     NOT NULL DEFAULT GETUTCDATE(),
                CONSTRAINT [PK_ActivityLogs] PRIMARY KEY ([Id])
            );
            CREATE INDEX [IX_ActivityLogs_ActivityId]
                ON [ActivityLogs] ([ActivityId]);
        END

        IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'ProfessorLogins')
        BEGIN
            CREATE TABLE [ProfessorLogins] (
                [Id]          INT       NOT NULL IDENTITY(1,1),
                [ProfessorId] INT       NOT NULL,
                [CreatedAt]   DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                CONSTRAINT [PK_ProfessorLogins] PRIMARY KEY ([Id]),
                CONSTRAINT [FK_ProfessorLogins_Professors_ProfessorId]
                    FOREIGN KEY ([ProfessorId]) REFERENCES [Professors]([Id]) ON DELETE CASCADE
            );
            CREATE INDEX [IX_ProfessorLogins_ProfessorId]
                ON [ProfessorLogins] ([ProfessorId]);
            CREATE INDEX [IX_ProfessorLogins_CreatedAt]
                ON [ProfessorLogins] ([CreatedAt]);
        END

        IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'AdminAuditLogs')
        BEGIN
            CREATE TABLE [AdminAuditLogs] (
                [Id]        INT           NOT NULL IDENTITY(1,1),
                [Action]    NVARCHAR(100) NOT NULL,
                [ActorId]   INT           NULL,
                [ActorName] NVARCHAR(300) NULL,
                [Details]   NVARCHAR(MAX) NULL,
                [CreatedAt] DATETIME2     NOT NULL DEFAULT GETUTCDATE(),
                CONSTRAINT [PK_AdminAuditLogs] PRIMARY KEY ([Id])
            );
            CREATE INDEX [IX_AdminAuditLogs_CreatedAt]
                ON [AdminAuditLogs] ([CreatedAt]);
        END
        """);

    // SQL Server Express activa AUTO_CLOSE per defecte: desactivar-lo evita
    // que la BD s'aturi entre peticions i torna a arrencar amb cada connexió nova.
    try
    {
        var dbName = db.Database.GetDbConnection().Database;
#pragma warning disable EF1002 // dbName prové de la cadena de connexió, no de l'usuari
        await db.Database.ExecuteSqlRawAsync($"ALTER DATABASE [{dbName}] SET AUTO_CLOSE OFF");
#pragma warning restore EF1002
    }
    catch { /* ignora si no té permisos o si ja està desactivat */ }
    await SeedData.InitializeAsync(db, config);
}

// Restaurar el nivell de log guardat a Redis (persistent entre reinicis)
{
    var cache = app.Services.GetRequiredService<IDistributedCache>();
    var saved = await cache.GetStringAsync("autoco:loglevel");
    if (saved is not null && Enum.TryParse<LogLevel>(saved, out var savedLevel))
        logHolder.Level = savedLevel;
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi(); // disponible a /openapi/v1.json
}

app.UseExceptionHandler();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

// ── Helpers locals ────────────────────────────────────────────────────────────
static int GetUserId(ClaimsPrincipal user) =>
    int.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;

static bool IsAdmin(ClaimsPrincipal user) =>
    user.IsInRole("Admin");

static bool IsProfessor(ClaimsPrincipal user) =>
    user.IsInRole("Professor") || user.IsInRole("Admin");

// Valida DataAnnotations d'un request DTO; retorna 422 si hi ha errors.
static IResult? Validate<T>(T req) where T : class
{
    var ctx     = new ValidationContext(req);
    var results = new List<ValidationResult>();
    return Validator.TryValidateObject(req, ctx, results, true)
        ? null
        : Results.ValidationProblem(results
            .GroupBy(r => r.MemberNames.FirstOrDefault() ?? "")
            .ToDictionary(g => g.Key, g => g.Select(r => r.ErrorMessage!).ToArray()));
}

// Registra una acció al log d'auditoria (fire-and-forget, no llança).
static async Task AuditAsync(AppDbContext db, string action, int? actorId, string? actorName, string? details = null)
{
    try
    {
        db.AdminAuditLogs.Add(new AutoCo.Api.Data.Models.AdminAuditLog
        {
            Action    = action,
            ActorId   = actorId,
            ActorName = actorName,
            Details   = details,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
    }
    catch { /* ignora errors de log per no trencar el flux principal */ }
}

// ════════════════════════════════════════════════════════════════════════════
// AUTENTICACIÓ
// ════════════════════════════════════════════════════════════════════════════

app.MapPost("/api/auth/professor", async (ProfessorLoginRequest req, IAuthService svc) =>
{
    var result = await svc.ProfessorLoginAsync(req);
    return result is null ? Results.Unauthorized() : Results.Ok(result);
}).RequireRateLimiting("auth");

app.MapPost("/api/auth/student", async (StudentLoginRequest req, IAuthService svc) =>
{
    var result = await svc.StudentLoginAsync(req);
    return result is null ? Results.Unauthorized() : Results.Ok(result);
}).RequireRateLimiting("auth");

app.MapPost("/api/auth/refresh", async (RefreshRequest req, IAuthService svc) =>
{
    if (string.IsNullOrWhiteSpace(req.Token)) return Results.Unauthorized();
    var result = await svc.RefreshAsync(req.Token);
    return result is null ? Results.Unauthorized() : Results.Ok(result);
}).RequireRateLimiting("auth");

app.MapPost("/api/auth/logout", async (LogoutRequest req, IAuthService svc) =>
{
    if (!string.IsNullOrWhiteSpace(req.Token))
        await svc.LogoutAsync(req.Token);
    return Results.NoContent();
});

app.MapPost("/api/auth/request-reset", async (
    PasswordResetRequestDto req, AppDbContext db, IEmailService email,
    Microsoft.Extensions.Caching.Distributed.IDistributedCache cache) =>
{
    // Sempre retorna Ok per no revelar si el correu existeix
    var prof = await db.Professors.FirstOrDefaultAsync(
        p => p.Email == req.Email.Trim().ToLower());
    if (prof is not null && email.IsEnabled)
    {
        var code = System.Security.Cryptography.RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
        await cache.SetStringAsync($"autoco:reset:{req.Email.Trim().ToLower()}", code,
            new Microsoft.Extensions.Caching.Distributed.DistributedCacheEntryOptions
            { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(15) });
        await email.SendPasswordResetAsync(prof.Email, prof.NomComplet, code);
    }
    return Results.Ok(new { message = "Si el correu existeix, rebràs el codi en breu." });
}).RequireRateLimiting("auth");

app.MapPost("/api/auth/confirm-reset", async (
    PasswordResetConfirmDto req, AppDbContext db,
    Microsoft.Extensions.Caching.Distributed.IDistributedCache cache) =>
{
    var email = req.Email.Trim().ToLower();
    var stored = await cache.GetStringAsync($"autoco:reset:{email}");
    if (stored is null || stored != req.Code.Trim())
        return Results.BadRequest(new { error = "Codi incorrecte o expirat." });
    if (req.NewPassword.Length < 8)
        return Results.BadRequest(new { error = "La contrasenya ha de tenir almenys 8 caràcters." });

    var prof = await db.Professors.FirstOrDefaultAsync(p => p.Email == email);
    if (prof is null) return Results.NotFound();

    prof.PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.NewPassword);
    await db.SaveChangesAsync();
    await cache.RemoveAsync($"autoco:reset:{email}");
    return Results.Ok(new { message = "Contrasenya actualitzada correctament." });
}).RequireRateLimiting("auth");

// ════════════════════════════════════════════════════════════════════════════
// PROFESSORS  (Admin only per a escriptura)
// ════════════════════════════════════════════════════════════════════════════

app.MapGet("/api/professors", async (IProfessorService svc) =>
    Results.Ok(await svc.GetAllAsync()))
    .RequireAuthorization();

app.MapGet("/api/professors/{id:int}", async (int id, IProfessorService svc) =>
{
    var p = await svc.GetByIdAsync(id);
    return p is null ? Results.NotFound() : Results.Ok(p);
}).RequireAuthorization();

app.MapPost("/api/professors", async (CreateProfessorRequest req, IProfessorService svc,
    AppDbContext db, ClaimsPrincipal user) =>
{
    if (!IsAdmin(user)) return Results.Forbid();
    if (Validate(req) is { } err) return err;
    var p = await svc.CreateAsync(req);
    await AuditAsync(db, "professor.created", GetUserId(user),
        user.FindFirstValue(ClaimTypes.Name),
        $"Id:{p.Id} Email:{p.Email}");
    return Results.Created($"/api/professors/{p.Id}", p);
}).RequireAuthorization();

app.MapPut("/api/professors/{id:int}", async (int id, UpdateProfessorRequest req,
    IProfessorService svc, ClaimsPrincipal user) =>
{
    if (!IsAdmin(user)) return Results.Forbid();
    if (Validate(req) is { } err) return err;
    var p = await svc.UpdateAsync(id, req);
    return p is null ? Results.NotFound() : Results.Ok(p);
}).RequireAuthorization();

app.MapDelete("/api/professors/{id:int}", async (int id, IProfessorService svc,
    AppDbContext db, ClaimsPrincipal user) =>
{
    if (!IsAdmin(user)) return Results.Forbid();
    var target = await db.Professors.FindAsync(id);
    try
    {
        var ok = await svc.DeleteAsync(id);
        if (ok) await AuditAsync(db, "professor.deleted", GetUserId(user),
            user.FindFirstValue(ClaimTypes.Name),
            $"Id:{id} Email:{target?.Email}");
        return ok ? Results.NoContent() : Results.NotFound();
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
}).RequireAuthorization();

app.MapGet("/api/professors/me", async (AppDbContext db, ClaimsPrincipal user) =>
{
    if (!IsProfessor(user)) return Results.Forbid();
    var prof = await db.Professors.FindAsync(GetUserId(user));
    if (prof is null) return Results.NotFound();
    return Results.Ok(new ProfessorDto(prof.Id, prof.Email, prof.Nom, prof.Cognoms,
        prof.NomComplet, prof.IsAdmin, prof.CreatedAt));
}).RequireAuthorization();

app.MapPut("/api/professors/me", async (UpdateOwnProfileRequest req, AppDbContext db, ClaimsPrincipal user) =>
{
    if (!IsProfessor(user)) return Results.Forbid();
    var prof = await db.Professors.FindAsync(GetUserId(user));
    if (prof is null) return Results.NotFound();

    // Verifica contrasenya actual si es vol canviar la contrasenya
    if (!string.IsNullOrWhiteSpace(req.NewPassword))
    {
        if (string.IsNullOrWhiteSpace(req.CurrentPassword) ||
            !BCrypt.Net.BCrypt.Verify(req.CurrentPassword, prof.PasswordHash))
            return Results.BadRequest(new { error = "La contrasenya actual és incorrecta." });
        if (req.NewPassword.Length < 8)
            return Results.BadRequest(new { error = "La nova contrasenya ha de tenir almenys 8 caràcters." });
        prof.PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.NewPassword);
    }

    prof.Nom     = req.Nom.Trim();
    prof.Cognoms = req.Cognoms.Trim();
    await db.SaveChangesAsync();
    return Results.Ok(new ProfessorDto(prof.Id, prof.Email, prof.Nom, prof.Cognoms,
        prof.NomComplet, prof.IsAdmin, prof.CreatedAt));
}).RequireAuthorization();

app.MapPost("/api/professors/{professorId:int}/send-credentials", async (
    int professorId, IProfessorService svc, ClaimsPrincipal user) =>
{
    if (!IsAdmin(user)) return Results.Forbid();
    var result = await svc.SendCredentialsAsync(professorId);
    return Results.Ok(result);
}).RequireAuthorization().RequireRateLimiting("remind");

app.MapPost("/api/professors/send-all-credentials", async (
    IProfessorService svc, ClaimsPrincipal user) =>
{
    if (!IsAdmin(user)) return Results.Forbid();
    var result = await svc.SendAllCredentialsAsync();
    return Results.Ok(result);
}).RequireAuthorization().RequireRateLimiting("remind");

// ════════════════════════════════════════════════════════════════════════════
// CLASSES  (lectura per a tots els professors; escriptura admin only)
// ════════════════════════════════════════════════════════════════════════════

app.MapGet("/api/classes", async (IClassService svc, ClaimsPrincipal user) =>
{
    if (!IsProfessor(user)) return Results.Forbid();
    return Results.Ok(await svc.GetAllAsync());
}).RequireAuthorization();

app.MapGet("/api/classes/{id:int}", async (int id, IClassService svc, ClaimsPrincipal user) =>
{
    if (!IsProfessor(user)) return Results.Forbid();
    var c = await svc.GetByIdAsync(id);
    return c is null ? Results.NotFound() : Results.Ok(c);
}).RequireAuthorization();

app.MapPost("/api/classes", async (CreateClassRequest req, IClassService svc,
    ClaimsPrincipal user) =>
{
    if (!IsAdmin(user)) return Results.Forbid();
    if (Validate(req) is { } err) return err;
    var c = await svc.CreateAsync(req);
    return Results.Created($"/api/classes/{c.Id}", c);
}).RequireAuthorization();

app.MapPut("/api/classes/{id:int}", async (int id, UpdateClassRequest req,
    IClassService svc, ClaimsPrincipal user) =>
{
    if (!IsAdmin(user)) return Results.Forbid();
    if (Validate(req) is { } err) return err;
    var c = await svc.UpdateAsync(id, req);
    return c is null ? Results.NotFound() : Results.Ok(c);
}).RequireAuthorization();

app.MapDelete("/api/classes/{id:int}", async (int id, IClassService svc,
    AppDbContext db, ClaimsPrincipal user) =>
{
    if (!IsAdmin(user)) return Results.Forbid();
    var target = await db.Classes.FindAsync(id);
    var ok = await svc.DeleteAsync(id);
    if (ok) await AuditAsync(db, "class.deleted", GetUserId(user),
        user.FindFirstValue(ClaimTypes.Name),
        $"Id:{id} Name:{target?.Name}");
    return ok ? Results.NoContent() : Results.NotFound();
}).RequireAuthorization();

// ── Alumnes dins d'una classe ─────────────────────────────────────────────────

app.MapGet("/api/classes/{classId:int}/students", async (
    int classId, IClassService svc, ClaimsPrincipal user,
    int page = 1, int size = 500) =>
{
    if (!IsProfessor(user)) return Results.Forbid();
    var (items, total) = await svc.GetStudentsPagedAsync(classId, page, size);
    return Results.Ok(new PagedResult<StudentDto>(items, total, page, size));
}).RequireAuthorization();

app.MapPost("/api/classes/{classId:int}/students", async (int classId,
    CreateStudentRequest req, IClassService svc, ClaimsPrincipal user) =>
{
    if (!IsAdmin(user)) return Results.Forbid();
    if (Validate(req) is { } err) return err;
    var s = await svc.AddStudentAsync(classId, req);
    return Results.Created($"/api/classes/{classId}/students/{s.Id}", s);
}).RequireAuthorization();

app.MapPut("/api/classes/{classId:int}/students/{studentId:int}", async (
    int classId, int studentId, UpdateStudentRequest req,
    IClassService svc, ClaimsPrincipal user) =>
{
    if (!IsAdmin(user)) return Results.Forbid();
    if (Validate(req) is { } err) return err;
    var s = await svc.UpdateStudentAsync(classId, studentId, req);
    return s is null ? Results.NotFound() : Results.Ok(s);
}).RequireAuthorization();

app.MapDelete("/api/classes/{classId:int}/students/{studentId:int}", async (
    int classId, int studentId, IClassService svc, ClaimsPrincipal user) =>
{
    if (!IsAdmin(user)) return Results.Forbid();
    var ok = await svc.DeleteStudentAsync(classId, studentId);
    return ok ? Results.NoContent() : Results.NotFound();
}).RequireAuthorization();

app.MapPost("/api/classes/{classId:int}/students/bulk", async (
    int classId, BulkCreateStudentsRequest req, IClassService svc, ClaimsPrincipal user) =>
{
    if (!IsAdmin(user)) return Results.Forbid();
    var result = await svc.BulkAddStudentsAsync(classId, req);
    return Results.Ok(result);
}).RequireAuthorization();

app.MapPost("/api/classes/{classId:int}/students/{studentId:int}/reset-password", async (
    int classId, int studentId, IClassService svc, ClaimsPrincipal user) =>
{
    if (!IsAdmin(user)) return Results.Forbid();
    var result = await svc.ResetPasswordAsync(classId, studentId);
    return result is null ? Results.NotFound() : Results.Ok(result);
}).RequireAuthorization();

app.MapPost("/api/classes/{classId:int}/students/{studentId:int}/send-password", async (
    int classId, int studentId, IClassService svc, ClaimsPrincipal user) =>
{
    if (!IsAdmin(user)) return Results.Forbid();
    var result = await svc.SendPasswordAsync(classId, studentId);
    return Results.Ok(result);
}).RequireAuthorization().RequireRateLimiting("remind");

app.MapPost("/api/classes/{classId:int}/students/send-all-passwords", async (
    int classId, IClassService svc, ClaimsPrincipal user) =>
{
    if (!IsAdmin(user)) return Results.Forbid();
    var result = await svc.SendAllPasswordsAsync(classId);
    return Results.Ok(result);
}).RequireAuthorization().RequireRateLimiting("remind");

app.MapPost("/api/classes/{classId:int}/students/{studentId:int}/move", async (
    int classId, int studentId, MoveStudentRequest req, IClassService svc, ClaimsPrincipal user) =>
{
    if (!IsAdmin(user)) return Results.Forbid();
    var s = await svc.MoveStudentAsync(classId, studentId, req.TargetClassId);
    return s is null ? Results.NotFound() : Results.Ok(s);
}).RequireAuthorization();

// ════════════════════════════════════════════════════════════════════════════
// MÒDULS
// ════════════════════════════════════════════════════════════════════════════

app.MapGet("/api/classes/{classId:int}/modules", async (int classId,
    IModuleService svc, ClaimsPrincipal user) =>
{
    if (!IsProfessor(user)) return Results.Forbid();
    var list = await svc.GetByClassAsync(classId);
    return Results.Ok(list);
}).RequireAuthorization();

app.MapGet("/api/classes/{classId:int}/modules/{id:int}", async (int classId, int id,
    IModuleService svc, ClaimsPrincipal user) =>
{
    if (!IsProfessor(user)) return Results.Forbid();
    var m = await svc.GetByIdAsync(id, GetUserId(user), IsAdmin(user));
    return m is null ? Results.NotFound() : Results.Ok(m);
}).RequireAuthorization();

app.MapPost("/api/classes/{classId:int}/modules", async (int classId,
    CreateModuleRequest req, IModuleService svc, ClaimsPrincipal user) =>
{
    if (!IsProfessor(user)) return Results.Forbid();
    if (Validate(req) is { } err) return err;
    try
    {
        var m = await svc.CreateAsync(classId, GetUserId(user), req);
        return Results.Created($"/api/classes/{classId}/modules/{m.Id}", m);
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
}).RequireAuthorization();

app.MapPut("/api/classes/{classId:int}/modules/{id:int}", async (int classId, int id,
    UpdateModuleRequest req, IModuleService svc, ClaimsPrincipal user) =>
{
    if (!IsProfessor(user)) return Results.Forbid();
    if (Validate(req) is { } err) return err;
    var m = await svc.UpdateAsync(id, GetUserId(user), IsAdmin(user), req);
    return m is null ? Results.NotFound() : Results.Ok(m);
}).RequireAuthorization();

app.MapDelete("/api/classes/{classId:int}/modules/{id:int}", async (int classId, int id,
    IModuleService svc, ClaimsPrincipal user) =>
{
    if (!IsProfessor(user)) return Results.Forbid();
    var ok = await svc.DeleteAsync(id, GetUserId(user), IsAdmin(user));
    return ok ? Results.NoContent() : Results.NotFound();
}).RequireAuthorization();

// ── Exclusions de mòdul ───────────────────────────────────────────────────────

app.MapGet("/api/modules/{moduleId:int}/exclusions", async (int moduleId,
    IModuleService svc, ClaimsPrincipal user) =>
{
    if (!IsProfessor(user)) return Results.Forbid();
    var list = await svc.GetExclusionsAsync(moduleId, GetUserId(user), IsAdmin(user));
    return Results.Ok(list);
}).RequireAuthorization();

app.MapPost("/api/modules/{moduleId:int}/exclusions/{studentId:int}", async (
    int moduleId, int studentId, IModuleService svc, ClaimsPrincipal user) =>
{
    if (!IsProfessor(user)) return Results.Forbid();
    var ok = await svc.AddExclusionAsync(moduleId, studentId, GetUserId(user), IsAdmin(user));
    return ok ? Results.NoContent() : Results.NotFound();
}).RequireAuthorization();

app.MapDelete("/api/modules/{moduleId:int}/exclusions/{studentId:int}", async (
    int moduleId, int studentId, IModuleService svc, ClaimsPrincipal user) =>
{
    if (!IsProfessor(user)) return Results.Forbid();
    var ok = await svc.RemoveExclusionAsync(moduleId, studentId, GetUserId(user), IsAdmin(user));
    return ok ? Results.NoContent() : Results.NotFound();
}).RequireAuthorization();

// ════════════════════════════════════════════════════════════════════════════
// ACTIVITATS
// ════════════════════════════════════════════════════════════════════════════

app.MapGet("/api/activities", async (IActivityService svc, ClaimsPrincipal user,
    int page = 1, int size = 500) =>
{
    if (!IsProfessor(user)) return Results.Forbid();
    var profId = IsProfessor(user) && !IsAdmin(user) ? GetUserId(user) : (int?)null;
    var (items, total) = await svc.GetAllPagedAsync(profId, page, size);
    return Results.Ok(new PagedResult<ActivityDto>(items, total, page, size));
}).RequireAuthorization();

app.MapGet("/api/activities/{id:int}", async (int id, IActivityService svc,
    ClaimsPrincipal user) =>
{
    if (!IsProfessor(user)) return Results.Forbid();
    var profId = IsProfessor(user) && !IsAdmin(user) ? GetUserId(user) : (int?)null;
    var a = await svc.GetByIdAsync(id, profId);
    return a is null ? Results.NotFound() : Results.Ok(a);
}).RequireAuthorization();

app.MapPost("/api/activities", async (CreateActivityRequest req, IActivityService svc,
    ClaimsPrincipal user) =>
{
    if (!IsProfessor(user)) return Results.Forbid();
    if (Validate(req) is { } err) return err;
    try
    {
        var a = await svc.CreateAsync(GetUserId(user), IsAdmin(user), req);
        return Results.Created($"/api/activities/{a.Id}", a);
    }
    catch (UnauthorizedAccessException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
}).RequireAuthorization();

app.MapPost("/api/activities/{id:int}/duplicate", async (int id, DuplicateActivityRequest req,
    IActivityService svc, ClaimsPrincipal user) =>
{
    if (!IsProfessor(user)) return Results.Forbid();
    try
    {
        var a = await svc.DuplicateAsync(id, GetUserId(user), IsAdmin(user), req);
        return Results.Created($"/api/activities/{a.Id}", a);
    }
    catch (UnauthorizedAccessException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
}).RequireAuthorization();

app.MapPost("/api/activities/{id:int}/duplicate-cross", async (int id, DuplicateCrossRequest req,
    IActivityService svc, ClaimsPrincipal user) =>
{
    if (!IsProfessor(user)) return Results.Forbid();
    try
    {
        var a = await svc.DuplicateCrossAsync(id, GetUserId(user), IsAdmin(user), req);
        return Results.Created($"/api/activities/{a.Id}", a);
    }
    catch (UnauthorizedAccessException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
}).RequireAuthorization();

app.MapGet("/api/activities/{id:int}/participation", async (int id, IActivityService svc,
    ClaimsPrincipal user) =>
{
    if (!IsProfessor(user)) return Results.Forbid();
    var p = await svc.GetParticipationAsync(id, GetUserId(user), IsAdmin(user));
    return Results.Ok(p);
}).RequireAuthorization();

app.MapPost("/api/activities/{id:int}/remind", async (int id, IActivityService svc,
    IEmailService email, ClaimsPrincipal user) =>
{
    if (!IsProfessor(user)) return Results.Forbid();
    var result = await svc.SendRemindersAsync(id, GetUserId(user), IsAdmin(user), email);
    return Results.Ok(result);
}).RequireAuthorization().RequireRateLimiting("remind");

app.MapGet("/api/activities/{id:int}/remind-targets", async (
    int id, AppDbContext db, ClaimsPrincipal user) =>
{
    if (!IsProfessor(user)) return Results.Forbid();
    var profId = GetUserId(user); var isAdmin = IsAdmin(user);
    var activity = await db.Activities
        .Include(a => a.Groups).ThenInclude(g => g.Members).ThenInclude(m => m.Student)
        .Include(a => a.Module)
        .FirstOrDefaultAsync(a => a.Id == id && a.IsOpen &&
            (isAdmin || a.Module.ProfessorId == profId));
    if (activity is null) return Results.NotFound();
    var submittedIds = await db.Evaluations
        .Where(e => e.ActivityId == id).Select(e => e.EvaluatorId).Distinct().ToHashSetAsync();
    var targets = activity.Groups
        .SelectMany(g => g.Members)
        .Select(m => m.Student).DistinctBy(s => s.Id)
        .Where(s => !submittedIds.Contains(s.Id))
        .Select(s => new InviteTargetDto(s.Id, s.NomComplet, s.Email, ""))
        .OrderBy(t => t.NomComplet).ToList();
    return Results.Ok(targets);
}).RequireAuthorization();

app.MapPost("/api/activities/{id:int}/remind-one/{studentId:int}", async (
    int id, int studentId, AppDbContext db, IEmailService emailSvc, ClaimsPrincipal user) =>
{
    if (!IsProfessor(user)) return Results.Forbid();
    var profId = GetUserId(user); var isAdmin = IsAdmin(user);
    var activity = await db.Activities
        .Include(a => a.Module).ThenInclude(m => m.Class)
        .FirstOrDefaultAsync(a => a.Id == id && a.IsOpen &&
            (isAdmin || a.Module.ProfessorId == profId));
    if (activity is null) return Results.NotFound();
    var student = await db.Students.FindAsync(studentId);
    if (student is null) return Results.NotFound();
    var sent = await emailSvc.SendReminderAsync(
        student.Email, student.NomComplet, activity.Name, activity.Module.Class.Name);
    return Results.Ok(new InviteOneResult(sent, sent ? null : "Error en l'enviament."));
}).RequireAuthorization();

app.MapGet("/api/activities/{id:int}/invite-targets", async (
    int id, AppDbContext db, ClaimsPrincipal user) =>
{
    if (!IsProfessor(user)) return Results.Forbid();
    var profId = GetUserId(user); var isAdmin = IsAdmin(user);
    var activity = await db.Activities
        .Include(a => a.Groups).ThenInclude(g => g.Members).ThenInclude(m => m.Student)
        .Include(a => a.Module)
        .FirstOrDefaultAsync(a => a.Id == id && a.IsOpen &&
            (isAdmin || a.Module.ProfessorId == profId));
    if (activity is null) return Results.NotFound();
    var targets = activity.Groups
        .SelectMany(g => g.Members, (g, m) => new InviteTargetDto(
            m.StudentId, m.Student.NomComplet, m.Student.Email, g.Name))
        .DistinctBy(t => t.StudentId)
        .OrderBy(t => t.GroupName).ThenBy(t => t.NomComplet).ToList();
    return Results.Ok(targets);
}).RequireAuthorization();

app.MapPost("/api/activities/{id:int}/invite/{studentId:int}", async (
    int id, int studentId, InviteOneRequest req,
    AppDbContext db, IClassService classSvc, IEmailService emailSvc, ClaimsPrincipal user) =>
{
    if (!IsProfessor(user)) return Results.Forbid();
    var profId = GetUserId(user); var isAdmin = IsAdmin(user);
    var activity = await db.Activities
        .Include(a => a.Module).ThenInclude(m => m.Class)
        .FirstOrDefaultAsync(a => a.Id == id && a.IsOpen &&
            (isAdmin || a.Module.ProfessorId == profId));
    if (activity is null) return Results.NotFound();
    var student = await db.Students.FindAsync(studentId);
    if (student is null) return Results.NotFound();
    string? password = req.IncludePassword
        ? await classSvc.GetOrRefreshPlainPasswordAsync(studentId) : null;
    var sent = await emailSvc.SendInvitationAsync(
        student.Email, student.NomComplet, activity.Name,
        activity.Module.Class.Name, req.IncludePassword, password);
    return Results.Ok(new InviteOneResult(sent, sent ? null : "Error en l'enviament."));
}).RequireAuthorization();

app.MapGet("/api/activities/{id:int}/criteria", async (int id, IActivityService svc,
    ClaimsPrincipal user) =>
{
    if (!IsProfessor(user)) return Results.Forbid();
    var list = await svc.GetCriteriaAsync(id, GetUserId(user), IsAdmin(user));
    return Results.Ok(list);
}).RequireAuthorization();

app.MapPut("/api/activities/{id:int}/criteria", async (int id, SaveCriteriaRequest req,
    IActivityService svc, IResultsService results, ClaimsPrincipal user) =>
{
    if (!IsProfessor(user)) return Results.Forbid();
    if (!req.Items.Any()) return Results.BadRequest(new { error = "Cal almenys un criteri." });
    if (req.Items.Count > 50) return Results.BadRequest(new { error = "No es poden desar més de 50 criteris per activitat." });
    var list = await svc.SaveCriteriaAsync(id, GetUserId(user), IsAdmin(user), req);
    await results.InvalidateCacheAsync(id); // criteris canviats → resultats i gràfica desactualitzats
    return Results.Ok(list);
}).RequireAuthorization();

app.MapGet("/api/activities/{id:int}/groups/export", async (int id, IActivityService svc,
    ClaimsPrincipal user) =>
{
    if (!IsProfessor(user)) return Results.Forbid();
    var result = await svc.ExportGroupsAsync(id, GetUserId(user), IsAdmin(user));
    if (result is null) return Results.NotFound();
    var (bytes, fileName) = result.Value;
    return Results.File(bytes, "text/csv; charset=utf-8", fileName);
}).RequireAuthorization();

app.MapPost("/api/activities/{id:int}/groups/import", async (int id, ImportGroupsRequest req,
    IActivityService svc, ClaimsPrincipal user) =>
{
    if (!IsProfessor(user)) return Results.Forbid();
    try
    {
        var result = await svc.ImportGroupsAsync(id, GetUserId(user), IsAdmin(user), req.CsvContent);
        return Results.Ok(result);
    }
    catch (UnauthorizedAccessException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
}).RequireAuthorization();

app.MapPut("/api/activities/{id:int}", async (int id, UpdateActivityRequest req,
    IActivityService svc, IResultsService results, ClaimsPrincipal user) =>
{
    if (!IsProfessor(user)) return Results.Forbid();
    if (Validate(req) is { } err) return err;
    var a = await svc.UpdateAsync(id, GetUserId(user), IsAdmin(user), req);
    if (a is not null) await results.InvalidateCacheAsync(id);
    return a is null ? Results.NotFound() : Results.Ok(a);
}).RequireAuthorization();

app.MapDelete("/api/activities/{id:int}", async (int id, IActivityService svc,
    ClaimsPrincipal user) =>
{
    if (!IsProfessor(user)) return Results.Forbid();
    var ok = await svc.DeleteAsync(id, GetUserId(user), IsAdmin(user));
    return ok ? Results.NoContent() : Results.NotFound();
}).RequireAuthorization();

app.MapPost("/api/activities/{id:int}/toggle", async (int id, IActivityService svc,
    IResultsService results, AppDbContext db, ClaimsPrincipal user, ILogger<Program> logger) =>
{
    if (!IsProfessor(user)) return Results.Forbid();
    var a = await svc.ToggleOpenAsync(id, GetUserId(user), IsAdmin(user));
    if (a is not null)
    {
        await results.InvalidateCacheAsync(id); // IsOpen canvia → caché de resultats stale
        try
        {
            var prof = await db.Professors.FindAsync(GetUserId(user));
            db.ActivityLogs.Add(new ActivityLog
            {
                ActivityId   = id,
                ActivityName = a.Name,
                ActorName    = prof?.NomComplet,
                Action       = a.IsOpen ? "opened" : "closed",
                CreatedAt    = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }
        catch (Exception ex) { logger.LogWarning(ex, "Error desant log de toggle (activitat {Id})", id); }
    }
    return a is null ? Results.NotFound() : Results.Ok(a);
}).RequireAuthorization();

// ── Grups ─────────────────────────────────────────────────────────────────────

app.MapGet("/api/activities/{actId:int}/groups", async (int actId, IActivityService svc, ClaimsPrincipal user) =>
{
    if (!IsProfessor(user)) return Results.Forbid();
    var groups = await svc.GetGroupsAsync(actId, GetUserId(user), IsAdmin(user));
    return groups is null ? Results.Forbid() : Results.Ok(groups);
}).RequireAuthorization();

app.MapPost("/api/activities/{actId:int}/groups", async (int actId,
    CreateGroupRequest req, IActivityService svc, ClaimsPrincipal user) =>
{
    if (!IsProfessor(user)) return Results.Forbid();
    var g = await svc.CreateGroupAsync(actId, req, GetUserId(user), IsAdmin(user));
    return g is null ? Results.Forbid()
                     : Results.Created($"/api/activities/{actId}/groups/{g.Id}", g);
}).RequireAuthorization();

app.MapPut("/api/activities/{actId:int}/groups/{groupId:int}", async (
    int actId, int groupId, RenameGroupRequest req,
    IActivityService svc, ClaimsPrincipal user) =>
{
    if (!IsProfessor(user)) return Results.Forbid();
    if (string.IsNullOrWhiteSpace(req.Name)) return Results.BadRequest();
    var ok = await svc.RenameGroupAsync(actId, groupId, req.Name, GetUserId(user), IsAdmin(user));
    return ok ? Results.NoContent() : Results.NotFound();
}).RequireAuthorization();

app.MapDelete("/api/activities/{actId:int}/groups/{groupId:int}", async (
    int actId, int groupId, IActivityService svc, ClaimsPrincipal user) =>
{
    if (!IsProfessor(user)) return Results.Forbid();
    var ok = await svc.DeleteGroupAsync(actId, groupId, GetUserId(user), IsAdmin(user));
    return ok ? Results.NoContent() : Results.NotFound();
}).RequireAuthorization();

app.MapPost("/api/activities/{actId:int}/groups/{groupId:int}/members", async (
    int actId, int groupId, AddMemberRequest req,
    IActivityService svc, ClaimsPrincipal user) =>
{
    if (!IsProfessor(user)) return Results.Forbid();
    var ok = await svc.AddMemberAsync(actId, groupId, req.StudentId, GetUserId(user), IsAdmin(user));
    return ok ? Results.NoContent() : Results.BadRequest();
}).RequireAuthorization();

app.MapPut("/api/activities/{actId:int}/groups/reorder", async (
    int actId, ReorderGroupsRequest req, IActivityService svc, ClaimsPrincipal user) =>
{
    if (!IsProfessor(user)) return Results.Forbid();
    var ok = await svc.ReorderGroupsAsync(actId, req.OrderedGroupIds, GetUserId(user), IsAdmin(user));
    return ok ? Results.NoContent() : Results.Forbid();
}).RequireAuthorization();

app.MapDelete("/api/activities/{actId:int}/groups/{groupId:int}/members/{studentId:int}",
    async (int actId, int groupId, int studentId, IActivityService svc,
    ClaimsPrincipal user) =>
{
    if (!IsProfessor(user)) return Results.Forbid();
    var ok = await svc.RemoveMemberAsync(actId, groupId, studentId, GetUserId(user), IsAdmin(user));
    return ok ? Results.NoContent() : Results.NotFound();
}).RequireAuthorization();

// ── Dashboard alumne ──────────────────────────────────────────────────────────

app.MapGet("/api/student/activities", async (IActivityService svc, ClaimsPrincipal user) =>
{
    if (!user.IsInRole("Student")) return Results.Forbid();
    var classId = int.Parse(user.FindFirstValue("classId")!);
    var list    = await svc.GetStudentActivitiesAsync(GetUserId(user), classId);
    return Results.Ok(new StudentDashboardDto(list));
}).RequireAuthorization();

app.MapPut("/api/student/password", async (
    ChangeStudentPasswordRequest req, IClassService svc, ClaimsPrincipal user) =>
{
    if (!user.IsInRole("Student")) return Results.Forbid();
    if (string.IsNullOrWhiteSpace(req.NewPassword) || req.NewPassword.Length < 6)
        return Results.BadRequest(new { error = "La nova contrasenya ha de tenir almenys 6 caràcters." });
    var ok = await svc.ChangeStudentPasswordAsync(GetUserId(user), req.CurrentPassword, req.NewPassword);
    return ok ? Results.NoContent() : Results.BadRequest(new { error = "La contrasenya actual és incorrecta." });
}).RequireAuthorization().RequireRateLimiting("auth");

// ════════════════════════════════════════════════════════════════════════════
// AVALUACIONS
// ════════════════════════════════════════════════════════════════════════════

app.MapGet("/api/evaluations/{activityId:int}", async (int activityId,
    IEvaluationService svc, ClaimsPrincipal user) =>
{
    if (!user.IsInRole("Student")) return Results.Forbid();
    var form = await svc.GetFormAsync(activityId, GetUserId(user));
    return form is null ? Results.NotFound() : Results.Ok(form);
}).RequireAuthorization();

app.MapPost("/api/evaluations/{activityId:int}", async (int activityId,
    SaveEvaluationsRequest req, IEvaluationService svc, IResultsService results,
    IEmailService email, ClaimsPrincipal user) =>
{
    if (!user.IsInRole("Student")) return Results.Forbid();
    var ok = await svc.SaveAsync(activityId, GetUserId(user), req, email);
    if (ok) await results.InvalidateCacheAsync(activityId);
    return ok ? Results.NoContent() : Results.BadRequest();
}).RequireAuthorization();

// ════════════════════════════════════════════════════════════════════════════
// RESULTATS
// ════════════════════════════════════════════════════════════════════════════

app.MapGet("/api/results/{activityId:int}", async (int activityId,
    IResultsService svc, ClaimsPrincipal user) =>
{
    if (!IsProfessor(user)) return Results.Forbid();
    var r = await svc.GetResultsAsync(activityId, GetUserId(user), IsAdmin(user));
    return r is null ? Results.NotFound() : Results.Ok(r);
}).RequireAuthorization();

app.MapGet("/api/results/{activityId:int}/chart", async (int activityId,
    IResultsService svc, ClaimsPrincipal user) =>
{
    if (!IsProfessor(user)) return Results.Forbid();
    var r = await svc.GetChartAsync(activityId, GetUserId(user), IsAdmin(user));
    return r is null ? Results.NotFound() : Results.Ok(r);
}).RequireAuthorization();

app.MapGet("/api/results/{activityId:int}/csv", async (int activityId,
    IResultsService svc, ClaimsPrincipal user) =>
{
    if (!IsProfessor(user)) return Results.Forbid();
    var result = await svc.ExportCsvAsync(activityId, GetUserId(user), IsAdmin(user));
    if (result is null) return Results.NotFound();
    var (content, fileName) = result.Value;
    return Results.File(content, "text/csv; charset=utf-8", fileName);
}).RequireAuthorization();

app.MapGet("/api/results/{activityId:int}/excel", async (int activityId,
    IResultsService svc, ClaimsPrincipal user) =>
{
    if (!IsProfessor(user)) return Results.Forbid();
    var result = await svc.ExportExcelAsync(activityId, GetUserId(user), IsAdmin(user));
    if (result is null) return Results.NotFound();
    var (content, fileName) = result.Value;
    return Results.File(content,
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
}).RequireAuthorization();

// ── Criteri ───────────────────────────────────────────────────────────────────
app.MapGet("/api/criteria", () =>
    Results.Ok(Criteria.All.Select(c => new CriteriaDto(c.Key, c.Label))));

// ── Compte d'avaluacions (per confirmació d'eliminació) ────────────────────────
app.MapGet("/api/activities/{id:int}/evals-count", async (
    int id, AppDbContext db, ClaimsPrincipal user) =>
{
    if (!IsProfessor(user)) return Results.Forbid();
    var count = await db.Evaluations.CountAsync(e => e.ActivityId == id);
    return Results.Ok(new { count });
}).RequireAuthorization();

// ── Health check ──────────────────────────────────────────────────────────────
app.MapGet("/api/health", async (AppDbContext db, StackExchange.Redis.IConnectionMultiplexer redis) =>
{
    var dbOk    = false;
    var redisOk = false;
    try { dbOk    = await db.Database.CanConnectAsync(); }    catch { }
    try { redisOk = redis.IsConnected; }                      catch { }
    var status = dbOk && redisOk ? "ok" : "degraded";
    return Results.Ok(new { status, db = dbOk ? "ok" : "error", redis = redisOk ? "ok" : "error" });
});

// ════════════════════════════════════════════════════════════════════════════
// ESTADÍSTIQUES D'ÚS (admin only)
// ════════════════════════════════════════════════════════════════════════════

app.MapGet("/api/admin/stats", async (AppDbContext db, ClaimsPrincipal user) =>
{
    if (!IsAdmin(user)) return Results.Forbid();

    var since30  = DateTime.UtcNow.AddDays(-30);
    var since6mo = DateTime.UtcNow.AddMonths(-6);

    var professors = await db.Professors
        .OrderBy(p => p.Cognoms).ThenBy(p => p.Nom)
        .ToListAsync();

    var loginStats = await db.ProfessorLogins
        .GroupBy(l => l.ProfessorId)
        .Select(g => new {
            ProfessorId = g.Key,
            Last30      = g.Count(l => l.CreatedAt >= since30),
            LastAccess  = (DateTime?)g.Max(l => l.CreatedAt)
        })
        .ToListAsync();

    // Totes les activitats amb el professor propietari (via mòdul)
    var profActivities = await db.Activities
        .Join(db.Modules, a => a.ModuleId, m => m.Id,
              (a, m) => new { ActivityId = a.Id, m.ProfessorId })
        .ToListAsync();

    // Membres per activitat (per calcular participació)
    var memberCounts = await db.GroupMembers
        .GroupBy(gm => gm.Group.ActivityId)
        .Select(g => new { ActivityId = g.Key, Count = g.Count() })
        .ToDictionaryAsync(x => x.ActivityId, x => x.Count);

    // Alumnes que han enviat autoavaluació (IsSelf) per activitat
    var submittedCounts = await db.Evaluations
        .Where(e => e.IsSelf)
        .Select(e => new { e.ActivityId, e.EvaluatorId })
        .Distinct()
        .GroupBy(e => e.ActivityId)
        .Select(g => new { ActivityId = g.Key, Count = g.Count() })
        .ToDictionaryAsync(x => x.ActivityId, x => x.Count);

    // Accessos per mes (últims 6 mesos) — tipus anònim per evitar problemes de traducció SQL
    var monthlyLogins = (await db.ProfessorLogins
        .Where(l => l.CreatedAt >= since6mo)
        .GroupBy(l => new { l.CreatedAt.Year, l.CreatedAt.Month })
        .Select(g => new { g.Key.Year, g.Key.Month, Count = g.Count() })
        .OrderBy(m => m.Year).ThenBy(m => m.Month)
        .ToListAsync())
        .Select(m => new MonthlyStatDto(m.Year, m.Month, m.Count))
        .ToList();

    // Activitats creades per mes (últims 6 mesos)
    var monthlyActivities = (await db.Activities
        .Where(a => a.CreatedAt >= since6mo)
        .GroupBy(a => new { a.CreatedAt.Year, a.CreatedAt.Month })
        .Select(g => new { g.Key.Year, g.Key.Month, Count = g.Count() })
        .OrderBy(m => m.Year).ThenBy(m => m.Month)
        .ToListAsync())
        .Select(m => new MonthlyStatDto(m.Year, m.Month, m.Count))
        .ToList();

    var stats = professors.Select(p =>
    {
        var myIds = profActivities
            .Where(x => x.ProfessorId == p.Id).Select(x => x.ActivityId).ToList();
        var login = loginStats.FirstOrDefault(l => l.ProfessorId == p.Id);

        var parts = myIds
            .Where(id => memberCounts.ContainsKey(id) && memberCounts[id] > 0)
            .Select(id => Math.Min(100.0, (double)submittedCounts.GetValueOrDefault(id) / memberCounts[id] * 100))
            .ToList();

        return new ProfessorStatsDto(
            p.Id, p.NomComplet, p.Email, p.IsAdmin,
            login?.Last30 ?? 0,
            myIds.Count,
            Math.Round(parts.Count > 0 ? parts.Average() : 0, 1),
            login?.LastAccess);
    }).ToList();

    return Results.Ok(new AdminStatsDto(stats, monthlyLogins, monthlyActivities));
}).RequireAuthorization().RequireRateLimiting("admin");

app.MapDelete("/api/admin/stats/logins", async (AppDbContext db, ClaimsPrincipal user) =>
{
    if (!IsAdmin(user)) return Results.Forbid();
    await db.ProfessorLogins.ExecuteDeleteAsync();
    return Results.NoContent();
}).RequireAuthorization().RequireRateLimiting("admin");

// ════════════════════════════════════════════════════════════════════════════
// BACKUP / RESTORE (admin only)
// ════════════════════════════════════════════════════════════════════════════

app.MapGet("/api/admin/backup/export", async (IBackupService svc, ClaimsPrincipal user) =>
{
    if (!IsAdmin(user)) return Results.Forbid();
    var backup   = await svc.ExportAsync();
    var json     = System.Text.Json.JsonSerializer.Serialize(backup,
        new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
    var bytes    = System.Text.Encoding.UTF8.GetBytes(json);
    var fileName = $"autoco_backup_{DateTime.UtcNow:yyyyMMdd_HHmmss}.json";
    return Results.File(bytes, "application/json", fileName);
}).RequireAuthorization();

app.MapPost("/api/admin/backup/import", async (
    BackupDto backup, IBackupService svc, AppDbContext db, ClaimsPrincipal user) =>
{
    if (!IsAdmin(user)) return Results.Forbid();
    var result = await svc.ImportAsync(backup);
    if (result.Success)
        await AuditAsync(db, "backup.imported", GetUserId(user),
            user.FindFirstValue(ClaimTypes.Name),
            $"Professors:{result.Professors} Classes:{result.Classes} Students:{result.Students}");
    return result.Success ? Results.Ok(result) : Results.BadRequest(result);
}).RequireAuthorization().RequireRateLimiting("admin");

app.MapGet("/api/admin/backup/files", async (IBackupService svc, ClaimsPrincipal user) =>
{
    if (!IsAdmin(user)) return Results.Forbid();
    return Results.Ok(await svc.ListFilesAsync());
}).RequireAuthorization();

app.MapPost("/api/admin/backup/files", async (IBackupService svc, ClaimsPrincipal user) =>
{
    if (!IsAdmin(user)) return Results.Forbid();
    var info = await svc.CreateFileAsync();
    return Results.Ok(info);
}).RequireAuthorization();

app.MapGet("/api/admin/backup/files/{name}", async (
    string name, IBackupService svc, ClaimsPrincipal user) =>
{
    if (!IsAdmin(user)) return Results.Forbid();
    var result = await svc.DownloadFileAsync(name);
    if (result is null) return Results.NotFound();
    return Results.File(result.Value.Data, "application/json", result.Value.Name);
}).RequireAuthorization();

app.MapDelete("/api/admin/backup/files/{name}", async (
    string name, IBackupService svc, ClaimsPrincipal user) =>
{
    if (!IsAdmin(user)) return Results.Forbid();
    return await svc.DeleteFileAsync(name) ? Results.NoContent() : Results.NotFound();
}).RequireAuthorization();

app.MapPost("/api/admin/backup/files/{name}/restore", async (
    string name, IBackupService svc, AppDbContext db, ClaimsPrincipal user) =>
{
    if (!IsAdmin(user)) return Results.Forbid();
    var result = await svc.RestoreFileAsync(name);
    if (result.Success)
        await AuditAsync(db, "backup.restored", GetUserId(user),
            user.FindFirstValue(ClaimTypes.Name),
            $"File:{name}");
    return result.Success ? Results.Ok(result) : Results.BadRequest(result);
}).RequireAuthorization().RequireRateLimiting("admin");

// ════════════════════════════════════════════════════════════════════════════
// NOTES DEL PROFESSOR PER ALUMNE
// ════════════════════════════════════════════════════════════════════════════

app.MapGet("/api/notes/{activityId:int}/{studentId:int}", async (
    int activityId, int studentId, AppDbContext db, ClaimsPrincipal user) =>
{
    if (!IsProfessor(user)) return Results.Forbid();
    var activity = await db.Activities.Include(a => a.Module)
        .FirstOrDefaultAsync(a => a.Id == activityId);
    if (activity is null) return Results.NotFound();
    if (activity.Module.ProfessorId != GetUserId(user) && !IsAdmin(user)) return Results.Forbid();

    var note = await db.ProfessorNotes
        .FirstOrDefaultAsync(n => n.ActivityId == activityId && n.StudentId == studentId);
    if (note is null) return Results.Ok(new ProfessorNoteDto(studentId, "", DateTime.UtcNow));
    return Results.Ok(new ProfessorNoteDto(note.StudentId, note.Note, note.UpdatedAt));
}).RequireAuthorization();

app.MapGet("/api/notes/{activityId:int}", async (
    int activityId, AppDbContext db, ClaimsPrincipal user) =>
{
    if (!IsProfessor(user)) return Results.Forbid();
    var activity = await db.Activities.Include(a => a.Module)
        .FirstOrDefaultAsync(a => a.Id == activityId);
    if (activity is null) return Results.NotFound();
    if (activity.Module.ProfessorId != GetUserId(user) && !IsAdmin(user)) return Results.Forbid();

    var notes = await db.ProfessorNotes
        .Where(n => n.ActivityId == activityId)
        .Select(n => new ProfessorNoteDto(n.StudentId, n.Note, n.UpdatedAt))
        .ToListAsync();
    return Results.Ok(notes);
}).RequireAuthorization();

app.MapPut("/api/notes/{activityId:int}/{studentId:int}", async (
    int activityId, int studentId, SaveNoteRequest req,
    AppDbContext db, ClaimsPrincipal user) =>
{
    if (!IsProfessor(user)) return Results.Forbid();
    var activity = await db.Activities.Include(a => a.Module)
        .FirstOrDefaultAsync(a => a.Id == activityId);
    if (activity is null) return Results.NotFound();
    if (activity.Module.ProfessorId != GetUserId(user) && !IsAdmin(user)) return Results.Forbid();

    var note = await db.ProfessorNotes
        .FirstOrDefaultAsync(n => n.ActivityId == activityId && n.StudentId == studentId);
    if (note is null)
    {
        note = new ProfessorNote
        {
            ActivityId = activityId, StudentId = studentId,
            Note = req.Note.Trim(), UpdatedAt = DateTime.UtcNow
        };
        db.ProfessorNotes.Add(note);
    }
    else
    {
        note.Note = req.Note.Trim();
        note.UpdatedAt = DateTime.UtcNow;
    }
    await db.SaveChangesAsync();
    return Results.Ok(new ProfessorNoteDto(note.StudentId, note.Note, note.UpdatedAt));
}).RequireAuthorization();

// ════════════════════════════════════════════════════════════════════════════
// PLANTILLES D'ACTIVITAT
// ════════════════════════════════════════════════════════════════════════════

app.MapGet("/api/templates", async (AppDbContext db, ClaimsPrincipal user) =>
{
    if (!IsProfessor(user)) return Results.Forbid();
    var professorId = GetUserId(user);
    var isAdm = IsAdmin(user);
    var list = await db.ActivityTemplates
        .Where(t => t.ProfessorId == professorId || isAdm)
        .OrderByDescending(t => t.CreatedAt)
        .ToListAsync();

    // Precarrega noms de professors per a l'admin (evita N+1)
    var professorIds  = list.Select(t => t.ProfessorId).Distinct().ToList();
    var professorNames = await db.Professors
        .Where(p => professorIds.Contains(p.Id))
        .ToDictionaryAsync(p => p.Id, p => p.NomComplet);

    var dtos = list.Select(t =>
    {
        var criteria = System.Text.Json.JsonSerializer.Deserialize<List<CriterionItem>>(t.CriteriaJson)
            ?? new List<CriterionItem>();
        professorNames.TryGetValue(t.ProfessorId, out var profName);
        return new ActivityTemplateDto(t.Id, t.Name, t.Description, criteria, t.CreatedAt, profName);
    }).ToList();
    return Results.Ok(dtos);
}).RequireAuthorization();

app.MapPost("/api/templates", async (CreateTemplateRequest req, AppDbContext db, ClaimsPrincipal user) =>
{
    if (!IsProfessor(user)) return Results.Forbid();
    var criteriaJson = System.Text.Json.JsonSerializer.Serialize(req.Criteria ?? new List<CriterionItem>());
    var t = new ActivityTemplate
    {
        ProfessorId  = GetUserId(user),
        Name         = req.Name.Trim(),
        Description  = req.Description?.Trim(),
        CriteriaJson = criteriaJson,
        CreatedAt    = DateTime.UtcNow
    };
    db.ActivityTemplates.Add(t);
    await db.SaveChangesAsync();
    var criteria = System.Text.Json.JsonSerializer.Deserialize<List<CriterionItem>>(t.CriteriaJson)
        ?? new List<CriterionItem>();
    return Results.Created($"/api/templates/{t.Id}",
        new ActivityTemplateDto(t.Id, t.Name, t.Description, criteria, t.CreatedAt));
}).RequireAuthorization();

app.MapDelete("/api/templates/{id:int}", async (int id, AppDbContext db, ClaimsPrincipal user) =>
{
    if (!IsProfessor(user)) return Results.Forbid();
    var t = await db.ActivityTemplates.FindAsync(id);
    if (t is null) return Results.NotFound();
    if (t.ProfessorId != GetUserId(user) && !IsAdmin(user)) return Results.Forbid();
    db.ActivityTemplates.Remove(t);
    await db.SaveChangesAsync();
    return Results.NoContent();
}).RequireAuthorization();

// ════════════════════════════════════════════════════════════════════════════
// REGISTRE D'ACTIVITAT (LOG)
// ════════════════════════════════════════════════════════════════════════════

app.MapGet("/api/activities/{id:int}/log", async (
    int id, AppDbContext db, ClaimsPrincipal user) =>
{
    if (!IsProfessor(user)) return Results.Forbid();
    var activity = await db.Activities.Include(a => a.Module)
        .FirstOrDefaultAsync(a => a.Id == id);
    if (activity is null) return Results.NotFound();
    if (activity.Module.ProfessorId != GetUserId(user) && !IsAdmin(user)) return Results.Forbid();

    var logs = await db.ActivityLogs
        .Where(l => l.ActivityId == id)
        .OrderByDescending(l => l.CreatedAt)
        .Take(100)
        .Select(l => new ActivityLogDto(l.Id, l.Action, l.ActorName, l.Details, l.CreatedAt))
        .ToListAsync();
    return Results.Ok(logs);
}).RequireAuthorization();

// ════════════════════════════════════════════════════════════════════════════
// FOTOS
// ════════════════════════════════════════════════════════════════════════════

// Pujar foto d'alumne individual
app.MapPost("/api/classes/{classId:int}/students/{studentId:int}/foto",
    async (int classId, int studentId, HttpRequest request,
           AppDbContext db, IPhotoService photos, ClaimsPrincipal user) =>
{
    if (!IsAdmin(user)) return Results.Forbid();
    var student = await db.Students.FirstOrDefaultAsync(s => s.Id == studentId && s.ClassId == classId);
    if (student is null) return Results.NotFound();

    var file = request.Form.Files.FirstOrDefault();
    if (file is null || file.Length == 0) return Results.BadRequest(new { error = "Cap fitxer." });
    if (file.Length > 5 * 1024 * 1024) return Results.BadRequest(new { error = "Fitxer massa gran (màx 5 MB)." });

    using var stream = file.OpenReadStream();
    var ok = await photos.SaveStudentFotoAsync(studentId, stream, file.ContentType);
    if (!ok) return Results.Problem("Error en desar la foto.");

    var url = photos.GetStudentFotoUrl(studentId);
    return Results.Ok(new { fotoUrl = url });
}).RequireAuthorization().DisableAntiforgery();

// Eliminar foto d'alumne
app.MapDelete("/api/classes/{classId:int}/students/{studentId:int}/foto",
    async (int classId, int studentId, AppDbContext db, IPhotoService photos, ClaimsPrincipal user) =>
{
    if (!IsAdmin(user)) return Results.Forbid();
    var exists = await db.Students.AnyAsync(s => s.Id == studentId && s.ClassId == classId);
    if (!exists) return Results.NotFound();
    photos.DeleteStudentFoto(studentId);
    return Results.NoContent();
}).RequireAuthorization();

// Importar fotos en ZIP (coincidència per DNI)
app.MapPost("/api/classes/{classId:int}/students/fotos/zip",
    async (int classId, HttpRequest request,
           AppDbContext db, IPhotoService photos, ClaimsPrincipal user) =>
{
    if (!IsAdmin(user)) return Results.Forbid();

    var file = request.Form.Files.FirstOrDefault();
    if (file is null || file.Length == 0) return Results.BadRequest(new { error = "Cap fitxer ZIP." });
    if (file.Length > 100 * 1024 * 1024) return Results.BadRequest(new { error = "Fitxer massa gran (màx 100 MB)." });

    // Construeix mapa DNI→StudentId per als alumnes d'aquesta classe
    var students = await db.Students
        .Where(s => s.ClassId == classId && s.Dni != null)
        .Select(s => new { s.Id, s.Dni })
        .ToListAsync();

    var dniMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    foreach (var s in students)
    {
        // Guarda la part numèrica del DNI com a clau (eg "53971108X" → "53971108")
        var dniParts = System.Text.RegularExpressions.Regex.Match(s.Dni!, @"^\d+");
        if (dniParts.Success) dniMap[dniParts.Value] = s.Id;
        dniMap[s.Dni!] = s.Id;
    }

    using var stream = file.OpenReadStream();
    var (imported, notFound, errors) = await photos.ImportZipFotosAsync(stream, dniMap);

    return Results.Ok(new ImportFotosResult(imported, notFound, errors));
}).RequireAuthorization().DisableAntiforgery();

// Pujar foto de professor (perfil propi)
app.MapPost("/api/professors/{id:int}/foto",
    async (int id, HttpRequest request,
           AppDbContext db, IPhotoService photos, ClaimsPrincipal user) =>
{
    if (!IsProfessor(user)) return Results.Forbid();
    var callerProfId = GetUserId(user);
    // Cada professor pot canviar la seva pròpia foto; l'admin pot canviar qualsevol
    if (callerProfId != id && !IsAdmin(user)) return Results.Forbid();

    var professor = await db.Professors.FindAsync(id);
    if (professor is null) return Results.NotFound();

    var file = request.Form.Files.FirstOrDefault();
    if (file is null || file.Length == 0) return Results.BadRequest(new { error = "Cap fitxer." });
    if (file.Length > 5 * 1024 * 1024) return Results.BadRequest(new { error = "Fitxer massa gran (màx 5 MB)." });

    using var stream = file.OpenReadStream();
    var ok = await photos.SaveProfessorFotoAsync(id, stream, file.ContentType);
    if (!ok) return Results.Problem("Error en desar la foto.");

    var url = photos.GetProfessorFotoUrl(id);
    return Results.Ok(new { fotoUrl = url });
}).RequireAuthorization().DisableAntiforgery();

// Eliminar foto de professor
app.MapDelete("/api/professors/{id:int}/foto",
    async (int id, AppDbContext db, IPhotoService photos, ClaimsPrincipal user) =>
{
    if (!IsProfessor(user)) return Results.Forbid();
    var callerProfId = GetUserId(user);
    if (callerProfId != id && !IsAdmin(user)) return Results.Forbid();
    var exists = await db.Professors.AnyAsync(p => p.Id == id);
    if (!exists) return Results.NotFound();
    photos.DeleteProfessorFoto(id);
    return Results.NoContent();
}).RequireAuthorization();

// ── Nivell de log (admin) ─────────────────────────────────────────────────────
app.MapGet("/api/admin/log-level", (LogLevelHolder h) =>
    Results.Ok(new LogLevelDto(h.Level.ToString())))
    .RequireAuthorization();

app.MapPut("/api/admin/log-level", async (
    SetLogLevelRequest req, LogLevelHolder h, IDistributedCache cache,
    AppDbContext db, ClaimsPrincipal user) =>
{
    if (!IsAdmin(user)) return Results.Forbid();
    if (!Enum.TryParse<LogLevel>(req.Level, out var newLevel) || newLevel == LogLevel.None)
        return Results.BadRequest(new { error = "Nivell invàlid" });
    var prev = h.Level;
    await cache.SetStringAsync("autoco:loglevel", newLevel.ToString());
    h.Level = newLevel;
    await AuditAsync(db, "log_level.changed", GetUserId(user),
        user.FindFirstValue(ClaimTypes.Name),
        $"{prev}→{newLevel}");
    return Results.Ok(new LogLevelDto(newLevel.ToString()));
}).RequireAuthorization().RequireRateLimiting("admin");

// ── Auditoria d'accions admin ─────────────────────────────────────────────────
app.MapGet("/api/admin/audit", async (AppDbContext db, ClaimsPrincipal user,
    int page = 1, int size = 50) =>
{
    if (!IsAdmin(user)) return Results.Forbid();
    var q     = db.AdminAuditLogs.OrderByDescending(l => l.CreatedAt);
    var total = await q.CountAsync();
    var items = await q.Skip((page - 1) * size).Take(size)
        .Select(l => new AdminAuditLogDto(l.Id, l.Action, l.ActorName, l.Details, l.CreatedAt))
        .ToListAsync();
    return Results.Ok(new PagedResult<AdminAuditLogDto>(items, total, page, size));
}).RequireAuthorization().RequireRateLimiting("admin");

app.Run();
