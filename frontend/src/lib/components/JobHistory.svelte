<script lang="ts">
  import { apiFetch } from '../api/client'

  interface Props {
    projectId: string
    onSelect: (url: string) => void
  }

  let { projectId, onSelect }: Props = $props()

  type JobStatus = 'Success' | 'Failed' | 'Running' | 'Queued' | 'Cancelled'

  type CompileJob = {
    id: string
    projectId: string
    status: JobStatus
    createdAt: string
    startedAt?: string | null
    completedAt?: string | null
    durationMs?: number | null
    errorMessage?: string | null
    hasOutput: boolean
  }

  let open = $state(false)
  let loading = $state(false)
  let fetchError = $state<string | null>(null)
  let jobs = $state<CompileJob[]>([])
  let wrapperEl: HTMLDivElement | null = $state(null)

  function toggle() {
    open = !open
    if (open) loadJobs()
  }

  function close() {
    open = false
  }

  async function loadJobs() {
    loading = true
    fetchError = null

    try {
      const items = await apiFetch<CompileJob[]>(`/api/projects/${projectId}/jobs`)
      jobs = items.slice(0, 10)
    } catch (err) {
      fetchError = err instanceof Error ? err.message : 'Failed to load jobs'
      jobs = []
    } finally {
      loading = false
    }
  }

  async function selectJob(job: CompileJob) {
    if (job.status !== 'Success' || !job.hasOutput) return

    close()

    try {
      const response = await fetch(`/api/projects/${projectId}/jobs/${job.id}/pdf`, {
        credentials: 'include',
        redirect: 'follow'
      })

      if (!response.ok) {
        throw new Error('PDF not available')
      }

      onSelect(response.url)
    } catch (err) {
      fetchError = err instanceof Error ? err.message : 'Failed to load PDF'
    }
  }

  $effect(() => {
    if (!open) return

    function handleClick(event: MouseEvent) {
      if (!wrapperEl || wrapperEl.contains(event.target as Node)) return
      close()
    }

    document.addEventListener('click', handleClick)
    return () => document.removeEventListener('click', handleClick)
  })

  function statusClass(status: JobStatus): string {
    switch (status) {
      case 'Success':
        return 'text-success border-success'
      case 'Failed':
        return 'text-error border-error'
      case 'Running':
      case 'Queued':
        return 'text-accent border-accent'
      default:
        return 'text-text-muted border-text-muted'
    }
  }

  function formatDuration(ms: number | null | undefined): string {
    if (ms == null) return '—'
    return `${(ms / 1000).toFixed(1)}s`
  }

  function formatTime(value: string): string {
    return new Date(value).toLocaleTimeString('en-GB', {
      hour: '2-digit',
      minute: '2-digit'
    })
  }
</script>

<div bind:this={wrapperEl} class="relative">
  <button
    type="button"
    onclick={toggle}
    class="border border-border bg-bg px-3 py-0.5 text-xs text-text hover:bg-bg-subtle"
  >
    History
  </button>

  {#if open}
    <div
      class="absolute right-0 top-full mt-1 w-64 border border-border bg-bg z-10 flex flex-col max-h-80 overflow-auto"
    >
      {#if loading}
        <div class="p-3 text-xs text-text-muted">Loading jobs…</div>
      {:else if fetchError}
        <div class="p-3 text-xs text-error">{fetchError}</div>
      {:else if jobs.length === 0}
        <div class="p-3 text-xs text-text-muted">No jobs yet.</div>
      {:else}
        {#each jobs as job (job.id)}
          <button
            type="button"
            onclick={() => selectJob(job)}
            disabled={job.status !== 'Success' || !job.hasOutput}
            class="w-full text-left border-b border-border last:border-b-0 px-3 py-2 hover:bg-bg-subtle disabled:opacity-50"
          >
            <div class="flex items-center justify-between">
              <span class="border px-1.5 py-0.5 text-xs {statusClass(job.status)}">
                {job.status.toLowerCase()}
              </span>
              <span class="text-xs text-text-muted">{formatDuration(job.durationMs)}</span>
            </div>
            <div class="mt-1 text-xs text-text-muted">{formatTime(job.createdAt)}</div>
          </button>
        {/each}
      {/if}
    </div>
  {/if}
</div>
