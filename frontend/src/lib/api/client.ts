import { setLoggedOut } from '../stores/auth'
import { navigate } from '../router'

export class ApiError extends Error {
  status: number
  errors: string[]

  constructor(status: number, message: string, errors: string[] = []) {
    super(message)
    this.status = status
    this.errors = errors
  }
}

export async function apiFetch<T>(
  path: string,
  options?: {
    method?: string
    body?: unknown
    headers?: Record<string, string>
  }
): Promise<T> {
  const init: RequestInit = {
    method: options?.method ?? 'GET',
    headers: {
      Accept: 'application/json',
      'Content-Type': 'application/json',
      ...(options?.headers ?? {})
    }
  }

  if (options?.body !== undefined) {
    init.body = JSON.stringify(options.body)
  }

  const response = await fetch(path, init)

  if (!response.ok) {
    const bodyText = await response.text()
    let message = bodyText
    let errors: string[] = []

    try {
      const parsed = JSON.parse(bodyText)
      if (Array.isArray(parsed)) {
        errors = parsed
        message = errors.join(' ')
      } else if (typeof parsed === 'string') {
        message = parsed
      } else if (parsed && typeof parsed === 'object') {
        // ASP.NET ProblemDetails: { title, detail, status, ... }
        if (parsed.detail) message = parsed.detail
        else if (parsed.title) message = parsed.title
        else if (parsed.message) message = parsed.message
        if (parsed.errors) {
          if (Array.isArray(parsed.errors)) {
            errors = parsed.errors
          } else if (typeof parsed.errors === 'object') {
            // ModelState dictionary: { "field": ["err1", "err2"] }
            errors = Object.values(parsed.errors).flat().map(String)
          } else {
            errors = [String(parsed.errors)]
          }
        }
      }
    } catch {
      // leave message as raw response text
    }

    // Auth endpoints handle their own 401s (bad credentials); don't
    // auto-logout for login/register, that would wipe the form state.
    const isAuthEndpoint =
      path === '/api/auth/login' || path === '/api/auth/register'
    if (response.status === 401 && !isAuthEndpoint) {
      setLoggedOut()
      navigate('/login')
    }

    throw new ApiError(response.status, message, errors)
  }

  if (response.status === 204) {
    return undefined as T
  }

  return response.json() as Promise<T>
}
