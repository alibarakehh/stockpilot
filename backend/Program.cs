using System.Text.Json.Serialization;
using System.Security.Claims;
using InventoryApi.Data;
using InventoryApi.Infrastructure;
using InventoryApi.Models;
using InventoryApi.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);
const long maxRequestBodySize = 64 * 1024;
builder.WebHost.ConfigureKestrel(options =>
    options.Limits.MaxRequestBodySize = maxRequestBodySize);

builder.Services.AddControllers(options =>
    options.Filters.Add<ApiAntiforgeryFilter>())
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
    ApiProblems.Enrich(context.ProblemDetails, context.HttpContext));
builder.Services.Configure<ApiBehaviorOptions>(options =>
    options.InvalidModelStateResponseFactory = context =>
    {
        var details = new ValidationProblemDetails(context.ModelState)
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Request validation failed",
            Instance = context.HttpContext.Request.Path
        };
        ApiProblems.Enrich(details, context.HttpContext, ApiErrorCodes.ValidationFailed);
        return new BadRequestObjectResult(details);
    });
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-CSRF-TOKEN";
    options.Cookie.Name = builder.Environment.IsDevelopment()
        ? "StockPilot.Antiforgery"
        : "__Host-StockPilot.Antiforgery";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
        ? CookieSecurePolicy.SameAsRequest
        : CookieSecurePolicy.Always;
    options.SuppressXFrameOptionsHeader = true;
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddResponseCompression();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "StockPilot API", Version = "v1" });
    options.AddSecurityDefinition("Cookie", new OpenApiSecurityScheme
    {
        Name = builder.Environment.IsDevelopment() ? "StockPilot.Auth" : "__Host-StockPilot.Auth",
        Type = SecuritySchemeType.ApiKey,
        In = ParameterLocation.Cookie
    });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        [new OpenApiSecurityScheme
        {
            Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Cookie" }
        }] = Array.Empty<string>()
    });
});

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection must be configured.");
builder.Services.AddDbContext<AppDbContext>(options => options.UseSqlServer(
    connectionString,
    sqlServer => sqlServer.EnableRetryOnFailure()));
builder.Services.AddDataProtection()
    .SetApplicationName("StockPilot")
    .PersistKeysToDbContext<AppDbContext>();
builder.Services.AddHsts(options =>
{
    options.MaxAge = TimeSpan.FromDays(365);
    options.IncludeSubDomains = true;
});
builder.Services.AddHttpsRedirection(options =>
{
    options.HttpsPort = 443;
    options.RedirectStatusCode = StatusCodes.Status308PermanentRedirect;
});
builder.Services.AddScoped<IInventoryService, InventoryService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IAiInventoryService, AiInventoryService>();
builder.Services.Configure<AiSmartIntakeOptions>(
    builder.Configuration.GetSection(AiSmartIntakeOptions.SectionName));
builder.Services.AddHttpClient<OpenAiInventoryDraftProvider>((services, client) =>
{
    var options = services.GetRequiredService<Microsoft.Extensions.Options.IOptions<AiSmartIntakeOptions>>().Value;
    client.Timeout = TimeSpan.FromSeconds(Math.Clamp(options.TimeoutSeconds, 5, 60));
});
builder.Services.AddScoped<DisabledAiInventoryDraftProvider>();
builder.Services.AddScoped<IAiInventoryDraftProvider>(services =>
{
    var options = services.GetRequiredService<Microsoft.Extensions.Options.IOptions<AiSmartIntakeOptions>>().Value;
    return options.Enabled
        && options.Provider.Equals("OpenAI", StringComparison.OrdinalIgnoreCase)
        && !string.IsNullOrWhiteSpace(options.ApiKey)
        ? services.GetRequiredService<OpenAiInventoryDraftProvider>()
        : services.GetRequiredService<DisabledAiInventoryDraftProvider>();
});
builder.Services.AddScoped<IAiInventoryDraftService, AiInventoryDraftService>();
builder.Services.AddScoped<ApiAntiforgeryFilter>();
builder.Services.AddScoped<IAuthorizationHandler, WorkspacePermissionHandler>();
builder.Services.AddIdentityCore<ApplicationUser>(options =>
    {
        options.User.RequireUniqueEmail = true;
        options.Password.RequiredLength = 10;
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireNonAlphanumeric = true;
        options.Lockout.AllowedForNewUsers = true;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    })
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = builder.Environment.IsDevelopment()
            ? "StockPilot.Auth"
            : "__Host-StockPilot.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
            ? CookieSecurePolicy.SameAsRequest
            : CookieSecurePolicy.Always;
        options.ExpireTimeSpan = TimeSpan.FromHours(
            builder.Configuration.GetValue("Authentication:ExpiryHours", 8));
        options.SlidingExpiration = false;
        options.Events = new CookieAuthenticationEvents
        {
            OnRedirectToLogin = context =>
                ApiProblems.WriteAsync(
                    context.HttpContext,
                    StatusCodes.Status401Unauthorized,
                    "Authentication required",
                    ApiErrorCodes.AuthenticationRequired),
            OnRedirectToAccessDenied = context =>
                ApiProblems.WriteAsync(
                    context.HttpContext,
                    StatusCodes.Status403Forbidden,
                    "Permission denied",
                    ApiErrorCodes.PermissionDenied)
        };
    });
