import { defineConfig, devices } from '@playwright/test';

/**
 * Сквозные тесты в настоящем браузере против запущенного приложения (фронт + API + база).
 * Адрес — E2E_BASE_URL (по умолчанию ng serve на 4200). Браузер — установленный Edge или Chrome (E2E_CHANNEL),
 * поэтому скачивать браузеры Playwright не нужно.
 *
 *   npm run e2e                                   # против http://localhost:4200
 *   E2E_BASE_URL=https://staging.example npm run e2e
 */
export default defineConfig({
  testDir: '.',
  timeout: 120_000,
  expect: { timeout: 10_000 },
  fullyParallel: false,
  retries: process.env['CI'] ? 1 : 0,
  reporter: [['list'], ['html', { open: 'never', outputFolder: '../dist/e2e-report' }]],
  outputDir: '../dist/e2e-results',
  use: {
    baseURL: process.env['E2E_BASE_URL'] ?? 'http://localhost:4200',
    channel: process.env['E2E_CHANNEL'] ?? 'msedge',
    locale: 'ru-RU',
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
  },
  projects: [
    { name: 'mobile', use: { ...devices['Pixel 7'], channel: process.env['E2E_CHANNEL'] ?? 'msedge' } },
    { name: 'desktop', use: { viewport: { width: 1280, height: 900 } } },
  ],
});
