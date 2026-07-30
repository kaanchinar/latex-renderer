# Real-Time API — SignalR Project Hub

The REST API is documented in Scalar (`/scalar/v1` in development). This document
is the contract for the SignalR hub, which OpenAPI cannot describe.

**Endpoint:** `/hubs/projects` (WebSockets, with SSE/long-polling fallback)
**Auth:** same cookie as the REST API (`[Authorize]`). Send the cookie on connect.

## Client → Server methods

Call these with `connection.invoke(...)`. All throw a `HubException` with
`"Project not found."` if the project doesn't exist or belongs to another user.

| Method | Arguments | Returns | Purpose |
|---|---|---|---|
| `JoinProject` | `projectId: string (guid)` | — | Subscribe this connection to the project's events |
| `TriggerCompile` | `projectId: string (guid)` | `jobId: string (guid)` | Create + queue a compile job |
| `UpdateFile` | `projectId: string (guid)`, `path: string`, `content: string` | — | Create/replace a text file |

## Server → Client events

Register handlers with `connection.on(...)`. Sent to every connection that
joined the project's group.

| Event | Payload | Meaning |
|---|---|---|
| `CompileStarted` | `jobId: string` | Worker picked up the job |
| `CompileOutput` | `line: string` | One line of compiler stdout (after process exits) |
| `CompileCompleted` | `jobId: string`, `pdfUrl: string` | Success; `pdfUrl` is a short-lived (15 min) download URL |
| `CompileFailed` | `jobId: string`, `error: string` | Failure or cancellation with a human-readable reason |

## JavaScript example

```js
import * as signalR from "@microsoft/signalr";

const connection = new signalR.HubConnectionBuilder()
    .withUrl("/hubs/projects")          // cookie is sent automatically (same origin)
    .withAutomaticReconnect()
    .build();

connection.on("CompileStarted", (jobId) => showSpinner(jobId));
connection.on("CompileOutput", (line) => appendLog(line));
connection.on("CompileCompleted", (jobId, pdfUrl) => refreshPreview(pdfUrl));
connection.on("CompileFailed", (jobId, error) => showError(error));

await connection.start();
await connection.invoke("JoinProject", projectId);

const jobId = await connection.invoke("TriggerCompile", projectId);
```

Notes:
- Same-origin cookie auth just works. Cross-origin SPA: the cookie needs
  `withCredentials` and CORS configured on the API (not yet — add when the frontend lands).
- `CompileOutput` lines arrive in a burst after the process exits (v1 does not stream live).
- The same flow exists as a C# reference in `tests/LatexEditor.IntegrationTests/ProjectHubIntegrationTests.cs`.
