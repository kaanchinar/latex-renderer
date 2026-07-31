<script lang="ts">
  import { compile } from '../stores/compile'

  interface Props {
    open: boolean
    onClear?: () => void
  }

  let { open = $bindable(), onClear }: Props = $props()

  let logsEl: HTMLDivElement | null = $state(null)

  $effect(() => {
    const _count = $compile.logs.length
    if (open && logsEl) {
      logsEl.scrollTop = logsEl.scrollHeight
    }
  })

  function statusText(): string | null {
    switch ($compile.status) {
      case 'running':
      case 'queued':
        return 'compiling…'
      case 'success':
        return 'compiled'
      case 'failed':
        return $compile.error ? truncate($compile.error, 80) : 'Compile failed'
      default:
        return null
    }
  }

  function truncate(s: string, max: number): string {
    return s.length > max ? s.slice(0, max - 1) + '…' : s
  }
</script>

{#if open}
  <div class="shrink-0 border-t border-border bg-bg-subtle flex flex-col" style="height: 100%;">
    <div
      class="h-6 shrink-0 flex items-center justify-between px-2 border-b border-border text-xs"
    >
      <div class="flex items-center gap-2 min-w-0">
        <span class="font-medium text-text-muted uppercase tracking-wide shrink-0">Compile log</span>
        {#if statusText()}
          <span
            class="text-xs truncate"
            class:text-accent={$compile.status === 'running' || $compile.status === 'queued'}
            class:text-success={$compile.status === 'success'}
            class:text-error={$compile.status === 'failed'}
            title={$compile.error ?? ''}
          >
            {statusText()}
          </span>
        {/if}
      </div>
      <div class="flex items-center gap-2">
        <button
          type="button"
          onclick={() => onClear?.()}
          disabled={$compile.logs.length === 0}
          class="text-xs text-text-muted hover:text-text disabled:opacity-50"
        >
          Clear
        </button>
        <button
          type="button"
          onclick={() => (open = false)}
          class="text-xs text-text-muted hover:text-text"
          aria-label="Close logs"
        >
          ✕
        </button>
      </div>
    </div>
    <div
      bind:this={logsEl}
      class="flex-1 overflow-auto p-2 font-mono text-xs text-text whitespace-pre-wrap"
    >
      {#if $compile.status === 'failed' && $compile.error && $compile.logs.length === 0}
        <div class="text-error">Error: {$compile.error}</div>
      {:else if $compile.logs.length === 0}
        <div class="text-text-muted italic">No compile output yet.</div>
      {:else}
        {#each $compile.logs as line, i (i)}
          <div>{line}</div>
        {/each}
      {/if}
    </div>
  </div>
{/if}