builder.Services.AddAuthorization(options =>
{
    options.DefaultPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .AddRequirements(new WorkspacePermissionRequirement())
        .Build();
    options.AddPolicy(StockPilotPolicies.AuthenticatedSession, policy =>
        policy.RequireAuthenticatedUser());
    options.AddPolicy(StockPilotPolicies.ManageInventory, policy =>
        policy.AddRequirements(new WorkspacePermissionRequirement(AppRoles.Admin, AppRoles.Manager)));
    options.AddPolicy(StockPilotPolicies.ArchiveInventory, policy =>
        policy.AddRequirements(new WorkspacePermissionRequirement(AppRoles.Admin)));
    options.AddPolicy(StockPilotPolicies.ManageTeam, policy =>
        policy.AddRequirements(new WorkspacePermissionRequirement(AppRoles.Admin)));
});
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("login", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = builder.Configuration.GetValue("Authentication:LoginRateLimit", 10),
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
            AutoReplenishment = true
        }));
    options.AddPolicy("ai", context => RateLimitPartition.GetFixedWindowLimiter(
        context.User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? context.Connection.RemoteIpAddress?.ToString()
            ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = builder.Configuration.GetValue("AI:SmartIntake:RateLimit", 5),
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
            AutoReplenishment = true
        }));
});
builder.Services.AddCors(options => options.AddPolicy("Frontend", policy => policy
    .WithOrigins(builder.Configuration.GetSection("Cors:Origins").Get<string[]>()
        ?? ["http://localhost:5173"])
    .AllowAnyHeader()
    .AllowAnyMethod()
    .AllowCredentials()));

var app = builder.Build();

app.UseExceptionHandler();
if (builder.Configuration.GetValue("Deployment:EnforceHttps", false))
{
    app.UseHsts();
    app.UseHttpsRedirection();
}
app.UseStatusCodePages(async context =>
{
    var status = context.HttpContext.Response.StatusCode;
    await ApiProblems.WriteAsync(
        context.HttpContext,
        status,
        status == StatusCodes.Status404NotFound ? "Resource not found" : "Request failed",
        ApiProblems.CodeForStatus(status));
});
app.UseResponseCompression();
app.Use(async (context, next) =>
{
    context.Response.Headers.XContentTypeOptions = "nosniff";
    context.Response.Headers.XFrameOptions = "DENY";
    context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    context.Response.Headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
    if (!app.Environment.IsDevelopment() || !context.Request.Path.StartsWithSegments("/swagger"))
    {
        context.Response.Headers["Content-Security-Policy"] =
            "default-src 'self'; script-src 'self'; style-src 'self'; img-src 'self' data:; " +
            "connect-src 'self'; object-src 'none'; frame-ancestors 'none'; base-uri 'self'; " +
            "form-action 'self'";
    }
    await next();
});
app.Use(async (context, next) =>
{
    if (context.Request.ContentLength > maxRequestBodySize)
    {
        await ApiProblems.WriteAsync(
            context,
            StatusCodes.Status413PayloadTooLarge,
            "Request body is too large",
            ApiErrorCodes.RequestTooLarge,
            $"Request bodies are limited to {maxRequestBodySize / 1024} KB.");
        return;
    }

    await next();
});

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("Frontend");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapGet("/api/health/live", () => Results.Ok(new { status = "healthy", utc = DateTime.UtcNow }))
    .AllowAnonymous();
app.MapGet("/api/health", DatabaseHealthAsync)
    .AllowAnonymous();
app.MapControllers();
app.Map("/api/{**path}", () => Results.Problem(
    statusCode: StatusCodes.Status404NotFound,
    title: "API endpoint not found"));
if (Directory.Exists(app.Environment.WebRootPath))
{
    app.UseDefaultFiles();
    app.UseStaticFiles(new StaticFileOptions
    {
        OnPrepareResponse = context =>
        {
            if (context.Context.Request.Path.StartsWithSegments("/assets"))
                context.Context.Response.Headers.CacheControl = "public,max-age=31536000,immutable";
        }
    });
    app.MapFallbackToFile("index.html");
}

if (args.Contains("--deploy", StringComparer.OrdinalIgnoreCase))
{
    await SeedData.MigrateAsync(app.Services);
    await SeedData.BootstrapAdminAsync(app.Services);
    return;
}
if (args.Contains("--migrate", StringComparer.OrdinalIgnoreCase))
{
    await SeedData.MigrateAsync(app.Services);
    return;
}
if (args.Contains("--bootstrap-admin", StringComparer.OrdinalIgnoreCase))
{
    await SeedData.BootstrapAdminAsync(app.Services);
    return;
}
if (!builder.Configuration.GetValue("SkipStartupDatabaseInitialization", false))
    await SeedData.InitializeAsync(app.Services);
await app.RunAsync();

static async Task<IResult> DatabaseHealthAsync(
    AppDbContext db,
    IConfiguration configuration,
    CancellationToken cancellationToken)
{
    var canConnect = await db.Database.CanConnectAsync(cancellationToken);
    var migrationCurrent = true;
    if (canConnect && configuration.GetValue("Deployment:RequireCurrentMigration", false))
    {
        var pendingMigrations = await db.Database.GetPendingMigrationsAsync(cancellationToken);
        migrationCurrent = !pendingMigrations.Any();
    }
    if (canConnect && migrationCurrent)
        return Results.Ok(new { status = "healthy", database = "available", utc = DateTime.UtcNow });

    return Results.Json(
        new
        {
            status = "unhealthy",
            database = canConnect ? "migration_required" : "unavailable",
            utc = DateTime.UtcNow
        },
        statusCode: StatusCodes.Status503ServiceUnavailable);
}

public partial class Program { }
