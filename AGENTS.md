# Agent Guide — Latex Renderer

This document is written for AI coding agents working on the Latex Renderer project. It describes the project as it currently exists, not as it is planned to become. Always check the source files if a detail here seems stale.

## Project Overview

Latex Renderer is a self-hosted, backend-focused platform for editing and compiling LaTeX documents in real time. It is an ASP.NET Core Web API organized around Clean Architecture principles.

The current implementation is an early skeleton focused on the `Project` domain and its REST API surface. Projects and project files are persisted to PostgreSQL via Entity Framework Core. Authentication is implemented with ASP.NET Core Identity and cookie auth; Google and GitHub OAuth providers are wired in but only activate when client credentials are configured. Real-time compile preview, the LaTeX compile pipeline, object storage integration, and observability are planned but not yet implemented.

Repository root: `/home/kaan/Projects/latex-renderer`
Solution file: `LatexEditor.sln`

## Technology Stack

| Concern | Current Choice | Notes |
|---------|---------------|-------|
| Backend framework | ASP.NET Core 8 | Target framework is `net8.0` in all `.csproj` files. |
| Language | C# 12 | Using implicit usings and nullable reference types. |
| Architecture | Clean Architecture | Four layers: `Api`, `Application`, `Core`, `Infrastructure`. |
| Persistence | PostgreSQL + EF Core | `AppDbContext`, `ProjectRepository`, and `ProjectFileRepository`. Migrations are applied at startup in development. In-memory repositories remain in the codebase for reference but are not registered. |
| Auth | ASP.NET Core Identity + cookie auth | `ApplicationUser`, `AppDbContext` integration, `/api/auth` endpoints. Google/GitHub OAuth activate when `Authentication:*:ClientId/ClientSecret` env vars are set. |
| Real-time | Not implemented | SignalR `ProjectHub` is on the roadmap. |
| LaTeX engine | Not implemented | Tectonic integration is planned. |
| Object storage | Not integrated | File content is stored in PostgreSQL alongside metadata. A MinIO container is provided for local development, but the application does not use S3-compatible storage yet. |
| Testing | No test projects | xUnit / WebApplicationFactory / Testcontainers are planned. |
| Deployment | Docker Compose available | `Dockerfile`, `docker-compose.yml`, `.env.example`, and `requests.http` are present. PostgreSQL, MinIO, Redis, and the app start together. Caddy is planned for production. |

## Project Structure

```text
src/
  LatexEditor.Api/              Controllers, middleware, DI registration, launch settings
  LatexEditor.Application/      Services, DTOs, use-case orchestration
  LatexEditor.Core/             Entities, interfaces, domain abstractions
  LatexEditor.Infrastructure/   Repository implementations, EF (planned), storage (planned)
```

### Dependency direction

- `LatexEditor.Core` has no project references.
- `LatexEditor.Application` references `LatexEditor.Core`.
- `LatexEditor.Infrastructure` references `LatexEditor.Core` and `LatexEditor.Application`.
- `LatexEditor.Api` references `LatexEditor.Infrastructure` and `LatexEditor.Application`.

### Key source files

| File | Responsibility |
|------|---------------|
| `src/LatexEditor.Api/Program.cs` | WebApplication setup, DI registration, middleware pipeline. |
| `src/LatexEditor.Api/Controllers/ProjectsController.cs` | `/api/projects` CRUD endpoints. |
| `src/LatexEditor.Api/Controllers/ProjectFilesController.cs` | `/api/projects/{projectId}/files` endpoints. |
| `src/LatexEditor.Application/Services/ProjectService.cs` | Validation and mapping for project operations. |
| `src/LatexEditor.Application/Services/ProjectFileService.cs` | Validation and mapping for project file operations. |
| `src/LatexEditor.Core/Entities/Project.cs` | `Project` entity. |
| `src/LatexEditor.Core/Entities/ProjectFile.cs` | `ProjectFile` entity and `StorageProvider` enum. |
| `src/LatexEditor.Core/Interfaces/IProjectRepository.cs` | Repository contract for projects. |
| `src/LatexEditor.Core/Interfaces/IProjectFileRepository.cs` | Repository contract for project files. |
| `src/LatexEditor.Infrastructure/Data/InMemoryProjectRepository.cs` | In-memory project store. |
| `src/LatexEditor.Infrastructure/Data/InMemoryProjectFileRepository.cs` | In-memory project file store. |
| `Dockerfile` | Multi-stage .NET build image. |
| `docker-compose.yml` | Local development stack: app, PostgreSQL, MinIO, Redis. |
| `.env.example` | Example environment variables for local development. |
| `requests.http` | IDE HTTP client requests for manual endpoint testing. |

### NuGet packages

Only `LatexEditor.Infrastructure` currently references external packages:

- `Microsoft.AspNetCore.Identity.EntityFrameworkCore` 8.0.0
- `Npgsql.EntityFrameworkCore.PostgreSQL` 8.0.0
- `Microsoft.EntityFrameworkCore.Design` 8.0.0

These packages are actively used for PostgreSQL persistence and ASP.NET Core Identity.

## Build and Run Commands

Prerequisites: .NET 8 SDK or later.

Build the solution:

```bash
dotnet build
```

Run the API locally:

```bash
dotnet run --project src/LatexEditor.Api
```

Default URLs from `launchSettings.json`:

