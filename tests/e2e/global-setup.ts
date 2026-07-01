import { chromium, type FullConfig } from '@playwright/test';
import { mkdirSync, readFileSync } from 'node:fs';
import path from 'node:path';

const usersFixturePath = path.join(__dirname, '../security/fixtures/users.json');
const usersFixture: { users: Array<{ email: string; role: string }> } = JSON.parse(
  readFileSync(usersFixturePath, 'utf-8'),
);

export const AUTH_DIR = path.join(__dirname, '.auth');

export function authFile(role: string): string {
  return path.join(AUTH_DIR, `${role}.json`);
}

export default async function globalSetup(config: FullConfig): Promise<void> {
  const baseURL = config.projects[0]?.use?.baseURL ?? 'http://127.0.0.1:4200';
  mkdirSync(AUTH_DIR, { recursive: true });

  const browser = await chromium.launch();
  try {
    for (const user of usersFixture.users) {
      const context = await browser.newContext({ baseURL });
      const page = await context.newPage();
      await page.goto('/login');
      await page.fill('#email', user.email);
      await page.fill('#password', 'local-demo');
      await page.selectOption('#role', user.role);
      await page.click('button[type="submit"]');
      await page.waitForURL('**/app**');
      await context.storageState({ path: authFile(user.role) });
      await context.close();
    }
  } finally {
    await browser.close();
  }
}
