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
    opt.UseNpgsql(connectionString, npg => npg.MigrationsHistoryTable("__ef_migrations", "identity")));

builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<SigningKeyProvider>();
builder.Services.AddScoped<TokenService>();
builder.Services.AddScoped<AuditService>();
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
            }
        };
    });
builder.Services.AddAuthorization();

var app = builder.Build();

// --- Migrate + seed on boot (Railway has no shell step) --------------------
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
    await db.Database.MigrateAsync();
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
