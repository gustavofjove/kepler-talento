import { expect, type Locator, type Page } from '@playwright/test';

/**
 * Drives the shared catalog value picker (KTL-24) through its test identifiers and roles
 * only, never its Spanish copy. `prefix` is the picker's family prefix, e.g. `search-skill`
 * or `candidate-language`; each page renders one picker per prefix.
 *
 * Search pickers take an optional level: add the value, then `setLevel`. Candidate pickers
 * (except tags) require one: pass it to `addValue`, which chooses it in the editor that
 * opens on adding.
 */

/** Every chip of a picker. */
export function chips(page: Page, prefix: string): Locator {
  return page.getByTestId(`${prefix}-chip`);
}

/** The chip holding `value`. */
export function chip(page: Page, prefix: string, value: string): Locator {
  return page.locator(`[data-testid="${prefix}-chip"][data-value="${value}"]`);
}

/**
 * The picker's input, revealed through its (+) button when it is not already showing. The
 * (+) is disabled until the catalogs load, so this also waits for them.
 */
export async function openInput(page: Page, prefix: string): Promise<Locator> {
  const input = page.getByTestId(`${prefix}-input`);
  if (await input.count()) return input;
  const add = page.getByTestId(`${prefix}-add`);
  await expect(add).toBeEnabled();
  // react-aria closes an open list when the page scrolls, and the scroll that brings (+) into
  // view is only dispatched a frame later, so it is allowed to settle first, as for a person.
  await add.scrollIntoViewIfNeeded();
  await settleScroll(page);
  await add.click();
  await expect(input).toBeFocused();
  return input;
}

/**
 * The values the picker offers for `text`, typed into its input. react-aria renders the
 * "no matches" message as an unkeyed option, so only keyed options count.
 */
export async function offered(page: Page, prefix: string, text: string): Promise<Locator> {
  const input = await openInput(page, prefix);
  // Retyping the same text is not a change, and would not reopen the list.
  await input.fill('');
  await input.fill(text);
  return page.getByRole('listbox').locator('[role="option"][data-key]');
}

/** Adds whichever value the picker offers first, for specs that need any value at all. */
export async function addFirstOffered(page: Page, prefix: string): Promise<void> {
  const before = await chips(page, prefix).count();
  // (+) reveals the input with every value not yet held already listed; once the input is
  // showing, a click on it lists them again.
  const wasOpen = (await page.getByTestId(`${prefix}-input`).count()) > 0;
  const input = await openInput(page, prefix);
  if (wasOpen) await input.click();
  await page.getByRole('listbox').locator('[role="option"][data-key]').first().click();
  await expect(chips(page, prefix)).toHaveCount(before + 1);
}

export async function addValue(
  page: Page,
  prefix: string,
  name: string,
  level?: string,
): Promise<void> {
  await (await openInput(page, prefix)).fill(name);
  await page.getByRole('listbox').getByRole('option', { name, exact: true }).click();
  if (level !== undefined) await chooseLevel(page, prefix, level);
  // A candidate chip is replaced by the saved entry's once the write lands; wait for that.
  await expect(chip(page, prefix, name)).toBeVisible();
  await expect(chip(page, prefix, name)).not.toHaveAttribute('data-status');
}

/** Opens an existing chip's editor and chooses `level`, changing the item in place. */
export async function setLevel(
  page: Page,
  prefix: string,
  name: string,
  level: string,
): Promise<void> {
  await chip(page, prefix, name).click();
  await chooseLevel(page, prefix, level);
}

export async function removeValue(page: Page, prefix: string, name: string): Promise<void> {
  await chip(page, prefix, name).getByTestId(`${prefix}-remove`).click();
  await expect(chip(page, prefix, name)).toHaveCount(0);
}

/** Waits two animation frames, after which a pending scroll event has been dispatched. */
async function settleScroll(page: Page): Promise<void> {
  await page.evaluate(
    () =>
      new Promise<void>((resolve) =>
        requestAnimationFrame(() => requestAnimationFrame(() => resolve())),
      ),
  );
}

async function chooseLevel(page: Page, prefix: string, level: string): Promise<void> {
  const editor = page.getByTestId(`${prefix}-editor`);
  await expect(editor).toBeVisible();
  const radios = editor.getByRole('radiogroup');
  if (await radios.count()) {
    await radios.getByRole('radio', { name: level, exact: true }).click();
  } else {
    // Long level families use a select instead of a toggle group.
    await editor.getByRole('button').first().click();
    await page.getByRole('option', { name: level, exact: true }).click();
  }
  await expect(editor).toHaveCount(0);
}
