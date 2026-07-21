import { useEffect, useState } from 'react'
import { Navigate, Route, Routes } from 'react-router-dom'
import { api } from './api'
import { Dashboard } from './components/Dashboard'
import { Login } from './components/Login'
import { clearAuthentication, UNAUTHORIZED_EVENT } from './features/auth/session'
import type { AuthResponse, User } from './types'

export default function App() {
  const [user, setUser] = useState<User | null | undefined>(undefined)

  useEffect(() => {
    clearAuthentication()
    void api
      .currentUser()
      .then(setUser)
      .catch(() => setUser(null))

    const handleExpiredSession = () => setUser(null)
    window.addEventListener(UNAUTHORIZED_EVENT, handleExpiredSession)
    return () => window.removeEventListener(UNAUTHORIZED_EVENT, handleExpiredSession)
  }, [])

  function authenticate(auth: AuthResponse) {
    setUser(auth.user)
  }

  async function logout() {
    try {
      await api.logout()
    } finally {
      clearAuthentication()
      setUser(null)
    }
  }

  if (user === undefined) {
    return (
      <main className="session-loading" aria-live="polite">
        Checking your session…
      </main>
    )
  }

  return (
    <Routes>
      <Route
        path="/login"
        element={
          user ? <Navigate replace to="/dashboard" /> : <Login onAuthenticated={authenticate} />
        }
      />
      <Route
        path="/*"
        element={
          user ? <Dashboard user={user} onLogout={logout} /> : <Navigate replace to="/login" />
        }
      />
    </Routes>
  )
}
