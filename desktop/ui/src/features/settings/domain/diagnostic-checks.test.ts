import aiToolsFixture from '@contracts/fixtures/aitools/list.json';
import connectedFixture from '@contracts/fixtures/models/connection.json';
import { describe, expect, it } from 'vitest';

import { EngineError } from '@/shared/api';
import type { EngineReport, Resource } from '@/shared/hooks';
import { paths } from '@/shared/routing';
import { aiToolListSchema, modelConnectionSchema, type AiTool, type ModelConnection } from '@/shared/schema';

import { buildChecks, formatChecks, type ChecksInput, type DiagnosticCheck } from './diagnostic-checks';

const connected = modelConnectionSchema.parse(connectedFixture);
const tools = aiToolListSchema.parse(aiToolsFixture).tools;
const ready = <T>(value: T): Resource<T> => ({ status: 'ready', value });
const failed = <T>(error: unknown): Resource<T> => ({ status: 'error', error });

type Provider = ModelConnection['providers'][number];
const withOpenai = (changes: Partial<Provider>): ModelConnection => ({
  ...connected,
  providers: connected.providers.map((provider) => (provider.id === 'openai' ? { ...provider, ...changes } : provider))
});
const none: ModelConnection = {
  ...connected,
  configured: false,
  providers: connected.providers.map((provider) => ({
    ...provider,
    configured: false,
    isDefault: false,
    status: 'none'
  }))
};

type Changes = Partial<Omit<ChecksInput, 'engine'>> & { engine?: Partial<ChecksInput['engine']> };

function input(changes: Changes = {}): ChecksInput {
  const engine: EngineReport & { hosted: boolean } = { hosted: true, status: 'ready', aiRuntime: 'stopped' };
  return {
    connection: ready(withOpenai({ protection: 'vault' })),
    tools: ready<AiTool[]>(tools),
    ...changes,
    engine: { ...engine, ...changes.engine }
  };
}

const check = (checks: DiagnosticCheck[], id: DiagnosticCheck['id']) => checks.find((entry) => entry.id === id);

describe('buildChecks', () => {
  it('com tudo certo, a inteligência local parada é o normal e não um problema', () => {
    const checks = buildChecks(input());

    expect(checks.map((entry) => [entry.id, entry.state])).toEqual([
      ['engine', 'ok'],
      ['localIntelligence', 'normal'],
      ['model', 'ok'],
      ['keyStorage', 'ok'],
      ['aiTools', 'ok']
    ]);
    expect(check(checks, 'localIntelligence')?.summary).toBe('Parada. Ela liga no primeiro pedido que precisa dela.');
    expect(check(checks, 'model')?.summary).toBe('OpenAI conectado, com o modelo gpt-4o-mini.');
    expect(check(checks, 'aiTools')?.summary).toMatch(/^\d de 4 encontradas: Claude Code/);
  });

  it('motor parado é o problema; o que depende dele espera, em vez de virar outro problema', () => {
    const down = new EngineError('ENGINE_NOT_RUNNING', 'O motor não está em execução.');
    const checks = buildChecks(
      input({ engine: { status: 'crashed', aiRuntime: 'unknown' }, connection: failed(down), tools: failed(down) })
    );

    expect(check(checks, 'engine')).toMatchObject({
      state: 'problem',
      summary: 'Parou e não voltou sozinho. Feche e abra o aplicativo.'
    });
    for (const id of ['localIntelligence', 'model', 'aiTools'] as const)
      expect(check(checks, id)).toMatchObject({ state: 'waiting', summary: 'Depende do motor, que está parado.' });
    expect(check(checks, 'keyStorage')).toBeUndefined();
  });

  it('inteligência local indisponível diz o que deixa de funcionar e o que fazer', () => {
    const checks = buildChecks(input({ engine: { aiRuntime: 'unavailable' } }));

    expect(check(checks, 'localIntelligence')).toMatchObject({ state: 'problem' });
    expect(check(checks, 'localIntelligence')?.summary).toMatch(/fluxos de engenharia não rodam\. Copie/);
  });

  it('sem modelo é opcional e leva a conectar; sem chave não há verificação de onde ela fica', () => {
    const checks = buildChecks(input({ connection: ready(none) }));

    expect(check(checks, 'model')).toMatchObject({
      state: 'normal',
      action: { label: 'Conectar um modelo', to: paths.connectionsModels }
    });
    expect(check(checks, 'keyStorage')).toBeUndefined();
  });

  it('chave recusada pelo provedor padrão é problema, com o próximo passo e o caminho para corrigir', () => {
    const checks = buildChecks(
      input({
        connection: ready(
          withOpenai({
            status: 'error',
            lastError: { code: 'MODEL_AUTH_FAILED', message: 'O provedor recusou a chave.', at: '2026-11-10T12:00:00Z' }
          })
        )
      })
    );

    expect(check(checks, 'model')).toMatchObject({ state: 'problem', action: { to: paths.connectionsModels } });
    expect(check(checks, 'model')?.summary).toBe(
      'OpenAI, o provedor padrão: O provedor recusou a chave. Confira se copiou a chave inteira, sem espaços. Se ela foi apagada ou venceu, crie outra no site do provedor.'
    );
  });

  it('chave nunca conferida pede atenção', () => {
    const checks = buildChecks(input({ connection: ready(withOpenai({ status: 'untested', checkedAt: null })) }));

    expect(check(checks, 'model')).toMatchObject({ state: 'attention' });
  });

  it('chave em arquivo, sem cofre, pede atenção e aponta Conexões › Serviços', () => {
    const checks = buildChecks(input({ connection: ready(withOpenai({ protection: 'file' })) }));

    expect(check(checks, 'keyStorage')).toMatchObject({
      state: 'attention',
      action: { label: 'Abrir Conexões › Serviços', to: paths.integrations }
    });
  });

  it('nenhuma ferramenta de IA encontrada é normal', () => {
    const checks = buildChecks(input({ tools: ready(tools.map((tool) => ({ ...tool, installed: false }))) }));

    expect(check(checks, 'aiTools')).toMatchObject({ state: 'normal' });
  });

  it('fora do aplicativo não finge que há motor', () => {
    const checks = buildChecks(
      input({ engine: { hosted: false, status: 'unknown', aiRuntime: 'unknown' }, connection: failed(new Error('x')) })
    );

    expect(check(checks, 'engine')).toMatchObject({ state: 'normal', summary: 'Indisponível na prévia do navegador.' });
    expect(check(checks, 'model')).toMatchObject({ summary: 'Indisponível na prévia do navegador.' });
  });
});

describe('formatChecks', () => {
  it('uma linha por verificação, com o nome, o estado e o que ele quer dizer', () => {
    expect(formatChecks([{ id: 'engine', title: 'Motor', state: 'problem', summary: 'Parou.' }])).toBe(
      'Motor: Problema. Parou.'
    );
  });
});
