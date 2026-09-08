using System.Text;
using HotelManagement.Application.Common.Mappings;
using HotelManagement.Infrastructure;
using HotelManagement.Infrastructure.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
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

// Allowed front-end origins come from config ("Cors:Origins") or env vars
// (Cors__Origins__0, Cors__Origins__1, …). Falls back to local dev.
var corsOrigins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>()
                  ?? new[] { "http://localhost:4200" };
builder.Services.AddCors(opts => opts.AddDefaultPolicy(policy =>
    policy.WithOrigins(corsOrigins).AllowAnyHeader().AllowAnyMethod().AllowCredentials()));
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(HotelManagement.Application.Common.Mappings.MappingProfile).Assembly));
builder.Services.AddAutoMapper(typeof(MappingProfile));
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await DbInitializer.SeedAsync(db);
}

// Turn unhandled exceptions into a JSON body (and a log line) instead of a bare 500.
app.UseMiddleware<ExceptionMiddleware>();

app.UseCors();

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

//if (app.Environment.IsDevelopment())
//{
//    app.UseSwagger();
//    app.UseSwaggerUI();
//}
app.UseSwagger();
app.UseSwaggerUI();

// Behind a hosting proxy (Render, Railway, …) TLS is terminated at the edge and
// the app receives plain HTTP on $PORT, so HTTPS redirection would loop. Only
// enforce it for local development.
if (app.Environment.IsDevelopment())
    app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
