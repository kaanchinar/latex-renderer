import { writable } from 'svelte/store'
import { apiFetch, ApiError } from '../api/client'
import { navigate } from '../router'
import * as hub from '../hub'

export type User = {
  id: string
  email: string
}

type AuthState =
  | { state: 'unknown'; user: null }
  | { state: 'loggedOut'; user: null }
  | { state: 'loggedIn'; user: User }

export let setLoggedOut: () => void

function createAuth() {
  const { subscribe, set } = writable<AuthState>({ state: 'unknown', user: null })

  function setLoggedIn(user: User) {
    set({ state: 'loggedIn', user })
    navigate('/projects')
  }

  setLoggedOut = () => {
    set({ state: 'loggedOut', user: null })
    navigate('/login')
  }

  async function initialize() {
    try {
      const user = await apiFetch<User>('/api/auth/me')
      setLoggedIn(user)
    } catch (error) {
      if (error instanceof ApiError && error.status === 401) {
        // apiFetch already set logged-out and navigated
      } else {
        setLoggedOut()
      }
    }
  }

  async function login(email: string, password: string) {
    const user = await apiFetch<User>('/api/auth/login', {
      method: 'POST',
      body: { email, password }
    })
    setLoggedIn(user)
  }

  async function register(email: string, password: string) {
    const user = await apiFetch<User>('/api/auth/register', {
      method: 'POST',
      body: { email, password }
    })
    setLoggedIn(user)
  }

  async function logout() {
    try {
      await apiFetch('/api/auth/logout', { method: 'POST' })
    } catch {
      // ignore; force local sign-out regardless
    }
    await hub.disconnect().catch(() => {})
    setLoggedOut()
  }

  initialize()

  return {
    subscribe,
    login,
    register,
    logout
  }
}

export const auth = createAuth()
