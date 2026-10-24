import js from '@eslint/js'
import globals from 'globals'
import reactHooks from 'eslint-plugin-react-hooks'
import reactRefresh from 'eslint-plugin-react-refresh'
import tseslint from 'typescript-eslint'
import { defineConfig, globalIgnores } from 'eslint/config'
import eslintConfigPrettier from 'eslint-config-prettier'

export default defineConfig([
  globalIgnores(['dist']),
  {
    files: ['**/*.{ts,tsx}'],
    extends: [
      js.configs.recommended,
      tseslint.configs.recommended,
      reactHooks.configs.flat.recommended,
      reactRefresh.configs.vite,
    ],
    languageOptions: {
      ecmaVersion: 2020,
      globals: globals.browser,
    },
    rules: {
      'no-restricted-syntax': [
        'error',
        {
          selector: 'Property[key.name=/^border(TopLeft|TopRight|BottomLeft|BottomRight)?Radius$/] > Literal.value[value>1][value!=999]',
          message: 'Numeric MUI radii multiply the theme radius. Use 1 for the app standard, an explicit CSS length for a custom radius, or 999 for a pill.',
        },
        {
          selector: 'JSXAttribute[name.name=/^border(TopLeft|TopRight|BottomLeft|BottomRight)?Radius$/] > JSXExpressionContainer > Literal[value>1][value!=999]',
          message: 'Numeric MUI radii multiply the theme radius. Use 1 for the app standard, an explicit CSS length for a custom radius, or 999 for a pill.',
        },
      ],
      '@typescript-eslint/no-explicit-any': 'warn',
      '@typescript-eslint/explicit-function-return-type': 'off',
      '@typescript-eslint/explicit-module-boundary-types': 'off',
    },
  },
  eslintConfigPrettier,
])
