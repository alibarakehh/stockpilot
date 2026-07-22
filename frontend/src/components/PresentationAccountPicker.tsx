import { LockKeyhole } from 'lucide-react'
import type { UserRole } from '../types'
import {
  presentationAccounts,
  type PresentationAccount,
} from '../features/auth/presentationAccounts'

interface PresentationAccountPickerProps {
  selectedRole?: UserRole
  onSelect: (account: PresentationAccount) => void
}

export function PresentationAccountPicker({
  selectedRole,
  onSelect,
}: PresentationAccountPickerProps) {
  return (
    <section className="presentation-access" aria-labelledby="presentation-access-title">
      <div className="presentation-access-heading">
        <strong id="presentation-access-title">Presentation accounts</strong>
        <span>Select a role</span>
      </div>
      <div
        className="presentation-role-options"
        role="group"
        aria-labelledby="presentation-access-title"
      >
        {presentationAccounts.map((account) => {
          const selected = selectedRole === account.role
          return (
            <button
              key={account.role}
              className={selected ? 'presentation-role selected' : 'presentation-role'}
              type="button"
              aria-pressed={selected}
              onClick={() => onSelect(account)}
            >
              {account.role}
            </button>
          )
        })}
      </div>
      <p className="presentation-role-summary" aria-live="polite">
        {selectedRole
          ? presentationAccounts.find((account) => account.role === selectedRole)?.summary
          : 'Choose a role to fill its email, or enter your own team email below.'}
      </p>
      <p className="presentation-password-note">
        <LockKeyhole size={14} aria-hidden="true" />
        Enter the private password shared by the administrator.
      </p>
    </section>
  )
}
