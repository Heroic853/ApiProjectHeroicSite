using System.IO.Compression;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.EntityFrameworkCore;
using WebApi.Data;
using WebApi.Middleware;
using WebApi.Services;
using Stripe;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------
// LOGGING — tutto quello che succede finisce in console con orario e durata
// ---------------------------------------------------------------------------
builder.Logging.ClearProviders();
builder.Logging.AddSimpleConsole(options =>
{
    options.SingleLine = true;
    options.TimestampFormat = "HH:mm:ss ";
    options.UseUtcTimestamp = true;
});

// Metti LOG_SQL=true tra le env var di Render per vedere l'SQL che EF genera
var logSql = string.Equals(Environment.GetEnvironmentVariable("LOG_SQL"), "true", StringComparison.OrdinalIgnoreCase);
builder.Logging.AddFilter("Microsoft.EntityFrameworkCore.Database.Command",
    logSql ? LogLevel.Information : LogLevel.Warning);

// EF logga ogni tentativo di riconnessione con l'intero stack trace a livello
// Information: durante un problema di database riempiva la console di migliaia
// di righe e non si leggeva piu' niente. A Warning resta solo cio' che conta.
builder.Logging.AddFilter("Microsoft.EntityFrameworkCore.Infrastructure", LogLevel.Warning);
builder.Logging.AddFilter("Microsoft.EntityFrameworkCore.Query", LogLevel.Warning);
builder.Logging.AddFilter("Microsoft.AspNetCore", LogLevel.Warning);
builder.Logging.AddFilter("Microsoft.AspNetCore.Authentication", LogLevel.Information);

// Stesso formato del logger principale, cosi' gli orari sono confrontabili
// (prima uno stampava ora locale e l'altro UTC: sembrava che i log fossero sfasati)
var startupLogger = LoggerFactory.Create(b =>
{
    b.AddSimpleConsole(o =>
    {
        o.SingleLine = true;
        o.TimestampFormat = "HH:mm:ss ";
        o.UseUtcTimestamp = true;
    });
}).CreateLogger("Startup");

startupLogger.LogInformation("Fatalis STARTING to fly...");

// ---------------------------------------------------------------------------
// DATABASE
// ---------------------------------------------------------------------------
var connectionString = Environment.GetEnvironmentVariable("DATABASE_URL")
    ?? builder.Configuration.GetConnectionString("DefaultConnection");

if (string.IsNullOrWhiteSpace(connectionString))
{
    startupLogger.LogCritical(
        "CONNECTION STRING MANCANTE.\n" +
        "  In produzione (Render): imposta la variabile d'ambiente DATABASE_URL.\n" +
        "  In locale: NON metterla in appsettings.Development.json (e' tracciata da git).\n" +
        "  Usa i user secrets, dalla cartella WebApi:\n" +
        "      dotnet user-secrets set \"ConnectionStrings:DefaultConnection\" \"Host=...;Port=5432;Database=postgres;Username=...;Password=...;SSL Mode=Require;Trust Server Certificate=true\"\n" +
        "  (i user secrets si caricano solo con ASPNETCORE_ENVIRONMENT=Development)");

    throw new InvalidOperationException(
        "Database connection string not found. Imposta DATABASE_URL oppure il user secret " +
        "ConnectionStrings:DefaultConnection (vedi il log sopra per il comando esatto).");
}
startupLogger.LogInformation("Connection string trovata (host: {Host})", DescribeHost(connectionString));

// AddDbContextPool riusa le istanze di DbContext invece di ricostruirle a ogni
// richiesta: meno allocazioni e nessun costo di inizializzazione per chiamata.
builder.Services.AddDbContextPool<DragonListDbContext>(options =>
{
    options.UseNpgsql(connectionString, npgsql =>
    {
        // Era 1800s (30 minuti): una query bloccata teneva appesa la richiesta
        // per mezz'ora invece di fallire subito. 30s e' piu' che sufficiente.
        npgsql.CommandTimeout(30);

        // I Postgres free si sospendono: ritenta invece di restituire errore
        npgsql.EnableRetryOnFailure(maxRetryCount: 3, maxRetryDelay: TimeSpan.FromSeconds(3), errorCodesToAdd: null);
    });
}, poolSize: 32);

