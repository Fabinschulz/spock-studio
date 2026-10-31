import js from '@eslint/js';
import prettier from 'eslint-config-prettier';
import jsxA11y from 'eslint-plugin-jsx-a11y';
import reactHooks from 'eslint-plugin-react-hooks';
import globals from 'globals';
import tseslint from 'typescript-eslint';

/** Mesma política de imports do Mariliz Web: nada de mega-barrel, entre slices só a API pública. */
const BANNED_PATHS = [
  { name: '@/shared', message: 'Não use o mega-barrel: importe @/shared/components, @/shared/utils etc.' }
];

const BANNED_IMPORTS = [
  {
    group: ['@/features/*/*', '!@/features/*/index'],
    message: 'Entre slices, importe só a API pública da feature (@/features/<slice>).'
  },
  {
    // O frontend não acessa o sistema operacional: só o motor .NET, via comandos do host Tauri.
    group: ['@tauri-apps/plugin-*'],
    message: 'A UI não usa plugins nativos; peça ao motor por um comando do host.'
  }
];

export default tseslint.config(
  { ignores: ['dist', 'node_modules'] },
  js.configs.recommended,
  ...tseslint.configs.recommended,
  {
    files: ['**/*.{ts,tsx}'],
    languageOptions: {
      ecmaVersion: 2023,
      globals: { ...globals.browser }
    },
    plugins: {
      'react-hooks': reactHooks,
      'jsx-a11y': jsxA11y
    },
    rules: {
      ...reactHooks.configs.recommended.rules,
      ...jsxA11y.flatConfigs.recommended.rules,
      // role="list" em ul/ol é intencional: o Safari/VoiceOver remove a semântica
      // de lista quando há list-style: none (o reset usa esse atributo como gancho).
      'jsx-a11y/no-redundant-roles': ['error', { ul: ['list'], ol: ['list'] }],
      '@typescript-eslint/consistent-type-imports': 'error',
      '@typescript-eslint/no-explicit-any': 'error',
      '@typescript-eslint/no-unused-vars': ['error', { argsIgnorePattern: '^_' }],
      'react-hooks/exhaustive-deps': 'error',
      'no-console': ['warn', { allow: ['warn', 'error'] }],
      'no-restricted-imports': ['error', { paths: BANNED_PATHS, patterns: BANNED_IMPORTS }]
    }
  },
  // Camadas: app → features → shared. Nada aponta para cima.
  {
    files: ['src/shared/**/*.{ts,tsx}'],
    rules: {
      'no-restricted-imports': [
        'error',
        {
          paths: BANNED_PATHS,
          patterns: [
            ...BANNED_IMPORTS,
            {
              group: ['@/features', '@/features/*', '@/app', '@/app/*'],
              message: 'shared não depende de features nem de app.'
            }
          ]
        }
      ]
    }
  },
  {
    files: ['src/features/**/*.{ts,tsx}'],
    rules: {
      'no-restricted-imports': [
        'error',
        {
          paths: BANNED_PATHS,
          patterns: [...BANNED_IMPORTS, { group: ['@/app', '@/app/*'], message: 'features não dependem de app.' }]
        }
      ],
      'no-restricted-syntax': [
        'error',
        { selector: 'ExportAllDeclaration', message: 'Barrels de feature usam exports nomeados (nada de export *).' }
      ]
    }
  },
  {
    files: ['*.config.{js,ts,cjs}'],
    languageOptions: { globals: { ...globals.node } }
  },
  prettier
);
