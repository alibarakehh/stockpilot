import { zodResolver } from '@hookform/resolvers/zod'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Trash2, UserPlus } from 'lucide-react'
import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { z } from 'zod'
import { api } from '../api'
import type { User, UserRole } from '../types'
import { ConfirmDialog } from './ConfirmDialog'

const teamKey = ['team'] as const
const memberSchema = z.object({
  name: z.string().trim().min(2, 'Enter at least 2 characters.').max(120),
  email: z.string().trim().email('Enter a valid email address.'),
  password: z
    .string()
    .min(10, 'Use at least 10 characters.')
    .regex(/[a-z]/, 'Add a lowercase letter.')
    .regex(/[A-Z]/, 'Add an uppercase letter.')
    .regex(/\d/, 'Add a number.')
    .regex(/[^A-Za-z0-9]/, 'Add a symbol.'),
  role: z.enum(['Admin', 'Manager', 'Viewer']),
})
type MemberForm = z.infer<typeof memberSchema>

export function TeamPanel({ currentUser }: { currentUser: User }) {
  const queryClient = useQueryClient()
  const users = useQuery({ queryKey: teamKey, queryFn: api.users })
  const [showForm, setShowForm] = useState(false)
  const [message, setMessage] = useState('')
  const [memberToDelete, setMemberToDelete] = useState<User | null>(null)
  const form = useForm<MemberForm>({
    resolver: zodResolver(memberSchema),
    defaultValues: { name: '', email: '', password: '', role: 'Viewer' },
  })
  const createUser = useMutation({
    mutationFn: api.createUser,
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: teamKey })
    },
  })
  const changeRole = useMutation({
    mutationFn: ({ id, role }: { id: string; role: UserRole }) => api.changeRole(id, role),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: teamKey })
    },
  })
  const deleteUser = useMutation({
    mutationFn: api.deleteUser,
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: teamKey })
    },
  })

  async function addMember(input: MemberForm) {
    try {
      await createUser.mutateAsync(input)
      form.reset()
      setShowForm(false)
      flash('Team member added.')
    } catch (error) {
      form.setError('root.server', {
        message: error instanceof Error ? error.message : 'Could not add user.',
      })
    }
  }
  function flash(text: string) {
    setMessage(text)
    window.setTimeout(() => setMessage(''), 3500)
  }

  return (
    <section className="panel team-panel">
      <header className="panel-header">
        <div>
          <span className="eyebrow">Access control</span>
          <h2>Team members</h2>
          <p className="muted">Control who can view, manage, and archive inventory.</p>
        </div>
        <button className="button primary" onClick={() => setShowForm((value) => !value)}>
          <UserPlus size={15} /> {showForm ? 'Cancel' : 'Add member'}
        </button>
      </header>
      {message && (
        <div className="inline-success" role="status">
          {message}
        </div>
      )}
      {showForm && (
        <form className="inline-form" noValidate onSubmit={form.handleSubmit(addMember)}>
          <label>
            Name
            <input {...form.register('name')} aria-invalid={Boolean(form.formState.errors.name)} />
            <FieldError text={form.formState.errors.name?.message} />
          </label>
          <label>
            Email
            <input
              type="email"
              {...form.register('email')}
              aria-invalid={Boolean(form.formState.errors.email)}
            />
            <FieldError text={form.formState.errors.email?.message} />
          </label>
          <label>
            Temporary password
            <input
              type="password"
              {...form.register('password')}
              aria-invalid={Boolean(form.formState.errors.password)}
            />
            <FieldError text={form.formState.errors.password?.message} />
          </label>
          <label>
            Role
            <select {...form.register('role')}>
              <option>Viewer</option>
              <option>Manager</option>
              <option>Admin</option>
            </select>
          </label>
          <button className="button primary" disabled={form.formState.isSubmitting} type="submit">
            {form.formState.isSubmitting ? 'Adding…' : 'Add member'}
          </button>
          {form.formState.errors.root?.server?.message && (
            <div className="form-error span-all" role="alert">
              {form.formState.errors.root.server.message}
            </div>
          )}
        </form>
      )}
      {(users.error || changeRole.error || deleteUser.error) && (
        <div className="form-error" role="alert">
          {(users.error ?? changeRole.error ?? deleteUser.error) instanceof Error
            ? (users.error ?? changeRole.error ?? deleteUser.error)?.message
            : 'Could not update the team.'}
        </div>
      )}
      {users.isLoading ? (
        <div className="empty-state">
          <span className="spinner" /> Loading team…
        </div>
      ) : (
        <div className="member-list">
          {(users.data ?? []).map((user) => (
            <div className="member" key={user.id}>
              <span className="avatar">
                {user.name
                  .split(' ')
                  .map((part) => part[0])
                  .slice(0, 2)
                  .join('')}
              </span>
              <div>
                <strong>
                  {user.name}
                  {user.id === currentUser.id && <small> You</small>}
                </strong>
                <span>{user.email}</span>
              </div>
              <select
                aria-label={`Role for ${user.name}`}
                value={user.role}
                disabled={
                  user.id === currentUser.id || changeRole.isPending || deleteUser.isPending
                }
                onChange={(event) =>
                  changeRole.mutate(
                    { id: user.id, role: event.target.value as UserRole },
                    { onSuccess: () => flash(`${user.name}'s role was updated.`) },
                  )
                }
              >
                <option>Viewer</option>
                <option>Manager</option>
                <option>Admin</option>
              </select>
              <p>{roleDescription(user.role)}</p>
              {user.id === currentUser.id ? (
                <span aria-hidden="true" />
              ) : (
                <button
                  className="icon-button danger member-delete"
                  type="button"
                  aria-label={`Remove ${user.name}`}
                  title={`Remove ${user.name}`}
                  disabled={changeRole.isPending || deleteUser.isPending}
                  onClick={() => setMemberToDelete(user)}
                >
                  <Trash2 size={15} />
                </button>
              )}
            </div>
          ))}
        </div>
      )}
      {memberToDelete && (
        <ConfirmDialog
          title={`Remove ${memberToDelete.name}?`}
          message={`${memberToDelete.email} will immediately lose access to this workspace. This action cannot be undone.`}
          confirmLabel="Remove member"
          onCancel={() => setMemberToDelete(null)}
          onConfirm={async () => {
            await deleteUser.mutateAsync(memberToDelete.id)
            setMemberToDelete(null)
            flash(`${memberToDelete.name} was removed from the team.`)
          }}
        />
      )}
    </section>
  )
}

function FieldError({ text }: { text?: string }) {
  return text ? <small className="field-error">{text}</small> : null
}
function roleDescription(role: UserRole) {
  return role === 'Admin'
    ? 'Full access, team management, and archival'
    : role === 'Manager'
      ? 'Create and update inventory'
      : 'Read-only inventory and insights'
}
