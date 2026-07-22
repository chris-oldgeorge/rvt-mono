// File summary: Supports the React/Vite SPA entry point, routing, tests, and build configuration.
// Major updates:
// - 2026-05-26 5f9e8ed Initial pre-release alpha SPA import.
// - 2026-06-03 f5fd01e Preserved React SPA/API host compatibility during provider update where applicable.

/// <reference types="vitest" />
import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';
const apiTarget = process.env.RVT_PORTAL_API_URL ?? 'http://localhost:5178';
export default defineConfig({
  plugins: [react()],
  test: {
    exclude: ['**/._*', 'tests/e2e/**', 'node_modules/**', 'dist/**'],
    environment: 'jsdom',
    globals: true,
    include: ['src/**/*.test.ts', 'src/**/*.test.tsx'],
    setupFiles: './src/test/setupTests.ts',
    css: true
  },
  server: {
    port: 5173,
    strictPort: false,
    proxy: {
      '/api': {
        target: apiTarget,
        changeOrigin: true,
        secure: false
      },
      '/swagger': {
        target: apiTarget,
        changeOrigin: true,
        secure: false
      }
    }
  }
});
