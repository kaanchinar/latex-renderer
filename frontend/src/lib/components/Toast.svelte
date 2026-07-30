<script lang="ts">
  import { toasts } from '../stores/toast'
</script>

{#if $toasts.length > 0}
  <div class="fixed bottom-4 right-4 flex flex-col gap-2 z-50">
    {#each $toasts as toast (toast.id)}
      <div class="border border-border bg-bg px-3 py-2 text-sm text-text">
        {toast.message}
      </div>
    {/each}
  </div>
{/if}
