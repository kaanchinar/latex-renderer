<script lang="ts">
  import { auth } from './lib/stores/auth'
  import { path, navigate } from './lib/router'
  import ThemeToggle from './lib/ThemeToggle.svelte'
  import Login from './lib/pages/Login.svelte'
  import Register from './lib/pages/Register.svelte'
  import Projects from './lib/pages/Projects.svelte'
  import ProjectWorkspace from './lib/pages/ProjectWorkspace.svelte'

  const publicPaths = new Set(['/login', '/register'])

  function workspaceId(path: string): string | null {
    if (path === '/projects' || path === '/projects/') {
      return null
    }
    if (path.startsWith('/projects/')) {
      return path.slice('/projects/'.length)
    }
    return null
  }

  $effect(() => {
    const state = $auth.state
    const current = $path

    if (state === 'unknown') {
      return
    }

    if (state === 'loggedOut' && !publicPaths.has(current)) {
      navigate('/login')
    } else if (state === 'loggedIn' && publicPaths.has(current)) {
      navigate('/projects')
    }
  })


</script>

<div class="min-h-full flex flex-col bg-bg text-text">
  <header
    class="h-10 shrink-0 flex items-center justify-between px-4 border-b border-border bg-bg"
  >
    <span class="font-semibold">Latex Renderer</span>
    <div class="flex items-center gap-3">
      {#if $auth.state === 'loggedIn'}
        <span class="text-sm text-text-muted">{$auth.user.email}</span>
        <button
          type="button"
          onclick={() => auth.logout()}
          class="text-sm text-accent hover:underline"
        >
          Logout
        </button>
      {/if}
      <ThemeToggle />
    </div>
  </header>

  <main class="flex-1 flex items-center justify-center bg-bg-subtle p-4">
    {#if $auth.state === 'unknown'}
      <div class="text-text-muted">Loading...</div>
    {:else if $path === '/login'}
      <Login />
    {:else if $path === '/register'}
      <Register />
    {:else if $path === '/projects'}
      <Projects />
    {:else if workspaceId($path)}
      <ProjectWorkspace id={workspaceId($path)!} />
    {:else}
      <Projects />
    {/if}
  </main>
</div>
