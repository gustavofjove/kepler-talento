/// <reference types="vitest/config" />
import react from '@vitejs/plugin-react';
import { defineConfig } from 'vite';

export default defineConfig({
  plugins: [react()],
  server: {
    host: '0.0.0.0',
    // strictPort keeps Playwright's webServer from attaching to a stale dev
    // server on another port and reporting false greens.
    port: 4200,
    strictPort: true,
  },
  build: {
    outDir: 'dist',
    // Mirrors the 700 kB initial-bundle warning budget angular.json used to set.
    chunkSizeWarningLimit: 700,
  },
  test: {
    projects: [
      {
        extends: true,
        test: {
          name: 'unit',
          include: ['tests/unit/**/*.spec.{ts,tsx}'],
          environment: 'jsdom',
          setupFiles: ['./tests/setup.ts'],
          globals: true,
        },
      },
      {
        // The integration specs only read files from disk - node, not jsdom.
        extends: true,
        test: {
          name: 'integration',
          include: ['tests/integration/**/*.spec.ts'],
          environment: 'node',
          globals: true,
        },
      },
    ],
  },
});
