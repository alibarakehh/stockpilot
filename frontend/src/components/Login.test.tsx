import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { describe, expect, it, vi } from 'vitest'
import { api } from '../api'
import { Login } from './Login'

describe('Login', () => {
  it('shows client-side validation before sending invalid credentials', async () => {
    const user = userEvent.setup()
    const login = vi.spyOn(api, 'login')

    render(
      <MemoryRouter future={{ v7_relativeSplatPath: true, v7_startTransition: true }}>
        <Login onAuthenticated={vi.fn()} />
      </MemoryRouter>,
    )

    const email = screen.getByLabelText('Email address')
    const password = screen.getByLabelText('Password')
    await user.clear(email)
    await user.type(email, 'not-an-email')
    await user.clear(password)
    await user.click(screen.getByRole('button', { name: 'Sign in' }))

    expect(await screen.findByText('Enter a valid email address.')).toBeInTheDocument()
    expect(screen.getByText('Password must contain at least 8 characters.')).toBeInTheDocument()
    expect(login).not.toHaveBeenCalled()
  })
})
