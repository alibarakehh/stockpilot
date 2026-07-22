import { ClipboardList, Eye, LockKeyhole, ShieldCheck } from 'lucide-react'
import type { UserRole } from '../types'
import {
  presentationAccounts,
  type PresentationAccount,
} from '../features/auth/presentationAccounts'

interface PresentationAccountPickerProps {
  selectedRole?: UserRole
  onSelect: (account: PresentationAccount) => void
}

const roleIcons = {
  Admin: ShieldCheck,
  Manager: ClipboardList,
  Viewer: Eye,
} as const

export function PresentationAccountPicker({
  selectedRole,
  onSelect,
}: PresentationAccountPickerProps) {
  return (
    <section className="presentation-access" aria-labelledby="presentation-access-title">
      <div className="presentation-access-heading">
        <strong id="presentation-access-title">Presentation accounts</strong>
        <span>Choose how you want to explore StockPilot.</span>
      </div>
      <div className="presentation-role-options">
        {presentationAccounts.map((account) => {
          const Icon = roleIcons[account.role]
          const selected = selectedRole === account.role
          return (
            <button
              key={account.role}
              className={selected ? 'presentation-role selected' : 'presentation-role'}
              type="button"
              aria-pressed={selected}
              onClick={() => onSelect(account)}
            >
              <Icon size={17} aria-hidden="true" />
              <span>
                <strong>{account.role}</strong>
                <small>{account.summary}</small>
              </span>
            </button>
          )
        })}
      </div>
      <p className="presentation-password-note">
        <LockKeyhole size={14} aria-hidden="true" />
        The role email is filled automatically. Enter the private password shared by the
        administrator.
      </p>
    </section>
  )
}
