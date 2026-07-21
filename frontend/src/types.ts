export type StockStatus = 'InStock' | 'LowStock' | 'OutOfStock' | 'Ordered' | 'Discontinued'
export type InventoryLifecycleStatus = 'Active' | 'Discontinued'
export type ProcurementStatus = 'None' | 'Ordered'
export type UserRole = 'Admin' | 'Manager' | 'Viewer'
export type MovementType =
  | 'OpeningBalance'
  | 'Receipt'
  | 'Issue'
  | 'Damage'
  | 'Return'
  | 'Correction'

export interface User {
  id: string
  name: string
  email: string
  role: UserRole
}

export interface AuthResponse {
  expiresAtUtc: string
  user: User
}

export interface InventoryItem {
  id: string
  name: string
  sku: string
  category: string
  description: string
  location: string
  supplier: string
  quantity: number
  reorderLevel: number
  purchasePrice: number
  sellingPrice: number
  inventoryValue: number
  currencyCode: string
  lifecycleStatus: InventoryLifecycleStatus
  procurementStatus: ProcurementStatus
  status: StockStatus
  isArchived: boolean
  deletedAtUtc: string | null
  version: number
  createdAtUtc: string
  updatedAtUtc: string
}

export interface ItemInput {
  name: string
  sku: string
  category: string
  description: string
  location: string
  supplier: string
  quantity: number
  reorderLevel: number
  purchasePrice: number
  sellingPrice: number
  lifecycleStatus: InventoryLifecycleStatus
  procurementStatus: ProcurementStatus
  version?: number
}

export interface PagedResult<T> {
  items: T[]
  total: number
  page: number
  pageSize: number
  totalPages: number
}

export interface InventoryInsight {
  itemId: string
  itemName: string
  sku: string
  severity: 'critical' | 'warning' | 'info'
  title: string
  recommendation: string
  suggestedOrderQuantity: number
  healthScore: number
}

export interface CategorySummary {
  category: string
  itemCount: number
  units: number
  value: number
  atRiskCount: number
}

export interface InventoryIntelligence {
  overallHealthScore: number
  totalInventoryValue: number
  currencyCode: string
  lowStockCount: number
  outOfStockCount: number
  orderedCount: number
  discontinuedCount: number
  executiveSummary: string
  insights: InventoryInsight[]
  categories: CategorySummary[]
  generatedAtUtc: string
}

export interface InventorySummary {
  totalItems: number
  totalUnits: number
  totalValue: number
  currencyCode: string
  inStockCount: number
  lowStockCount: number
  outOfStockCount: number
  orderedCount: number
  discontinuedCount: number
}

export interface InventoryMovement {
  id: string
  requestId: string
  itemId: string
  itemName: string
  sku: string
  type: MovementType
  change: number
  previousQuantity: number
  newQuantity: number
  reason: string
  performedByName: string
  createdAtUtc: string
}

export interface AiSmartIntakeAvailability {
  available: boolean
  provider: string
  reason: string | null
}

export interface AiInventoryDraft {
  name: string
  sku: string
  description: string
  category: string
  quantity: number
  reorderLevel: number
  purchasePrice: number
  sellingPrice: number
  supplier: string
  location: string
  generatedFields: string[]
  warnings: string[]
}