// ---------------------------------------------------------------------------
// AUTH0 / JWT
// ---------------------------------------------------------------------------
var auth0Authority = builder.Configuration["Auth0:Authority"] ?? "https://dev-fye25mtdiciuqtin.us.auth0.com/";
var auth0Audience = builder.Configuration["Auth0:Audience"] ?? "https://apiprojectheroicsite.onrender.com";

// L'Authority DEVE finire con lo slash, altrimenti la validazione dell'issuer
// puo' fallire perche' Auth0 mette lo slash nel claim "iss".
if (!auth0Authority.EndsWith('/')) auth0Authority += "/";

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
}).AddJwtBearer(options =>
{
    options.Authority = auth0Authority;
    options.Audience = auth0Audience;

    // Stesso claim custom che il Client legge in CustomUserFactory: senza questo
    // il server non vede i ruoli e [Authorize(Roles = "Admin")] fallisce sempre.
    options.TokenValidationParameters.RoleClaimType = "https://heroic853.github.io/roles";

    // Log dei problemi di autenticazione: senza questi un 401 non dice niente
    options.Events = new JwtBearerEvents
    {
        OnAuthenticationFailed = ctx =>
        {
            var log = ctx.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("Auth0");
            log.LogWarning("AUTH FALLITA su {Path}: {Error}", ctx.Request.Path, ctx.Exception.Message);
            return Task.CompletedTask;
        },
        OnChallenge = ctx =>
        {
            var log = ctx.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("Auth0");
            log.LogWarning("AUTH CHALLENGE su {Path}: {Error} — {Description}",
                ctx.Request.Path,
                string.IsNullOrEmpty(ctx.Error) ? "token mancante o non valido" : ctx.Error,
                ctx.ErrorDescription ?? "-");
            return Task.CompletedTask;
        },
        OnTokenValidated = ctx =>
        {
            var log = ctx.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("Auth0");
            var roles = ctx.Principal?.FindAll(options.TokenValidationParameters.RoleClaimType).Select(r => r.Value) ?? [];
            log.LogInformation("AUTH OK su {Path} — ruoli: {Roles}",
                ctx.Request.Path,
                roles.Any() ? string.Join(",", roles) : "nessuno");
            return Task.CompletedTask;
        }
    };
});

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
});

startupLogger.LogInformation("Auth0 configurato — authority: {Authority}, audience: {Audience}",
    auth0Authority, auth0Audience);

// ---------------------------------------------------------------------------
// CORS — solo le origini che servono davvero, non piu' AllowAnyOrigin
// ---------------------------------------------------------------------------
// Se serve aggiungere un'origine senza ricompilare, metti EXTRA_CORS_ORIGINS
// su Render con i valori separati da virgola.
var allowedOrigins = new List<string> { "https://heroic853.github.io" };

var extraOrigins = Environment.GetEnvironmentVariable("EXTRA_CORS_ORIGINS");
if (!string.IsNullOrWhiteSpace(extraOrigins))
    allowedOrigins.AddRange(extraOrigins.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

builder.Services.AddCors(options =>
{
    options.AddPolicy("SiteOnly", policy =>
    {
        policy.WithOrigins(allowedOrigins.ToArray())
              .AllowAnyHeader()
              .AllowAnyMethod();

        // In sviluppo accetta qualsiasi porta di localhost, cosi' il dev server
        // Blazor funziona senza dover indovinare la porta.
        if (builder.Environment.IsDevelopment())
            policy.SetIsOriginAllowed(origin =>
                Uri.TryCreate(origin, UriKind.Absolute, out var uri) && uri.IsLoopback);
    });
});

startupLogger.LogInformation("CORS consentito per: {Origins}", string.Join(", ", allowedOrigins));

// ---------------------------------------------------------------------------
// RESPONSE COMPRESSION — meno byte sul filo, risposte piu' rapide
// ---------------------------------------------------------------------------
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<BrotliCompressionProvider>();
    options.Providers.Add<GzipCompressionProvider>();
});
builder.Services.Configure<BrotliCompressionProviderOptions>(o => o.Level = CompressionLevel.Fastest);
builder.Services.Configure<GzipCompressionProviderOptions>(o => o.Level = CompressionLevel.Fastest);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// EmailService esisteva da tempo ma non era mai stato registrato, quindi non
// veniva mai usato da nessuno. Ora serve al webhook Stripe e al report visite.
builder.Services.AddScoped<EmailService>();

