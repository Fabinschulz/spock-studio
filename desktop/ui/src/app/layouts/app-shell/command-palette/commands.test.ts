import { describe, expect, it } from 'vitest';

import type { ActivityFeed } from '@/features/runs';

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

  describe('execuções', () => {
    const item = (runId: string, state: string) =>
      ({
        runId,
        projectId: 'p-1',
        projectName: 'loja-api',
        kind: 'validate',
        state,
        startedAt: '2026-11-20T10:00:00Z',
        interrupted: false
      }) as ActivityFeed['tasks'][number];

    it('cada tarefa e execução recente vira um comando que abre a execução, a tarefa primeiro', () => {
      const feed: ActivityFeed = {
        tasks: [item('run-t', 'awaiting_approval')],
        recent: [item('run-r', 'completed')]
      };

      const runs = buildCommands([], feed).filter((command) => command.group === 'Atividade');

      expect(runs.map((command) => command.id)).toEqual(['run:run-t', 'run:run-r']);
      expect(runs[0]).toMatchObject({
        label: 'Validação · loja-api',
        hint: 'Aguardando aprovação',
        action: { kind: 'go', to: '/execucao/run-t' }
      });
    });

    it('sem texto a lista não se enche: há um teto de tarefas e de execuções recentes', () => {
      const many = (prefix: string, state: string) =>
        Array.from({ length: 12 }, (_, index) => item(`${prefix}-${index}`, state));

      const runs = buildCommands([], { tasks: many('t', 'running'), recent: many('r', 'completed') }).filter(
        (command) => command.group === 'Atividade'
      );

      expect(runs).toHaveLength(10);
    });

    it('sem a lista do motor, não há grupo de atividade', () => {
      expect(buildCommands([]).some((command) => command.group === 'Atividade')).toBe(false);
    });
  });

  it('nenhum comando repete o id de outro', () => {
    const ids = buildCommands([recent('a')]).map((command) => command.id);

    expect(new Set(ids).size).toBe(ids.length);
  });
});
