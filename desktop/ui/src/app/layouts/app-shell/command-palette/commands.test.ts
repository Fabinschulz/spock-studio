import { describe, expect, it } from 'vitest';

import { buildCommands } from './commands';

const recent = (id: string, available = true) => ({
  id,
  displayName: `projeto-${id}`,
  displayPath: `~\\${id}`,
  lastOpenedMs: 1,
  available
});

describe('buildCommands', () => {
  it('cada tela do menu vira um comando que leva a ela, com o grupo como dica', () => {
    const commands = buildCommands([]);

    expect(commands).toContainEqual({
      id: 'screen:harness',
      label: 'Harness',
      hint: 'Engenharia',
      keywords: '',
      action: { kind: 'go', to: '/projeto/harness' }
    });
    expect(commands.find((command) => command.id === 'screen:settings-ai')?.hint).toBe('Configurações');
    expect(commands.find((command) => command.id === 'screen:home')?.hint).toBe('Aplicativo');
  });

  it('os dois ambientes aparecem juntos: a paleta leva a qualquer tela, não só às do ambiente atual', () => {
    const labels = buildCommands([]).map((command) => command.label);

    expect(labels).toEqual(expect.arrayContaining(['Harness', 'Portfólio', 'Execuções']));
  });

  it('projetos recentes viram comandos de abrir, e o caminho só serve à busca', () => {
    const commands = buildCommands([recent('a')]);

    expect(commands.at(-1)).toEqual({
      id: 'project:a',
      label: 'projeto-a',
      hint: 'Abrir projeto',
      keywords: '~\\a',
      action: { kind: 'open-project', recentId: 'a' }
    });
  });

  it('um projeto cuja pasta não existe mais não é oferecido', () => {
    expect(buildCommands([recent('a', false)]).some((command) => command.id === 'project:a')).toBe(false);
  });
});
