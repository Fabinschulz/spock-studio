import { describe, expect, it } from 'vitest';

import { executionPath, paths } from '@/shared/routing';

import { ENVIRONMENTS, environmentOf } from './environment';

describe('ambientes · a que ambiente uma tela pertence', () => {
  it.each([
    [paths.project, 'engineering'],
    [paths.configure, 'engineering'],
    [paths.engineeringRequest, 'engineering'],
    [executionPath('run-000000000000'), 'engineering'],
    [paths.portfolio, 'management'],
    [paths.management, 'management'],
    [paths.assistant, 'management']
  ])('%s é do ambiente %s', (pathname, environment) => {
    expect(environmentOf(pathname)).toBe(environment);
  });

  it.each([paths.home, paths.integrations, paths.settings])(
    '%s é comum: não pertence a nenhum ambiente',
    (pathname) => {
      expect(environmentOf(pathname)).toBeNull();
    }
  );

  it('não confunde caminhos parecidos: só vale o caminho inteiro ou o que está abaixo dele', () => {
    expect(environmentOf('/gestaoextra')).toBeNull();
    expect(environmentOf('/projetos')).toBeNull();
  });

  it('cada ambiente leva a uma tela que existe e que pertence a ele', () => {
    for (const entry of ENVIRONMENTS) expect(environmentOf(entry.to)).toBe(entry.id);
  });
});
