using System.Text;
using System.Threading.RateLimiting;
using HotelManagement.Infrastructure;
using HotelManagement.Infrastructure.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Microsoft.Extensions.FileProviders;
using HotelManagement.API.Middleware;

// Accept DateTime values that aren't tagged UTC (e.g. an <input type="date"> value
// like "2026-09-07" binds as Kind=Unspecified). Without this, Npgsql throws when
// writing them to 'timestamp with time zone' columns — which broke booking create,
// check-in, and any date-filtered query.
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

var builder = WebApplication.CreateBuilder(args);

// Hosting platforms (Render, Railway, Fly, …) tell the app which port to listen
// on via the $PORT environment variable. Locally $PORT is unset and Kestrel uses
// the URLs from launchSettings.json / ASPNETCORE_URLS as before.
var port = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrWhiteSpace(port))
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

builder.Services.AddControllers(o => o.Filters.Add<HotelManagement.API.Middleware.ModuleAccessFilter>());
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "HotelManagement API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header. Example: 'Bearer {token}'",
        Name = "Authorization", In = ParameterLocation.Header, Type = SecuritySchemeType.ApiKey, Scheme = "Bearer"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        { new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } }, Array.Empty<string>() }
    });
});

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opts =>
    {
        opts.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true, ValidateAudience = true, ValidateLifetime = true, ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Secret"]!))
        };
    });

builder.Services.AddAuthorization();

// Throttle login attempts per client IP — BCrypt.Verify is deliberately slow, but
// that alone doesn't stop a distributed credential-stuffing run against
// /api/auth/login. Applied to the login endpoint only via [EnableRateLimiting].
// Note: behind a reverse proxy (Render, etc.) RemoteIpAddress is the proxy hop
// unless forwarded-header trust is configured — this is still a real backstop,
// just not a substitute for an account-lockout policy.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("login", httpContext => RateLimitPartition.GetFixedWindowLimiter(
        partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        factory: _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        }));
});

// Allowed front-end origins come from config ("Cors:Origins") or env vars
// (Cors__Origins__0, Cors__Origins__1, …). Falls back to local dev.
var corsOrigins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>()
                  ?? new[] { "http://localhost:4200" };
builder.Services.AddCors(opts => opts.AddDefaultPolicy(policy =>
    policy.WithOrigins(corsOrigins).AllowAnyHeader().AllowAnyMethod().AllowCredentials()));
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(HotelManagement.Application.DependencyInjection).Assembly));
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await DbInitializer.SeedAsync(db, app.Configuration, app.Environment.IsDevelopment());
}

// Turn unhandled exceptions into a JSON body (and a log line) instead of a bare 500.
app.UseMiddleware<ExceptionMiddleware>();

// Baseline hardening headers on every response. No CSP here — the API serves
// JSON plus static uploads, not HTML pages, so a CSP would mostly protect the
// Swagger UI; the meaningful wins are clickjacking/MIME-sniffing protection for
// the /uploads static files, which anyone with a link can open directly.
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers["Referrer-Policy"] = "no-referrer";
    await next();
});

app.UseCors();

app.UseRateLimiter();

// Serve uploaded files (guest ID proofs, room images) from wwwroot at the root
// path. A fresh container has no wwwroot, so create it first — PhysicalFileProvider
// throws on a missing directory.
var webRoot = Path.Combine(builder.Environment.ContentRootPath, "wwwroot");
Directory.CreateDirectory(Path.Combine(webRoot, "Uploads", "rooms"));
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(webRoot),
    RequestPath = ""
});

// Swagger exposes the full API surface (routes, DTOs, the JWT scheme) — fine for
// local/QA, not something to hand an anonymous internet visitor in Production.
if (!app.Environment.IsProduction())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Behind a hosting proxy (Render, Railway, …) TLS is terminated at the edge and
// the app receives plain HTTP on $PORT, so HTTPS redirection would loop. Only
// enforce it for local development.
if (app.Environment.IsDevelopment())
    app.UseHttpsRedirection();
else
    app.UseHsts();

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
