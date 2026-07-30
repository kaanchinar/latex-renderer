import { describe, it, expect, beforeEach, vi } from 'vitest'
import { apiFetch, ApiError } from '../api/client'
import type { Project, ProjectsState } from './projects'

vi.mock('../api/client', () => {
  class ApiError extends Error {
    status: number
    errors: string[]
    constructor(status: number, message: string, errors: string[] = []) {
      super(message)
      this.status = status
      this.errors = errors
    }
  }
  return { ApiError, apiFetch: vi.fn() }
})

const mockedApiFetch = vi.mocked(apiFetch)

function emptyState(): ProjectsState {
  return { items: [], loading: false, error: null }
}

describe('projects store', () => {
  beforeEach(() => {
    vi.resetModules()
    mockedApiFetch.mockReset()
  })

  const project: Project = {
    id: 'p1',
    name: 'Alpha',
    slug: 'alpha',
    lastCompileStatus: null,
    createdAt: '2024-01-01T00:00:00Z'
  }

  it('loads projects on success', async () => {
    mockedApiFetch.mockResolvedValueOnce([project])
    const { projects } = await import('./projects')

    await projects.load()
    const state = emptyState()
    projects.subscribe((s) => Object.assign(state, s))()

    expect(mockedApiFetch).toHaveBeenCalledWith('/api/projects')
    expect(state.items).toEqual([project])
    expect(state.loading).toBe(false)
    expect(state.error).toBe(null)
  })

  it('sets error state on load failure', async () => {
    mockedApiFetch.mockRejectedValueOnce(new ApiError(500, 'Server error'))
    const { projects } = await import('./projects')

    await projects.load()
    const state = emptyState()
    projects.subscribe((s) => Object.assign(state, s))()

    expect(state.items).toEqual([])
    expect(state.loading).toBe(false)
    expect(state.error).toBe('Server error')
  })

  it('create adds the project to the list', async () => {
    const created: Project = { ...project, id: 'p2', name: 'Beta', slug: 'beta' }
    mockedApiFetch.mockResolvedValueOnce([]).mockResolvedValueOnce(created)
    const { projects } = await import('./projects')
    await projects.load()

    await projects.create('Beta')
    const state = emptyState()
    projects.subscribe((s) => Object.assign(state, s))()

    expect(state.items[0]).toEqual(created)
    expect(state.error).toBe(null)
  })

  it('rename updates the matching project', async () => {
    const renamed: Project = { ...project, name: 'Alpha renamed' }
    mockedApiFetch.mockResolvedValueOnce([project]).mockResolvedValueOnce(renamed)
    const { projects } = await import('./projects')
    await projects.load()

    await projects.rename(project.id, 'Alpha renamed')
    const state = emptyState()
    projects.subscribe((s) => Object.assign(state, s))()

    expect(state.items[0].name).toBe('Alpha renamed')
    expect(state.error).toBe(null)
  })

  it('remove deletes the matching project', async () => {
    const other: Project = { ...project, id: 'p2', name: 'Beta', slug: 'beta' }
    mockedApiFetch.mockResolvedValueOnce([project, other])
    const { projects } = await import('./projects')
    await projects.load()

    mockedApiFetch.mockResolvedValueOnce(undefined)
    await projects.remove(project.id)
    const state = emptyState()
    projects.subscribe((s) => Object.assign(state, s))()

    expect(state.items).toEqual([other])
    expect(state.error).toBe(null)
  })
})
