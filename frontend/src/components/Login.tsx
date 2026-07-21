import { zodResolver } from '@hookform/resolvers/zod'
import { useForm } from 'react-hook-form'
import { Link } from 'react-router-dom'
import { api } from '../api'
import { loginSchema, type LoginFormValues } from '../features/auth/validation'
import type { AuthResponse } from '../types'

interface LoginProps {
  onAuthenticated: (auth: AuthResponse) => void
}

export function Login({ onAuthenticated }: LoginProps) {
  const {
    formState: { errors, isSubmitting },
    handleSubmit,
    register,
    setError,
  } = useForm<LoginFormValues>({
    resolver: zodResolver(loginSchema),
    defaultValues: { email: '', password: '' },
  })

  const submit = handleSubmit(async ({ email: submittedEmail, password }) => {
    try {
      onAuthenticated(await api.login(submittedEmail, password))
    } catch (reason) {
      setError('root.server', {
        message: reason instanceof Error ? reason.message : 'Unable to sign in.',
        type: 'server',
      })
    }
  })

  return (
    <main className="login-page">
      <section className="login-story">
        <Link className="brand brand-light" to="/login" aria-label="StockPilot home">
          <span className="brand-mark">S</span>
          <span>StockPilot</span>
        </Link>
        <div className="story-content">
          <span className="eyebrow light">INVENTORY INTELLIGENCE</span>
          <h1>
            Know what you have.
            <br />
            Predict what you need.
          </h1>
          <p>
            One clear workspace for stock, purchasing signals, and the people responsible for
            keeping operations moving.
          </p>
          <div className="story-metrics">
            <div>
              <strong>Real-time</strong>
              <span>stock visibility</span>
            </div>
            <div>
              <strong>Explainable</strong>
              <span>AI recommendations</span>
            </div>
            <div>
              <strong>Role-based</strong>
              <span>team access</span>
            </div>
          </div>
        </div>
        <p className="story-foot">Built for focused operations teams.</p>
      </section>

      <section className="login-panel">
        <form className="login-card" noValidate onSubmit={submit}>
          <div className="mobile-brand">
            <span className="brand-mark">S</span>
            <span>StockPilot</span>
          </div>
          <span className="eyebrow">WELCOME BACK</span>
          <h2>Sign in to your workspace</h2>
          <p className="muted">Enter your team credentials to continue.</p>

          <label>
            Email address
            <input
              {...register('email')}
              aria-invalid={Boolean(errors.email)}
              autoComplete="email"
              type="email"
            />
            {errors.email?.message && <small className="field-error">{errors.email.message}</small>}
          </label>
          <label>
            Password
            <input
              {...register('password')}
              aria-invalid={Boolean(errors.password)}
              autoComplete="current-password"
              type="password"
            />
            {errors.password?.message && (
              <small className="field-error">{errors.password.message}</small>
            )}
          </label>
          {errors.root?.server?.message && (
            <div className="form-error" role="alert">
              {errors.root.server.message}
            </div>
          )}
          <button className="button primary full" disabled={isSubmitting} type="submit">
            {isSubmitting ? 'Signing in…' : 'Sign in'}
          </button>
        </form>
      </section>
    </main>
  )
}
