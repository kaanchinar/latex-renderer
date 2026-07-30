import { writable } from 'svelte/store'
import { apiFetch, ApiError } from '../api/client'
import * as hub from '../hub'
import { push as pushToast } from './toast'

export type CompileStatus = 'idle' | 'queued' | 'running' | 'success' | 'failed'

type CompileJobDto = {
  id: string
  durationMs?: number | null
}

type CompileState = {
  status: CompileStatus
  currentJobId: string | null
  logs: string[]
  pdfUrl: string | null
  error: string | null
  lastDurationMs: number | null
}

const initialState: CompileState = {
  status: 'idle',
  currentJobId: null,
  logs: [],
  pdfUrl: null,
  error: null,
  lastDurationMs: null
}

function createCompile() {
  const { subscribe, set, update } = writable<CompileState>({ ...initialState })

  let unsubscribers: (() => void)[] = []
  let activeProjectId: string | null = null

  function isRateLimit(error: unknown): boolean {
    if (error instanceof ApiError && error.status === 429) return true
    if (error instanceof Error && /rate|429/i.test(error.message)) return true
    return false
  }

  function start(projectId: string) {
    activeProjectId = projectId
    stop()
    set({ ...initialState })
    listen(projectId)
  }

  function stop() {
    unsubscribers.forEach((u) => u())
    unsubscribers = []
    set({ ...initialState })
  }

  function listen(projectId: string) {
    unsubscribers.push(
      hub.onCompileStarted((jobId) => {
        update((state) => ({
          ...state,
          status: 'running',
          currentJobId: jobId,
          logs: []
        }))
      }),
      hub.onCompileOutput((line) => {
        update((state) => {
          const logs = [...state.logs, line]
          if (logs.length > 1000) logs.shift()
          return { ...state, logs }
        })
      }),
      hub.onCompileCompleted((jobId, pdfUrl) => {
        update((state) => ({ ...state, status: 'success', pdfUrl }))
        fetchDuration(projectId, jobId)
      }),
      hub.onCompileFailed((jobId, error) => {
        update((state) => ({ ...state, status: 'failed', error }))
      })
    )
  }

  async function fetchDuration(projectId: string, jobId: string) {
    try {
      const jobs = await apiFetch<CompileJobDto[]>(`/api/projects/${projectId}/jobs`)
      const job = jobs.find((j) => j.id === jobId)
      if (job?.durationMs != null) {
        update((state) => ({ ...state, lastDurationMs: job.durationMs ?? null }))
      }
    } catch {
      // duration is optional
    }
  }

  async function triggerCompile(projectId: string) {
    update((state) => ({ ...state, status: 'queued', error: null }))

    try {
      const jobId = await hub.triggerCompile(projectId)
      update((state) => ({ ...state, currentJobId: jobId }))
      return
    } catch (hubError) {
      if (isRateLimit(hubError)) {
        pushToast('Compile rate limit reached — wait a minute')
        update((state) => ({ ...state, status: 'idle' }))
        return
      }

      try {
        const job = await apiFetch<CompileJobDto>(`/api/projects/${projectId}/compile`, {
          method: 'POST'
        })
        update((state) => ({ ...state, currentJobId: job.id }))
      } catch (restError) {
        if (isRateLimit(restError)) {
          pushToast('Compile rate limit reached — wait a minute')
        }
        update((state) => ({
          ...state,
          status: 'failed',
          error: restError instanceof Error ? restError.message : 'Compile failed'
        }))
      }
    }
  }

  return {
    subscribe,
    start,
    stop,
    triggerCompile
  }
}

export const compile = createCompile()
