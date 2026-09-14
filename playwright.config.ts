import { defineConfig, devices } from '@playwright/test';

export default defineConfig({
  testDir: './tests/e2e',
  timeout: 30_000,
  globalSetup: './tests/e2e/global-setup.ts',
  // The Vite dev server, so specs run the working tree. It proxies /api to the Compose
  // stack, which must be up (`docker compose up`). Never 4200: that is nginx serving the
  // image's prebuilt bundle.
  use: {
    baseURL: 'http://127.0.0.1:5173',
    trace: 'on-first-retry',
  },
  webServer: {
    command: 'npm start',
    url: 'http://127.0.0.1:5173',
    reuseExistingServer: !process.env['CI'],
    timeout: 120_000,
  },
  projects: [
    {
      name: 'chromium',
      use: { ...devices['Desktop Chrome'] },
    },
  ],
});
