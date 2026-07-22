import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { api } from '../api'
import { Login } from './Login'

describe('Login', () => {
  beforeEach(() => {
    vi.spyOn(api, 'prepareSession').mockResolvedValue()
  })

  afterEach(() => {
    vi.restoreAllMocks()
  })

  it('keeps credentials empty while letting users review role permissions', async () => {
    const user = userEvent.setup()

    render(
      <MemoryRouter future={{ v7_relativeSplatPath: true, v7_startTransition: true }}>
        <Login onAuthenticated={vi.fn()} />
      </MemoryRouter>,
    )

    expect(screen.getByLabelText('Email address')).toHaveValue('')
    expect(screen.getByLabelText('Email address')).toHaveAttribute(
      'placeholder',
      'name@company.com',
    )
    expect(screen.getByLabelText('Password')).toHaveValue('')
    expect(screen.getByLabelText('Password')).toHaveAttribute('placeholder', 'Enter your password')
    expect(screen.getByText('Access levels')).toBeVisible()
    expect(screen.getByText(/type your own credentials/i)).toBeVisible()

    await user.click(screen.getByRole('button', { name: 'Manager' }))

    expect(screen.getByLabelText('Email address')).toHaveValue('')
    expect(screen.getByLabelText('Email address')).toHaveFocus()
    expect(screen.getByLabelText('Password')).toHaveValue('')
    expect(screen.getByText('Create, edit, and update stock')).toBeVisible()
    expect(screen.getByText(/credentials are never filled/i)).toBeVisible()

    await user.type(screen.getByLabelText('Password'), 'Private123!')
    await user.click(screen.getByRole('button', { name: 'Show password' }))

    expect(screen.getByLabelText('Password')).toHaveAttribute('type', 'text')
    expect(screen.getByRole('button', { name: 'Hide password' })).toHaveAttribute(
      'aria-pressed',
      'true',
    )
  })

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
