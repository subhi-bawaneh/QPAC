import { useEffect, useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { Dialog } from '@/shared/ui/dialog'
import { Button } from '@/shared/ui/button'
import { Checkbox } from '@/shared/ui/checkbox'
import { SelectItem, SimpleSelect } from '@/shared/ui/select'
import { api, apiErrorMessage } from '@/shared/api/client'
import type { FolderNode, PicklistGroup } from '@/shared/api/types'
import { useSetFolderCompany } from './api'

const NONE = '__none__'

// A company folder holds one delivery partner's work. The author comes from the
// Author picklist, which is what the Corporate Summary groups by.
export function CompanyDialog({ open, onClose, folder, projectId }: {
  open: boolean
  onClose: () => void
  folder: FolderNode
  projectId: string
}) {
  const [isCompany, setIsCompany] = useState(folder.isCompany)
  const [authorId, setAuthorId] = useState(folder.authorId ?? NONE)
  const [error, setError] = useState<string | null>(null)
  const save = useSetFolderCompany(projectId)

  useEffect(() => {
    setIsCompany(folder.isCompany)
    setAuthorId(folder.authorId ?? NONE)
  }, [folder])

  const authors = useQuery({
    queryKey: ['picklists', projectId],
    enabled: open,
    queryFn: async () => {
      const { data } = await api.get<PicklistGroup[]>(`/api/projects/${projectId}/picklists`)
      return data.find((group) => group.field === 'Author')?.items ?? []
    },
  })

  const submit = async () => {
    setError(null)
    try {
      await save.mutateAsync({
        folderId: folder.id,
        isCompany,
        authorId: isCompany && authorId !== NONE ? authorId : null,
      })
      onClose()
    } catch (caught) {
      setError(apiErrorMessage(caught, 'Could not save the company'))
    }
  }

  return (
    <Dialog
      open={open}
      onClose={onClose}
      title={`Company settings for ${folder.name}`}
      description="Not every folder is a company. A company folder names the partner its documents belong to."
    >
      <div className="space-y-4">
        <label className="flex items-center gap-2 text-sm">
          <Checkbox
            checked={isCompany}
            onCheckedChange={(value) => setIsCompany(value === true)}
            aria-label="This folder is a company"
          />
          This folder is a company
        </label>

        <div className="space-y-1">
          <span className="text-xs font-medium text-muted-foreground">Author</span>
          <SimpleSelect
            label="Author"
            value={authorId}
            disabled={!isCompany}
            onValueChange={setAuthorId}
          >
            <SelectItem value={NONE}>No author</SelectItem>
            {(authors.data ?? []).map((item) => (
              <SelectItem key={item.id} value={item.id}>{item.code}</SelectItem>
            ))}
          </SimpleSelect>
        </div>

        {error ? (
          <p role="alert" className="rounded-md bg-destructive/10 px-3 py-2 text-sm text-destructive">
            {error}
          </p>
        ) : null}

        <div className="flex justify-end gap-2">
          <Button variant="outline" onClick={onClose} disabled={save.isPending}>Cancel</Button>
          <Button onClick={() => void submit()} disabled={save.isPending}>
            {save.isPending ? 'Saving…' : 'Save'}
          </Button>
        </div>
      </div>
    </Dialog>
  )
}
