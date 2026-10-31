import type { GlyphName } from '@/shared/components/ui';

export interface UpcomingActivity {
  title: string;
  text: string;
  glyph: GlyphName;
}

export const UPCOMING_ACTIVITIES: readonly UpcomingActivity[] = [
  {
    title: 'Importar requisitos',
    text: 'Gera especificações e backlog a partir de documentos existentes.',
    glyph: 'file'
  },
  { title: 'Conectar ferramentas', text: 'Jira, Azure DevOps, GitHub, MCPs e bancos de dados.', glyph: 'plug' },
  { title: 'Modernizar sistema', text: 'Analisa um legado e planeja a modernização.', glyph: 'restart' }
];
