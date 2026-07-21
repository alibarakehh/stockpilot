import { defineConfig, devices } from '@playwright/test'

const connectionString = process.env.E2E_CONNECTION_STRING
const demoPassword = process.env.E2E_DEMO_PASSWORD

if (!connectionString || !demoPassword) {
  throw new Error('E2E environment is missing. Run the suite with npm run test:e2e.')
}

export default defineConfig({
  testDir: './e2e',
  fullyParallel: false,
  forbidOnly: Boolean(process.env.CI),
  retries: 0,
  workers: 1,
  reporter: process.env.CI ? [['github'], ['html', { open: 'never' }]] : 'list',
  outputDir: 'test-results',
  expect: { timeout: 10_000 },
  use: {
    baseURL: 'http://127.0.0.1:5175',
    screenshot: 'only-on-failure',
    trace: 'retain-on-failure',
  },
  projects: [
    {
      name: 'chromium',
      use: { ...devices['Desktop Chrome'] },
    },
  ],
  webServer: [
    {
      name: 'api',
      command:
        'dotnet run --project ../backend --configuration Release --urls http://127.0.0.1:5100',
      url: 'http://127.0.0.1:5100/api/health',
      env: {
        ...process.env,
        ASPNETCORE_ENVIRONMENT: 'Development',
        ConnectionStrings__DefaultConnection: connectionString,
        SeedDemoPassword: demoPassword,
        Database__ApplyMigrationsOnStartup: 'false',
        Deployment__RequireCurrentMigration: 'true',
        AI__SmartIntake__Enabled: 'false',
      },
      reuseExistingServer: false,
      timeout: 120_000,
      stdout: 'ignore',
      stderr: 'pipe',
    },
    {
      name: 'web',
      command: 'npm run dev -- --host 127.0.0.1 --port 5175',
      url: 'http://127.0.0.1:5175/login',
      env: {
        ...process.env,
        VITE_API_PROXY_TARGET: 'http://127.0.0.1:5100',
      },
      reuseExistingServer: false,
      timeout: 60_000,
      stdout: 'ignore',
      stderr: 'pipe',
    },
  ],
})
