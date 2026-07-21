import { render, screen } from '@testing-library/react'
import { describe, expect, it, vi } from 'vitest'
import { InventoryTable } from './InventoryTable'

describe('InventoryTable', () => {
  it('exposes its loading skeleton as a named status', () => {
    render(
      <InventoryTable
        items={[]}
        loading
        role="Viewer"
        onEdit={vi.fn()}
        onAdjust={vi.fn()}
        onDelete={vi.fn()}
      />,
    )

    expect(screen.getByRole('status', { name: 'Loading inventory' })).toBeInTheDocument()
  })
})
