import { describe, expect, it, vi } from 'vitest'
import { render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import userEvent from '@testing-library/user-event'
import { FileList } from '../FileList'
import { formatSize } from '@/shared/lib/utils'
import type { FolderFileSummary } from '@/shared/api/types'

const file: FolderFileSummary = {
  id: 'file-1',
  name: 'TIDP-STL.xlsx',
  kind: 'Tidp',
  source: 'Upload',
  state: 'NotImported',
  sizeBytes: 348_048,
  driveModifiedAt: null,
  driveFileId: null,
  md5: null,
}

describe('FileList', () => {
  it('explains an empty folder', () => {
    render(<FileList files={[]} canImport canManage isDraftFolder={false} onImport={vi.fn()} onDelete={vi.fn()} />)

    expect(screen.getByText(/no files in this folder/i)).toBeInTheDocument()
  })

  it('shows the kind and import state of each file', () => {
    render(<FileList files={[file]} canImport canManage isDraftFolder={false} onImport={vi.fn()} onDelete={vi.fn()} />)

    expect(screen.getByText('TIDP-STL.xlsx')).toBeInTheDocument()
    expect(screen.getByText('TIDP')).toBeInTheDocument()
    expect(screen.getByText('Not imported')).toBeInTheDocument()
  })

  it('hides the actions a user has no permission for', () => {
    render(<FileList files={[file]} canImport={false} canManage={false} isDraftFolder={false} onImport={vi.fn()} onDelete={vi.fn()} />)

    expect(screen.queryByRole('button', { name: /import/i })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /delete/i })).not.toBeInTheDocument()
  })

  it('asks to import the clicked file', async () => {
    const user = userEvent.setup()
    const onImport = vi.fn()
    render(<FileList files={[file]} canImport canManage isDraftFolder={false} onImport={onImport} onDelete={vi.fn()} />)

    await user.click(screen.getByRole('button', { name: /import/i }))

    expect(onImport).toHaveBeenCalledWith(file)
  })
})

describe('formatSize', () => {
  it.each([
    [0, '—'],
    [512, '512 B'],
    [348_048, '339.9 KB'],
    [8_674_890, '8.3 MB'],
  ])('formats %i bytes', (bytes, expected) => {
    expect(formatSize(bytes)).toBe(expected)
  })
})

describe('FileList draft review action', () => {
  it('offers Review draft only for an imported file in a Draft folder', () => {
    render(
      <MemoryRouter>
        <FileList
          files={[{ ...file, state: 'Imported' }]}
          canImport
          canManage
          isDraftFolder
          onImport={vi.fn()}
          onDelete={vi.fn()}
        />
      </MemoryRouter>,
    )

    expect(screen.getByRole('link', { name: /review draft/i })).toHaveAttribute('href', '/drafts/file-1')
  })

  it('hides it for a Live folder', () => {
    render(
      <MemoryRouter>
        <FileList
          files={[{ ...file, state: 'Imported' }]}
          canImport
          canManage
          isDraftFolder={false}
          onImport={vi.fn()}
          onDelete={vi.fn()}
        />
      </MemoryRouter>,
    )

    expect(screen.queryByRole('link', { name: /review draft/i })).not.toBeInTheDocument()
  })

  it('hides it until the file has actually been imported', () => {
    render(
      <MemoryRouter>
        <FileList files={[file]} canImport canManage isDraftFolder onImport={vi.fn()} onDelete={vi.fn()} />
      </MemoryRouter>,
    )

    expect(screen.queryByRole('link', { name: /review draft/i })).not.toBeInTheDocument()
  })
})
