using LatexEditor.Application.Services;
using LatexEditor.Core.Interfaces;
using LatexEditor.Infrastructure.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.Services.AddSingleton<IProjectRepository, InMemoryProjectRepository>();
builder.Services.AddScoped<ProjectService>();

var app = builder.Build();

app.UseRouting();
app.MapControllers();

app.Run();
