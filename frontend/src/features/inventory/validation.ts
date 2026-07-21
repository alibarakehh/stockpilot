import { z } from 'zod'

const lifecycleStatusSchema = z.enum(['Active', 'Discontinued'])
const procurementStatusSchema = z.enum(['None', 'Ordered'])

export const itemInputSchema = z.object({
  name: z.string().trim().min(2, 'Enter at least 2 characters.').max(160),
  sku: z.string().trim().min(2, 'Enter at least 2 characters.').max(80),
  category: z.string().trim().min(2, 'Enter at least 2 characters.').max(100),
  description: z.string().trim().max(1000),
  location: z.string().trim().max(120),
  supplier: z.string().trim().max(160),
  quantity: z.number().int('Quantity must be a whole number.').min(0).max(2_147_483_647),
  reorderLevel: z.number().int('Reorder level must be a whole number.').min(0).max(2_147_483_647),
  purchasePrice: z.number().min(0).max(999_999_999),
  sellingPrice: z.number().min(0).max(999_999_999),
  lifecycleStatus: lifecycleStatusSchema,
  procurementStatus: procurementStatusSchema,
  version: z.number().int().positive().optional(),
})

export type ItemFormValues = z.infer<typeof itemInputSchema>
