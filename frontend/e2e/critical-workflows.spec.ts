import { expect, test, type Page } from '@playwright/test'
import { loginAs } from './authentication'

async function openInventory(page: Page) {
  await page.getByRole('link', { name: 'Inventory', exact: true }).click()
  await expect(page.getByRole('heading', { name: 'Inventory', exact: true })).toBeVisible()
}

async function openItem(page: Page, name: string) {
  await openInventory(page)
  await page
    .getByRole('link', { name: new RegExp(name) })
    .first()
    .click()
  await expect(page.getByRole('heading', { name, exact: true })).toBeVisible()
}

async function expectStatus(page: Page, label: string) {
  await expect(page.locator('.status').filter({ hasText: new RegExp(`^${label}$`) })).toBeVisible()
}

async function recordReceipt(page: Page, quantity: number, reason: string) {
  await page.getByRole('button', { name: 'Update stock' }).click()
  const dialog = page.getByRole('dialog', { name: /Update/ })
  await dialog.getByLabel('Movement type').selectOption('Receipt')
  await dialog.getByLabel('Quantity').fill(String(quantity))
  await dialog.getByLabel('Reason or reference').fill(reason)
  await dialog.getByRole('button', { name: 'Record movement' }).click()
  await expect(page.getByRole('status')).toHaveText('Stock movement recorded.')
}

test('Manager creates an inventory item through the real application', async ({ page }) => {
  await loginAs(page, 'Manager')
  await openInventory(page)
  await page.getByRole('button', { name: 'Add item' }).click()

  const dialog = page.getByRole('dialog', { name: 'Add a new item' })
  await dialog.getByLabel('Item name').fill('E2E Manager Item')
  await dialog.getByLabel('SKU').fill('E2E-MANAGER-001')
  await dialog.getByLabel('Category').fill('Quality Assurance')
  await dialog.getByLabel('Storage location').fill('QA-01')
  await dialog.getByLabel('Supplier').fill('Test Supplier')
  await dialog.getByLabel('Quantity').fill('3')
  await dialog.getByLabel('Reorder level').fill('5')
  await dialog.getByLabel(/Purchase price/).fill('10')
  await dialog.getByLabel(/Selling price/).fill('15')
  await dialog.getByLabel('Description').fill('Created by the Manager browser workflow.')
  await dialog.getByRole('button', { name: 'Add item' }).click()

  await expect(page.getByRole('status')).toHaveText('Item added to inventory.')
  await page.getByRole('searchbox', { name: 'Search inventory' }).fill('E2E Manager Item')
  await page
    .getByRole('link', { name: /E2E Manager Item/ })
    .first()
    .click()
  await expect(page.getByRole('heading', { name: 'E2E Manager Item' })).toBeVisible()
  await expectStatus(page, 'Low stock')
})

test('Viewer is denied inventory-management and team controls', async ({ page }) => {
  await loginAs(page, 'Viewer')
  await openInventory(page)

  await expect(page.getByRole('button', { name: 'Add item' })).toHaveCount(0)
  await expect(page.getByRole('button', { name: 'AI Smart Entry' })).toHaveCount(0)
  await expect(page.getByRole('button', { name: /Edit / })).toHaveCount(0)
  await expect(page.getByRole('button', { name: 'Stock' })).toHaveCount(0)
  await expect(page.getByRole('link', { name: 'Team' })).toHaveCount(0)

  await page.goto('/team')
  await expect(page).toHaveURL(/\/dashboard$/)
  await expect(page.getByRole('heading', { name: 'Welcome back, Team' })).toBeVisible()
})

test('Administrator can add and remove a team member', async ({ page }) => {
  const unique = Date.now()
  const name = `E2E Remove ${unique}`
  const email = `e2e-remove-${unique}@stockpilot.local`
  await loginAs(page, 'Admin')
  await page.getByRole('link', { name: 'Team', exact: true }).click()
  await expect(page.getByRole('heading', { name: 'Team members' })).toBeVisible()
  await page.getByRole('button', { name: 'Add member' }).click()
  await page.getByLabel('Name').fill(name)
  await page.getByLabel('Email').fill(email)
  await page.getByLabel('Temporary password').fill('Temporary123!')
  await page.locator('form.inline-form select[name="role"]').selectOption('Viewer')
  await page.getByRole('button', { name: 'Add member', exact: true }).click()
  await expect(page.getByText(email, { exact: true })).toBeVisible()

  await page.getByRole('button', { name: `Remove ${name}` }).click()
  const confirmation = page.getByRole('alertdialog', { name: `Remove ${name}?` })
  await expect(confirmation).toContainText('immediately lose access')
  await confirmation.getByRole('button', { name: 'Remove member' }).click()

  await expect(page.getByRole('status')).toHaveText(`${name} was removed from the team.`)
  await expect(page.getByText(email, { exact: true })).toHaveCount(0)
})

