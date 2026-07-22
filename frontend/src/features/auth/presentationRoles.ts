import type { UserRole } from '../../types'

export interface PresentationRole {
  role: UserRole
  summary: string
}

export const presentationRoles: readonly PresentationRole[] = [
  {
    role: 'Admin',
    summary: 'Full access and team management',
  },
  {
    role: 'Manager',
    summary: 'Create, edit, and update stock',
  },
  {
    role: 'Viewer',
    summary: 'Read-only inventory and insights',
  },
]
