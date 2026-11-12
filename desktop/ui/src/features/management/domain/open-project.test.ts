import { describe, expect, it } from 'vitest';

import type { RecentProject, SelectedProject } from '@/features/project-selection';

import { resolveOpenAction } from './open-project';

const loja = { projectId: 'p-1', displayName: 'loja', displayPath: '~\\code\\loja' };

function recent(overrides: Partial<RecentProject> = {}): RecentProject {
  return {
    id: 'r-1',
    displayName: 'loja',
    displayPath: '~\\code\\loja',
    lastOpenedMs: 1,
    available: true,
    ...overrides
  };
}

describe('Gestão · abrir um projeto pelo portfólio', () => {
  it('o projeto aberto agora é reconhecido pelo id do motor, sem olhar os recentes', () => {
    const selected: SelectedProject = { projectId: 'p-1', displayName: 'outro nome', displayPath: '~' };

    expect(resolveOpenAction(loja, selected, [])).toEqual({ kind: 'current' });
  });

  it('um recente com o mesmo nome e caminho abreviado abre o projeto', () => {
    expect(resolveOpenAction(loja, null, [recent({ id: 'r-9' })])).toEqual({ kind: 'recent', recentId: 'r-9' });
  });

  it('sem recente correspondente, não há ação: o portfólio lista mais projetos do que os seis recentes', () => {
    expect(resolveOpenAction(loja, null, [recent({ displayName: 'portal' })])).toBeNull();
    expect(resolveOpenAction(loja, null, [recent({ displayPath: '~\\outra\\loja' })])).toBeNull();
  });

  it('na dúvida entre dois recentes, não abre nenhum para não abrir o projeto errado', () => {
    expect(resolveOpenAction(loja, null, [recent({ id: 'r-1' }), recent({ id: 'r-2' })])).toBeNull();
  });

  it('uma pasta que não está mais acessível não oferece abrir', () => {
    expect(resolveOpenAction(loja, null, [recent({ available: false })])).toBeNull();
  });
});
