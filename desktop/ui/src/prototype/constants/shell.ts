import type { NavItem } from '@/prototype/types/shell';

export const WIDE_QUERY = '(min-width: 1180px)';

export const NAV_ITEMS: readonly NavItem[] = [
  { label: 'Início', glyph: 'home', to: '/', end: true },
  { label: 'Projeto', glyph: 'folder', to: '/projeto' },
  { label: 'Configurar', glyph: 'layers', to: '/configurar' }
];

export const NAV_SOON_ITEMS: readonly NavItem[] = [
  { label: 'Catálogo', glyph: 'file' },
  { label: 'Configurações', glyph: 'settings' }
];
