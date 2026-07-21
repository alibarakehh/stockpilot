import { z } from 'zod'

export const smartIntakeSchema = z.object({
  description: z
    .string()
    .trim()
    .min(10, 'Describe the item in at least 10 characters.')
    .max(2000, 'Keep the description under 2,000 characters.'),
})

export type SmartIntakeForm = z.infer<typeof smartIntakeSchema>
