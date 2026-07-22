import type { UserRole } from '../../types'

export interface PresentationAccount {
  role: UserRole
  email: string
  summary: string
}

const demoAccountEmails = import.meta.env.PROD
  ? {
      Admin: 'admin.demo@stockpilot.app',
      Manager: 'manager.demo@stockpilot.app',
      Viewer: 'viewer.demo@stockpilot.app',
    }
  : {
      Admin: 'admin@stockpilot.local',
      Manager: 'manager@stockpilot.local',
      Viewer: 'viewer@stockpilot.local',
    }

export const presentationAccounts: readonly PresentationAccount[] = [
  {
    role: 'Admin',
    email: demoAccountEmails.Admin,
    summary: 'Full access and team management',
  },
  {
    role: 'Manager',
    email: demoAccountEmails.Manager,
    summary: 'Create, edit, and update stock',
  },
  {
    role: 'Viewer',
    email: demoAccountEmails.Viewer,
    summary: 'Read-only inventory and insights',
  },
]
