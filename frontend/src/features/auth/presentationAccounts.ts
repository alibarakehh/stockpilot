import type { UserRole } from '../../types'

export interface PresentationAccount {
  role: UserRole
  email: string
  summary: string
}

export const presentationAccounts: readonly PresentationAccount[] = [
  {
    role: 'Admin',
    email: 'admin.demo@stockpilot.app',
    summary: 'Full access and team management',
  },
  {
    role: 'Manager',
    email: 'manager.demo@stockpilot.app',
    summary: 'Create, edit, and update stock',
  },
  {
    role: 'Viewer',
    email: 'viewer.demo@stockpilot.app',
    summary: 'Read-only inventory and insights',
  },
]
