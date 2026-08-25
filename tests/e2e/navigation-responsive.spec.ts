import { expect, test, type Page } from '@playwright/test';
import { authFile } from './global-setup';

/**
 * The counterpart to tests/unit/primary-nav.spec.tsx. Everything that depends
 * on the 768px breakpoint lives here, because jsdom has no media queries (see
 * design.md - D1).
 */

test.use({ storageState: authFile('rrhh_admin') });

const ADMIN_ITEMS = ['nav-catalogs', 'nav-users', 'nav-roles', 'nav-import'];

async function headerIsOneRow(page: Page): Promise<void> {
  const header = page.locator('.shell header');
  const brand = page.locator('.shell .brand');
  const user = page.locator('.shell .user');

  const [brandBox, userBox, headerBox] = await Promise.all([
    brand.boundingBox(),
    user.boundingBox(),
    header.boundingBox(),
  ]);
  expect(brandBox).not.toBeNull();
  expect(userBox).not.toBeNull();
  expect(headerBox).not.toBeNull();

  // Brand and user block share a row and do not overlap horizontally.
  expect(brandBox!.x + brandBox!.width).toBeLessThanOrEqual(userBox!.x + 1);
  // Nothing spills past the viewport.
  const viewport = page.viewportSize();
  expect(headerBox!.x + headerBox!.width).toBeLessThanOrEqual(viewport!.width + 1);
}

test.describe('Primary navigation - wide viewport', () => {
  test('groups the administration entries behind a single Admin trigger', async ({ page }) => {
    await page.goto('/app');

    await expect(page.getByTestId('nav-dashboard')).toBeVisible();
    await expect(page.getByTestId('nav-candidates')).toBeVisible();
    await expect(page.getByTestId('nav-search')).toBeVisible();
    await expect(page.getByTestId('nav-admin-trigger')).toBeVisible();
    await expect(page.getByTestId('nav-hamburger')).toBeHidden();

    for (const testId of ADMIN_ITEMS) {
      await expect(page.getByTestId(testId)).toHaveCount(0);
    }
  });

  test('the Admin trigger is a button that does not navigate', async ({ page }) => {
    await page.goto('/app');
    const trigger = page.getByTestId('nav-admin-trigger');

    await expect(trigger).toHaveJSProperty('tagName', 'BUTTON');
    await expect(trigger).not.toHaveAttribute('href', /.*/);

    await trigger.click();
    await expect(page).toHaveURL(/\/app$/);
  });

  test('opens a dropdown below the trigger, fully visible over the page', async ({ page }) => {
    await page.goto('/app');
    const trigger = page.getByTestId('nav-admin-trigger');
    await expect(trigger).toHaveAttribute('aria-expanded', 'false');

    await trigger.click();

    const panel = page.getByTestId('nav-admin-panel');
    await expect(trigger).toHaveAttribute('aria-expanded', 'true');
    await expect(panel).toBeVisible();
    for (const testId of ADMIN_ITEMS) {
      await expect(page.getByTestId(testId)).toBeVisible();
    }

    // Not clipped by the header, and positioned below the trigger.
    const triggerBox = await trigger.boundingBox();
    const panelBox = await panel.boundingBox();
    expect(panelBox!.y).toBeGreaterThanOrEqual(triggerBox!.y + triggerBox!.height - 1);
    expect(panelBox!.height).toBeGreaterThan(0);
  });

  test('closes on a second click, on Escape and on an outside click', async ({ page }) => {
    await page.goto('/app');
    const trigger = page.getByTestId('nav-admin-trigger');
    const panel = page.getByTestId('nav-admin-panel');

    await trigger.click();
    await trigger.click();
    await expect(panel).toHaveCount(0);

    await trigger.click();
    await page.keyboard.press('Escape');
    await expect(panel).toHaveCount(0);
    await expect(trigger).toBeFocused();

    await trigger.click();
    await page.locator('#main-content').click({ position: { x: 5, y: 5 } });
    await expect(panel).toHaveCount(0);
  });

  test('navigating to a child closes the panel and marks the parent active', async ({ page }) => {
    await page.goto('/app');

    await page.getByTestId('nav-admin-trigger').click();
    await page.getByTestId('nav-roles').click();

    await expect(page).toHaveURL(/\/app\/admin\/roles$/);
    await expect(page.getByTestId('nav-admin-panel')).toHaveCount(0);
    await expect(page.getByTestId('nav-admin-trigger')).toHaveClass(/active/);
  });

  test('landing directly on an administration route opens the group already active', async ({
    page,
  }) => {
    await page.goto('/app/admin/import');

    const trigger = page.getByTestId('nav-admin-trigger');
    await expect(trigger).toHaveClass(/active/);
    await expect(trigger).toHaveAttribute('aria-expanded', 'true');
    await expect(page.getByTestId('nav-import')).toBeVisible();
  });

  test('the whole group is reachable by keyboard alone', async ({ page }) => {
    await page.goto('/app');
    const trigger = page.getByTestId('nav-admin-trigger');

    await trigger.focus();
    await page.keyboard.press('Enter');
    await expect(page.getByTestId('nav-admin-panel')).toBeVisible();

    await page.keyboard.press('Tab');
    await expect(page.getByTestId('nav-catalogs')).toBeFocused();
    await page.keyboard.press('Enter');
    await expect(page).toHaveURL(/\/app\/catalogs$/);
  });
});