- HTTP: `http://localhost:5257`
- HTTPS: `https://localhost:7134` (when using the `https` launch profile)

Run the full local stack with Docker Compose:

```bash
docker compose up --build
```

This starts the API on `http://localhost:5000` with PostgreSQL on `5432`, MinIO on `9000`/`9001`, and Redis on `6379`. EF Core migrations are applied automatically when the app starts.

## Current API Surface

All endpoints except `/api/auth` require authentication via cookie. Requests are owner-scoped against the authenticated user's ID.

### Projects

```text
GET    /api/projects
POST   /api/projects
GET    /api/projects/{id:guid}
PUT    /api/projects/{id:guid}
DELETE /api/projects/{id:guid}
```

### Project files

```text
GET    /api/projects/{projectId:guid}/files
GET    /api/projects/{projectId:guid}/files/{path}
PUT    /api/projects/{projectId:guid}/files/{path}
DELETE /api/projects/{projectId:guid}/files/{path}
```

### Planned API surface (not implemented)

```text
/api/auth
  POST /register
  POST /login
  POST /logout
  GET  /external-login?provider=Google|GitHub
  POST /external-login-callback

/api/projects/{id}/compile
  POST /

/api/projects/{id}/jobs
  GET /
  GET /{jobId}/pdf

/hubs/projects
  SignalR hub for real-time compile events
```

## Code Style Guidelines

The project uses standard ASP.NET Core conventions:

- Implicit usings are enabled in all projects.
- Nullable reference types are enabled in all projects.
- Target framework is `net8.0`.
- File-scoped namespaces are used.
- Primary constructors are used where appropriate (controllers and services).
- DTOs use init/optional properties with `string.Empty` defaults.
- Entity properties are public get/set with `DateTime.UtcNow` defaults for timestamps.
- Repository interfaces live in `LatexEditor.Core`; implementations live in `LatexEditor.Infrastructure`.
- Controllers return `IActionResult` and use standard HTTP status codes (`200 OK`, `204 NoContent`, `404 NotFound`).

There is no `.editorconfig` file currently. If you add one, align it with the existing C# style above.

## Testing Instructions

There are no test projects in the repository yet. The planned testing strategy includes:

- Unit tests with xUnit and a mocking library.
- Integration tests using `WebApplicationFactory`.
- Testcontainers for PostgreSQL integration tests.
- SignalR in-memory test server for hub events.

Until tests are added, the primary verification command is:

```bash
dotnet build
```

## Configuration

Configuration is minimal:

- `src/LatexEditor.Api/appsettings.json` — default logging and `AllowedHosts`.
- `src/LatexEditor.Api/appsettings.Development.json` — development logging override.
- `src/LatexEditor.Api/Properties/launchSettings.json` — launch profiles and URLs.

Sensitive configuration (connection strings, storage credentials) is loaded from environment variables. `appsettings` files do not contain credentials.

- `DotNetEnv` loads a `.env` file at API startup for local `dotnet run`.
- `docker-compose.yml` uses variable substitution (`${VAR_NAME}`) and reads the same `.env` file automatically.
- Copy `.env.example` to `.env` for local development.
- `appsettings.json` and `appsettings.Development.json` only contain non-sensitive logging and host settings.

## Data Model

### Project

```csharp
public class Project
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string OwnerId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
```

### ProjectFile

```csharp
public enum StorageProvider { Local, S3 }

public class ProjectFile
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public string Path { get; set; } = string.Empty;
    public string StorageKey { get; set; } = string.Empty;
    public StorageProvider StorageProvider { get; set; }
    public bool IsBinary { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string Content { get; set; } = string.Empty;
}
```

File content is currently stored in memory alongside metadata. The `StorageKey` and `StorageProvider` fields are populated for future S3/local storage use but are not functional.

## Security Considerations

- Authentication is enforced via `[Authorize]` on project/file controllers. `CurrentUserId` is read from the authenticated user's `NameIdentifier` claim.
- User isolation is enforced both by middleware (authentication) and by repository query parameters (ownerId filtering).
- LaTeX compilation is not implemented yet. When it is added, the project design requires:
  - Tectonic shell escape disabled.
  - Hard timeouts and cancellation tokens on compile jobs.
  - Per-job temporary directories cleaned in a `finally` block.
  - Verification that generated output is a PDF before storing.
- Identity tables are isolated in a dedicated `identity` PostgreSQL schema; application tables remain in the default schema.

## Development Notes

- The `ProjectService` validates that a project name is non-empty and throws `ArgumentException` if it is.
- The `ProjectFileService` requires the owning project to exist before allowing file operations.
- In-memory repositories are registered as singletons in `Program.cs`. Services are registered as scoped.
- The solution was created with Visual Studio / Rider conventions. `.idea/` is ignored.
- Do not commit `bin/`, `obj/`, `.env` files, or publish output. See `.gitignore` for the full list.

## Roadmap Context

The project is intentionally a backend skeleton right now. The next likely areas of work are:

1. `ProjectFile` storage abstraction (`IFileStorage`) with local disk and S3-compatible implementations.
4. Tectonic integration and the compile pipeline (`ICompileQueue`, `ITectonicCompiler`, background service).
5. SignalR `ProjectHub` for real-time compile events.
6. Unit and integration test projects.

When making changes, keep the Clean Architecture boundaries intact: domain logic and interfaces belong in `Core`, use-case orchestration in `Application`, and concrete technology implementations in `Infrastructure`.
