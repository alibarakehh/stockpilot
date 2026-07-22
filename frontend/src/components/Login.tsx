import { zodResolver } from '@hookform/resolvers/zod'
import { Eye, EyeOff, LockKeyhole } from 'lucide-react'
import { useEffect, useState } from 'react'
import { useForm } from 'react-hook-form'
import { Link } from 'react-router-dom'
import { api } from '../api'
import { loginSchema, type LoginFormValues } from '../features/auth/validation'
import type { AuthResponse } from '../types'

interface LoginProps {
  onAuthenticated: (auth: AuthResponse) => void
}

export function Login({ onAuthenticated }: LoginProps) {
  const [showPassword, setShowPassword] = useState(false)
  const {
    formState: { errors, isSubmitting },
    handleSubmit,
    register,
    setError,
  } = useForm<LoginFormValues>({
    resolver: zodResolver(loginSchema),
    defaultValues: { email: '', password: '' },
  })

  useEffect(() => {
    void api.prepareSession().catch(() => undefined)
  }, [])

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
        <form autoComplete="off" className="login-card" noValidate onSubmit={submit}>
          <div className="mobile-brand">
            <span className="brand-mark">S</span>
            <span>StockPilot</span>
          </div>
          <span className="eyebrow">WELCOME BACK</span>
          <h2>Sign in to your workspace</h2>
          <p className="muted">Enter your team credentials to continue.</p>

          <div className="login-field">
            <label htmlFor="login-email">Email address</label>
            <input
              {...register('email')}
              id="login-email"
              aria-invalid={Boolean(errors.email)}
              autoCapitalize="none"
              autoComplete="off"
              inputMode="email"
              placeholder="name@company.com"
              spellCheck={false}
              type="email"
            />
            {errors.email?.message && <small className="field-error">{errors.email.message}</small>}
          </div>
          <div className="login-field">
            <label htmlFor="login-password">Password</label>
            <span className="password-field">
              <input
                {...register('password')}
                id="login-password"
                aria-invalid={Boolean(errors.password)}
                autoComplete="off"
                placeholder="Enter your password"
                type={showPassword ? 'text' : 'password'}
              />
              <button
                aria-label={showPassword ? 'Hide password' : 'Show password'}
                aria-pressed={showPassword}
                className="password-toggle"
                onClick={() => setShowPassword((visible) => !visible)}
                type="button"
              >
                {showPassword ? (
                  <EyeOff aria-hidden="true" size={17} />
                ) : (
                  <Eye aria-hidden="true" size={17} />
                )}
              </button>
            </span>
            {errors.password?.message && (
              <small className="field-error">{errors.password.message}</small>
            )}
          </div>
          <p className="login-security-note">
            <LockKeyhole aria-hidden="true" size={14} />
            Fields always start empty. Use credentials provided by your workspace administrator.
          </p>
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
