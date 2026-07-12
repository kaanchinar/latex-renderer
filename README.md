# Latex Renderer

A self-hosted, backend-focused platform for editing and compiling LaTeX documents in real time. It is built as a deployable alternative to cloud-based LaTeX editors, giving users full control over their documents, toolchain, and infrastructure.

The project targets teams and individuals who want a private LaTeX editing environment with real-time preview, versioned file storage, and a compile pipeline they can run on their own server.

## What It Is

- An ASP.NET Core Web API organized around Clean Architecture principles: `Api`, `Application`, `Core`, and `Infrastructure`.
- A project-based workspace where each project contains `.tex` source files, assets, and compiled PDFs.
- A real-time compile-preview loop powered by SignalR.
- A background LaTeX compilation pipeline using [Tectonic](https://tectonic-typesetting.github.io/), a modern, self-contained XeTeX-based engine.
- S3-compatible object storage for source files and generated PDFs, with a local-disk implementation for development.
- PostgreSQL-backed metadata and identity management.
- Docker-based deployment for self-hosted environments.

## What It Is Not

These are intentional boundaries for the current phase, not limitations:

- It is not a multi-user collaborative editor in v1. Real-time collaboration is a planned extension, and the architecture is designed to support it without a rewrite.
- It is not a managed SaaS. The goal is self-hosting, not running a public multi-tenant service.
- It is not a frontend-first project. The user interface is secondary to building a reliable, observable, and well-tested backend.

## Why This Project

LaTeX users who need privacy, custom packages, or long-term control over their documents often prefer to self-host. Existing cloud solutions are convenient but tie users to specific pricing, storage limits, and toolchains. This project aims to provide a solid backend foundation for a self-controlled LaTeX workflow.

## Tech Stack

| Layer | Choice |
|-------|--------|
| Backend | ASP.NET Core |
| Real-time | SignalR |
| Auth | ASP.NET Core Identity + Google/GitHub OAuth |
| Database | PostgreSQL with Entity Framework Core |
| Object storage | Cloudflare R2 (S3-compatible), local disk fallback |
| LaTeX engine | Tectonic |
| Frontend | SPA (React/Vue/Svelte) — planned, not implemented |
| Deployment | Docker + Docker Compose, reverse proxy via Caddy |
| Observability | Serilog, OpenTelemetry, Prometheus, health checks |
| Testing | xUnit, WebApplicationFactory, Testcontainers |

## Project Structure

```text
src/
  LatexEditor.Api/              Controllers, middleware, DI registration
  LatexEditor.Application/      Services, DTOs, use-case orchestration
  LatexEditor.Core/             Entities, interfaces, domain abstractions
  LatexEditor.Infrastructure/   Repositories, EF, storage, Tectonic wrapper
```

## Current Status

The project is in early development. The current focus is the `Project` domain and its REST API surface.

### Implemented

- Solution and layered project skeleton.
- `Project` entity with `Id`, `Name`, `OwnerId`, and `CreatedAt`.
- `IProjectRepository` and `IProjectFileRepository` abstractions with EF Core PostgreSQL implementations.
- `ProjectService` and `ProjectFileService` with validation and mapping to DTOs.
- `ProjectsController` endpoints:
  - `GET    /api/projects`
  - `POST   /api/projects`
  - `GET    /api/projects/{id:guid}`
  - `PUT    /api/projects/{id:guid}`
  - `DELETE /api/projects/{id:guid}`
- `ProjectFilesController` endpoints:
  - `GET    /api/projects/{id:guid}/files`
  - `GET    /api/projects/{id:guid}/files/{path}`
  - `PUT    /api/projects/{id:guid}/files/{path}`
  - `DELETE /api/projects/{id:guid}/files/{path}`
- ASP.NET Core Identity with cookie authentication and Google/GitHub OAuth hooks.
- Owner-scoped queries: every project and file operation is filtered by the authenticated user's ID from `User.FindFirstValue`.
- Docker Compose setup with PostgreSQL, MinIO, and Redis for local development.

### Roadmap / TODO

- Tectonic integration and `ICompileQueue` / `ITectonicCompiler` abstractions.
- `/api/projects/{id}/compile` and `/api/projects/{id}/jobs` endpoints.
- SignalR `ProjectHub` for real-time compile events.
- `IFileStorage` abstraction with local disk and S3-compatible implementations.
- Structured logging, health checks, and metrics.
- Integration tests using `WebApplicationFactory`.

## Planned API Surface

```text
/api/auth
  POST /register
  POST /login
  POST /logout
  GET  /external-login?provider=Google|GitHub
  POST /external-login-callback

/api/projects
  GET    /
  POST   /
  GET    /{id}
  PUT    /{id}
  DELETE /{id}

/api/projects/{id}/files
  GET    /
  GET    /{path}
  PUT    /{path}
  DELETE /{path}

/api/projects/{id}/compile
  POST /

/api/projects/{id}/jobs
  GET /
  GET /{jobId}/pdf

/hubs/projects
  SignalR hub for real-time compile events
```

## Getting Started

> Prerequisites: .NET 8 or later.

Build the solution:

```bash
dotnet build
```

Create a local environment file from the example and switch database host to `localhost`:

```bash
cp .env.example .env
# Edit .env: change Host=postgres to Host=localhost in ConnectionStrings__DefaultConnection
```

Run the API locally:

```bash
dotnet run --project src/LatexEditor.Api
```

The API will be available at `http://localhost:5257` (or the configured `launchSettings` URL). Credentials are read from `.env`, not from `appsettings` files.

### Docker Compose

A `docker-compose.yml` is provided with PostgreSQL, MinIO, and Redis for local development. Make sure `.env` exists (it is loaded automatically by Docker Compose):

```bash
docker compose up --build
```

Services:

| Service | URL | Notes |
|---------|-----|-------|
| App | http://localhost:5000 | ASP.NET Core API |
| PostgreSQL | localhost:5432 | Database for metadata and Identity |
| MinIO API | localhost:9000 | S3-compatible object storage |
| MinIO Console | http://localhost:9001 | Web admin UI |
| Redis | localhost:6379 | Cache / message broker future use |

Default credentials are defined in `.env.example`. `docker-compose.yml` references them through environment variable substitution.

> The app persists projects and files to PostgreSQL. MinIO and Redis containers are running and ready for future object storage and caching features.

### Testing endpoints

Use `requests.http` with an IDE HTTP client (Visual Studio, Rider, or VS Code REST Client extension) to exercise the endpoints. Update the `@projectId` and `@filePath` variables after creating a project.

## Design Notes

- The compile worker runs in-process in v1. The `ICompileQueue` abstraction allows swapping to RabbitMQ/Redis and extracting the worker into its own service later.
- `IFileStorage` has a local implementation for development and a planned S3-compatible implementation for production object stores.
- LaTeX compilation runs with shell escape disabled, a hard timeout, and per-job temporary directories cleaned in a `finally` block.
- Identity tables will be isolated in a dedicated `identity` PostgreSQL schema, separate from application data.

## License

MIT
