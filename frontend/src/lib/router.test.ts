import { describe, it, expect, beforeEach, vi } from 'vitest'

describe('router', () => {
  beforeEach(() => {
    vi.resetModules()
    window.location.hash = ''
  })

  it('navigate updates the current path store', async () => {
    window.location.hash = '#/projects'
    const { navigate, path } = await import('./router')

    let value = ''
    path.subscribe((p) => (value = p))()
    expect(value).toBe('/projects')

    navigate('/login')
    window.dispatchEvent(new HashChangeEvent('hashchange'))
    path.subscribe((p) => (value = p))()
    expect(value).toBe('/login')
  })

  it('keeps unknown paths unchanged', async () => {
    window.location.hash = '#/unknown/path'
    const { path } = await import('./router')

    let value = ''
    path.subscribe((p) => (value = p))()
    expect(value).toBe('/unknown/path')
  })

  it('exposes workspace id helper', async () => {
    const { workspaceId } = await import('./router')
    expect(workspaceId('/projects/abc-123')).toBe('abc-123')
    expect(workspaceId('/projects')).toBeNull()
    expect(workspaceId('/projects/')).toBeNull()
    expect(workspaceId('/login')).toBeNull()
  })
})
