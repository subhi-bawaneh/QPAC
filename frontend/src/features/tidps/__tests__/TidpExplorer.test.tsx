import { describe, expect, it, vi, beforeEach } from 'vitest'
import { render, screen, fireEvent } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { TidpExplorerPage } from '../TidpExplorerPage'
import type { Discipline, TidpFile } from '@/shared/api/types'

const navigate = vi.fn()
vi.mock('react-router-dom', async () => ({
  ...(await vi.importActual<typeof import('react-router-dom')>('react-router-dom')),
  useNavigate: () => navigate,
}))

let permissions: string[] = []
vi.mock('@/shared/auth/useAuth', () => ({
  useAuth: () => ({ can: (p: string) => permissions.includes(p) }),
}))

const disciplines: Discipline[] = [
  { id: 'd1', code: 'STL', corporateName: 'Structural', fileCount: 2 },
  { id: 'd2', code: 'ARC', corporateName: 'Architectural', fileCount: 0 },
]

const files: TidpFile[] = [
  {
    id: 'f1', projectId: 'p', disciplineId: 'd1', disciplineCode: 'STL',
    disciplineName: 'Structural', fileName: 'TIDP-STL.xlsx', documentReference: 'REF',
    revisionNumber: '00', rowsRead: 1283, rowsImported: 1283, rowsSkipped: 0,
    documentCount: 1283, editedCount: 4, uploadedBy: 'admin', uploadedAt: '2026-09-01T10:00:00',
    status: 'Imported', error: null,
  },
  {
    id: 'f2', projectId: 'p', disciplineId: 'd1', disciplineCode: 'STL',
    disciplineName: 'Structural', fileName: 'TIDP-STL-AFCO.xlsx', documentReference: 'REF',
    revisionNumber: '00', rowsRead: 69, rowsImported: 69, rowsSkipped: 0,
    documentCount: 69, editedCount: 0, uploadedBy: 'admin', uploadedAt: '2026-09-02T10:00:00',
    status: 'Failed', error: 'boom',
  },
]

vi.mock('../api', () => ({
  useDisciplines: () => ({ isPending: false, data: disciplines }),
  useTidpFiles: () => ({ isPending: false, data: files }),
  useUploadTidp: () => ({ isPending: false, isError: false, mutateAsync: vi.fn() }),
  useReplaceTidp: () => ({ isPending: false, mutateAsync: vi.fn() }),
  useDeleteTidp: () => ({ isPending: false, mutateAsync: vi.fn() }),
  useReplacePreview: () => ({ data: null }),
}))

function renderPage() {
  return render(
    <MemoryRouter>
      <TidpExplorerPage />
    </MemoryRouter>,
  )
}

describe('TidpExplorerPage', () => {
  beforeEach(() => {
    permissions = ['reports.view']
    navigate.mockClear()
  })

  // An empty discipline still appears. Hiding it would make the register look complete
  // when a whole discipline has never been uploaded.
  it('opens on the disciplines, including the ones with no file', () => {
    renderPage()

    expect(screen.getAllByText('Structural').length).toBeGreaterThan(0)
    expect(screen.getAllByText('Architectural').length).toBeGreaterThan(0)
    expect(screen.getByText('0 files')).toBeInTheDocument()
    expect(screen.getByText('2 files')).toBeInTheDocument()
  })

  it('goes into a discipline and lists its files with row count and date', () => {
    renderPage()

    fireEvent.doubleClick(screen.getByRole('option', { name: /Structural/ }))

    expect(screen.getByText('TIDP-STL.xlsx')).toBeInTheDocument()
    // The count and the date share one element with a line break between them.
    expect(screen.getByText((_, node) => node?.textContent?.includes('1,283 rows') === true,
      { selector: 'span' })).toBeTruthy()
    expect(screen.getByRole('button', { name: 'TIDPs' })).toBeInTheDocument()
  })

  it('opens a file in the grid on double click', () => {
    renderPage()

    fireEvent.doubleClick(screen.getByRole('option', { name: /Structural/ }))
    fireEvent.doubleClick(screen.getByRole('option', { name: /TIDP-STL\.xlsx/ }))

    expect(navigate).toHaveBeenCalledWith('/tidps/f1')
  })

  // A file manager is operated from the keyboard as often as from the mouse.
  it('moves the selection with the arrow keys and opens with Enter', () => {
    renderPage()

    const grid = screen.getByRole('listbox')
    fireEvent.keyDown(grid, { key: 'ArrowRight' })
    expect(screen.getByRole('option', { name: /Structural/ })).toHaveAttribute('aria-selected', 'true')

    fireEvent.keyDown(grid, { key: 'Enter' })
    expect(screen.getByText('TIDP-STL.xlsx')).toBeInTheDocument()
  })

  it('hides upload and the file actions without files.manage', () => {
    renderPage()

    expect(screen.queryByRole('button', { name: /Upload TIDP/ })).not.toBeInTheDocument()

    fireEvent.doubleClick(screen.getByRole('option', { name: /Structural/ }))
    fireEvent.click(screen.getByRole('option', { name: /TIDP-STL\.xlsx/ }))

    expect(screen.queryByRole('button', { name: 'Replace' })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Delete' })).not.toBeInTheDocument()
  })

  it('shows upload and the file actions to the super admin', () => {
    permissions = ['reports.view', 'files.manage']
    renderPage()

    expect(screen.getByRole('button', { name: /Upload TIDP/ })).toBeInTheDocument()

    fireEvent.doubleClick(screen.getByRole('option', { name: /Structural/ }))
    fireEvent.click(screen.getByRole('option', { name: /TIDP-STL\.xlsx/ }))

    expect(screen.getByRole('button', { name: 'Replace' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Delete' })).toBeInTheDocument()
  })
})