test('stock adjustment updates the balance and attributable activity', async ({ page }) => {
  await loginAs(page, 'Manager')
  await openItem(page, 'Apple iPhone 15 128GB')

  await recordReceipt(page, 3, 'E2E receipt PO-7001')

  await expect(page.getByText('27', { exact: true }).first()).toBeVisible()
  await expect(page.getByText('Received Apple iPhone 15 128GB')).toBeVisible()
  await expect(page.getByText(/E2E receipt PO-7001.*Inventory Manager.*24.*27/)).toBeVisible()
})

test('derived status honors the reorder boundary and operational priority', async ({ page }) => {
  await loginAs(page, 'Manager')
  await openItem(page, 'Apple iPad Air 11-inch 128GB')
  await expectStatus(page, 'Low stock')

  await recordReceipt(page, 6, 'E2E boundary receipt')
  await expect(page.getByText('10', { exact: true }).first()).toBeVisible()
  await expectStatus(page, 'Low stock')

  await recordReceipt(page, 1, 'E2E above-boundary receipt')
  await expect(page.getByText('11', { exact: true }).first()).toBeVisible()
  await expectStatus(page, 'In stock')

  await openItem(page, 'Sony WH-1000XM5 Headphones')
  await expectStatus(page, 'Ordered')

  await openItem(page, 'Apple iPhone 11 64GB')
  await expectStatus(page, 'Discontinued')
  await expect(page.getByRole('button', { name: 'Update stock' })).toHaveCount(0)
})

test('AI draft remains preview-only until edited and explicitly saved', async ({ page }) => {
  const generatedName = 'E2E AI Suggested Scanner'
  const reviewedName = 'E2E Reviewed Scanner'
  await page.route('**/api/ai/inventory-draft/availability', (route) =>
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({ available: true, provider: 'E2E stub', reason: null }),
    }),
  )
  await page.route('**/api/ai/inventory-draft', (route) =>
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        name: generatedName,
        sku: 'E2E-AI-001',
        description: 'Barcode scanner suggested from untrusted text.',
        category: 'Equipment',
        quantity: 12,
        reorderLevel: 4,
        purchasePrice: 45,
        sellingPrice: 69,
        supplier: 'Scanner Supply',
        location: 'Shelf B2',
        generatedFields: [
          'name',
          'sku',
          'description',
          'category',
          'quantity',
          'reorderLevel',
          'purchasePrice',
          'sellingPrice',
          'supplier',
          'location',
        ],
        warnings: [],
      }),
    }),
  )

  await loginAs(page, 'Manager')
  await openInventory(page)
  await page.getByRole('button', { name: 'AI Smart Entry' }).click()
  await page
    .getByLabel('Item description')
    .fill('Add twelve barcode scanners from Scanner Supply to shelf B2.')
  await page.getByRole('button', { name: 'Create draft' }).click()
  await expect(page.getByText('Nothing has been saved')).toBeVisible()

  const previewCheck = await page.context().request.get(`/api/inventory?search=${generatedName}`)
  expect(previewCheck.ok()).toBeTruthy()
  expect((await previewCheck.json()).total).toBe(0)

  await page.getByRole('button', { name: 'Review and edit' }).click()
  const reviewDialog = page.getByRole('dialog', { name: 'Review AI-assisted draft' })
  const name = reviewDialog.getByLabel('Item name')
  await expect(name).toHaveValue(generatedName)
  await expect(name.locator('..')).toHaveAttribute('data-ai-suggested', 'true')
  await name.fill(reviewedName)
  await reviewDialog.getByRole('button', { name: 'Add item' }).click()

  await expect(page.getByRole('status')).toHaveText('Item added to inventory.')
  await page.getByRole('searchbox', { name: 'Search inventory' }).fill(reviewedName)
  await expect(page.getByRole('link', { name: new RegExp(reviewedName) }).first()).toBeVisible()
})
