import { writable } from 'svelte/store'

type Theme = 'light' | 'dark'

const storageKey = 'latex-renderer-theme'

function getInitialTheme(): Theme {
  if (typeof window === 'undefined') {
    return 'light'
  }

  const stored = localStorage.getItem(storageKey) as Theme | null
  if (stored) {
    return stored
  }

  return window.matchMedia('(prefers-color-scheme: dark)').matches
    ? 'dark'
    : 'light'
}

export const theme = writable<Theme>(getInitialTheme())

theme.subscribe((value) => {
  if (typeof document === 'undefined') {
    return
  }

  const root = document.documentElement
  if (value === 'dark') {
    root.classList.add('dark')
  } else {
    root.classList.remove('dark')
  }

  localStorage.setItem(storageKey, value)
})

export function toggleTheme() {
  theme.update((current) => (current === 'light' ? 'dark' : 'light'))
}
