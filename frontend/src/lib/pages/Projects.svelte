<script lang="ts">
  import { onMount } from 'svelte'
  import { link, navigate } from '../router'
  import { projects, type CompileStatus } from '../stores/projects'

  let showNewForm = $state(false)
  let newName = $state('')
  let editingId = $state<string | null>(null)
  let editingName = $state('')
  let deletingId = $state<string | null>(null)
  let actionError = $state<string | null>(null)

  onMount(() => {
    projects.load()
  })

  function formatDate(value: string): string {
    const date = new Date(value)
    const dateStr = date.toLocaleDateString('en-CA')
    const timeStr = date.toLocaleTimeString('en-GB', {
      hour: '2-digit',
      minute: '2-digit'
    })
    return `${dateStr} ${timeStr}`
  }

  function statusLabel(status: CompileStatus): string {
    if (status === null) return '—'
    return status.toLowerCase()
  }

  function statusClass(status: CompileStatus): string {
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

  async function handleCreate(event: SubmitEvent) {
    event.preventDefault()
    if (!newName.trim()) return

    try {
      await projects.create(newName.trim())
      newName = ''
      showNewForm = false
      actionError = null
    } catch (error) {
      actionError = error instanceof Error ? error.message : 'Failed to create project.'
    }
  }

  function startRename(id: string, name: string) {
    editingId = id
    editingName = name
    deletingId = null
    actionError = null
  }

  async function handleRename(id: string) {
    if (!editingName.trim()) return

    try {
      await projects.rename(id, editingName.trim())
      editingId = null
      editingName = ''
      actionError = null
    } catch (error) {
      actionError = error instanceof Error ? error.message : 'Failed to rename project.'
    }
  }

  function cancelRename() {
    editingId = null
    editingName = ''
  }

  function startDelete(id: string) {
    deletingId = id
    editingId = null
    actionError = null
  }

  async function handleDelete(id: string) {
    try {
      await projects.remove(id)
      deletingId = null
      actionError = null
    } catch (error) {
      actionError = error instanceof Error ? error.message : 'Failed to delete project.'
    }
  }

  function cancelDelete() {
    deletingId = null
  }
</script>

<div class="w-full max-w-[880px] border border-border bg-bg p-6">
  <div class="flex items-center justify-between mb-4">
    <h1 class="text-lg font-semibold text-text">Projects</h1>
    <button
      type="button"
      onclick={() => {
        showNewForm = true
        actionError = null
      }}
      class="border border-accent bg-accent px-3 py-1.5 text-white hover:opacity-90 disabled:opacity-50"
      disabled={showNewForm}
    >
      New project
    </button>
  </div>

  {#if $projects.error || actionError}
    <div class="mb-4 border border-error p-2 text-error">
      {$projects.error ?? actionError}
    </div>
  {/if}

  {#if showNewForm}
    <form onsubmit={handleCreate} class="mb-4 flex items-center gap-2 border border-border bg-bg-subtle p-2">
      <input
        type="text"
        bind:value={newName}
        placeholder="Project name"
        class="flex-1 border border-border bg-bg p-2 text-text focus:outline-none focus:border-accent"
      />
      <button
        type="submit"
        disabled={!newName.trim() || $projects.loading}
        class="border border-accent bg-accent px-3 py-2 text-white hover:opacity-90 disabled:opacity-50"
      >
        Create
      </button>
      <button
        type="button"
        onclick={() => {
          showNewForm = false
          newName = ''
        }}
        class="border border-border bg-bg px-3 py-2 text-text hover:bg-bg-subtle"
      >
        Cancel
      </button>
    </form>
  {/if}

  {#if $projects.loading && $projects.items.length === 0}
    <div class="py-8 text-center text-text-muted">Loading projects...</div>
  {:else if $projects.items.length === 0}
    <div class="py-8 text-center text-text-muted">
      No projects yet. Create your first project.
    </div>
  {:else}
    <table class="w-full border-collapse border border-border text-left">
      <thead>
        <tr class="border-b border-border bg-bg-subtle text-sm text-text-muted">
          <th class="p-2 font-medium">Name</th>
          <th class="p-2 font-medium">Last compile</th>
          <th class="p-2 font-medium">Created</th>
          <th class="p-2 font-medium text-right">Actions</th>
        </tr>
      </thead>
      <tbody>
        {#each $projects.items as project (project.id)}
          <tr class="border-b border-border last:border-b-0">
            {#if editingId === project.id}
              <td class="p-2">
                <input
                  type="text"
                  bind:value={editingName}
                  class="w-full border border-border bg-bg p-2 text-text focus:outline-none focus:border-accent"
                />
              </td>
              <td class="p-2">
                <span class="border px-2 py-0.5 text-xs {statusClass(project.lastCompileStatus)}">
                  {statusLabel(project.lastCompileStatus)}
                </span>
              </td>
              <td class="p-2 text-text-muted">{formatDate(project.createdAt)}</td>
              <td class="p-2 text-right">
                <button
                  type="button"
                  onclick={() => handleRename(project.id)}
                  disabled={!editingName.trim()}
                  class="mr-2 text-accent hover:underline disabled:opacity-50"
                >
                  Save
                </button>
                <button
                  type="button"
                  onclick={cancelRename}
                  class="text-text-muted hover:underline"
                >
                  Cancel
                </button>
              </td>
            {:else if deletingId === project.id}
              <td class="p-2">
                <a
                  use:link
                  href="/projects/{project.id}"
                  class="text-accent hover:underline"
                >
                  {project.name}
                </a>
              </td>
              <td class="p-2">
                <span class="border px-2 py-0.5 text-xs {statusClass(project.lastCompileStatus)}">
                  {statusLabel(project.lastCompileStatus)}
                </span>
              </td>
              <td class="p-2 text-text-muted">{formatDate(project.createdAt)}</td>
              <td class="p-2 text-right">
                <span class="text-text-muted">Delete?</span>
                <button
                  type="button"
                  onclick={() => handleDelete(project.id)}
                  class="ml-2 text-error hover:underline"
                >
                  Yes
                </button>
                <button
                  type="button"
                  onclick={cancelDelete}
                  class="ml-2 text-text-muted hover:underline"
                >
                  No
                </button>
              </td>
            {:else}
              <td class="p-2">
                <a
                  use:link
                  href="/projects/{project.id}"
                  class="text-accent hover:underline"
                >
                  {project.name}
                </a>
              </td>
              <td class="p-2">
                <span class="border px-2 py-0.5 text-xs {statusClass(project.lastCompileStatus)}">
                  {statusLabel(project.lastCompileStatus)}
                </span>
              </td>
              <td class="p-2 text-text-muted">{formatDate(project.createdAt)}</td>
              <td class="p-2 text-right">
                <button
                  type="button"
                  onclick={() => startRename(project.id, project.name)}
                  class="mr-3 text-accent hover:underline"
                >
                  Rename
                </button>
                <button
                  type="button"
                  onclick={() => startDelete(project.id)}
                  class="text-error hover:underline"
                >
                  Delete
                </button>
              </td>
            {/if}
          </tr>
        {/each}
      </tbody>
    </table>
  {/if}
</div>
