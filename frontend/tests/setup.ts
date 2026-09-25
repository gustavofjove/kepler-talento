import '@testing-library/jest-dom/vitest';
import { cleanup } from '@testing-library/react';
import { afterEach } from 'vitest';
// Same bundled Spanish resources as the app; a missing key throws under test.
import '../src/app/core/i18n/i18n';

afterEach(() => {
  cleanup();
});

// jsdom does not implement these; several services rely on them.
// structuredClone: candidate-form drafts and cloneSearchFilters.
// createObjectURL: document.service.createSecureUrl and the CSV downloads.
if (typeof globalThis.structuredClone !== 'function') {
  globalThis.structuredClone = (value: unknown) => JSON.parse(JSON.stringify(value));
}

// React Router's data routers (the candidate page's `useBlocker`, KTL-29) build a `Request`
// for every navigation with jsdom's `AbortSignal`, which Node's own `Request` refuses. Retry
// without the signal only in that case; nothing under test aborts a navigation request.
{
  const NativeRequest = globalThis.Request;
  globalThis.Request = class extends NativeRequest {
    constructor(input: RequestInfo | URL, init?: RequestInit) {
      try {
        super(input, init);
      } catch (error) {
        if (!init?.signal || !(error instanceof TypeError)) throw error;
        const { signal: _signal, ...rest } = init;
        super(input, rest);
      }
    }
  } as typeof Request;
}

if (typeof URL.createObjectURL !== 'function') {
  URL.createObjectURL = () => 'blob:mock-url';
}
if (typeof URL.revokeObjectURL !== 'function') {
  URL.revokeObjectURL = () => undefined;
}
