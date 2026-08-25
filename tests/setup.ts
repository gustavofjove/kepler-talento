import '@testing-library/jest-dom/vitest';
import { cleanup } from '@testing-library/react';
import { afterEach } from 'vitest';

afterEach(() => {
  cleanup();
});

// jsdom does not implement these; several services rely on them.
// structuredClone: candidate-form drafts and cloneSearchFilters.
// createObjectURL: document.service.createSecureUrl and the CSV downloads.
if (typeof globalThis.structuredClone !== 'function') {
  globalThis.structuredClone = (value: unknown) => JSON.parse(JSON.stringify(value));
}

if (typeof URL.createObjectURL !== 'function') {
  URL.createObjectURL = () => 'blob:mock-url';
}
if (typeof URL.revokeObjectURL !== 'function') {
  URL.revokeObjectURL = () => undefined;
}
