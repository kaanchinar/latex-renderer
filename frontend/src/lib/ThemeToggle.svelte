<script lang="ts">
  import { theme, toggleTheme } from './stores/theme'
</script>

<button
  type="button"
  onclick={toggleTheme}
  aria-label="Toggle theme"
  class="h-6 w-6 border border-border bg-bg-subtle text-text hover:text-accent flex items-center justify-center"
>
  {$theme === 'dark' ? '☀' : '☾'}
</button>
