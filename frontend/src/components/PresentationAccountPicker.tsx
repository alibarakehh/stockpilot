import { LockKeyhole } from 'lucide-react'
import type { UserRole } from '../types'
import { presentationRoles, type PresentationRole } from '../features/auth/presentationRoles'

interface PresentationAccountPickerProps {
  selectedRole?: UserRole
  onSelect: (role: PresentationRole) => void
}

export function PresentationAccountPicker({
  selectedRole,
  onSelect,
}: PresentationAccountPickerProps) {
  return (
    <section className="presentation-access" aria-labelledby="presentation-access-title">
      <div className="presentation-access-heading">
        <strong id="presentation-access-title">Access levels</strong>
        <span>Review a role</span>
      </div>
      <div
        className="presentation-role-options"
        role="group"
        aria-labelledby="presentation-access-title"
      >
        {presentationRoles.map((role) => {
          const selected = selectedRole === role.role
          return (
            <button
              key={role.role}
              className={selected ? 'presentation-role selected' : 'presentation-role'}
              type="button"
              aria-pressed={selected}
              onClick={() => onSelect(role)}
            >
              {role.role}
            </button>
          )
        })}
      </div>
      <p className="presentation-role-summary" aria-live="polite">
        {selectedRole
          ? presentationRoles.find((role) => role.role === selectedRole)?.summary
          : 'Choose a role to review its permissions, then type your own credentials.'}
      </p>
      <p className="presentation-password-note">
        <LockKeyhole size={14} aria-hidden="true" />
        Your account determines actual permissions. Credentials are never filled by StockPilot.
      </p>
    </section>
  )
}
