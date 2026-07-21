import { render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import App from './App'
import { api } from './api'
import { clearAuthentication } from './features/auth/session'

describe('App routing', () => {
  beforeEach(() => {
    clearAuthentication()
    vi.spyOn(api, 'currentUser').mockRejectedValue(new Error('Unauthenticated'))
  })

  it('redirects an unauthenticated protected route to login', async () => {
    render(
      <MemoryRouter
        future={{ v7_relativeSplatPath: true, v7_startTransition: true }}
        initialEntries={['/inventory']}
      >
        <App />
      </MemoryRouter>,
    )

    expect(await screen.findByRole('heading', { name: 'Sign in to your workspace' })).toBeVisible()
  })
})
