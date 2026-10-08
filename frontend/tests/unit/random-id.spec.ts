import { afterEach, describe, expect, it, vi } from 'vitest';
import { randomId } from '../../src/app/shared/random-id';
import { ToastService } from '../../src/app/core/services/toast.service';

const UUID_V4 = /^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/;

/** What a plain-HTTP page such as http://ktl.lan offers: getRandomValues, but no randomUUID. */
function insecureContextCrypto(): void {
  const real = globalThis.crypto;
  vi.stubGlobal('crypto', { getRandomValues: real.getRandomValues.bind(real) });
}

describe('randomId', () => {
  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('uses crypto.randomUUID when the page is a secure context', () => {
    expect(randomId()).toMatch(UUID_V4);
  });

  it('builds distinct version 4 UUIDs without crypto.randomUUID', () => {
    insecureContextCrypto();

    const ids = Array.from({ length: 50 }, () => randomId());

    for (const id of ids) expect(id).toMatch(UUID_V4);
    expect(new Set(ids).size).toBe(ids.length);
  });

  it('lets a toast be shown without crypto.randomUUID', () => {
    insecureContextCrypto();
    const toasts = new ToastService();

    toasts.show('synthetic message', 'error');

    expect(toasts.messages()).toEqual([
      { id: expect.stringMatching(UUID_V4), text: 'synthetic message', type: 'error' },
    ]);
  });
});
