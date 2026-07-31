<script lang="ts">
  import { theme, toggleTheme } from './stores/theme'
  import { Sun, Moon } from '@lucide/svelte'
</script>

<button
  type="button"
  onclick={toggleTheme}
  aria-label="Toggle theme"
  class="h-6 w-6 border border-border bg-bg-subtle text-text hover:text-accent flex items-center justify-center"
>
  {#if $theme === 'dark'}
    <Sun size={14} strokeWidth={1.75} />
  {:else}
    <Moon size={14} strokeWidth={1.75} />
  {/if}
</button>
