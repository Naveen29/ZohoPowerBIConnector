using System.Text;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.IdentityModel.Tokens;
using System.Reflection;
using Microsoft.OpenApi.Models;
using ZohoPowerBIConnector.WebAPI.Data;
using ZohoPowerBIConnector.WebAPI.Models;
using ZohoPowerBIConnector.Services.ZohoSyncService;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    // Add JWT auth to Swagger
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\"",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            new string[] { }
        }
    });

    // Include XML comments if available
    var xmlFile = (Assembly.GetExecutingAssembly().GetName().Name ?? "ZohoPowerBIConnector.WebAPI") + ".xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath)) options.IncludeXmlComments(xmlPath);
});
builder.Services.AddDataProtection();
builder.Services.AddHttpClient();

// Initialize Zoho Sync Service configuration
Config.Initialize(builder.Configuration);

// Register Zoho Sync Services
//builder.Services.AddSingleton<DatabaseService>(sp => new DatabaseService(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddSingleton<DatabaseService>(sp =>
{
    var protectionProvider = sp.GetRequiredService<IDataProtectionProvider>();

    return new DatabaseService(
        protectionProvider,
        builder.Configuration.GetConnectionString("DefaultConnection"));
});
builder.Services.AddHostedService<ZohoSyncBackgroundService>();

// Bind ZohoModules from configuration for dynamic module support
var zohoModules = builder.Configuration.GetSection("ZohoModules").Get<List<ZohoPowerBIConnector.WebAPI.Models.ZohoModule>>()
                  ?? new List<ZohoPowerBIConnector.WebAPI.Models.ZohoModule>();
builder.Services.AddSingleton(zohoModules);

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection") ??
        "Server=(localdb)\\mssqllocaldb;Database=ZohoPowerBIDb;Trusted_Connection=True;MultipleActiveResultSets=true"));

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    var secret = builder.Configuration["Jwt:Key"] ?? "ReplaceWithStrongSecretKeyForDevelopment";
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = false,
        ValidateAudience = false,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)),
        RoleClaimType = ClaimTypes.Role
    };
});

builder.Services.AddAuthorization();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy
            .WithOrigins(
                "http://localhost:5000",
                "https://localhost:5000",
                "http://localhost:4200",
                "https://localhost:4200"
            )
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

// Add in-memory cache for rate limiting and other features
builder.Services.AddMemoryCache();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseDefaultFiles(new DefaultFilesOptions
{
    FileProvider = new PhysicalFileProvider(Path.Combine(app.Environment.ContentRootPath, "..", "Frontend")),
    RequestPath = ""
});

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(Path.Combine(app.Environment.ContentRootPath, "..", "Frontend")),
    RequestPath = ""
});

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

// Enforce HTTPS for all environments (development can use dev cert)
app.UseHttpsRedirection();

// Simple rate limiting middleware (per-IP)
app.UseMiddleware<ZohoPowerBIConnector.WebAPI.Middleware.RateLimitMiddleware>();

app.UseCors("AllowFrontend");
app.UseAuthentication();
app.UseAuthorization();

DbInitializer.Seed(app);

app.MapControllers();

app.Run();
