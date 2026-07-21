import { Boxes, History, LayoutDashboard, LogOut, Package, Sparkles, Users } from 'lucide-react'
import { NavLink, Navigate, Route, Routes } from 'react-router-dom'
import type { User } from '../types'
import { ActivityPage } from './pages/ActivityPage'
import { DashboardPage } from './pages/DashboardPage'
import { InsightsPage } from './pages/InsightsPage'
import { InventoryPage } from './pages/InventoryPage'
import { ItemDetailPage } from './pages/ItemDetailPage'
import { TeamPanel } from './TeamPanel'

const navigation = [
  { to: '/dashboard', label: 'Dashboard', icon: LayoutDashboard },
  { to: '/inventory', label: 'Inventory', icon: Package },
  { to: '/activity', label: 'Activity', icon: History },
  { to: '/insights', label: 'Intelligence', icon: Sparkles },
]

export function Dashboard({ user, onLogout }: { user: User; onLogout: () => void }) {
  return (
    <div className="app-shell">
      <aside className="sidebar">
        <NavLink className="brand brand-light" to="/dashboard">
          <span className="brand-mark">S</span>
          <span>StockPilot</span>
        </NavLink>
        <nav aria-label="Primary navigation">
          {navigation.map(({ to, label, icon: Icon }) => (
            <NavLink key={to} className={({ isActive }) => (isActive ? 'active' : '')} to={to}>
              <Icon aria-hidden="true" size={18} /> {label}
            </NavLink>
          ))}
          {user.role === 'Admin' && (
            <NavLink className={({ isActive }) => (isActive ? 'active' : '')} to="/team">
              <Users aria-hidden="true" size={18} /> Team
            </NavLink>
          )}
        </nav>
        <div className="sidebar-foot">
          <div className="user-chip">
            <span className="avatar">{initials(user.name)}</span>
            <div>
              <strong>{user.name}</strong>
              <span>{user.role}</span>
            </div>
          </div>
          <button className="logout" onClick={onLogout}>
            <LogOut aria-hidden="true" size={14} /> Sign out
          </button>
        </div>
      </aside>

      <main className="workspace">
        <div className="mobile-shell-header">
          <NavLink className="brand" to="/dashboard">
            <span className="brand-mark">S</span>
            <span>StockPilot</span>
          </NavLink>
          <button className="mobile-logout" onClick={onLogout} aria-label="Sign out">
            <LogOut aria-hidden="true" size={15} /> Sign out
          </button>
        </div>
        <Routes>
          <Route path="dashboard" element={<DashboardPage user={user} />} />
          <Route path="inventory" element={<InventoryPage user={user} />} />
          <Route path="inventory/:id" element={<ItemDetailPage user={user} />} />
          <Route path="activity" element={<ActivityPage />} />
          <Route path="insights" element={<InsightsPage />} />
          <Route
            path="team"
            element={
              user.role === 'Admin' ? (
                <TeamRoute user={user} />
              ) : (
                <Navigate replace to="/dashboard" />
              )
            }
          />
          <Route path="*" element={<Navigate replace to="/dashboard" />} />
        </Routes>
      </main>
    </div>
  )
}

function initials(name: string) {
  return name
    .split(' ')
    .map((part) => part[0])
    .slice(0, 2)
    .join('')
}

function TeamRoute({ user }: { user: User }) {
  return (
    <>
      <PageHeader eyebrow="Administration" title="Your team" />
      <TeamPanel currentUser={user} />
    </>
  )
}

export function PageHeader({
  eyebrow,
  title,
  description,
  actions,
}: {
  eyebrow: string
  title: string
  description?: string
  actions?: React.ReactNode
}) {
  return (
    <header className="topbar">
      <div>
        <span className="eyebrow">{eyebrow}</span>
        <h1>{title}</h1>
        {description && <p className="page-description">{description}</p>}
      </div>
      <div className="topbar-actions">
        <span className="today">
          {new Intl.DateTimeFormat('en-US', {
            month: 'short',
            day: 'numeric',
            year: 'numeric',
          }).format(new Date())}
        </span>
        {actions}
      </div>
    </header>
  )
}

export function ErrorState({ error, onRetry }: { error: unknown; onRetry?: () => void }) {
  return (
    <div className="panel empty-state error-state" role="alert">
      <Boxes aria-hidden="true" size={28} />
      <strong>We couldn’t load this information</strong>
      <span>{error instanceof Error ? error.message : 'Please try again.'}</span>
      {onRetry && (
        <button className="button secondary" onClick={onRetry}>
          Try again
        </button>
      )}
    </div>
  )
}
