import { defineConfig, devices } from '@playwright/test';

export default defineConfig({
  testDir: './tests/e2e',
  timeout: 30_000,
  // The suite mutates one shared development database and scanner. Serial execution keeps
  // lifecycle journeys deterministic and mirrors their operator-facing use.
  workers: 1,
  globalSetup: './tests/e2e/global-setup.ts',
  // Purges every row the suite created; names carry a Date.now() marker for this.
  globalTeardown: './tests/e2e/global-teardown.ts',
  // The Vite dev server, so specs run the working tree. It proxies /api to the Compose
  // stack, which must be up (`docker compose up`). Never 4200: that is nginx serving the
  // image's prebuilt bundle.
  use: {
    baseURL: 'http://127.0.0.1:4300',
    trace: 'on-first-retry',
  },
  webServer: {
    command: 'npm start',
    url: 'http://127.0.0.1:4300',
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
