import js from '@eslint/js';
import reactHooks from 'eslint-plugin-react-hooks';
import reactRefresh from 'eslint-plugin-react-refresh';
import tseslint from 'typescript-eslint';

export default tseslint.config(
  {
    ignores: ['**/._*', 'dist', 'node_modules', 'coverage', 'playwright-report', 'test-results']
  },
  js.configs.recommended,
  ...tseslint.configs.recommended,
  {
    files: ['**/*.{ts,tsx}'],
    languageOptions: {
      ecmaVersion: 2022,
      globals: {
        document: 'readonly',
        fetch: 'readonly',
        FormEvent: 'readonly',
        history: 'readonly',
        importMeta: 'readonly',
        location: 'readonly',
        PopStateEvent: 'readonly',
        RequestInfo: 'readonly',
        Response: 'readonly',
        URL: 'readonly',
        URLSearchParams: 'readonly'
      },
      parserOptions: {
        project: ['./tsconfig.app.json', './tsconfig.test.json', './tsconfig.node.json'],
        tsconfigRootDir: import.meta.dirname
      }
    },
    plugins: {
      'react-hooks': reactHooks,
      'react-refresh': reactRefresh
    },
    rules: {
      ...reactHooks.configs.recommended.rules,
      'react-refresh/only-export-components': ['warn', { allowConstantExport: true }],
      '@typescript-eslint/no-unused-vars': ['error', { argsIgnorePattern: '^_' }]
    }
  },
  {
    files: ['tests/e2e/**/*.ts', 'playwright.config.ts'],
    languageOptions: {
      globals: {
        process: 'readonly'
      }
    }
  }
);
