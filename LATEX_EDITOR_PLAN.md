# Local Overleaf Alternative — Design Blueprint

 This document is a standalone design spec for a deployable LaTeX editor service.


## Vision

A deployable, backend-heavy .NET service for editing and compiling LaTeX documents.
The initial focus is a single-user experience with real-time compile preview.
Collaboration may be added later, but the v1 architecture must not require a rewrite to support it.

## Goals

- Strengthen a backend .NET developer portfolio.
- Demonstrate real-time communication, background job processing, cloud storage, auth, and observability.
- Start simple and core-focused, then evolve toward collaboration.
- Run on a cheap VPS with Docker.

## Tech Stack

| Layer | Choice |
|-------|--------|
| Backend | ASP.NET Core 8/9 Web API |
| Real-time | SignalR (WebSockets → SSE → Long Polling) |
| Auth | ASP.NET Core Identity + Google + GitHub OAuth |
| Database | PostgreSQL (Entity Framework Core) |
| Object storage | Cloudflare R2 (S3-compatible), local disk fallback for dev |
| LaTeX engine | Tectonic (XeTeX-based, self-contained) |
| Frontend | SPA (React/Vue/Svelte) — secondary concern |
| Deployment | Docker + Docker Compose, VPS behind Caddy |
| Observability | Serilog, OpenTelemetry, Prometheus, health checks |
| Testing | xUnit, WebApplicationFactory, Testcontainers |

## Architecture

A single deployable ASP.NET Core app with clean internal boundaries.
The compile worker and storage backend can be extracted into separate services later without rewriting the domain.

```text
┌─────────────────────────────────────────────┐
│           ASP.NET Core App                  │
│  ┌─────────┐ ┌─────────┐ ┌──────────────┐   │
│  │ Auth    │ │ Project │ │ SignalR Hub  │   │
│  │ API     │ │ API     │ │              │   │ 
│  └────┬────┘ └────┬────┘ └──────┬───────┘   │
│       └─────────────┴─────────────┘         │
│                    │                        │
│         ┌──────────▼──────────┐             │
│         │   Compile Domain    │             │
│         │ (ICompileQueue,     │             │
│         │  BackgroundService, │             │
│         │  Tectonic runner)   │             │
│         └──────────┬──────────┘             │
│                    │                        │
│    ┌───────────────┼───────────────┐        │
│    ▼               ▼               ▼        │
│ PostgreSQL    R2 Storage      Temp disk     │
│ (metadata)    (files/PDFs)    (compile)     │
└─────────────────────────────────────────────┘
```

### Key abstractions

- `ICompileQueue` — v1 backed by an in-memory channel, swappable for RabbitMQ/Redis later.
- `IFileStorage` — v1 local disk implementation, production implementation for R2/MinIO.
- `ITectonicCompiler` — wraps the Tectonic process so tests never shell out directly.

## Data Model

### Entities

**`User`**
Managed by ASP.NET Core Identity. Stored in a dedicated `identity` schema.

**`Project`**
- `Id`, `Name`, `Slug`, `OwnerId`, `CreatedAt`, `UpdatedAt`
- `DefaultEngine` (Tectonic in v1)
- `LastCompileStatus`

**`ProjectFile`**
- `Id`, `ProjectId`, `Path`
- `StorageKey`, `StorageProvider` (Local | S3/R2)
- `IsBinary`, `CreatedAt`, `UpdatedAt`
- Source `.tex` content is stored as objects in R2; metadata lives in PostgreSQL.

**`CompileJob`**
- `Id`, `ProjectId`, `Status` (Queued | Running | Success | Failed | Cancelled)
- `StartedAt`, `CompletedAt`, `DurationMs`
- `StdOut`, `StdErr`, `ErrorMessage`
- `OutputStorageKey`

**`ProjectMember`** *(future collaboration)*
- `ProjectId`, `UserId`, `Role` (Owner | Editor | Viewer)

## Compile Flow

1. User pauses typing; frontend debounce fires (or explicit Ctrl+S).
2. Frontend sends `UpdateFile` and `TriggerCompile` through the SignalR hub.
3. API writes the updated file to R2 and creates a `CompileJob` with status `Queued`.
4. The job is pushed to `ICompileQueue`.
5. A hosted `BackgroundService` dequeues the job.
6. The worker downloads project files into a fresh temp directory.
7. Tectonic runs with a cancellation token and a hard timeout (e.g. 30–60 seconds).
8. On success: the generated PDF is uploaded to R2; the job status is updated.
9. The SignalR hub pushes a `CompileCompleted` event with the PDF URL.
10. The frontend refreshes the preview.