// Scalda il database in background appena l'app e' su
builder.Services.AddHostedService<DatabaseWarmupService>();

startupLogger.LogInformation("SERVICES CONFIGURED");
var app = builder.Build();
startupLogger.LogInformation("CASTLE BUILT");

var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Heroic853");

// ---------------------------------------------------------------------------
// STRIPE — controlla la chiave all'avvio, non al primo pagamento
// ---------------------------------------------------------------------------
// Prima la variabile d'ambiente (produzione su Render), poi la configurazione
// (in locale: user secrets). Cosi' lo stesso codice funziona in entrambi i casi
// senza mai scrivere una chiave in un file tracciato da git.
string? Secret(string name) =>
    Environment.GetEnvironmentVariable(name) is { Length: > 0 } fromEnv
        ? fromEnv
        : app.Configuration[name];

var stripeKey = Secret("STRIPE_SECRET_KEY");
if (string.IsNullOrWhiteSpace(stripeKey))
{
    logger.LogError("STRIPE_SECRET_KEY non impostata — /create-checkout restituira' errore 503");
}
else
{
    StripeConfiguration.ApiKey = stripeKey;
    var mode = stripeKey.StartsWith("sk_live_") ? "LIVE (soldi veri)"
             : stripeKey.StartsWith("sk_test_") ? "TEST"
             : "sconosciuto — la chiave non sembra una secret key valida";
    logger.LogInformation("Stripe configurato in modalita': {Mode}", mode);
}

// Controlla le altre variabili, dicendo cosa smette di funzionare se mancano.
// Meglio scoprirlo dal log all'avvio che quando un cliente paga.
var richieste = new (string Nome, string A_cosa_serve)[]
{
    ("AUTH0_DOMAIN",           "cancellazione account"),
    ("AUTH0_M2M_CLIENT_ID",    "cancellazione account"),
    ("AUTH0_M2M_CLIENT_SECRET","cancellazione account"),
    ("STRIPE_WEBHOOK_SECRET",  "ricevuta al cliente e notifica di vendita"),
    ("SENDGRID_API_KEY",       "invio di qualsiasi email"),
    ("NOTIFY_EMAIL",           "destinatario di notifiche e report"),
    ("CRON_SECRET",            "report giornaliero delle visite"),
};

foreach (var (nome, aCosaServe) in richieste)
{
    if (string.IsNullOrWhiteSpace(Secret(nome)))
        logger.LogWarning("{Name} non impostata — non funzionera': {A}", nome, aCosaServe);
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    logger.LogInformation("Swagger attivo su /swagger");
}

app.UseResponseCompression();
app.UseMiddleware<RequestLoggingMiddleware>();
app.UseCors("SiteOnly");
app.UseAuthentication(); // Prima di UseAuthorization
app.UseAuthorization();

app.MapGet("/", () => Results.Ok(new { message = "API is running!", timestamp = DateTime.UtcNow }));
app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }));

app.MapControllers();

// app.Urls.Add() SOVRASCRIVE l'applicationUrl di launchSettings: prima forzava
// sempre la 8080, quindi avviando da Visual Studio il browser si apriva su
// localhost:5176 e non trovava niente. Ora la porta la forza solo se c'e' la
// variabile PORT, che e' quella che imposta Render in produzione.
var port = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrWhiteSpace(port))
{
    app.Urls.Add($"http://0.0.0.0:{port}");
    logger.LogInformation("STARTING ON DEMON PORT {Port}", port);
}
else
{
    logger.LogInformation("PORT non impostata: uso gli URL di launchSettings / ASPNETCORE_URLS");
}

app.Run();

// Estrae solo l'host dalla connection string, per non stampare la password nei log
static string DescribeHost(string connectionString)
{
    try
    {
        if (connectionString.StartsWith("postgres://") || connectionString.StartsWith("postgresql://"))
            return new Uri(connectionString).Host;

        var hostPart = connectionString
            .Split(';', StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault(p => p.TrimStart().StartsWith("Host=", StringComparison.OrdinalIgnoreCase));

        return hostPart?.Split('=', 2)[1].Trim() ?? "sconosciuto";
    }
    catch
    {
        return "sconosciuto";
    }
}
