<script lang="ts">
  import { ApiError } from '../api/client'
  import { auth } from '../stores/auth'
  import { link } from '../router'

  let email = $state('')
  let password = $state('')
  let errors = $state<string[]>([])
  let loading = $state(false)

  async function submit(event: SubmitEvent) {
    event.preventDefault()
    loading = true
    errors = []

    try {
      await auth.login(email, password)
    } catch (error) {
      if (error instanceof ApiError) {
        errors = error.errors.length > 0 ? error.errors : [error.message]
      } else {
        errors = ['An unexpected error occurred.']
      }
    } finally {
      loading = false
    }
  }
</script>

<div class="w-full max-w-[360px] border border-border bg-bg-subtle p-6">
  <h1 class="text-lg font-semibold text-text mb-4">Sign in</h1>

  <form onsubmit={submit} class="space-y-4" data-testid="login-form">
    <div>
      <label class="block text-sm text-text-muted mb-1" for="login-email">Email</label>
      <input
        id="login-email"
        data-testid="login-email"
        type="email"
        bind:value={email}
        required
        class="w-full border border-border bg-bg p-2 text-text focus:outline-none focus:border-accent"
      />
    </div>

    <div>
      <label class="block text-sm text-text-muted mb-1" for="login-password">Password</label>
      <input
        id="login-password"
        data-testid="login-password"
        type="password"
        bind:value={password}
        required
        class="w-full border border-border bg-bg p-2 text-text focus:outline-none focus:border-accent"
      />
    </div>

    <button
      type="submit"
      data-testid="login-submit"
      disabled={loading}
      class="w-full border border-accent bg-accent text-white p-2 hover:opacity-90 disabled:opacity-50"
    >
      {loading ? 'Signing in...' : 'Sign in'}
    </button>
  </form>

  {#if errors.length > 0}
    <ul class="mt-4 text-sm text-error space-y-1">
      {#each errors as error}
        <li>{error}</li>
      {/each}
    </ul>
  {/if}

  <p class="mt-4 text-sm text-text-muted">
    No account?
    <a use:link href="/register" class="text-accent hover:underline">Create one</a>
  </p>

  <div class="mt-4 space-y-2">
    <a
      href="/api/auth/external-login?provider=Google"
      class="block w-full border border-border bg-bg p-2 text-center text-text hover:bg-bg-subtle"
    >
      Continue with Google
    </a>
    <a
      href="/api/auth/external-login?provider=GitHub"
      class="block w-full border border-border bg-bg p-2 text-center text-text hover:bg-bg-subtle"
    >
      Continue with GitHub
    </a>
  </div>
</div>
