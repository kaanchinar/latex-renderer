import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest'

function createLocalStorage() {
  const store: Record<string, string> = {}
  return {
    getItem: (key: string) => store[key] ?? null,
    setItem: (key: string, value: string) => {
      store[key] = value
    },
    removeItem: (key: string) => {
      delete store[key]
    },
    clear: () => {
      for (const key in store) delete store[key]
    },
    length: 0,
    key: () => null
  }
}

describe('theme store', () => {
  const unsubscribers: (() => void)[] = []

  beforeEach(() => {
    vi.resetModules()
    vi.stubGlobal('localStorage', createLocalStorage())
    document.documentElement.classList.remove('dark')
  })

  afterEach(() => {
    unsubscribers.forEach((unsubscribe) => unsubscribe())
    unsubscribers.length = 0
    vi.restoreAllMocks()
  })

  function mockMatchMedia(prefersDark: boolean) {
    Object.defineProperty(window, 'matchMedia', {
      writable: true,
      value: vi.fn((query: string) => ({
        matches: prefersDark,
        media: query,
        addEventListener: vi.fn(),
        removeEventListener: vi.fn(),
        dispatchEvent: vi.fn()
      }))
    })
  }

  it('defaults to light when matchMedia does not prefer dark', async () => {
    mockMatchMedia(false)
    const { theme } = await import('./theme')
    let value = ''
    unsubscribers.push(theme.subscribe((v) => (value = v)))
    expect(value).toBe('light')
  })

  it('defaults to dark when matchMedia prefers dark', async () => {
    mockMatchMedia(true)
    const { theme } = await import('./theme')
    let value = ''
    unsubscribers.push(theme.subscribe((v) => (value = v)))
    expect(value).toBe('dark')
  })

  it('prefers localStorage over matchMedia', async () => {
    localStorage.setItem('latex-renderer-theme', 'dark')
    mockMatchMedia(false)
    const { theme } = await import('./theme')
    let value = ''
    unsubscribers.push(theme.subscribe((v) => (value = v)))
    expect(value).toBe('dark')
  })

  it('toggles theme and persists to localStorage', async () => {
    mockMatchMedia(false)
    const { theme, toggleTheme } = await import('./theme')
    let value = ''
    unsubscribers.push(theme.subscribe((v) => (value = v)))

    toggleTheme()
    expect(value).toBe('dark')
    expect(localStorage.getItem('latex-renderer-theme')).toBe('dark')
    expect(document.documentElement.classList.contains('dark')).toBe(true)

    toggleTheme()
    expect(value).toBe('light')
    expect(localStorage.getItem('latex-renderer-theme')).toBe('light')
    expect(document.documentElement.classList.contains('dark')).toBe(false)
  })
})
