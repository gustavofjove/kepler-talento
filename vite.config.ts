/// <reference types="vitest/config" />
import react from '@vitejs/plugin-react';
import { defineConfig } from 'vite';

export default defineConfig({
  plugins: [react()],
  server: {
    host: '0.0.0.0',
    // Not 4200: that port belongs to the Compose nginx, which serves the bundle baked into
    // its image. Sharing it let Playwright reuse nginx and test stale code. strictPort still
    // makes a clash fail loudly instead of drifting to another port.
    port: 5173,
    strictPort: true,
    // The API is not published to the host; reach it through the Compose nginx, which
    // forwards /api same-origin exactly as in the full stack.
    proxy: {
      '/api': process.env['KTL_API_PROXY_TARGET'] ?? 'http://127.0.0.1:4200',
    },
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
      {
        extends: true,
        test: {
          name: 'security',
          include: ['tests/security/**/*.spec.ts'],
          environment: 'node',
          globals: true,
        },
      },
    ],
  },
});
