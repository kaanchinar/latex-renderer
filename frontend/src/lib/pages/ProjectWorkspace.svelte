<script lang="ts">
  import { onDestroy } from 'svelte'
  import { path, link, workspaceId } from '../router'
  import { files, type ProjectFile } from '../stores/files'
  import { compile } from '../stores/compile'
  import { parseLogErrors } from '../editor/logErrors'
  import * as hub from '../hub'
  import { apiFetch, ApiError } from '../api/client'
  import { push as pushToast } from '../stores/toast'
  import Editor from '../editor/Editor.svelte'
  import LogsDrawer from '../components/LogsDrawer.svelte'
  import PdfViewer from '../pdf/PdfViewer.svelte'
  import JobHistory from '../components/JobHistory.svelte'
  import Menu from '../components/Menu.svelte'
  import Splitter from '../components/Splitter.svelte'
  import { toggleTheme } from '../stores/theme'

  type ProjectDetails = {
    id: string
    name: string
  }

  let project = $state<ProjectDetails | null>(null)
  let notFound = $state(false)
  let projectError = $state<string | null>(null)

  let expanded = $state<Record<string, boolean>>({})
  let showCreatePopover = $state(false)
  let createButtonEl: HTMLButtonElement | null = $state(null)
  let createPopoverEl: HTMLDivElement | null = $state(null)
  let createInputEl: HTMLInputElement | null = $state(null)
  let newPath = $state('')
  let creating = $state(false)
  let createError = $state<string | null>(null)
  let deletingPath = $state<string | null>(null)
  let logsOpen = $state(false)
  let sessionCompileRan = $state(false)

  let cursorLine = $state(1)
  let cursorCol = $state(1)
  let wordCount = $state(0)
  let charCount = $state(0)
  let statsTimeout: ReturnType<typeof setTimeout> | null = null

  let projectId = $derived(workspaceId($path) ?? '')

  let saveTimeout: ReturnType<typeof setTimeout> | null = null
  let savePending: { path: string; content: string } | null = null
  let saveState = $state<'idle' | 'saving' | 'saved'>('idle')
  let saveStateTimeout: ReturnType<typeof setTimeout> | null = null
  let compileTimeout: ReturnType<typeof setTimeout> | null = null
  let previousActivePath: string | null = null
  let selectedPdfUrl = $state<string | null>(null)
  let viewerUrl = $derived($compile.pdfUrl ?? selectedPdfUrl)

  let openMenu = $state<string | null>(null)
  let editorRef = $state<Editor | null>(null)
  let downloadAnchor: HTMLAnchorElement | null = $state(null)

  let mainEl: HTMLElement | null = $state(null)
  let mainWidth = $state(0)
  let sidebarCollapsed = $state(loadBool('lr-sidebar-collapsed', false))
  let sidebarWidth = $state(loadSize('lr-sidebar', 240))
  let editorWidth = $state(loadSize('lr-editor', 0))
  let logsHeight = $state(loadSize('lr-logs', 160))
  let windowHeight = $state(
    typeof window !== 'undefined' ? window.innerHeight : 0
  )

  const SPLITTER = 8

  let effectiveSidebarWidth = $derived(
    sidebarCollapsed ? 0 : clampSidebarWidth(sidebarWidth)
  )
  let effectiveEditorWidth = $derived(
    editorWidth > 0 ? clampEditorWidth(editorWidth) : defaultEditorWidth()
  )
  let effectiveLogsHeight = $derived(clampLogsHeight(logsHeight))

  let fileOpen = $derived($files.activePath !== null)

  function loadSize(key: string, fallback: number): number {
    if (typeof localStorage === 'undefined') return fallback
    const v = localStorage.getItem(key)
    if (!v) return fallback
    const n = parseInt(v, 10)
    return isNaN(n) ? fallback : n
  }

  function saveSize(key: string, value: number) {
    if (typeof localStorage !== 'undefined') {
      localStorage.setItem(key, String(value))
    }
  }

  function loadBool(key: string, fallback: boolean): boolean {
    if (typeof localStorage === 'undefined') return fallback
    const v = localStorage.getItem(key)
    return v === null ? fallback : v === 'true'
  }

  function saveBool(key: string, value: boolean) {
    if (typeof localStorage !== 'undefined') {
      localStorage.setItem(key, String(value))
    }
  }

  function leftSplitterWidth() {
    return sidebarCollapsed ? 0 : SPLITTER
  }

  function rightSplitterWidth() {
    return SPLITTER
  }

  function maxSidebarWidth() {
    const cw = mainWidth
    if (cw <= 0) return 160
    return Math.max(
      160,
      cw - leftSplitterWidth() - rightSplitterWidth() - 300 - 300
    )
  }

  function clampSidebarWidth(w: number) {
    return Math.max(160, Math.min(w, maxSidebarWidth()))
  }

  function maxEditorWidth() {
    const cw = mainWidth
    const sw = effectiveSidebarWidth
    if (cw <= 0) return 300
    return Math.max(
      300,
      cw - sw - leftSplitterWidth() - rightSplitterWidth() - 300
    )
  }

  function clampEditorWidth(w: number) {
    return Math.max(300, Math.min(w, maxEditorWidth()))
  }

  function defaultEditorWidth() {
    const cw = mainWidth
    if (cw <= 0) return 500
    return Math.max(
      300,
      Math.floor(
        (cw - effectiveSidebarWidth - leftSplitterWidth() - rightSplitterWidth()) / 2
      )
    )
  }

  function maxLogsHeight() {
    return Math.max(120, Math.floor(windowHeight / 2))
  }

  function clampLogsHeight(h: number) {
    return Math.max(120, Math.min(h, maxLogsHeight()))
  }

  function handleSidebarResize(w: number) {
    sidebarCollapsed = false
    sidebarWidth = clampSidebarWidth(w)
  }

  function handleSidebarResizeEnd() {
    saveSize('lr-sidebar', effectiveSidebarWidth)
    saveBool('lr-sidebar-collapsed', false)
  }

  function resetSidebar() {
    sidebarCollapsed = false
    sidebarWidth = 240
    saveSize('lr-sidebar', effectiveSidebarWidth)
    saveBool('lr-sidebar-collapsed', false)
  }

  function handleEditorResize(w: number) {
    editorWidth = clampEditorWidth(w)
  }

  function handleEditorResizeEnd() {
    saveSize('lr-editor', effectiveEditorWidth)
  }

  function resetEditor() {
    editorWidth = defaultEditorWidth()
    saveSize('lr-editor', effectiveEditorWidth)
  }

  function handleLogsResize(h: number) {
    logsHeight = clampLogsHeight(h)
  }

  function handleLogsResizeEnd() {
    saveSize('lr-logs', effectiveLogsHeight)
  }

  function resetLogsHeight() {
    logsHeight = 160
    saveSize('lr-logs', effectiveLogsHeight)
  }

  function toggleSidebar() {
    if (sidebarCollapsed) {
      sidebarCollapsed = false
      if (sidebarWidth < 160) sidebarWidth = 240
      saveBool('lr-sidebar-collapsed', false)
    } else {
      sidebarCollapsed = true
      saveBool('lr-sidebar-collapsed', true)
    }
  }

  function toggleLogs() {
    logsOpen = !logsOpen
  }

  function setSaveState(next: 'idle' | 'saving' | 'saved') {
    if (saveStateTimeout) {
      clearTimeout(saveStateTimeout)
      saveStateTimeout = null
    }
    saveState = next
    if (next === 'saved') {
      saveStateTimeout = setTimeout(() => {
        saveState = 'idle'
        saveStateTimeout = null
      }, 1500)
    }
  }

  $effect(() => {
    const id = projectId
    if (!id) return
    loadProject(id)
    files.load(id)
  })

  $effect(() => {
    const id = projectId
    if (!id) return

    sessionCompileRan = false
    hub.joinProject(id).catch(() => {})
    compile.start(id)

    return () => {
      flushSave()
      cancelCompile()
      compile.stop()
      hub.leaveCurrentProject()
    }
  })

  $effect(() => {
    const path = $files.activePath
    if (previousActivePath !== path) {
      flushSave()
      cancelCompile()
      previousActivePath = path
    }
  })

  $effect(() => {
    if (
      $compile.status === 'running' ||
      $compile.status === 'queued'
    ) {
      sessionCompileRan = true
    }
  })

  $effect(() => {
    if ($compile.status === 'failed') {
      logsOpen = true
    }
  })

  $effect(() => {
    function onResize() {
      windowHeight = window.innerHeight
    }
    window.addEventListener('resize', onResize)
    return () => window.removeEventListener('resize', onResize)
  })

  let activeErrors = $derived(
    $files.activePath
      ? parseLogErrors($compile.logs.join('\n')).filter(
          (e) => !e.file || e.file === $files.activePath!.split('/').pop()
        )
      : []
  )

  $effect(() => {
    const text = $files.activeContent
    if (statsTimeout) clearTimeout(statsTimeout)
    statsTimeout = setTimeout(() => {
      charCount = text.length
      wordCount = text.trim() === '' ? 0 : text.trim().split(/\s+/).length
    }, 300)
    return () => {
      if (statsTimeout) {
        clearTimeout(statsTimeout)
        statsTimeout = null
      }
    }
  })

  $effect(() => {
    if (showCreatePopover && createInputEl) {
      createInputEl.focus()
    }
  })

  $effect(() => {
    if (!showCreatePopover) return

    function onDocClick(event: MouseEvent) {
      const target = event.target as Node | null
      if (!target) return
      if (
        createPopoverEl?.contains(target) ||
        createButtonEl?.contains(target)
      ) {
        return
      }
      showCreatePopover = false
    }

    document.addEventListener('pointerdown', onDocClick)
    return () => document.removeEventListener('pointerdown', onDocClick)
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
        out.push(
          ...flatten(
            (node as Extract<TreeNode, { type: 'folder' }>).children,
            depth + 1
          )
        )
      }
    }

    return out
  }

  let treeNodes = $derived(flatten(buildTree($files.items)))

  function validatePath(value: string): string | null {
    const trimmed = value.trim()
    if (!trimmed) return 'Path is required.'
    if (trimmed.startsWith('/') || trimmed.endsWith('/')) {
      return 'No leading or trailing slashes.'
    }
    const name = trimmed.split('/').pop() ?? ''
    if (!name.includes('.')) {
      return 'File name must end in .tex or include an extension.'
    }
    return null
  }

  async function handleCreate() {
    const trimmed = newPath.trim()
    const validationError = validatePath(trimmed)
    if (validationError) {
      createError = validationError
      return
    }

    creating = true
    createError = null

    try {
      await files.create(trimmed)
      newPath = ''
      showCreatePopover = false
    } catch (error) {
      createError = error instanceof Error ? error.message : 'Failed to create file.'
    } finally {
      creating = false
    }
  }

  function cancelCreate() {
    showCreatePopover = false
    newPath = ''
    createError = null
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

  function scheduleSave() {
    cancelSave()
    cancelCompile()

    const id = projectId
    const path = $files.activePath
    const content = $files.activeContent
    if (!id || !path) return

    setSaveState('saving')
    savePending = { path, content }
    saveTimeout = setTimeout(() => {
      saveTimeout = null
      flushSave()
    }, 300)
  }

  async function flushSave() {
    cancelSave()
    if (!savePending) return

    const { path, content } = savePending
    savePending = null

    setSaveState('saving')
    try {
      await hub.updateFile(projectId, path, content)
      scheduleCompile()
      setSaveState('saved')
    } catch {
      savePending = { path, content }
      setSaveState('idle')
      pushToast('Save failed — retrying')
    }
  }

  function cancelSave() {
    if (saveTimeout) {
      clearTimeout(saveTimeout)
      saveTimeout = null
    }
  }

  function scheduleCompile() {
    cancelCompile()

    const id = projectId
    if (!id) return

    compileTimeout = setTimeout(() => {
      compileTimeout = null
      compile.triggerCompile(id)
    }, 1200)
  }

  function cancelCompile() {
    if (compileTimeout) {
      clearTimeout(compileTimeout)
      compileTimeout = null
    }
  }

  function handleChange(value: string) {
    files.updateActiveContent(value)
    scheduleSave()
  }

  async function handleCompile() {
    await flushSave()
    if (projectId) {
      sessionCompileRan = true
      compile.triggerCompile(projectId)
    }
  }

  function downloadCurrentPdf() {
    if (!viewerUrl || !downloadAnchor) return
    downloadAnchor.href = viewerUrl
    downloadAnchor.click()
  }

  function deleteCurrentFile() {
    const activePath = $files.activePath
    if (!activePath) return
    if (confirm('Delete current file?')) {
      files.remove(activePath)
    }
  }

  let fileMenu = $derived([
    {
      label: 'New file…',
      action: () => {
        sidebarCollapsed = false
        showCreatePopover = true
        createError = null
      }
    },
    {
      label: 'Delete current file',
      disabled: !fileOpen,
      action: deleteCurrentFile
    },
    {
      label: 'Download PDF',
      disabled: !viewerUrl,
      action: downloadCurrentPdf
    }
  ])

  let editMenu = $derived([
    { label: 'Undo', shortcut: 'Ctrl+Z', disabled: !fileOpen, action: () => editorRef?.undo() },
    { label: 'Redo', shortcut: 'Ctrl+Y', disabled: !fileOpen, action: () => editorRef?.redo() },
    { label: 'Find in file', shortcut: 'Ctrl+F', disabled: !fileOpen, action: () => editorRef?.findInFile() },
    { label: 'Replace', shortcut: 'Ctrl+H', disabled: !fileOpen, action: () => editorRef?.replaceInFile() }
  ])

  let viewMenu = $derived([
    {
      label: sidebarCollapsed ? 'Show sidebar' : 'Hide sidebar',
      action: toggleSidebar
    },
    {
      label: logsOpen ? 'Hide logs panel' : 'Show logs panel',
      action: toggleLogs
    },
    {
      label: 'Toggle theme',
      action: toggleTheme
    }
  ])

  onDestroy(() => {
    if (saveTimeout) clearTimeout(saveTimeout)
    if (saveStateTimeout) clearTimeout(saveStateTimeout)
    if (compileTimeout) clearTimeout(compileTimeout)
    if (statsTimeout) clearTimeout(statsTimeout)
  })
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
    <div
      bind:this={mainEl}
      bind:clientWidth={mainWidth}
      class="flex-1 flex flex-col min-h-0"
    >
      <!-- Menu bar -->
      <div class="h-8 shrink-0 flex items-center justify-between px-2 border-b border-border bg-bg-subtle">
        <div class="flex items-center gap-1">
          <a
            use:link
            href="/projects"
            class="h-8 px-3 flex items-center text-xs text-text hover:bg-bg-subtle"
          >
            ← Projects
          </a>
          <Menu label="File" name="file" openName={openMenu} onOpen={(n) => (openMenu = n)} items={fileMenu} />
          <Menu label="Edit" name="edit" openName={openMenu} onOpen={(n) => (openMenu = n)} items={editMenu} />
          <Menu label="View" name="view" openName={openMenu} onOpen={(n) => (openMenu = n)} items={viewMenu} />
        </div>
        <div class="flex items-center gap-3">
          {#if $compile.status !== 'idle'}
            <span
              data-testid="compile-status-menubar"
              class="text-xs"
              class:text-accent={$compile.status === 'running' || $compile.status === 'queued'}
              class:text-success={$compile.status === 'success'}
              class:text-error={$compile.status === 'failed'}
              title={$compile.status === 'failed' ? ($compile.error ?? 'Compile failed') : undefined}
            >
              {#if $compile.status === 'running' || $compile.status === 'queued'}
                compiling…
              {:else if $compile.status === 'success'}
                ✔ compiled
              {:else if $compile.status === 'failed'}
                ✘ failed
              {/if}
            </span>
          {/if}
          <JobHistory {projectId} onSelect={(url) => (selectedPdfUrl = url)} />
          <button
            type="button"
            data-testid="compile-button"
            onclick={handleCompile}
            disabled={$compile.status === 'running' || $compile.status === 'queued'}
            class="border border-accent bg-accent px-3 py-0.5 text-xs text-white hover:opacity-90 disabled:opacity-60"
          >
            Compile (F6)
          </button>
        </div>
      </div>

      <a
        bind:this={downloadAnchor}
        href={viewerUrl ?? undefined}
        download={`${project?.name ?? 'document'}.pdf`}
        class="hidden"
        aria-hidden="true"
      ></a>

      <!-- Workspace -->
      <div class="flex-1 min-h-0 flex">
        <!-- Sidebar -->
        <aside
          class="shrink-0 flex flex-col min-h-0 bg-bg border-r border-border overflow-hidden"
          style="width: {effectiveSidebarWidth}px"
        >
          <div class="h-8 shrink-0 flex items-center justify-between px-2 border-b border-border bg-bg-subtle relative">
            <span class="text-xs font-medium text-text-muted uppercase tracking-wide">Files</span>
            <button
              type="button"
              bind:this={createButtonEl}
              data-testid="new-file-button"
              onclick={() => {
                showCreatePopover = !showCreatePopover
                createError = null
              }}
              class="text-sm text-accent hover:opacity-90"
              aria-label="New file"
            >
              +
            </button>

            {#if showCreatePopover}
              <div
                bind:this={createPopoverEl}
                data-testid="new-file-popover"
                class="absolute left-0 top-full mt-px w-full border border-border bg-bg p-2 z-20 flex flex-col gap-2"
              >
                <input
                  type="text"
                  bind:this={createInputEl}
                  bind:value={newPath}
                  data-testid="new-file-input"
                  placeholder="notes.tex or sections/intro.tex"
                  onkeydown={(event) => {
                    if (event.key === 'Escape') cancelCreate()
                  }}
                  class="w-full border border-border bg-bg p-1 text-text focus:outline-none focus:border-accent"
                />
                {#if createError}
                  <div class="text-xs text-error">{createError}</div>
                {/if}
                <div class="flex gap-2">
                  <button
                    type="button"
                    data-testid="create-file-button"
                    onclick={handleCreate}
                    disabled={!newPath.trim() || creating}
                    class="border border-accent bg-accent px-2 py-1 text-xs text-white disabled:opacity-50"
                  >
                    Create
                  </button>
                  <button
                    type="button"
                    onclick={cancelCreate}
                    class="border border-border bg-bg px-2 py-1 text-xs text-text hover:bg-bg-subtle"
                  >
                    Cancel
                  </button>
                </div>
              </div>
            {/if}
          </div>

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
                    data-testid={node.type === 'file' ? 'file-item' : undefined}
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
                          aria-label="Delete file"
                        >
                          ✕
                        </button>
                      {/if}
                    </div>
                  {/if}
                </div>
              {/each}
            {/if}
          </div>
        </aside>

        {#if !sidebarCollapsed}
          <Splitter
            direction="vertical"
            value={effectiveSidebarWidth}
            min={160}
            max={maxSidebarWidth()}
            onChange={handleSidebarResize}
            onEnd={handleSidebarResizeEnd}
            onReset={resetSidebar}
            testid="sidebar-splitter"
          />
        {/if}

        <!-- Editor + preview -->
        <div class="flex-1 min-w-0 flex min-h-0">
          <!-- Editor stack -->
          <div
            class="shrink-0 flex flex-col min-h-0 overflow-hidden"
            style="width: {effectiveEditorWidth}px"
          >
            <div class="h-8 shrink-0 flex items-center justify-between px-3 border-b border-border bg-bg-subtle text-xs">
              <span class="text-text-muted truncate">{$files.activePath ?? 'No file selected'}</span>
              <span class="text-text-muted">
                {#if saveState === 'saving'}saving…{:else if saveState === 'saved'}saved{/if}
              </span>
            </div>
            <div class="flex-1 min-h-0 overflow-hidden">
              {#if $files.loading && $files.activePath}
                <div class="p-4 text-text-muted">Loading content...</div>
              {:else if $files.activePath}
                <Editor
                  bind:this={editorRef}
                  value={$files.activeContent}
                  onChange={handleChange}
                  onSave={handleCompile}
                  errors={activeErrors}
                  onCursorChange={(line, col) => {
                    cursorLine = line
                    cursorCol = col
                  }}
                />
              {:else}
                <div class="p-4 text-text-muted">Select a file to edit</div>
              {/if}
            </div>
          </div>

          <Splitter
            direction="vertical"
            value={effectiveEditorWidth}
            min={300}
            max={maxEditorWidth()}
            onChange={handleEditorResize}
            onEnd={handleEditorResizeEnd}
            onReset={resetEditor}
            testid="editor-splitter"
          />

          <!-- Preview -->
          <div class="flex-1 min-w-0 min-h-0 flex flex-col bg-bg">
            <PdfViewer url={viewerUrl} />
          </div>
        </div>
      </div>

      {#if logsOpen}
        <Splitter
          direction="horizontal"
          value={effectiveLogsHeight}
          min={120}
          max={maxLogsHeight()}
          onChange={handleLogsResize}
          onEnd={handleLogsResizeEnd}
          onReset={resetLogsHeight}
          testid="logs-splitter"
        />
        <div style="height: {effectiveLogsHeight}px">
          <LogsDrawer bind:open={logsOpen} onClear={() => compile.clearLogs()} />
        </div>
      {/if}

      <div class="h-6 shrink-0 flex items-center justify-between px-2 border-t border-border bg-bg-subtle text-xs text-text-muted">
        <div class="flex items-center gap-3">
          <span>Ln {cursorLine}, Col {cursorCol}</span>
          <span>{wordCount} words</span>
          <span>{charCount} chars</span>
        </div>
        <div class="flex items-center gap-3">
          {#if $compile.status === 'success' && $compile.lastDurationMs != null}
            <span class="text-success">✔ {($compile.lastDurationMs / 1000).toFixed(1)}s</span>
          {:else if $compile.status === 'failed'}
            <span class="text-error" title={$compile.error ?? 'Compile failed'}>✘ failed</span>
          {/if}
          <button
            type="button"
            onclick={() => (logsOpen = !logsOpen)}
            class="hover:text-text"
          >
            logs
          </button>
          {#if $compile.status !== 'idle'}
            <span
              data-testid="compile-status-statusbar"
              class="text-xs"
              class:text-accent={$compile.status === 'running' || $compile.status === 'queued'}
              class:text-success={$compile.status === 'success'}
              class:text-error={$compile.status === 'failed'}
              title={$compile.status === 'failed' ? ($compile.error ?? 'Compile failed') : undefined}
            >
              {#if $compile.status === 'running' || $compile.status === 'queued'}
                compiling…
              {:else if $compile.status === 'success'}
                ✔ compiled
              {:else if $compile.status === 'failed'}
                ✘ failed
              {/if}
            </span>
          {/if}
        </div>
      </div>
    </div>
  {/if}
</div>
