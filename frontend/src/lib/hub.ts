import {
  HubConnection,
  HubConnectionBuilder,
  HubConnectionState,
  LogLevel
} from '@microsoft/signalr'

type StartedHandler = (jobId: string) => void
type OutputHandler = (line: string) => void
type CompletedHandler = (jobId: string, pdfUrl: string) => void
type FailedHandler = (jobId: string, error: string) => void

let connection: HubConnection | null = null
let currentProjectId: string | null = null

const startedHandlers: StartedHandler[] = []
const outputHandlers: OutputHandler[] = []
const completedHandlers: CompletedHandler[] = []
const failedHandlers: FailedHandler[] = []

function getConnection(): HubConnection {
  if (!connection) {
    connection = new HubConnectionBuilder()
      .withUrl('/hubs/projects')
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning)
      .build()

    connection.on('CompileStarted', (jobId: string) => {
      startedHandlers.forEach((h) => h(jobId))
    })
    connection.on('CompileOutput', (line: string) => {
      outputHandlers.forEach((h) => h(line))
    })
    connection.on('CompileCompleted', (jobId: string, pdfUrl: string) => {
      completedHandlers.forEach((h) => h(jobId, pdfUrl))
    })
    connection.on('CompileFailed', (jobId: string, error: string) => {
      failedHandlers.forEach((h) => h(jobId, error))
    })

    connection.onreconnected(() => {
      if (currentProjectId) {
        connection?.invoke('JoinProject', currentProjectId).catch(() => {})
      }
    })
  }

  return connection
}

export function onCompileStarted(handler: StartedHandler): () => void {
  startedHandlers.push(handler)
  return () => {
    const index = startedHandlers.indexOf(handler)
    if (index >= 0) startedHandlers.splice(index, 1)
  }
}

export function onCompileOutput(handler: OutputHandler): () => void {
  outputHandlers.push(handler)
  return () => {
    const index = outputHandlers.indexOf(handler)
    if (index >= 0) outputHandlers.splice(index, 1)
  }
}

export function onCompileCompleted(handler: CompletedHandler): () => void {
  completedHandlers.push(handler)
  return () => {
    const index = completedHandlers.indexOf(handler)
    if (index >= 0) completedHandlers.splice(index, 1)
  }
}

export function onCompileFailed(handler: FailedHandler): () => void {
  failedHandlers.push(handler)
  return () => {
    const index = failedHandlers.indexOf(handler)
    if (index >= 0) failedHandlers.splice(index, 1)
  }
}

export async function connect(): Promise<void> {
  const conn = getConnection()
  if (
    conn.state === HubConnectionState.Connected ||
    conn.state === HubConnectionState.Connecting ||
    conn.state === HubConnectionState.Reconnecting
  ) {
    return
  }
  await conn.start()
}

export async function joinProject(projectId: string): Promise<void> {
  await connect()
  currentProjectId = projectId
  await getConnection().invoke('JoinProject', projectId)
}

export function leaveCurrentProject(): void {
  const hadProject = currentProjectId !== null
  currentProjectId = null
  if (!hadProject || !connection) return
  if (connection.state === HubConnectionState.Disconnected) {
    connection = null
    return
  }
  connection
    .stop()
    .then(() => {
      connection = null
    })
    .catch(() => {
      connection = null
    })
}

export async function disconnect(): Promise<void> {
  currentProjectId = null
  if (!connection) return
  if (connection.state === HubConnectionState.Disconnected) {
    connection = null
    return
  }
  await connection.stop()
  connection = null
}

export async function triggerCompile(projectId: string): Promise<string> {
  await connect()
  return getConnection().invoke('TriggerCompile', projectId)
}

export async function updateFile(
  projectId: string,
  path: string,
  content: string
): Promise<void> {
  await connect()
  await getConnection().invoke('UpdateFile', projectId, path, content)
}
