import { writable } from 'svelte/store'
import { apiFetch, ApiError } from '../api/client'

export type CompileStatus = 'Success' | 'Failed' | 'Running' | 'Queued' | 'Cancelled' | null

export type Project = {
  id: string
  name: string
  slug: string
  lastCompileStatus: CompileStatus
  createdAt: string
}

export type ProjectsState = {
  items: Project[]
  loading: boolean
  error: string | null
}

function createProjects() {
  const { subscribe, set, update } = writable<ProjectsState>({
    items: [],
    loading: false,
    error: null
  })

  async function load() {
    update((state) => ({ ...state, loading: true, error: null }))

    try {
      const items = await apiFetch<Project[]>('/api/projects')
      set({ items, loading: false, error: null })
    } catch (error) {
      set({
        items: [],
        loading: false,
        error: error instanceof ApiError ? error.message : 'Failed to load projects.'
      })
    }
  }

  async function create(name: string) {
    const item = await apiFetch<Project>('/api/projects', {
      method: 'POST',
      body: { name }
    })

    update((state) => ({ ...state, items: [item, ...state.items], error: null }))
    return item
  }

  async function rename(id: string, name: string) {
    const item = await apiFetch<Project>(`/api/projects/${id}`, {
      method: 'PUT',
      body: { name }
    })

    update((state) => ({
      ...state,
      items: state.items.map((p) => (p.id === id ? item : p)),
      error: null
    }))
    return item
  }

  async function remove(id: string) {
    await apiFetch(`/api/projects/${id}`, { method: 'DELETE' })

    update((state) => ({
      ...state,
      items: state.items.filter((p) => p.id !== id),
      error: null
    }))
  }

  return {
    subscribe,
    load,
    create,
    rename,
    remove
  }
}

export const projects = createProjects()
