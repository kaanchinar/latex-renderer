<script lang="ts">
  import { path, link, workspaceId } from '../router'
  import { files, type ProjectFile } from '../stores/files'
  import { apiFetch, ApiError } from '../api/client'

  type ProjectDetails = {
    id: string
    name: string
  }

  let project = $state<ProjectDetails | null>(null)
  let notFound = $state(false)
  let projectError = $state<string | null>(null)

  let expanded = $state<Record<string, boolean>>({})
  let showCreateInput = $state(false)
  let newPath = $state('')
  let creating = $state(false)
  let createError = $state<string | null>(null)
  let deletingPath = $state<string | null>(null)

  let projectId = $derived(workspaceId($path) ?? '')

  $effect(() => {
    const id = projectId
    if (!id) return
    loadProject(id)
    files.load(id)
  })

  async function loadProject(id: string) {
    project = null
    projectError = null
    notFound = false

    try {
      project = await apiFetch<ProjectDetails>(`/api/projects/${id}`)
    } catch (error) {
      if (error instanceof ApiError && error.status === 404) {
        notFound = true
      } else {
        projectError = error instanceof Error ? error.message : 'Failed to load project.'
      }
    }
  }

  function isExpanded(path: string) {
    return expanded[path] !== false
  }

  function toggleFolder(path: string) {
    expanded[path] = !isExpanded(path)
  }

  type TreeNode =
    | { type: 'folder'; name: string; path: string; children: TreeNode[] }
    | { type: 'file'; name: string; path: string; file: ProjectFile }

  function buildTree(files: ProjectFile[]): TreeNode[] {
    const root: TreeNode[] = []

    for (const file of files) {
      const parts = file.path.split('/')
      let current = root
      let prefix = ''

      for (let i = 0; i < parts.length; i++) {
        const name = parts[i]
        const isLast = i === parts.length - 1
        const fullPath = prefix ? `${prefix}/${name}` : name

        if (isLast) {
          current.push({ type: 'file', name, path: fullPath, file })
        } else {
          let folder = current.find(
            (n) => n.type === 'folder' && n.name === name
          ) as Extract<TreeNode, { type: 'folder' }> | undefined

          if (!folder) {
            folder = { type: 'folder', name, path: fullPath, children: [] }
            current.push(folder)
          }

          current = folder.children
        }

        prefix = fullPath
      }
    }

    return sortNodes(root)
  }

  function sortNodes(nodes: TreeNode[]): TreeNode[] {
    return nodes
      .sort((a, b) => {
        if (a.type === b.type) return a.name.localeCompare(b.name)
        return a.type === 'folder' ? -1 : 1
      })
      .map((n) =>
        n.type === 'folder' ? { ...n, children: sortNodes(n.children) } : n
      )
  }

  type ViewNode = TreeNode & { depth: number }

  function flatten(nodes: TreeNode[], depth = 0): ViewNode[] {
    const out: ViewNode[] = []

    for (const node of nodes) {
      out.push({ ...node, depth })
      if (node.type === 'folder' && isExpanded(node.path)) {
        out.push(...flatten((node as Extract<TreeNode, { type: 'folder' }>).children, depth + 1))
      }
    }

    return out
  }

  let treeNodes = $derived(flatten(buildTree($files.items)))

  async function handleCreate(event: SubmitEvent) {
    event.preventDefault()
    const path = newPath.trim()
    if (!path) return

    creating = true
    createError = null

    try {
      await files.create(path)
      newPath = ''
      showCreateInput = false
    } catch (error) {
      createError = error instanceof Error ? error.message : 'Failed to create file.'
    } finally {
      creating = false
    }
  }

  async function handleDelete(path: string) {
    try {
      await files.remove(path)
      deletingPath = null
    } catch (error) {
      createError = error instanceof Error ? error.message : 'Failed to delete file.'
    }
  }

  function selectFile(path: string) {
    files.select(path)
  }
</script>

