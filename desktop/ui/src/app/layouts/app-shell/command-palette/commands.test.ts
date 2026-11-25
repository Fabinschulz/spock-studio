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
  it('cada tela do menu vira um comando que leva a ela, com a área como dica e o nome técnico só para a busca', () => {
    const commands = buildCommands([]);

    expect(commands).toContainEqual({
      id: 'screen:harness',
      group: 'Telas',
      label: 'Padrões do projeto',
      hint: 'Engenharia',
      description: 'Regras, verificações e assistentes de IA aplicados ao projeto.',
      keywords: 'Harness',
      action: { kind: 'go', to: '/projeto/harness' }
    });
    expect(commands.find((command) => command.id === 'screen:settings-ai')?.hint).toBe('Configurações');
    expect(commands.find((command) => command.id === 'screen:home')?.hint).toBe('Aplicativo');
  });

  it('todas as áreas aparecem juntas: a paleta leva a qualquer tela, não só às da área aberta', () => {
    const labels = buildCommands([]).map((command) => command.label);

    expect(labels).toEqual(expect.arrayContaining(['Padrões do projeto', 'Portfólio', 'Execuções', 'Serviços']));
  });

  it('as abas e as telas fora do menu também são achadas', () => {
    const byId = new Map(buildCommands([]).map((command) => [command.id, command]));

    expect(byId.get('screen:catalog')?.hint).toBe('Engenharia, Padrões do projeto');
    expect(byId.get('screen:architecture')?.action).toEqual({ kind: 'go', to: '/projeto/arquitetura' });
    expect(byId.get('screen:idea')?.action).toEqual({ kind: 'go', to: '/ideia' });
  });

  it('as ações do aplicativo vêm antes das telas', () => {
    const commands = buildCommands([]);

    expect(commands[0]).toMatchObject({ group: 'Ações', action: { kind: 'pick-project' } });
    expect(commands.filter((command) => command.group === 'Ações').map((command) => command.action)).toEqual(
      expect.arrayContaining([
        { kind: 'shell', action: 'activity' },
        { kind: 'shell', action: 'terminal' },
        { kind: 'shell', action: 'shortcuts' }
      ])
    );
  });

  it('projetos recentes viram comandos de abrir, e o caminho só serve à busca', () => {
    const commands = buildCommands([recent('a')]);

    expect(commands.at(-1)).toEqual({
      id: 'project:a',
      group: 'Projetos',
      label: 'projeto-a',
      hint: 'Abrir projeto',
      keywords: '~\\a',
      action: { kind: 'open-project', recentId: 'a' }
    });
  });

  it('um projeto cuja pasta não existe mais não é oferecido', () => {
    expect(buildCommands([recent('a', false)]).some((command) => command.id === 'project:a')).toBe(false);
  });

  it('nenhum comando repete o id de outro', () => {
    const ids = buildCommands([recent('a')]).map((command) => command.id);

    expect(new Set(ids).size).toBe(ids.length);
  });
});
