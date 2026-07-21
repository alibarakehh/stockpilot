import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import type { StockStatus } from '../types'
import { StatusBadge } from './StatusBadge'

describe('StatusBadge', () => {
  it.each<[StockStatus, string]>([
    ['InStock', 'In stock'],
    ['LowStock', 'Low stock'],
    ['OutOfStock', 'Out of stock'],
    ['Ordered', 'Ordered'],
    ['Discontinued', 'Discontinued'],
  ])('renders %s with its operational label and class', (status, label) => {
    render(<StatusBadge status={status} />)

    expect(screen.getByText(label)).toHaveClass('status', status.toLowerCase())
  })
})
