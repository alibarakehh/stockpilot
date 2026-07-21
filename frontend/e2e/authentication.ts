import { expect, type Page } from '@playwright/test'

const demoPassword = requiredDemoPassword()

const emails = {
  Admin: 'admin@stockpilot.local',
  Manager: 'manager@stockpilot.local',
  Viewer: 'viewer@stockpilot.local',
} as const

export async function loginAs(page: Page, role: keyof typeof emails) {
  await page.goto('/login')
  await page.getByLabel('Email address').fill(emails[role])
  await page.getByLabel('Password').fill(demoPassword)
  await page.getByRole('button', { name: 'Sign in', exact: true }).click()
  await expect(page).toHaveURL(/\/dashboard$/)
}

function requiredDemoPassword() {
  const value = process.env.E2E_DEMO_PASSWORD
  if (!value) {
    throw new Error('E2E_DEMO_PASSWORD is missing. Run the suite with npm run test:e2e.')
  }
  return value
}
