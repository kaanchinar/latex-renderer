<script lang="ts">
  import ThemeToggle from './lib/ThemeToggle.svelte'
</script>

<div class="min-h-full flex flex-col bg-bg text-text">
  <header
    class="h-10 shrink-0 flex items-center justify-between px-4 border-b border-border bg-bg"
  >
    <span class="font-semibold">Latex Renderer</span>
    <ThemeToggle />
  </header>

  <main class="flex-1 flex items-center justify-center bg-bg-subtle">
    <div class="border border-border p-8 bg-bg">
      <h1 class="text-lg font-semibold mb-2 text-text">Coming soon</h1>
      <p class="text-text-muted">The LaTeX editor is under construction.</p>
    </div>
  </main>
</div>
