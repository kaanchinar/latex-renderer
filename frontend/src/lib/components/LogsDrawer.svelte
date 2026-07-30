<script lang="ts">
  import { compile } from '../stores/compile'

  interface Props {
    open: boolean
  }

  let { open = $bindable() }: Props = $props()

  let logsEl: HTMLPreElement | null = $state(null)

  $effect(() => {
    const _count = $compile.logs.length
    if (open && logsEl) {
      logsEl.scrollTop = logsEl.scrollHeight
    }
  })
</script>

{#if open}
  <div class="h-40 shrink-0 border-t border-border bg-bg-subtle flex flex-col">
    <div
      class="h-6 shrink-0 flex items-center px-2 border-b border-border text-xs text-text-muted"
    >
      Compile logs
    </div>
    <pre
      bind:this={logsEl}
      class="flex-1 overflow-auto p-2 text-xs font-mono text-text whitespace-pre-wrap"
    >
      {#each $compile.logs as line, i (i)}
        <div>{line}</div>
      {/each}
    </pre>
  </div>
{/if}
