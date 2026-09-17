import { test as base, expect } from '@playwright/test';
import path from 'node:path';
import { signInAs } from './support/auth';

export const test = base.extend({
  page: async ({ page, storageState }, use) => {
    if (typeof storageState === 'string') {
      const role = path.basename(storageState, '.json');
      if (role === 'rrhh_admin' || role === 'readonly' || role === 'system_admin')
        await signInAs(page, role);
    }
    await use(page);
  },
});

export { expect };
export type { Page, APIRequestContext } from '@playwright/test';
