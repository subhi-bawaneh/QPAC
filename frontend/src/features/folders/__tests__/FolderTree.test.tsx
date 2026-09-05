import { describe, expect, it, vi } from 'vitest'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { FolderTree } from '../FolderTree'
import type { FolderTreeNode } from '@/shared/api/types'

const node = (name: string, children: FolderTreeNode[] = [], target: 'Live' | 'Draft' = 'Live'): FolderTreeNode => ({
  id: name,
  parentId: null,
  name,
  path: name,
  target,
  children,
})

const tree = [node('02.TIDPs', [node('01.NAP', [node('ST-Structural', [], 'Draft')])])]

describe('FolderTree', () => {
  it('tells the user when there is nothing to show', () => {
    render(<FolderTree nodes={[]} selectedId={null} onSelect={vi.fn()} />)

    expect(screen.getByText(/no folders yet/i)).toBeInTheDocument()
  })

  it('opens the top level but leaves deeper folders collapsed', () => {
    render(<FolderTree nodes={tree} selectedId={null} onSelect={vi.fn()} />)

    expect(screen.getByRole('button', { name: '01.NAP' })).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'ST-Structural' })).not.toBeInTheDocument()
  })

  it('expands a folder when its chevron is used', async () => {
    const user = userEvent.setup()
    render(<FolderTree nodes={tree} selectedId={null} onSelect={vi.fn()} />)

    await user.click(screen.getByRole('button', { name: /expand 01\.NAP/i }))

    expect(screen.getByRole('button', { name: 'ST-Structural' })).toBeInTheDocument()
  })

  it('reports the clicked folder', async () => {
    const user = userEvent.setup()
    const onSelect = vi.fn()
    render(<FolderTree nodes={tree} selectedId={null} onSelect={onSelect} />)

    await user.click(screen.getByRole('button', { name: '01.NAP' }))

    expect(onSelect).toHaveBeenCalledWith(expect.objectContaining({ name: '01.NAP' }))
  })

  it('marks the selected folder for assistive technology', () => {
    render(<FolderTree nodes={tree} selectedId="02.TIDPs" onSelect={vi.fn()} />)

    const selected = screen.getAllByRole('treeitem').find((item) => item.getAttribute('aria-selected') === 'true')
    expect(selected).toHaveTextContent('02.TIDPs')
  })

  // A Draft folder is the one thing a reviewer must never miss at a glance.
  it('badges a Draft folder in the tree', async () => {
    const user = userEvent.setup()
    render(<FolderTree nodes={tree} selectedId={null} onSelect={vi.fn()} />)

    await user.click(screen.getByRole('button', { name: /expand 01\.NAP/i }))

    expect(screen.getByText('Draft')).toBeInTheDocument()
  })
})
