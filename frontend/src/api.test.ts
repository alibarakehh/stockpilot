import { afterEach, describe, expect, it, vi } from 'vitest'
import { api } from './api'

describe('api requests', () => {
  afterEach(() => {
    vi.restoreAllMocks()
  })

  it('does not attach a JSON content type to bodyless requests', async () => {
    const fetchMock = vi.spyOn(globalThis, 'fetch').mockResolvedValue(
      new Response(JSON.stringify([]), {
        status: 200,
        headers: { 'Content-Type': 'application/json' },
      }),
    )

    await api.categories()

    const headers = new Headers(fetchMock.mock.calls[0]?.[1]?.headers)
    expect(headers.has('Content-Type')).toBe(false)
  })

  it('attaches a JSON content type to requests with a body', async () => {
    const fetchMock = vi
      .spyOn(globalThis, 'fetch')
      .mockResolvedValueOnce(
        new Response(JSON.stringify({ requestToken: 'test-token' }), {
          status: 200,
          headers: { 'Content-Type': 'application/json' },
        }),
      )
      .mockResolvedValueOnce(
        new Response(null, {
          status: 204,
        }),
      )

    await api.createUser({
      name: 'Test User',
      email: 'test@stockpilot.local',
      password: 'Integration123!',
      role: 'Viewer',
    })

    const headers = new Headers(fetchMock.mock.calls[1]?.[1]?.headers)
    expect(headers.get('Content-Type')).toBe('application/json')
  })
})
