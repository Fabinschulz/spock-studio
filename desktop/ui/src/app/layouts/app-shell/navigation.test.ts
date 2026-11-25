import { describe, expect, it } from 'vitest';

import { executionPath, paths } from '@/shared/routing';

import { areaDestination, areaOf, AREAS, describeRunActivity } from './navigation';

describe('áreas · a que área uma tela pertence', () => {
  it.each([
    [paths.home, 'home'],
    [paths.project, 'engineering'],
    [paths.projectHarness, 'engineering'],
    [paths.projectArchitecture, 'engineering'],
    [paths.configure, 'engineering'],
    [paths.agents, 'engineering'],
    [paths.idea, 'engineering'],
    [paths.portfolio, 'management'],
    [paths.management, 'management'],
    [paths.assistant, 'management'],
    [paths.runs, 'activity'],
    [paths.audit, 'activity'],
    [executionPath('run-000000000000'), 'activity'],
    [paths.integrations, 'connections'],
    [paths.cloud, 'connections'],
    [paths.connectionsModels, 'connections'],
    [paths.settings, 'settings'],
    [paths.settingsDiagnostics, 'settings']
  ])('%s é da área %s', (pathname, area) => {
    expect(areaOf(pathname)?.id).toBe(area);
  });

  it('não confunde caminhos parecidos: só vale o caminho inteiro ou o que está abaixo dele', () => {
    expect(areaOf('/gestaoextra')).toBeNull();
    expect(areaOf('/projetos')).toBeNull();
  });

  it('cada área leva a uma tela que pertence a ela', () => {
    for (const area of AREAS) expect(areaOf(area.to)?.id).toBe(area.id);
  });

  it('sem projeto aberto a Gestão abre no Portfólio; com projeto, no Acompanhamento', () => {
    const management = AREAS.find((area) => area.id === 'management')!;

    expect(areaDestination(management, false)).toBe(paths.portfolio);
    expect(areaDestination(management, true)).toBe(paths.management);
  });

  it('nenhuma tela repete o id de outra: a busca usa o id como chave', () => {
    const ids = AREAS.flatMap((area) => area.items.flatMap((item) => [item.id, ...(item.tabs ?? []).map((t) => t.id)]));

    expect(new Set(ids).size).toBe(ids.length);
  });
});

describe('aviso de execuções', () => {
  it('prefere a aprovação pendente e some quando não há nada', () => {
    expect(describeRunActivity(null)).toBeUndefined();
    expect(describeRunActivity({ running: 0, awaitingApproval: 0 })).toBeUndefined();
    expect(describeRunActivity({ running: 2, awaitingApproval: 1 })).toEqual({
      text: '1',
      label: '1 aguardando aprovação, 2 em andamento',
      tone: 'warning'
    });
    expect(describeRunActivity({ running: 2, awaitingApproval: 0 })).toEqual({
      text: '2',
      label: '2 em andamento',
      tone: 'info'
    });
  });
});
