import { writable } from 'svelte/store'

function getPath(): string {
  if (typeof window === 'undefined') {
    return '/'
  }

  const hash = window.location.hash
  if (!hash || hash === '#') {
    return '/'
  }

  return hash.slice(1) || '/'
}

export const path = writable<string>(getPath())

function syncPath() {
  path.set(getPath())
}

export function navigate(to: string) {
  if (typeof window === 'undefined') {
    return
  }

  window.location.hash = `#${to}`
}

export function workspaceSlug(path: string): string | null {
  if (path === '/projects' || path === '/projects/') {
    return null
  }
  if (path.startsWith('/projects/')) {
    return path.slice('/projects/'.length)
  }
  return null
}

export function link(node: HTMLAnchorElement) {
  const rawHref = node.getAttribute('href') || '/'
  node.href = `#${rawHref}`

  const handleClick = (event: MouseEvent) => {
    event.preventDefault()
    navigate(rawHref)
  }

  node.addEventListener('click', handleClick)

  return {
    destroy() {
      node.removeEventListener('click', handleClick)
    }
  }
}

if (typeof window !== 'undefined') {
  window.addEventListener('hashchange', syncPath)

  if (getPath() === '/') {
    navigate('/projects')
  }
}
