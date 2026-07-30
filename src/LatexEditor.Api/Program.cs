using LatexEditor.Api.Hubs;
using LatexEditor.Application.Services;
using LatexEditor.Core.Entities;
using LatexEditor.Core.Interfaces;
using LatexEditor.Infrastructure.Data;
using LatexEditor.Infrastructure.Compile;
using LatexEditor.Infrastructure.Storage;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using LatexEditor.Infrastructure.HealthChecks;
using LatexEditor.Infrastructure.Telemetry;
using OpenTelemetry.Trace;
using Prometheus;
using Scalar.AspNetCore;
using Serilog;
using Serilog.Formatting.Compact;
using System.Security.Claims;
using System.Threading.RateLimiting;

DotNetEnv.Env.TraversePath().Load();

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console(new CompactJsonFormatter()));

builder.Services.AddControllers();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        options.Password.RequireDigit = false;
        options.Password.RequiredLength = 6;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequireUppercase = false;
        options.Password.RequireLowercase = false;
        options.SignIn.RequireConfirmedEmail =
            builder.Configuration.GetValue("Authentication:RequireConfirmedEmail", false);
    })
.AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.Events.OnRedirectToLogin = context =>
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return Task.CompletedTask;
    };
    options.Events.OnRedirectToAccessDenied = context =>
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        return Task.CompletedTask;
    };
});

var authBuilder = builder.Services.AddAuthentication();

var googleClientId = builder.Configuration["Authentication:Google:ClientId"];
var googleClientSecret = builder.Configuration["Authentication:Google:ClientSecret"];
if (!string.IsNullOrWhiteSpace(googleClientId) && !string.IsNullOrWhiteSpace(googleClientSecret))
{
    authBuilder.AddGoogle(options =>
    {
        options.ClientId = googleClientId;
        options.ClientSecret = googleClientSecret;
    });
}

var githubClientId = builder.Configuration["Authentication:GitHub:ClientId"];
var githubClientSecret = builder.Configuration["Authentication:GitHub:ClientSecret"];
if (!string.IsNullOrWhiteSpace(githubClientId) && !string.IsNullOrWhiteSpace(githubClientSecret))
{
    authBuilder.AddGitHub(options =>
    {
        options.ClientId = githubClientId;
        options.ClientSecret = githubClientSecret;
        options.Scope.Add("user:email");
    });
}

builder.Services.AddAuthorization();

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("compile", httpContext =>
    {
        // Read config per request so test/host overrides applied late are honored.
        var config = httpContext.RequestServices.GetRequiredService<IConfiguration>();
        var permitLimit = config.GetValue("RateLimiting:CompilePermitLimit", 5);
        var windowSeconds = config.GetValue("RateLimiting:CompileWindowSeconds", 60);

        var partitionKey = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? httpContext.Connection.RemoteIpAddress?.ToString()
            ?? "anonymous";
        return RateLimitPartition.GetSlidingWindowLimiter(
            partitionKey,
            _ => new SlidingWindowRateLimiterOptions
            {
                PermitLimit = permitLimit,
                Window = TimeSpan.FromSeconds(windowSeconds),
                SegmentsPerWindow = 4,
                QueueLimit = 0
            });
    });
});

builder.Services.Configure<StorageOptions>(builder.Configuration.GetSection(StorageOptions.SectionName));

// Resolved lazily so host/test configuration overrides applied late are honored.
builder.Services.AddSingleton<IFileStorage>(sp =>
{
    var configuration = sp.GetRequiredService<IConfiguration>();
    var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<StorageOptions>>();
    return configuration["Storage:Provider"]?.Equals("S3", StringComparison.OrdinalIgnoreCase) == true
        ? new S3FileStorage(options)
        : (IFileStorage)new LocalFileStorage(options);
});

builder.Services.AddScoped<IProjectRepository, ProjectRepository>();
builder.Services.AddScoped<IProjectFileRepository, ProjectFileRepository>();
builder.Services.AddScoped<ICompileJobRepository, CompileJobRepository>();
builder.Services.AddScoped<ProjectService>();
builder.Services.AddScoped<ProjectFileService>();
builder.Services.AddScoped<CompileService>();

builder.Services.Configure<LatexEditor.Infrastructure.Email.EmailOptions>(
    builder.Configuration.GetSection(LatexEditor.Infrastructure.Email.EmailOptions.SectionName));

// Resolved lazily: Resend when an API key is configured, SMTP otherwise
// (which itself falls back to log-only when no host is set).
builder.Services.AddHttpClient<LatexEditor.Infrastructure.Email.ResendEmailSender>(
    client => client.BaseAddress = new Uri("https://api.resend.com/"));
builder.Services.AddSingleton<LatexEditor.Infrastructure.Email.SmtpEmailSender>();
builder.Services.AddSingleton<IEmailSender>(sp =>
    !string.IsNullOrWhiteSpace(sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<LatexEditor.Infrastructure.Email.EmailOptions>>().Value.ResendApiKey)
        ? sp.GetRequiredService<LatexEditor.Infrastructure.Email.ResendEmailSender>()
        : (IEmailSender)sp.GetRequiredService<LatexEditor.Infrastructure.Email.SmtpEmailSender>());

builder.Services.Configure<TectonicOptions>(builder.Configuration.GetSection(TectonicOptions.SectionName));
builder.Services.AddSingleton<ICompileQueue, ChannelCompileQueue>();
builder.Services.AddSingleton<ITectonicCompiler, TectonicCompiler>();
builder.Services.AddScoped<CompileJobProcessor>();
builder.Services.AddHostedService<CompileWorker>();

builder.Services.AddSignalR();
builder.Services.AddSingleton<ICompileEventPublisher, SignalRCompileEventPublisher>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, "LatexEditor.Api.xml"));
});

builder.Services.AddOpenTelemetry()
    .WithTracing(tracing =>
    {
        tracing.AddAspNetCoreInstrumentation();
        tracing.AddSource(CompileTelemetry.ActivitySourceName);
        var otlpEndpoint = builder.Configuration["OpenTelemetry:OtlpEndpoint"];
        if (!string.IsNullOrWhiteSpace(otlpEndpoint))
            tracing.AddOtlpExporter(options => options.Endpoint = new Uri(otlpEndpoint));
    });

builder.Services.AddHealthChecks()
    .AddNpgSql(builder.Configuration.GetConnectionString("DefaultConnection")!, name: "postgresql", tags: ["ready"])
    .AddCheck<TectonicHealthCheck>("tectonic", tags: ["ready"])
    .AddCheck<StorageHealthCheck>("storage", tags: ["ready"]);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.MapScalarApiReference(options =>
        options.WithOpenApiRoutePattern("/swagger/v1/swagger.json"));
}

app.UseSerilogRequestLogging();

app.UseAuthentication();
app.UseRouting();
app.UseRateLimiter();
app.UseAuthorization();
app.MapControllers();
app.MapHub<ProjectHub>("/hubs/projects");
app.MapMetrics();
app.MapHealthChecks("/health");
app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
}

app.Run();

public partial class Program;