### Real-time contract

```csharp
public interface IProjectClient
{
    Task CompileStarted(Guid jobId);
    Task CompileCompleted(Guid jobId, string pdfUrl);
    Task CompileFailed(Guid jobId, string error);
    Task CompileOutput(string stdoutLine);
}

public class ProjectHub : Hub<IProjectClient>
{
    public async Task JoinProject(Guid projectId) { ... }
    public async Task TriggerCompile(Guid projectId) { ... }
    public async Task UpdateFile(Guid projectId, string path, string content) { ... }
}
```

## API Surface

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
  POST   /

/api/projects/{id}/jobs
  GET    /
  GET    /{jobId}/pdf

/hubs/projects
  SignalR hub for real-time compile events
```

## Authentication

- ASP.NET Core Identity with cookie authentication.
- Email/password registration with confirmation flow.
- Google and GitHub OAuth providers.
- Identity tables isolated in a dedicated `identity` PostgreSQL schema.
- Authorization in v1 is simple ownership checks; later extended to `ProjectMember` roles.

## Security Considerations

LaTeX compilation can execute arbitrary code if shell escape is enabled.

| Risk | Mitigation |
|------|------------|
| Shell escape via `\write18` | Run Tectonic with shell escape disabled |
| Resource exhaustion | Job timeout, memory limits, cancellation tokens |
| Disk filling up | Per-job temp directory cleaned in a `finally` block |
| Reading host files | Compile inside an empty temp dir with no mounted volumes |
| Malicious packages | Pin Tectonic bundle version |
| Abuse | Per-user rate limiting on compile endpoint |
| Invalid output | Verify generated file is a PDF before storing |

## Storage Strategy

- **Cloudflare R2** is the production object store (S3-compatible, no egress fees, generous free tier).
- Local disk implementation is used for development.
- Source files, binary assets, and generated PDFs are stored as objects.
- PostgreSQL stores metadata and pointers (storage keys).
- PDFs are served to the frontend via presigned URLs or short-lived redirect endpoints.

## Deployment

### Container layout

Single Dockerfile bundling the .NET runtime and the Tectonic binary.

```yaml
services:
  app:
    build: .
    ports:
      - "5000:8080"
    env_file: .env
    depends_on:
      - postgres

  postgres:
    image: postgres:16-alpine
    volumes:
      - pgdata:/var/lib/postgresql/data

  minio:
    image: minio/minio
    # optional, for local S3 testing
```

### VPS setup

- Reverse proxy: Caddy for automatic HTTPS.
- Database: PostgreSQL running on the same VPS or managed service.
- Object storage: Cloudflare R2.
- CI/CD: GitHub Actions builds a container image, pushes to GHCR, and deploys to the VPS via SSH.

## Observability

- **Logging**: Serilog with structured JSON output and correlation IDs.
- **Tracing**: OpenTelemetry tracing across the full compile pipeline.
- **Metrics**: Prometheus metrics for compile duration, success/failure rate, queue depth, active SignalR connections.
- **Health checks**: `/health` and `/health/ready` covering PostgreSQL, Tectonic, and R2.
- **Uptime monitoring**: Uptime Kuma or Pingdom with alerts on failure spikes.

## Testing Strategy

- **Unit tests**: xUnit with NSubstitute/Moq; test domain logic and auth policies.
- **Integration tests**: `WebApplicationFactory` with Testcontainers PostgreSQL.
- **SignalR tests**: In-memory test server verifying hub events.
- **Contract tests**: OpenAPI/Swagger validation.
- **E2E smoke test**: Create project, upload `.tex`, trigger compile, verify PDF in R2.

## Future Extensibility

The design leaves open seams for later features without rewriting v1:

- **Extract compile worker**: swap `ICompileQueue` for RabbitMQ/Redis and move the worker to its own container.
- **Collaboration**: add `ProjectMember`, presence tracking, and OT/CRDT-based concurrent editing.
- **Version history**: store file snapshots in R2 and expose a Git-like timeline.
- **Multiple engines**: add `ILaTeXCompiler` implementations for LuaLaTeX or pdfLaTeX behind a feature flag.

## Why This Design?

- Focuses on backend engineering: auth, real-time communication, background jobs, cloud storage, observability, testing.
- Deployable and demo-ready on a cheap VPS.
- Opinionated enough to be shippable, flexible enough to grow.
- Avoids the operational complexity of microservices in v1 while keeping extraction possible.
