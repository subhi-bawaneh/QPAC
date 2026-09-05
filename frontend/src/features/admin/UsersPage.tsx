import { useState } from 'react'
import { UserPlus } from 'lucide-react'
import { Button } from '@/shared/ui/button'
import { Card, CardBody } from '@/shared/ui/card'
import { Input } from '@/shared/ui/input'
import { Badge } from '@/shared/ui/badge'
import { Spinner } from '@/shared/ui/spinner'
import { Dialog } from '@/shared/ui/dialog'
import { apiErrorMessage } from '@/shared/api/client'
import { useAssignRoles, useCreateUser, useRoles, useSetDisciplines, useUsers, type UserListItem } from './api'

export function UsersPage() {
  const [search, setSearch] = useState('')
  const [createOpen, setCreateOpen] = useState(false)
  const [editing, setEditing] = useState<UserListItem | null>(null)

  const users = useUsers(search)
  const roles = useRoles()

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h1 className="text-xl font-semibold">Users</h1>
          <p className="mt-1 text-sm text-muted-foreground">
            Accounts, their roles, and the disciplines an editor is scoped to.
          </p>
        </div>

        <Button size="sm" onClick={() => setCreateOpen(true)}>
          <UserPlus className="h-4 w-4" aria-hidden />
          New user
        </Button>
      </div>

      <Card>
        <div className="border-b border-border px-5 py-3">
          <Input
            className="h-8 w-64"
            placeholder="Search name or email"
            aria-label="Search users"
            value={search}
            onChange={(event) => setSearch(event.target.value)}
          />
        </div>

        <CardBody className="p-0">
          {users.isPending ? <Spinner /> : null}
          {users.isError ? (
            <p className="px-5 py-4 text-sm text-destructive">{apiErrorMessage(users.error)}</p>
          ) : null}

          {users.data?.length ? (
            <table className="w-full text-sm">
              <thead className="border-b border-border text-left text-xs uppercase text-muted-foreground">
                <tr>
                  <th className="px-5 py-2 font-medium">Name</th>
                  <th className="px-5 py-2 font-medium">Email</th>
                  <th className="px-5 py-2 font-medium">Roles</th>
                  <th className="px-5 py-2 font-medium">Disciplines</th>
                  <th className="px-5 py-2 font-medium">State</th>
                  <th className="px-5 py-2 text-right font-medium">Actions</th>
                </tr>
              </thead>
              <tbody>
                {users.data.map((user) => (
                  <tr key={user.id} className="border-b border-border last:border-0">
                    <td className="px-5 py-2">{user.fullName}</td>
                    <td className="px-5 py-2 text-muted-foreground">{user.email}</td>
                    <td className="px-5 py-2">
                      <span className="flex flex-wrap gap-1">
                        {user.roles.map((role) => <Badge key={role} tone="info">{role}</Badge>)}
                      </span>
                    </td>
                    <td className="px-5 py-2">
                      {user.disciplineCodes.length === 0 ? (
                        <span className="text-xs text-muted-foreground">All disciplines</span>
                      ) : (
                        <span className="flex flex-wrap gap-1">
                          {user.disciplineCodes.map((code) => <Badge key={code}>{code}</Badge>)}
                        </span>
                      )}
                    </td>
                    <td className="px-5 py-2">
                      <Badge tone={user.isActive ? 'success' : 'neutral'}>
                        {user.isActive ? 'Active' : 'Disabled'}
                      </Badge>
                    </td>
                    <td className="px-5 py-2 text-right">
                      <Button size="sm" variant="ghost" onClick={() => setEditing(user)}>Edit access</Button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          ) : null}

          {users.data?.length === 0 ? (
            <p className="px-5 py-6 text-sm text-muted-foreground">No users match that search.</p>
          ) : null}
        </CardBody>
      </Card>

      <CreateUserDialog
        open={createOpen}
        onClose={() => setCreateOpen(false)}
        roleNames={roles.data?.map((role) => role.name) ?? []}
      />

      <EditAccessDialog
        user={editing}
        onClose={() => setEditing(null)}
        roleNames={roles.data?.map((role) => role.name) ?? []}
      />
    </div>
  )
}

function CreateUserDialog({ open, onClose, roleNames }: {
  open: boolean
  onClose: () => void
  roleNames: string[]
}) {
  const [email, setEmail] = useState('')
  const [fullName, setFullName] = useState('')
  const [password, setPassword] = useState('')
  const [selected, setSelected] = useState<string[]>([])
  const [error, setError] = useState<string | null>(null)
  const create = useCreateUser()

  const onSubmit = async () => {
    setError(null)
    try {
      await create.mutateAsync({ email, password, fullName, roles: selected })
      setEmail('')
      setFullName('')
      setPassword('')
      setSelected([])
      onClose()
    } catch (caught) {
      setError(apiErrorMessage(caught, 'Could not create the user'))
    }
  }

  return (
    <Dialog open={open} onClose={onClose} title="New user"
      description="The account is created active; the password can be changed by the user later.">
      <div className="space-y-3">
        <Field label="Full name"><Input value={fullName} aria-label="Full name"
          onChange={(event) => setFullName(event.target.value)} /></Field>
        <Field label="Email"><Input type="email" value={email} aria-label="Email"
          onChange={(event) => setEmail(event.target.value)} /></Field>
        <Field label="Password"><Input type="password" value={password} aria-label="Password"
          onChange={(event) => setPassword(event.target.value)} /></Field>

        <RolePicker roleNames={roleNames} selected={selected} onChange={setSelected} />

        {error ? (
          <p role="alert" className="rounded-md bg-destructive/10 px-3 py-2 text-sm text-destructive">{error}</p>
        ) : null}

        <div className="flex justify-end gap-2">
          <Button variant="outline" onClick={onClose} disabled={create.isPending}>Cancel</Button>
          <Button onClick={() => void onSubmit()} disabled={create.isPending}>
            {create.isPending ? 'Creating…' : 'Create user'}
          </Button>
        </div>
      </div>
    </Dialog>
  )
}

function EditAccessDialog({ user, onClose, roleNames }: {
  user: UserListItem | null
  onClose: () => void
  roleNames: string[]
}) {
  const [roles, setRoles] = useState<string[]>([])
  const [disciplines, setDisciplines] = useState('')
  const [error, setError] = useState<string | null>(null)
  const assignRoles = useAssignRoles()
  const setUserDisciplines = useSetDisciplines()

  // Re-seed the form whenever a different user is opened.
  const [lastId, setLastId] = useState<string | null>(null)
  if (user && user.id !== lastId) {
    setLastId(user.id)
    setRoles(user.roles)
    setDisciplines(user.disciplineCodes.join(', '))
    setError(null)
  }

  if (!user) return null

  const onSave = async () => {
    setError(null)
    try {
      await assignRoles.mutateAsync({ userId: user.id, roles })
      await setUserDisciplines.mutateAsync({
        userId: user.id,
        disciplineCodes: disciplines.split(',').map((code) => code.trim()).filter(Boolean),
      })
      onClose()
    } catch (caught) {
      setError(apiErrorMessage(caught, 'Could not update access'))
    }
  }

  const busy = assignRoles.isPending || setUserDisciplines.isPending

  return (
    <Dialog open onClose={onClose} title={user.fullName || user.email}
      description="Roles grant permissions; disciplines narrow what an editor may change.">
      <div className="space-y-3">
        <RolePicker roleNames={roleNames} selected={roles} onChange={setRoles} />

        <Field label="Discipline codes">
          <Input
            value={disciplines}
            aria-label="Discipline codes"
            placeholder="STL, STR — leave empty for all"
            onChange={(event) => setDisciplines(event.target.value)}
          />
        </Field>
        <p className="text-xs text-muted-foreground">
          Empty means every discipline. A code here stops the user editing rows outside it.
        </p>

        {error ? (
          <p role="alert" className="rounded-md bg-destructive/10 px-3 py-2 text-sm text-destructive">{error}</p>
        ) : null}

        <div className="flex justify-end gap-2">
          <Button variant="outline" onClick={onClose} disabled={busy}>Cancel</Button>
          <Button onClick={() => void onSave()} disabled={busy}>
            {busy ? 'Saving…' : 'Save access'}
          </Button>
        </div>
      </div>
    </Dialog>
  )
}

function RolePicker({ roleNames, selected, onChange }: {
  roleNames: string[]
  selected: string[]
  onChange: (roles: string[]) => void
}) {
  return (
    <fieldset>
      <legend className="mb-1 text-xs text-muted-foreground">Roles</legend>
      <div className="flex flex-wrap gap-3">
        {roleNames.map((role) => (
          <label key={role} className="flex items-center gap-2 text-sm">
            <input
              type="checkbox"
              checked={selected.includes(role)}
              onChange={(event) =>
                onChange(event.target.checked
                  ? [...selected, role]
                  : selected.filter((value) => value !== role))
              }
            />
            {role}
          </label>
        ))}
      </div>
    </fieldset>
  )
}

function Field({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <label className="block space-y-1">
      <span className="text-xs text-muted-foreground">{label}</span>
      {children}
    </label>
  )
}
