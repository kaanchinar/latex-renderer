import { describe, it, expect, beforeEach, vi } from 'vitest'
import { apiFetch, ApiError } from './client'
import { setLoggedOut } from '../stores/auth'
import { navigate } from '../router'

vi.mock('../stores/auth', () => ({
  setLoggedOut: vi.fn()
}))

vi.mock('../router', () => ({
  navigate: vi.fn()
}))

describe('apiFetch', () => {
  beforeEach(() => {
    vi.restoreAllMocks()
  })

  it('returns parsed JSON for 200 responses', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn().mockResolvedValue({
        ok: true,
        status: 200,
        json: vi.fn().mockResolvedValue({ id: '1', email: 'a@b.com' })
      })
    )

    const result = await apiFetch('/api/projects')
    expect(result).toEqual({ id: '1', email: 'a@b.com' })
  })

  it('returns undefined for 204 responses', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn().mockResolvedValue({
        ok: true,
        status: 204
      })
    )

    const result = await apiFetch('/api/projects/1')
    expect(result).toBeUndefined()
  })

  it('throws ApiError for Identity-style array body on 400', async () => {
    const body = JSON.stringify(['Passwords must be at least 6 characters.'])
    vi.stubGlobal(
      'fetch',
      vi.fn().mockResolvedValue({
        ok: false,
        status: 400,
        text: vi.fn().mockResolvedValue(body)
      })
    )

    await expect(apiFetch('/api/auth/register')).rejects.toBeInstanceOf(ApiError)
    try {
      await apiFetch('/api/auth/register')
    } catch (error) {
      expect(error).toBeInstanceOf(ApiError)
      expect((error as ApiError).status).toBe(400)
      expect((error as ApiError).errors).toEqual([
        'Passwords must be at least 6 characters.'
      ])
      expect((error as ApiError).message).toBe(
        'Passwords must be at least 6 characters.'
      )
    }
  })

  it('logs out and redirects to login on 401', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn().mockResolvedValue({
        ok: false,
        status: 401,
        text: vi.fn().mockResolvedValue('Unauthorized')
      })
    )

    await expect(apiFetch('/api/projects')).rejects.toBeInstanceOf(ApiError)
    expect(setLoggedOut).toHaveBeenCalled()
    expect(navigate).toHaveBeenCalledWith('/login')
  })
})
