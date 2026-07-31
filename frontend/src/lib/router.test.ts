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

  it('exposes workspace slug helper', async () => {
    const { workspaceSlug } = await import('./router')
    expect(workspaceSlug('/projects/my-slug')).toBe('my-slug')
    expect(workspaceSlug('/projects')).toBeNull()
    expect(workspaceSlug('/projects/')).toBeNull()
    expect(workspaceSlug('/login')).toBeNull()
  })
})
