using AutoCo.Web;
using AutoCo.Web.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Localization;
using MudBlazor.Services;
using StackExchange.Redis;
using AutoCo.Web.Resources;

// Necessari per a ExcelDataReader: suport d'encodings Windows (cp1252, etc.)
System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

var builder = WebApplication.CreateBuilder(args);

// logging.json muntat com a volum Docker — recarrega automàticament sense reinici.
// DOTNET_USE_POLLING_FILE_WATCHER=true al docker-compose garanteix la detecció en Docker.
builder.Configuration.AddJsonFile("logging.json", optional: true, reloadOnChange: true);

// LogLevelHolder: singleton capturat pel filtre; el nivell es pot canviar en calent des de la UI.
var logHolder = new AutoCo.Web.Services.LogLevelHolder();
builder.Services.AddSingleton(logHolder);
builder.Logging.AddFilter((category, level) =>
{
    if (category?.StartsWith("Microsoft.AspNetCore.Antiforgery") == true)
        return level >= LogLevel.Critical;
    return level >= logHolder.Level;
});

var redisConn = builder.Configuration["Redis:ConnectionString"] ?? "localhost:6379";
var redis = await ConnectionMultiplexer.ConnectAsync(redisConn);
builder.Services.AddSingleton<IConnectionMultiplexer>(redis);

// Restaurar el nivell de log guardat a Redis (persistent entre reinicis)
var savedLogLevel = await redis.GetDatabase().StringGetAsync("autoco:loglevel");
if (!savedLogLevel.IsNull && Enum.TryParse<LogLevel>(savedLogLevel.ToString(), out var savedLevel))
    logHolder.Level = savedLevel;

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddSignalR()
    .AddStackExchangeRedis(redisConn, opts =>
        opts.Configuration.ChannelPrefix = StackExchange.Redis.RedisChannel.Literal("AutoCo"));

builder.Services.AddMudServices();

// ── Localització (i18n) ───────────────────────────────────────────────────────
builder.Services.AddLocalization(opts => opts.ResourcesPath = "Resources");

// Usem DictionaryLocalizer (diccionaris estàtics + fitxers JSON externs a I18N_PATH)
// per evitar problemes de resolució de recursos embeguts en Docker.
var localizer = new AutoCo.Web.Resources.DictionaryLocalizer(builder.Configuration);
builder.Services.AddSingleton<IStringLocalizer<SharedResources>>(localizer);

var defaultLang = builder.Configuration["DEFAULT_LANGUAGE"] ?? "ca";
var builtinCultures = new[] { "ca", "es" };
// Afegir idiomes descoberts als fitxers JSON externs (si no estan ja inclosos)
var supportedCultures = builtinCultures
    .Concat(localizer.ExternalLanguages.Where(l => !builtinCultures.Contains(l)))
    .ToArray();
if (!supportedCultures.Contains(defaultLang)) defaultLang = "ca";

builder.Services.Configure<Microsoft.AspNetCore.Builder.RequestLocalizationOptions>(opts =>
{
    opts.SetDefaultCulture(defaultLang);
    opts.AddSupportedCultures(supportedCultures);
    opts.AddSupportedUICultures(supportedCultures);
    // Prioritat: cookie → accept-language header → default (DEFAULT_LANGUAGE o "ca")
    opts.ApplyCurrentCultureToResponseHeaders = true;
});

// Persistir les claus de DataProtection al sistema de fitxers (volum Docker independent de Redis)
// Això evita que les claus es perdin si Redis reinicia, i manté la coherència
// de cookies d'antiforgery i sessions de ProtectedLocalStorage entre desplegaments.
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new System.IO.DirectoryInfo("/app/dp-keys"))
    .SetApplicationName("AutoCo");

// IHttpContextAccessor: captura IP del client a OnInitialized (MainLayout)
builder.Services.AddHttpContextAccessor();

// Estat de l'usuari (substitueix ISession + SessionHelper)
builder.Services.AddScoped<UserStateService>();

// Notificacions de participació en temps real (Redis pub/sub → Blazor)
builder.Services.AddSingleton<ParticipationNotificationService>();
builder.Services.AddHostedService<ParticipationRedisSubscriber>();

// Notificacions in-app (Redis pub/sub → Blazor)
builder.Services.AddSingleton<NotificationBroadcastService>();
builder.Services.AddHostedService<NotificationRedisSubscriber>();

// Branding corporatiu (variables BRAND_* + logo via volum ./config/branding)
builder.Services.AddSingleton<BrandingService>();

// Presència online (heartbeat Redis 10s / TTL 20s) per a /admin/connexions
builder.Services.AddScoped<OnlinePresenceService>();

// HTTP client cap a l'API
builder.Services.AddHttpClient<ApiClient>(client =>
{
    var baseUrl = builder.Configuration["ApiSettings:BaseUrl"] ?? "http://localhost:7000";
    client.BaseAddress = new Uri(baseUrl);
    client.Timeout     = TimeSpan.FromMinutes(3); // permet importacions CSV grans i enviaments massius
});

var app = builder.Build();

app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});

if (!app.Environment.IsDevelopment())
    app.UseExceptionHandler("/Error");

app.UseStaticFiles();
app.UseRequestLocalization();
app.UseAntiforgery();

// Fitxer de referència per crear noves traduccions: /i18n/reference.json
// Conté totes les claus del diccionari en català (base per a nous idiomes)
app.MapGet("/i18n/reference.json", () =>
    Results.Json(AutoCo.Web.Resources.DictionaryLocalizer.Reference
        .OrderBy(kv => kv.Key)
        .ToDictionary(kv => kv.Key, kv => kv.Value)));

// manifest.json dinàmic: usa els valors de BrandingService
app.MapGet("/manifest.json", (BrandingService b) =>
{
    var json = System.Text.Json.JsonSerializer.Serialize(new
    {
        name             = b.AppName,
        short_name       = b.AppShortName,
        description      = "Sistema d'avaluació entre iguals per a activitats de grup",
        start_url        = "/",
        display          = "standalone",
        background_color = "#ffffff",
        theme_color      = b.NavColor,
        lang             = "ca",
        icons = new[]
        {
            new { src = b.LogoUrl, sizes = "192x192", type = "image/png", purpose = "any maskable" },
            new { src = b.LogoUrl, sizes = "512x512", type = "image/png", purpose = "any maskable" },
        },
        categories  = new[] { "education", "productivity" },
        orientation = "any",
    });
    return Results.Content(json, "application/manifest+json");
});

app.MapRazorComponents<AutoCo.Web.Components.App>()
    .AddInteractiveServerRenderMode();

app.Run();
