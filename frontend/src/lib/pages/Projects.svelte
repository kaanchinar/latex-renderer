<script lang="ts">
  import { onMount, tick } from 'svelte'
  import { link, navigate } from '../router'
  import { projects, type CompileStatus, type Project } from '../stores/projects'

  let showNewForm = $state(false)
  let newName = $state('')
  let newInputEl: HTMLInputElement | null = $state(null)
  let editingId = $state<string | null>(null)
  let editingName = $state('')
  let deletingId = $state<string | null>(null)
  let actionError = $state<string | null>(null)
  let sortBy = $state<'name' | 'created'>('created')
  let sortDir = $state<'asc' | 'desc'>('desc')
  let submitting = $state(false)
  let renaming = $state(false)

  onMount(() => {
    projects.load()
  })

  async function openNew() {
    showNewForm = true
    newName = ''
    actionError = null
    await tick()
    newInputEl?.focus()
  }

  function closeNew() {
    showNewForm = false
    newName = ''
  }

  async function handleCreate(event: SubmitEvent) {
    event.preventDefault()
    const name = newName.trim()
    if (!name || submitting) return
    submitting = true
    actionError = null
    try {
      const created = await projects.create(name)
      closeNew()
      navigate(`/projects/${created.slug}`)
    } catch (error) {
      actionError = error instanceof Error ? error.message : 'Failed to create project.'
    } finally {
      submitting = false
    }
  }

  function handleNewKeydown(event: KeyboardEvent) {
    if (event.key === 'Escape') {
      event.preventDefault()
      closeNew()
    }
  }

  function toggleSort(column: 'name' | 'created') {
    if (sortBy === column) {
      sortDir = sortDir === 'asc' ? 'desc' : 'asc'
    } else {
      sortBy = column
      sortDir = column === 'name' ? 'asc' : 'desc'
    }
  }

  function compareProjects(a: Project, b: Project): number {
    if (sortBy === 'name') {
      return a.name.localeCompare(b.name)
    }
    return new Date(a.createdAt).getTime() - new Date(b.createdAt).getTime()
  }

  let sortedItems = $derived(
    $projects.items.slice().sort((a, b) => {
      const cmp = compareProjects(a, b)
      return sortDir === 'asc' ? cmp : -cmp
    })
  )

  function formatDate(value: string): string {
    const date = new Date(value)
    const pad = (n: number) => String(n).padStart(2, '0')
    return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())} ${pad(date.getHours())}:${pad(date.getMinutes())}`
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

  function startRename(project: Project) {
    editingId = project.id
    editingName = project.name
    deletingId = null
    actionError = null
  }

  async function handleRename(id: string) {
    const name = editingName.trim()
    if (!name || renaming) return
    renaming = true
    actionError = null
    try {
      await projects.rename(id, name)
      editingId = null
      editingName = ''
    } catch (error) {
      actionError = error instanceof Error ? error.message : 'Failed to rename project.'
    } finally {
      renaming = false
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
    } catch (error) {
      actionError = error instanceof Error ? error.message : 'Failed to delete project.'
    }
  }

  function cancelDelete() {
    deletingId = null
  }
</script>

<div class="w-full px-6 py-6">
  <div class="mb-4 flex items-baseline justify-between">
    <div class="flex items-baseline gap-2">
      <h1 class="text-xl font-normal text-text">Projects</h1>
      <span class="text-sm text-text-muted" data-testid="projects-count">
        {$projects.items.length}
        {$projects.items.length === 1 ? 'project' : 'projects'}
      </span>
    </div>
    {#if !showNewForm}
      <button
        type="button"
        data-testid="new-project-button"
        onclick={openNew}
        class="border border-accent bg-accent px-3 py-1.5 text-white hover:opacity-90"
      >
        New project
      </button>
    {/if}
  </div>

  {#if $projects.error || actionError}
    <div
      class="mb-4 border border-error p-2 text-error"
      data-testid="projects-error"
    >
      {$projects.error ?? actionError}
    </div>
  {/if}

  {#if showNewForm}
    <form
      onsubmit={handleCreate}
      class="mb-4 border border-border bg-bg-subtle p-4"
      data-testid="new-project-panel"
    >
      <label
        for="project-name-input"
        class="mb-1 block text-sm text-text-muted"
      >
        Project name
      </label>
      <input
        id="project-name-input"
        type="text"
        data-testid="project-name-input"
        bind:this={newInputEl}
        bind:value={newName}
        onkeydown={handleNewKeydown}
        placeholder="e.g. research-paper"
        class="block w-full max-w-[480px] border border-border bg-bg p-2 text-text focus:outline-none focus:border-accent"
        disabled={submitting}
      />
      <p class="mt-1 text-xs text-text-muted">You can rename it later.</p>
      <div class="mt-3 flex items-center gap-2">
        <button
          type="submit"
          data-testid="create-project-button"
          disabled={!newName.trim() || submitting}
          class="border border-accent bg-accent px-3 py-1.5 text-white hover:opacity-90 disabled:opacity-50"
        >
          Create
        </button>
        <button
          type="button"
          data-testid="cancel-new-project-button"
          onclick={closeNew}
          disabled={submitting}
          class="border border-border bg-bg px-3 py-1.5 text-text hover:bg-bg-subtle disabled:opacity-50"
        >
          Cancel
        </button>
      </div>
    </form>
  {/if}

  {#if $projects.loading && $projects.items.length === 0}
    <div class="border border-border" data-testid="projects-loading">
      {#each Array.from({ length: 5 }) as _, i (i)}
        <div
          class="grid h-10 grid-cols-[1fr_140px_160px_180px] items-center border-b border-border px-3 last:border-b-0"
        >
          <div class="h-3 w-40 bg-bg-subtle"></div>
          <div class="h-3 w-16 bg-bg-subtle"></div>
          <div class="h-3 w-32 bg-bg-subtle"></div>
          <div class="h-3 w-20 justify-self-end bg-bg-subtle"></div>
        </div>
      {/each}
    </div>
  {:else if $projects.items.length === 0}
    <div
      class="flex flex-col items-center justify-center gap-2 border border-border bg-bg p-8 text-center"
      data-testid="projects-empty"
    >
      <h2 class="text-base text-text">No projects yet</h2>
      <p class="text-sm text-text-muted">
        Create your first LaTeX project to get started.
      </p>
      <button
        type="button"
        data-testid="empty-new-project-button"
        onclick={openNew}
        class="mt-2 border border-accent bg-accent px-3 py-1.5 text-white hover:opacity-90"
      >
        New project
      </button>
    </div>
  {:else}
    <div class="border border-border" data-testid="projects-table">
      <div
        class="grid grid-cols-[1fr_140px_160px_180px] border-b border-border bg-bg-subtle text-xs uppercase tracking-wide text-text-muted"
      >
        <button
          type="button"
          data-testid="sort-name-header"
          onclick={() => toggleSort('name')}
          class="flex items-center gap-1 px-3 py-2 text-left hover:text-text"
        >
          <span>Name</span>
          {#if sortBy === 'name'}
            <span aria-hidden="true">{sortDir === 'asc' ? '▲' : '▼'}</span>
          {/if}
        </button>
        <div class="px-3 py-2">Last compile</div>
        <button
          type="button"
          data-testid="sort-created-header"
          onclick={() => toggleSort('created')}
          class="flex items-center gap-1 px-3 py-2 text-left hover:text-text"
        >
          <span>Created</span>
          {#if sortBy === 'created'}
            <span aria-hidden="true">{sortDir === 'asc' ? '▲' : '▼'}</span>
          {/if}
        </button>
        <div class="px-3 py-2 text-right">Actions</div>
      </div>
      {#each sortedItems as project (project.id)}
        <div
          class="group grid h-10 grid-cols-[1fr_140px_160px_180px] items-center border-b border-border text-sm last:border-b-0 cursor-pointer hover:bg-bg-subtle"
          data-testid="project-row"
          onclick={() => navigate(`/projects/${project.slug}`)}
          onkeydown={(e) => {
            if (e.key === 'Enter' || e.key === ' ') {
              e.preventDefault()
              navigate(`/projects/${project.slug}`)
            }
          }}
          role="button"
          tabindex="0"
        >
          {#if editingId === project.id}
            <div class="flex items-center px-3" onclick={(e) => e.stopPropagation()}>
              <input
                type="text"
                data-testid="rename-input"
                bind:value={editingName}
                class="w-full border border-border bg-bg p-1 text-text focus:outline-none focus:border-accent"
              />
            </div>
            <div class="px-3" onclick={(e) => e.stopPropagation()}>
              <span
                class="border px-2 py-0.5 text-xs {statusClass(
                  project.lastCompileStatus
                )}"
              >
                {statusLabel(project.lastCompileStatus)}
              </span>
            </div>
            <div class="px-3 text-text-muted" onclick={(e) => e.stopPropagation()}>{formatDate(project.createdAt)}</div>
            <div class="flex items-center justify-end gap-2 px-3" onclick={(e) => e.stopPropagation()}>
              <button
                type="button"
                data-testid="rename-save"
                onclick={() => handleRename(project.id)}
                disabled={!editingName.trim() || renaming}
                class="border border-accent bg-accent px-2 py-0.5 text-xs text-white hover:opacity-90 disabled:opacity-50"
              >
                Save
              </button>
              <button
                type="button"
                data-testid="rename-cancel"
                onclick={cancelRename}
                class="text-xs text-text-muted hover:underline"
              >
                Cancel
              </button>
            </div>
          {:else if deletingId === project.id}
            <div class="flex items-center px-3 text-text" onclick={(e) => e.stopPropagation()}>
              <span>Delete {project.name}? This cannot be undone.</span>
            </div>
            <div class="px-3" onclick={(e) => e.stopPropagation()}>
              <span
                class="border px-2 py-0.5 text-xs {statusClass(
                  project.lastCompileStatus
                )}"
              >
                {statusLabel(project.lastCompileStatus)}
              </span>
            </div>
            <div class="px-3 text-text-muted" onclick={(e) => e.stopPropagation()}>{formatDate(project.createdAt)}</div>
            <div class="flex items-center justify-end gap-2 px-3" onclick={(e) => e.stopPropagation()}>
              <button
                type="button"
                data-testid="delete-confirm"
                onclick={() => handleDelete(project.id)}
                class="border border-error bg-bg px-2 py-0.5 text-xs text-error hover:bg-error hover:text-white"
              >
                Delete
              </button>
              <button
                type="button"
                data-testid="delete-cancel"
                onclick={cancelDelete}
                class="text-xs text-text-muted hover:underline"
              >
                Cancel
              </button>
            </div>
          {:else}
            <div class="flex h-full flex-col justify-center px-3 leading-tight">
              <span class="text-text font-medium">
                {project.name}
              </span>
              <span class="text-xs text-text-muted" data-testid="project-slug">
                {project.slug}
              </span>
            </div>
            <div class="px-3">
              <span
                class="border px-2 py-0.5 text-xs {statusClass(
                  project.lastCompileStatus
                )}"
              >
                {statusLabel(project.lastCompileStatus)}
              </span>
            </div>
            <div class="px-3 text-text-muted">{formatDate(project.createdAt)}</div>
            <div class="flex items-center justify-end gap-2 px-3">
              <button
                type="button"
                data-testid="row-action-open"
                onclick={(e) => {
                  e.stopPropagation()
                  navigate(`/projects/${project.slug}`)
                }}
                class="invisible text-xs text-accent hover:underline group-hover:visible"
              >
                Open
              </button>
              <button
                type="button"
                data-testid="row-action-rename"
                onclick={(e) => {
                  e.stopPropagation()
                  startRename(project)
                }}
                class="invisible text-xs text-text-muted hover:text-text group-hover:visible"
              >
                Rename
              </button>
              <button
                type="button"
                data-testid="row-action-delete"
                onclick={(e) => {
                  e.stopPropagation()
                  startDelete(project.id)
                }}
                class="invisible text-xs text-error hover:underline group-hover:visible"
              >
                Delete
              </button>
            </div>
          {/if}
        </div>
      {/each}
    </div>
  {/if}
</div>
