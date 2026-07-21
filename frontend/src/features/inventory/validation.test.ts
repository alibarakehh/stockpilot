import { describe, expect, it } from 'vitest'
import { itemInputSchema } from './validation'

const validItem = {
  name: '  Thermal labels  ',
  sku: '  LAB-100  ',
  category: '  Supplies  ',
  description: '',
  location: 'A-01',
  supplier: 'Northwind',
  quantity: 10,
  reorderLevel: 4,
  purchasePrice: 12.5,
  sellingPrice: 18,
  lifecycleStatus: 'Active' as const,
  procurementStatus: 'None' as const,
}

describe('itemInputSchema', () => {
  it('normalizes valid text before it reaches the API', () => {
    const result = itemInputSchema.parse(validItem)

    expect(result.name).toBe('Thermal labels')
    expect(result.sku).toBe('LAB-100')
    expect(result.category).toBe('Supplies')
  })

  it.each([
    ['negative quantity', { quantity: -1 }],
    ['fractional quantity', { quantity: 1.5 }],
    ['negative purchase price', { purchasePrice: -0.01 }],
    ['short SKU', { sku: 'A' }],
  ])('rejects %s', (_scenario, override) => {
    expect(itemInputSchema.safeParse({ ...validItem, ...override }).success).toBe(false)
  })
})