test.describe('Primary navigation - narrow viewport', () => {
  // hasTouch is what enables .tap(); isMobile alone does not.
  test.use({ viewport: { width: 390, height: 844 }, isMobile: true, hasTouch: true });

  test('replaces the horizontal entries with a hamburger', async ({ page }) => {
    await page.goto('/app');

    await expect(page.getByTestId('nav-hamburger')).toBeVisible();
    await expect(page.getByTestId('nav-dashboard')).toBeHidden();
    await expect(page.getByTestId('nav-admin-trigger')).toBeHidden();
    await headerIsOneRow(page);
  });

  test('the hamburger meets the 44px touch target and announces its state', async ({ page }) => {
    await page.goto('/app');
    const hamburger = page.getByTestId('nav-hamburger');

    const box = await hamburger.boundingBox();
    expect(box!.width).toBeGreaterThanOrEqual(44);
    expect(box!.height).toBeGreaterThanOrEqual(44);

    await expect(hamburger).toHaveAttribute('aria-label', 'Abrir menú');
    await hamburger.tap();
    await expect(hamburger).toHaveAttribute('aria-label', 'Cerrar menú');
    await expect(hamburger).toHaveAttribute('aria-expanded', 'true');
  });

  test('opens a full-width panel that pushes the content down', async ({ page }) => {
    await page.goto('/app');
    const main = page.locator('#main-content');
    const before = await main.boundingBox();

    await page.getByTestId('nav-hamburger').tap();

    const panel = page.getByTestId('nav-mobile-panel');
    await expect(panel).toBeVisible();
    await expect(page.getByTestId('nav-dashboard')).toBeVisible();
    await expect(page.getByTestId('nav-admin-trigger')).toBeVisible();

    const panelBox = await panel.boundingBox();
    const after = await main.boundingBox();
    // Full width, in flow: main moved down rather than being covered.
    expect(panelBox!.width).toBeGreaterThan(300);
    expect(after!.y).toBeGreaterThan(before!.y);
  });

  test('the Admin accordion indents its children and displaces what follows', async ({ page }) => {
    await page.goto('/app');
    await page.getByTestId('nav-hamburger').tap();
    await page.getByTestId('nav-admin-trigger').tap();

    const trigger = page.getByTestId('nav-admin-trigger');
    const child = page.getByTestId('nav-catalogs');
    await expect(child).toBeVisible();

    const triggerBox = await trigger.boundingBox();
    const childBox = await child.boundingBox();
    expect(childBox!.x).toBeGreaterThan(triggerBox!.x);
    expect(childBox!.y).toBeGreaterThan(triggerBox!.y);
  });

  test('activating a link closes the whole panel', async ({ page }) => {
    await page.goto('/app');
    await page.getByTestId('nav-hamburger').tap();
    await page.getByTestId('nav-candidates').tap();

    await expect(page).toHaveURL(/\/app\/candidates$/);
    await expect(page.getByTestId('nav-candidates')).toBeHidden();
    await expect(page.getByTestId('nav-hamburger')).toHaveAttribute('aria-expanded', 'false');
  });
});

test.describe('Primary navigation - hiding a link is not the control', () => {
  test('a readonly user sees no Admin group and is still refused the route by URL', async ({
    browser,
    baseURL,
  }) => {
    const context = await browser.newContext({ baseURL, storageState: authFile('readonly') });
    const page = await context.newPage();

    await page.goto('/app');
    await expect(page.getByTestId('nav-admin-trigger')).toHaveCount(0);

    // The refusal comes from RequirePermission and RLS, not from the missing link.
    await page.goto('/app/admin/roles');
    await expect(page).toHaveURL(/\/app$/);

    await context.close();
  });
});

test.describe('Primary navigation - header holds at every width', () => {
  for (const width of [390, 768, 1024, 1440]) {
    test(`no overflow or overlap at ${width}px`, async ({ page }) => {
      await page.setViewportSize({ width, height: 900 });
      await page.goto('/app');

      await headerIsOneRow(page);

      const scrolls = await page.evaluate(
        () => document.documentElement.scrollWidth > document.documentElement.clientWidth,
      );
      expect(scrolls).toBe(false);
    });
  }
});
