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
- `IProjectRepository` abstraction with an in-memory implementation for early iteration.
- `ProjectService` with validation and mapping to `ProjectDto`.
- `ProjectsController` endpoints:
  - `GET    /api/projects`
  - `POST   /api/projects`
  - `GET    /api/projects/{id:guid}`
- Owner-scoped queries: every project operation is filtered by the current user's `OwnerId`. Authentication is not wired up yet, so the current user is temporarily hardcoded as `demo-user`.

### Roadmap / TODO

- `PUT    /api/projects/{id:guid}`
- `DELETE /api/projects/{id:guid}`
- PostgreSQL + Entity Framework Core persistence, replacing the in-memory store.
- `ProjectFile` entity and `/api/projects/{id}/files` endpoints.
- Tectonic integration and `ICompileQueue` / `ITectonicCompiler` abstractions.
- `/api/projects/{id}/compile` and `/api/projects/{id}/jobs` endpoints.
- SignalR `ProjectHub` for real-time compile events.
- ASP.NET Core Identity, cookie authentication, and Google/GitHub OAuth.
- Docker Compose setup with PostgreSQL and optional MinIO.
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

Run the API:

```bash
dotnet run --project src/LatexEditor.Api
```

The API will be available at `http://localhost:5000` (or the configured `launchSettings` URL).

> Data is currently stored in memory and lost on restart. Database persistence is on the roadmap.

## Design Notes

- The compile worker runs in-process in v1. The `ICompileQueue` abstraction allows swapping to RabbitMQ/Redis and extracting the worker into its own service later.
- `IFileStorage` has a local implementation for development and a planned S3-compatible implementation for production object stores.
- LaTeX compilation runs with shell escape disabled, a hard timeout, and per-job temporary directories cleaned in a `finally` block.
- Identity tables will be isolated in a dedicated `identity` PostgreSQL schema, separate from application data.

## License

MIT
