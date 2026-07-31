<script lang="ts">
  import { compile } from '../stores/compile'
  import { X, Eraser } from '@lucide/svelte'

  interface Props {
    open: boolean
    onClear?: () => void
  }

  let { open = $bindable(), onClear }: Props = $props()

  let logsEl: HTMLDivElement | null = $state(null)
  let stickToBottom = $state(true)
  let hasNew = $state(false)

  const NEAR_BOTTOM_THRESHOLD = 40

  function isNearBottom(): boolean {
    if (!logsEl) return true
    return logsEl.scrollHeight - logsEl.scrollTop - logsEl.clientHeight <= NEAR_BOTTOM_THRESHOLD
  }

  function scrollToBottom() {
    if (logsEl) logsEl.scrollTop = logsEl.scrollHeight
  }

  function onScroll() {
    if (isNearBottom()) {
      stickToBottom = true
      hasNew = false
    } else {
      stickToBottom = false
    }
  }

  function jumpToBottom() {
    stickToBottom = true
    hasNew = false
    scrollToBottom()
  }

  $effect(() => {
    const _count = $compile.logs.length
    const _status = $compile.status
    if (!open || !logsEl) return

    // Auto-open-on-failure should always snap to the latest output.
    if (_status === 'failed') {
      stickToBottom = true
    }

    if (stickToBottom) {
      scrollToBottom()
    } else {
      hasNew = true
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
  <div class="shrink-0 border-t border-border bg-bg-subtle flex flex-col relative" style="height: 100%;">
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
          class="text-xs text-text-muted hover:text-text disabled:opacity-50 flex items-center gap-1"
        >
          <Eraser size={14} strokeWidth={1.75} />
          Clear
        </button>
        <button
          type="button"
          onclick={() => (open = false)}
          class="text-xs text-text-muted hover:text-text flex items-center"
          aria-label="Close logs"
        >
          <X size={14} strokeWidth={1.75} />
        </button>
      </div>
    </div>
    <div
      bind:this={logsEl}
      onscroll={onScroll}
      class="flex-1 overflow-auto p-2 font-mono text-xs text-text whitespace-pre-wrap relative"
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

    {#if hasNew && !stickToBottom}
      <button
        type="button"
        onclick={jumpToBottom}
        class="absolute bottom-2 right-2 border border-border bg-bg px-2 py-1 text-xs text-text hover:bg-bg-subtle"
      >
        ↓ new output
      </button>
    {/if}
  </div>
{/if}
