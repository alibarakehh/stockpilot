import { devices, expect, test } from '@playwright/test'
import { loginAs } from './authentication'

test.use({ ...devices['Pixel 5'] })

test('mobile inventory uses cards, avoids horizontal overflow, and keeps primary controls tappable', async ({
  page,
}) => {
  await loginAs(page, 'Manager')
  await page.getByRole('link', { name: 'Inventory', exact: true }).click()

  await expect(page.locator('.inventory-cards')).toBeVisible()
  await expect(page.locator('.desktop-inventory')).toBeHidden()
  expect(
    await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth),
  ).toBeTruthy()

  for (const [name, control] of [
    ['Add item', page.getByRole('button', { name: 'Add item' })],
    ['Inventory navigation', page.getByRole('link', { name: 'Inventory', exact: true })],
    [
      'Stock action',
      page.locator('.inventory-cards').getByRole('button', { name: 'Stock' }).first(),
    ],
  ] as const) {
    const box = await control.boundingBox()
    expect(box, `${name} must be visible`).not.toBeNull()
    expect(box!.height, `${name} touch target height`).toBeGreaterThanOrEqual(44)
  }
})
