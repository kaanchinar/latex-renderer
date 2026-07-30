import { writable } from 'svelte/store'
import { apiFetch, ApiError } from '../api/client'

export type ProjectFile = {
  id: string
  projectId: string
  path: string
  content: string
  isBinary: boolean
  createdAt: string
  updatedAt: string
}

type FilesState = {
  projectId: string | null
  items: ProjectFile[]
  activePath: string | null
  activeContent: string
  loading: boolean
  saving: boolean
  error: string | null
}

function createFiles() {
  const { subscribe, set, update } = writable<FilesState>({
    projectId: null,
    items: [],
    activePath: null,
    activeContent: '',
    loading: false,
    saving: false,
    error: null
  })

  function setError(error: unknown) {
    return error instanceof ApiError
      ? error.message
      : error instanceof Error
        ? error.message
        : 'Failed to load files.'
  }

  async function load(projectId: string) {
    update((state) => ({
      ...state,
      projectId,
      loading: true,
      error: null,
      activePath: null,
      activeContent: ''
    }))

    try {
      const items = await apiFetch<ProjectFile[]>(`/api/projects/${projectId}/files`)
      set({
        projectId,
        items,
        activePath: null,
        activeContent: '',
        loading: false,
        saving: false,
        error: null
      })
    } catch (error) {
      update((state) => ({ ...state, loading: false, error: setError(error) }))
    }
  }

  async function select(path: string) {
    update((state) => ({
      ...state,
      activePath: path,
      activeContent: '',
      loading: true,
      error: null
    }))

    try {
      const file = await apiFetch<ProjectFile>(
        `/api/projects/${getProjectId()}/files/${encodeURIComponent(path)}`
      )
      update((state) => ({
        ...state,
        activeContent: file.content,
        loading: false
      }))
    } catch (error) {
      update((state) => ({
        ...state,
        activePath: null,
        activeContent: '',
        loading: false,
        error: setError(error)
      }))
    }
  }

  async function create(path: string) {
    const projectId = getProjectId()
    await apiFetch(`/api/projects/${projectId}/files/${encodeURIComponent(path)}`, {
      method: 'PUT',
      body: { content: `% ${path}` }
    })
    await load(projectId)
  }

  async function remove(path: string) {
    const projectId = getProjectId()
    await apiFetch(`/api/projects/${projectId}/files/${encodeURIComponent(path)}`, {
      method: 'DELETE'
    })

    update((state) => ({
      ...state,
      items: state.items.filter((f) => f.path !== path),
      activePath: state.activePath === path ? null : state.activePath,
      activeContent: state.activePath === path ? '' : state.activeContent,
      error: null
    }))
  }

  async function updateContent(path: string, content: string) {
    const projectId = getProjectId()
    update((state) => ({ ...state, saving: true, error: null }))

    try {
      await apiFetch(`/api/projects/${projectId}/files/${encodeURIComponent(path)}`, {
        method: 'PUT',
        body: { content }
      })
      update((state) => ({ ...state, saving: false }))
    } catch (error) {
      update((state) => ({ ...state, saving: false, error: setError(error) }))
    }
  }

  function getProjectId(): string {
    let projectId = ''
    subscribe((state) => {
      projectId = state.projectId ?? ''
    })()
    return projectId
  }

  return {
    subscribe,
    load,
    select,
    create,
    remove,
    updateContent
  }
}

export const files = createFiles()
