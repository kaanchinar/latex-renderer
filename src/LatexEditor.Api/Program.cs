using LatexEditor.Api.Hubs;
using LatexEditor.Application.Services;
using LatexEditor.Core.Entities;
using LatexEditor.Core.Interfaces;
using LatexEditor.Infrastructure.Data;
using LatexEditor.Infrastructure.Compile;
using LatexEditor.Infrastructure.Storage;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using System.Security.Claims;
using System.Threading.RateLimiting;

DotNetEnv.Env.TraversePath().Load();

var builder = WebApplication.CreateBuilder(args);

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

var compilePermitLimit = builder.Configuration.GetValue("RateLimiting:CompilePermitLimit", 5);
var compileWindowSeconds = builder.Configuration.GetValue("RateLimiting:CompileWindowSeconds", 60);
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("compile", httpContext =>
    {
        var partitionKey = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? httpContext.Connection.RemoteIpAddress?.ToString()
            ?? "anonymous";
        return RateLimitPartition.GetSlidingWindowLimiter(
            partitionKey,
            _ => new SlidingWindowRateLimiterOptions
            {
                PermitLimit = compilePermitLimit,
                Window = TimeSpan.FromSeconds(compileWindowSeconds),
                SegmentsPerWindow = 4,
                QueueLimit = 0
            });
    });
});

builder.Services.Configure<StorageOptions>(builder.Configuration.GetSection(StorageOptions.SectionName));

var storageProvider = builder.Configuration["Storage:Provider"] ?? "Local";
if (storageProvider.Equals("S3", StringComparison.OrdinalIgnoreCase))
    builder.Services.AddSingleton<IFileStorage, S3FileStorage>();
else
    builder.Services.AddSingleton<IFileStorage, LocalFileStorage>();

builder.Services.AddScoped<IProjectRepository, ProjectRepository>();
builder.Services.AddScoped<IProjectFileRepository, ProjectFileRepository>();
builder.Services.AddScoped<ICompileJobRepository, CompileJobRepository>();
builder.Services.AddScoped<ProjectService>();
builder.Services.AddScoped<ProjectFileService>();
builder.Services.AddScoped<CompileService>();

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

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.MapScalarApiReference(options =>
        options.WithOpenApiRoutePattern("/swagger/v1/swagger.json"));
}

app.UseAuthentication();
app.UseRouting();
app.UseRateLimiter();
app.UseAuthorization();
app.MapControllers();
app.MapHub<ProjectHub>("/hubs/projects");

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
}

app.Run();
