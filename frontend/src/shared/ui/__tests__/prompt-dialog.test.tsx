import { describe, expect, it, vi } from 'vitest'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { ConfirmDialog, PromptDialog } from '../prompt-dialog'

// Radix locks pointer events on the body while a modal is open, which userEvent
// refuses to click through unless told the check is intentional.
const user = userEvent.setup({ pointerEventsCheck: 0 })

describe('PromptDialog', () => {
  it('will not submit an empty value', async () => {
    const onConfirm = vi.fn()
    render(
      <PromptDialog open title="New folder" label="Folder name" onCancel={vi.fn()} onConfirm={onConfirm} />,
    )

    expect(screen.getByRole('button', { name: /create|save/i })).toBeDisabled()
    expect(onConfirm).not.toHaveBeenCalled()
  })

  it('returns the trimmed value', async () => {
    const onConfirm = vi.fn()
    render(
      <PromptDialog open title="New folder" label="Folder name" confirmLabel="Create"
        onCancel={vi.fn()} onConfirm={onConfirm} />,
    )

    await user.type(screen.getByRole('textbox', { name: 'Folder name' }), '  02.TIDPs  ')
    await user.click(screen.getByRole('button', { name: 'Create' }))

    expect(onConfirm).toHaveBeenCalledWith('02.TIDPs')
  })

  it('seeds the field for a rename', () => {
    render(
      <PromptDialog open title="Rename" label="Folder name" initialValue="01.NAP"
        onCancel={vi.fn()} onConfirm={vi.fn()} />,
    )

    expect(screen.getByRole('textbox', { name: 'Folder name' })).toHaveValue('01.NAP')
  })

  it('renders nothing when closed', () => {
    render(
      <PromptDialog open={false} title="New folder" label="Folder name" onCancel={vi.fn()} onConfirm={vi.fn()} />,
    )

    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
  })
})

describe('ConfirmDialog', () => {
  it('states what will happen before destroying anything', () => {
    render(
      <ConfirmDialog
        open
        title="Delete this folder?"
        description="'DraftTest' is removed from the explorer."
        onCancel={vi.fn()}
        onConfirm={vi.fn()}
      />,
    )

    expect(screen.getByText(/is removed from the explorer/i)).toBeInTheDocument()
  })

  it('confirms only when the destructive button is used', async () => {
    const onConfirm = vi.fn()
    const onCancel = vi.fn()
    render(
      <ConfirmDialog open title="Delete?" description="Gone." confirmLabel="Delete folder"
        onCancel={onCancel} onConfirm={onConfirm} />,
    )

    await user.click(screen.getByRole('button', { name: 'Cancel' }))
    expect(onCancel).toHaveBeenCalled()
    expect(onConfirm).not.toHaveBeenCalled()

    await user.click(screen.getByRole('button', { name: 'Delete folder' }))
    expect(onConfirm).toHaveBeenCalled()
  })
})