<div class="flex-1 flex flex-col min-h-0">
  {#if notFound}
    <div class="flex-1 flex items-center justify-center bg-bg-subtle p-4">
      <div class="border border-border bg-bg p-6 text-center">
        <h1 class="text-lg font-semibold text-text mb-2">Project not found</h1>
        <a use:link href="/projects" class="text-accent hover:underline">Back to projects</a>
      </div>
    </div>
  {:else if projectError}
    <div class="flex-1 flex items-center justify-center bg-bg-subtle p-4">
      <div class="border border-error bg-bg p-6 text-center">
        <p class="text-error mb-2">{projectError}</p>
        <a use:link href="/projects" class="text-accent hover:underline">Back to projects</a>
      </div>
    </div>
  {:else}
    <div class="h-8 shrink-0 flex items-center justify-between px-3 border-b border-border bg-bg-subtle">
      <span class="font-semibold text-text truncate">{project?.name ?? 'Loading...'}</span>
      <button
        type="button"
        disabled
        class="border border-border bg-bg px-3 py-0.5 text-xs text-text-muted disabled:opacity-60"
      >
        Compile (F6)
      </button>
    </div>

    <div class="flex-1 min-h-0 flex">
      <aside class="w-60 shrink-0 border-r border-border flex flex-col min-h-0 bg-bg">
        <div class="h-8 shrink-0 flex items-center justify-between px-2 border-b border-border bg-bg-subtle">
          <span class="text-xs font-medium text-text-muted uppercase tracking-wide">Files</span>
          <button
            type="button"
            onclick={() => {
              showCreateInput = true
              createError = null
            }}
            disabled={showCreateInput}
            class="text-sm text-accent hover:opacity-90 disabled:opacity-50"
          >
            +
          </button>
        </div>

        {#if createError}
          <div class="border-b border-error p-2 text-xs text-error">{createError}</div>
        {/if}

        {#if showCreateInput}
          <form onsubmit={handleCreate} class="border-b border-border p-2">
            <input
              type="text"
              bind:value={newPath}
              placeholder="notes.tex or sections/intro.tex"
              class="w-full border border-border bg-bg p-1 text-text focus:outline-none focus:border-accent mb-2"
            />
            <div class="flex gap-2">
              <button
                type="submit"
                disabled={!newPath.trim() || creating}
                class="border border-accent bg-accent px-2 py-1 text-xs text-white disabled:opacity-50"
              >
                Create
              </button>
              <button
                type="button"
                onclick={() => {
                  showCreateInput = false
                  newPath = ''
                  createError = null
                }}
                class="border border-border bg-bg px-2 py-1 text-xs text-text hover:bg-bg-subtle"
              >
                Cancel
              </button>
            </div>
          </form>
        {/if}

        <div class="flex-1 overflow-auto">
          {#if $files.loading && $files.items.length === 0}
            <div class="p-2 text-text-muted text-xs">Loading files...</div>
          {:else if $files.items.length === 0}
            <div class="p-2 text-text-muted text-xs">No files yet.</div>
          {:else}
            {#each treeNodes as node (node.path)}
              <div
                class="group flex items-center"
                class:bg-bg-subtle={node.type === 'file' && node.path === $files.activePath}
              >
                <button
                  type="button"
                  onclick={() =>
                    node.type === 'folder' ? toggleFolder(node.path) : selectFile(node.path)}
                  class="flex-1 flex items-center gap-1 py-1.5 px-2 text-left text-text hover:bg-bg-subtle"
                  style="padding-left: {node.depth * 16 + 8}px"
                >
                  <span class="w-3 text-text-muted text-center">
                    {#if node.type === 'folder'}
                      {isExpanded(node.path) ? '▾' : '▸'}
                    {:else}
                      &nbsp;
                    {/if}
                  </span>
                  <span class="truncate">{node.name}</span>
                </button>

                {#if node.type === 'file'}
                  <div class="shrink-0 pr-2">
                    {#if deletingPath === node.path}
                      <span class="text-text-muted text-xs">Delete?</span>
                      <button
                        type="button"
                        onclick={(event) => {
                          event.stopPropagation()
                          handleDelete(node.path)
                        }}
                        class="ml-1 text-xs text-error hover:underline"
                      >
                        Yes
                      </button>
                      <button
                        type="button"
                        onclick={(event) => {
                          event.stopPropagation()
                          deletingPath = null
                        }}
                        class="ml-1 text-xs text-text-muted hover:underline"
                      >
                        No
                      </button>
                    {:else}
                      <button
                        type="button"
                        onclick={(event) => {
                          event.stopPropagation()
                          deletingPath = node.path
                        }}
                        class="text-xs text-error opacity-0 group-hover:opacity-100 hover:underline"
                      >
                        Delete
                      </button>
                    {/if}
                  </div>
                {/if}
              </div>
            {/each}
          {/if}
        </div>
      </aside>

      <section class="flex-1 min-w-0 border-r border-border flex flex-col min-h-0 bg-bg">
        <div class="h-8 shrink-0 flex items-center px-3 border-b border-border bg-bg-subtle text-xs text-text-muted">
          {$files.activePath ?? 'main.tex — editor loads here (F5)'}
        </div>
        <div class="flex-1 overflow-auto">
          {#if $files.loading && $files.activePath}
            <div class="p-4 text-text-muted">Loading content...</div>
          {:else if $files.activePath}
            <pre class="p-4 whitespace-pre-wrap">{$files.activeContent}</pre>
          {:else}
            <div class="p-4 text-text-muted">Select a file to view its content.</div>
          {/if}
        </div>
      </section>

      <section class="flex-1 min-w-0 flex flex-col min-h-0 bg-bg">
        <div class="h-8 shrink-0 flex items-center px-3 border-b border-border bg-bg-subtle text-xs text-text-muted">
          Preview
        </div>
        <div class="flex-1 flex items-center justify-center text-text-muted">
          PDF preview (F7)
        </div>
      </section>
    </div>
  {/if}
</div>
