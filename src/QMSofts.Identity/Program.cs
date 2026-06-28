using Microsoft.EntityFrameworkCore;
using QMSofts.Identity;
using QMSofts.Identity.Data;
using QMSofts.Identity.Services;

var builder = WebApplication.CreateBuilder(args);

// Railway provides PORT; bind to it.
var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

// --- Configuration sanity (fail fast) -------------------------------------
// Resolve the connection from several sources, treating empty/whitespace as
// "not set" (appsettings ships an empty placeholder, and Railway injects
// DATABASE_URL). First non-blank wins.
static string? FirstNonBlank(params string?[] values) =>
    values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));

var connectionString = FirstNonBlank(
        Environment.GetEnvironmentVariable("DATABASE_URL"),
        builder.Configuration["ConnectionStrings:Identity"],
        builder.Configuration.GetConnectionString("Identity"))
    ?? throw new InvalidOperationException(
        "No database connection. Set DATABASE_URL or ConnectionStrings:Identity.");

// Railway/Heroku-style URLs need converting to Npgsql keyword form.
connectionString = NpgsqlConnectionHelper.Normalize(connectionString);

// --- Services --------------------------------------------------------------
builder.Services.AddDbContext<IdentityDbContext>(opt =>
    opt.UseNpgsql(connectionString));

builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<SigningKeyProvider>();
builder.Services.AddScoped<TokenService>();
builder.Services.AddScoped<AuditService>();
builder.Services.AddScoped<SettingsService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<IdentitySeeder>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// The Shell (browser) calls Identity directly; allow its origin.
var shellOrigins = builder.Configuration.GetSection("QmsAuth:AllowedOrigins").Get<string[]>()
    ?? Array.Empty<string>();
builder.Services.AddCors(o => o.AddPolicy("shell", p =>
    p.WithOrigins(shellOrigins).AllowAnyHeader().AllowAnyMethod()));

// Identity issues tokens AND protects its own admin endpoints with them.
// It validates against its own signing key (the same provider that signs them).
builder.Services
    .AddAuthentication(Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        var authority = builder.Configuration["QmsAuth:Authority"] ?? $"http://localhost:{port}";
        var audience = builder.Configuration["QmsAuth:Audience"] ?? "qmsofts";

        // Resolve the singleton key provider to validate our own signatures.
        options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = authority,
            ValidateAudience = true,
            ValidAudience = audience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = QMSofts.Shared.Auth.QmsClaimTypes.Name,
            RoleClaimType = QMSofts.Shared.Auth.QmsClaimTypes.Role
        };
        options.Events = new Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerEvents
        {
            OnMessageReceived = ctx =>
            {
                var keys = ctx.HttpContext.RequestServices.GetRequiredService<SigningKeyProvider>();
                ctx.Options.TokenValidationParameters.IssuerSigningKey = keys.SecurityKey;
                return Task.CompletedTask;
            },
            // Enforce force-logout and single-session on Identity's own protected
            // endpoints. Apps (ERES/Parakh) will apply equivalent checks themselves.
            OnTokenValidated = async ctx =>
            {
                var principal = ctx.Principal;
                if (principal is null) { ctx.Fail("No principal."); return; }

                var idStr = principal.FindFirst(QMSofts.Shared.Auth.QmsClaimTypes.UserId)?.Value;
                if (!Guid.TryParse(idStr, out var userId)) return; // not a user token

                var db = ctx.HttpContext.RequestServices
                    .GetRequiredService<QMSofts.Identity.Data.IdentityDbContext>();
                var user = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions
                    .FirstOrDefaultAsync(db.Users, u => u.Id == userId);
                if (user is null) { ctx.Fail("User no longer exists."); return; }
                if (user.Status != QMSofts.Identity.Models.UserStatus.Active)
                { ctx.Fail("Account is not active."); return; }

                // Single-session: token's sid must match the user's active session.
                var sid = principal.FindFirst(QMSofts.Shared.Auth.QmsClaimTypes.SessionId)?.Value;
                if (!string.IsNullOrEmpty(sid) && !string.IsNullOrEmpty(user.ActiveSessionId)
                    && sid != user.ActiveSessionId)
                { ctx.Fail("Session superseded by a newer login."); return; }

                // Force-logout: reject tokens issued before forceLogoutAt.
                if (user.ForceLogoutAt is { } flo)
                {
                    var iatClaim = principal.FindFirst("iat")?.Value;
                    if (long.TryParse(iatClaim, out var iat))
                    {
                        var issuedAt = DateTimeOffset.FromUnixTimeSeconds(iat);
                        if (issuedAt < flo) { ctx.Fail("Signed out by an administrator."); return; }
                    }
                }
            }
        };
    });
builder.Services.AddAuthorization();

var app = builder.Build();

// --- Create schema + seed on boot (Railway has no shell step) --------------
// EnsureCreated() is unreliable when the database has any leftover EF state
// (it silently no-ops). To be deterministic, we check whether our actual table
// exists and, if not, generate and run the create-schema SQL directly from the
// model. This does not depend on migration discovery or EnsureCreated's
// internal "already exists" heuristic.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    var tablesExist = false;
    try
    {
        var conn = db.Database.GetDbConnection();
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "SELECT to_regclass('identity.\"Roles\"') IS NOT NULL;";
        var result = await cmd.ExecuteScalarAsync();
        tablesExist = result is bool b && b;
        await conn.CloseAsync();
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "Could not check for existing tables; will attempt to create.");
    }

    if (!tablesExist)
    {
        logger.LogInformation("Schema not found. Generating and creating schema from model.");
        // Clear any partial 'identity' schema so the generated CREATE never
        // collides with leftover objects, then build fresh from the model.
        await db.Database.ExecuteSqlRawAsync("DROP SCHEMA IF EXISTS identity CASCADE;");
        var createSql = db.Database.GenerateCreateScript();
        await db.Database.ExecuteSqlRawAsync(createSql);
        logger.LogInformation("Schema created.");
    }
    else
    {
        logger.LogInformation("Schema already present.");
    }

    await scope.ServiceProvider.GetRequiredService<IdentitySeeder>().SeedAsync();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("shell");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

app.Run();
