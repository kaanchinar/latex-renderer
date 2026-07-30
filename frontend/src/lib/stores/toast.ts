import { writable } from 'svelte/store'

export type Toast = {
  id: number
  message: string
}

const { subscribe, update } = writable<Toast[]>([])

let nextId = 1

export function push(message: string) {
  const id = nextId++
  update((toasts) => [...toasts, { id, message }])
  setTimeout(() => {
    update((toasts) => toasts.filter((t) => t.id !== id))
  }, 4000)
}

export const toasts = { subscribe }
