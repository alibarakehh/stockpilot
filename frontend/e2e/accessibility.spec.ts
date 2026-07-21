import AxeBuilder from '@axe-core/playwright'
import { expect, test, type Page } from '@playwright/test'

const wcagTags = ['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa']

async function expectNoWcagViolations(page: Page) {
  await page.waitForLoadState('networkidle')
  await expect(
    page.locator('.session-loading, .activity-loading, .inventory-skeleton'),
  ).toHaveCount(0)
  await page.evaluate(async () => document.fonts.ready)

  const result = await new AxeBuilder({ page }).withTags(wcagTags).analyze()
  const violations = result.violations.map((violation) => ({
    id: violation.id,
    help: violation.help,
    nodes: violation.nodes.map((node) => ({
      target: node.target.join(' '),
      summary: node.failureSummary,
    })),
  }))
  expect(violations, JSON.stringify(violations, null, 2)).toEqual([])
}

async function loginAsManager(page: Page) {
  await page.goto('/login')
  await page.getByRole('button', { name: 'Manager', exact: true }).click()
  await page.getByRole('button', { name: 'Sign in', exact: true }).click()
  await expect(page).toHaveURL(/\/dashboard$/)
}

test('public login has no automatically detectable WCAG A or AA violations', async ({ page }) => {
  await page.goto('/login')
  await expect(page.getByRole('heading', { name: 'Sign in to your workspace' })).toBeVisible()
  await expectNoWcagViolations(page)
})

test('authenticated core inventory pages have no automatically detectable WCAG violations', async ({
  page,
}) => {
  await loginAsManager(page)
  await expectNoWcagViolations(page)

  await page.getByRole('link', { name: 'Inventory', exact: true }).click()
  await expect(page.getByRole('heading', { name: 'Inventory', exact: true })).toBeVisible()
  await expectNoWcagViolations(page)

  await page
    .getByRole('link', { name: /Ergonomic Keyboard/ })
    .first()
    .click()
  await expect(page.getByRole('heading', { name: 'Ergonomic Keyboard' })).toBeVisible()
  await expectNoWcagViolations(page)
})
