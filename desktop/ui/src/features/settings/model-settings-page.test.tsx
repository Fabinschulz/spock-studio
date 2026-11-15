import aiToolsFixture from '@contracts/fixtures/aitools/list.json';
import connectedFixture from '@contracts/fixtures/models/connection.json';
import multiFixture from '@contracts/fixtures/models/connection-multi.json';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it } from 'vitest';

import { EngineError, type EngineClient } from '@/shared/api';
import { AppProviders } from '@/shared/components';
import { modelConnectionSchema, type ModelConnection } from '@/shared/schema';
import { expectNoA11yViolations } from '@/test/a11y';
import { chooseOption, optionsOf } from '@/test/choose-option';

import { ModelSettingsPage } from './model-settings-page';

const testKey = 'sk-test-chave-fase-i1-7c2d41';
const checkedAt = '2026-11-10T12:00:00+00:00';

const connected = modelConnectionSchema.parse(connectedFixture);
const multi = modelConnectionSchema.parse(multiFixture);
const none: ModelConnection = {
  configured: false,
  provider: '',
  providers: connected.providers.map((provider) => ({
    ...provider,
    configured: false,
    isDefault: false,
    status: 'none',
    protection: null,
    checkedAt: null,
    lastError: null
  })),
  purposes: [],
  budgets: connected.budgets
};
const purposeTemplate = connected.purposes.map((purpose) => ({ ...purpose, isDefault: true }));

type Provider = ModelConnection['providers'][number];

function patch(state: ModelConnection, id: string, changes: Partial<Provider>): ModelConnection {
  return {
    ...state,
    providers: state.providers.map((provider) => (provider.id === id ? { ...provider, ...changes } : provider))
  };
}

// O que o motor faz depois de cada ação: o padrão é o escolhido se ainda tem chave, senão o primeiro; finalidade sem
// escolha, ou com escolha de provedor sem chave, segue o padrão.
function settle(state: ModelConnection): ModelConnection {
  const withKey = state.providers.filter((provider) => provider.configured);
  const fallback = withKey.find((provider) => provider.id === state.provider) ?? withKey[0];
  if (!fallback) return { ...state, configured: false, provider: '', purposes: [] };

  const source = state.purposes.length > 0 ? state.purposes : purposeTemplate;
  return {
    ...state,
    configured: true,
    provider: fallback.id,
    providers: state.providers.map((provider) => ({ ...provider, isDefault: provider.id === fallback.id })),
    purposes: source.map((purpose) =>
      purpose.isDefault || !withKey.some((provider) => provider.id === purpose.provider)
        ? { ...purpose, provider: fallback.id, model: fallback.model, isDefault: true }
        : purpose
    )
  };
}

type Behavior = { refuseSave?: boolean; refuseTest?: boolean; refusePurpose?: boolean; refuseBudget?: boolean };

type Fake = EngineClient & { requests: { method: string; params: Record<string, unknown> }[] };

function client(initial: ModelConnection, behavior: Behavior = {}): Fake {
  let state = initial;
  const requests: Fake['requests'] = [];
  return {
    requests,
    async request(method, params, schema) {
      requests.push({ method, params });
      const provider = String(params.provider ?? '');
      switch (method) {
        case 'model.connection':
          return schema.parse(state);
        case 'model.save':
          if (behavior.refuseSave) throw new EngineError('MODEL_AUTH_FAILED', 'O provedor recusou a chave.');
          state = settle(
            patch({ ...state, provider: state.provider || provider }, provider, {
              configured: true,
              status: 'connected',
              protection: 'vault',
              checkedAt,
              lastError: null
            })
          );
          return schema.parse({ configured: true, provider: state.provider });
        case 'model.test':
          state = patch(
            state,
            provider,
            behavior.refuseTest
              ? {
                  status: 'error',
                  checkedAt,
                  lastError: { code: 'MODEL_AUTH_FAILED', message: 'O provedor recusou a chave.', at: checkedAt }
                }
              : { status: 'connected', checkedAt, lastError: null }
          );
          return schema.parse(state);
        case 'model.remove':
          state = settle(
            patch(state, provider, {
              configured: false,
              status: 'none',
              protection: null,
              checkedAt: null,
              lastError: null
            })
          );
          return schema.parse(state);
        case 'model.default':
          state = settle({ ...state, provider });
          return schema.parse(state);
        case 'model.purpose': {
          if (behavior.refusePurpose)
            throw new EngineError(
              'MODEL_CHOICE_DENIED',
              'Esse modelo não está disponível: o provedor dele não tem chave.'
            );
          const model = typeof params.model === 'string' ? params.model : null;
          const owner = state.providers.find((entry) => entry.models.some((option) => option.id === model));
          const fallback = state.providers.find((entry) => entry.isDefault);
          state = {
            ...state,
            purposes: state.purposes.map((purpose) =>
              purpose.id !== params.purpose
                ? purpose
                : model === null || !owner || model === fallback?.model
                  ? { ...purpose, provider: fallback!.id, model: fallback!.model, isDefault: true }
                  : { ...purpose, provider: owner.id, model, isDefault: false }
            )
          };
          return schema.parse(state);
        }
        case 'model.budget': {
          if (behavior.refuseBudget)
            throw new EngineError(
              'MODEL_BUDGET_OUT_OF_RANGE',
              'O teto de Fluxos de engenharia vai de 2.000 a 64.000 tokens.'
            );
          const tokens = typeof params.tokens === 'number' ? params.tokens : null;
          state = {
            ...state,
            budgets: state.budgets.map((budget) =>
              budget.purpose !== params.purpose
                ? budget
                : { ...budget, tokens: tokens ?? budget.defaultTokens, isDefault: tokens === null }
            )
          };
          return schema.parse(state);
        }
        case 'aitool.list':
          return schema.parse(aiToolsFixture);
        default:
          throw new Error(`${method} ${JSON.stringify(params)}`);
      }
    },
    async onEvent() {
      return () => undefined;
    }
  };
}

const renderPage = (api: EngineClient) =>
  render(
    <AppProviders>
      <ModelSettingsPage client={api} />
    </AppProviders>
  );

const rowOf = async (name: string) => (await screen.findByRole('heading', { level: 3, name })).closest('li')!;
const modelCalls = (api: Fake) =>
  api.requests.map((request) => request.method).filter((method) => method.startsWith('model.'));

describe('Minha IA · provedores', () => {
  it('mostra um provedor por linha e guarda a chave sem ecoá-la na interface', async () => {
    const api = client(none);
    const { container } = renderPage(api);

    const openai = await rowOf('OpenAI');
    expect(screen.getAllByText('Sem chave neste computador.')).toHaveLength(3);
    const field = within(openai).getByLabelText('Chave de API (OpenAI)');
    await userEvent.type(field, testKey);
    await userEvent.click(within(openai).getByRole('button', { name: 'Salvar a chave de OpenAI' }));

    expect(await screen.findByText('Chave de OpenAI guardada: o provedor a aceitou.')).toBeInTheDocument();
    expect(within(openai).getByText('Conectado')).toBeInTheDocument();
    expect(within(openai).getByText('Padrão')).toBeInTheDocument();
    expect(field).toHaveValue('');
    expect(container.textContent).not.toContain(testKey);
    expect(modelCalls(api)).toEqual(['model.connection', 'model.save', 'model.connection']);
    expect(api.requests.find((request) => request.method === 'model.save')!.params).toEqual({
      provider: 'openai',
      key: testKey
    });

    await expectNoA11yViolations(container);
  });

  it('se o provedor recusa ao guardar, diz o motivo e o provedor continua sem chave', async () => {
    renderPage(client(none, { refuseSave: true }));

    const openai = await rowOf('OpenAI');
    await userEvent.type(within(openai).getByLabelText('Chave de API (OpenAI)'), testKey);
    await userEvent.click(within(openai).getByRole('button', { name: 'Salvar a chave de OpenAI' }));

    expect(await screen.findByRole('alert')).toHaveTextContent('O provedor recusou a chave.');
    expect(screen.getAllByText('Sem chave neste computador.')).toHaveLength(3);
    expect(screen.queryByText(/guardada: o provedor a aceitou/)).not.toBeInTheDocument();
  });

  it('diz que cada chave é de um provedor, que é conferida antes de ser guardada e que a atual fica se ele recusar', async () => {
    renderPage(client(connected));

    expect(await screen.findByText(/não mexe nas dos outros/)).toBeInTheDocument();
    expect(screen.getByText(/conferida com o provedor antes de ser guardada/)).toBeInTheDocument();
    expect(screen.getByText(/se ele recusar, a atual continua valendo/)).toBeInTheDocument();
  });

  it('mostra o modelo padrão do provedor, quando foi conferida e que o provedor aceitou', async () => {
    renderPage(client(connected));

    const openai = await rowOf('OpenAI');

    expect(within(openai).getByText('Conectado')).toBeInTheDocument();
    expect(within(openai).getByText(/Modelo padrão deste provedor:/)).toHaveTextContent('gpt-4o-mini');
    expect(within(openai).getByText(/Conferida .*/)).toBeInTheDocument();
  });

  it('uma chave que nunca foi conferida diz isso, sem fingir que funciona', async () => {
    renderPage(client(patch(connected, 'openai', { status: 'untested', checkedAt: null })));

    const openai = await rowOf('OpenAI');

    expect(within(openai).getByText('Ainda não conferida')).toBeInTheDocument();
    expect(within(openai).getByText(/Nunca foi conferida com o provedor/)).toBeInTheDocument();
    expect(within(openai).queryByText('Conectado')).not.toBeInTheDocument();
  });

  it('diz onde cada chave está guardada, sem rodeio', async () => {
    const vault = renderPage(client(patch(connected, 'openai', { protection: 'vault' })));
    expect(await screen.findByText('Guardada com a proteção do sistema operacional.')).toBeInTheDocument();
    vault.unmount();

    renderPage(client(patch(connected, 'openai', { protection: 'file' })));
    expect(await screen.findByText(/sem a proteção do sistema: não há cofre disponível aqui/)).toBeInTheDocument();
  });

  it('conferir um provedor confirma quando ele aceita, e só mexe no estado dele', async () => {
    const api = client(patch(multi, 'openai', { status: 'untested', checkedAt: null }));
    renderPage(api);

    await userEvent.click(await screen.findByRole('button', { name: 'Conferir OpenAI com o provedor' }));

    expect(await screen.findByText('OpenAI aceitou a chave.')).toBeInTheDocument();
    expect(within(await rowOf('OpenAI')).getByText('Conectado')).toBeInTheDocument();
    expect(within(await rowOf('Anthropic')).getByText('Com erro')).toBeInTheDocument();
    expect(api.requests.find((request) => request.method === 'model.test')!.params).toEqual({ provider: 'openai' });
  });

  it('conferir não confirma nada quando o provedor recusa: a recusa aparece no estado, não como sucesso', async () => {
    renderPage(client(connected, { refuseTest: true }));

    await userEvent.click(await screen.findByRole('button', { name: 'Conferir OpenAI com o provedor' }));

    expect(await screen.findByText('Com erro')).toBeInTheDocument();
    expect(screen.getByText('O provedor recusou a chave.')).toBeInTheDocument();
    expect(screen.queryByText('OpenAI aceitou a chave.')).not.toBeInTheDocument();
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });

  it('só oferece conferir, tornar padrão e remover onde faz sentido', async () => {
    renderPage(client(connected));

    await screen.findByRole('heading', { name: 'OpenAI' });

    expect(screen.getByRole('button', { name: 'Conferir OpenAI com o provedor' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Remover a chave de OpenAI' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Tornar OpenAI o provedor padrão' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /Conferir Anthropic/ })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /Remover a chave de Google/ })).not.toBeInTheDocument();
  });

  it('guardar a chave de um segundo provedor não troca o padrão e mantém o primeiro conectado', async () => {
    renderPage(client(connected));

    const anthropic = await rowOf('Anthropic');
    await userEvent.type(within(anthropic).getByLabelText('Chave de API (Anthropic)'), testKey);
    await userEvent.click(within(anthropic).getByRole('button', { name: 'Salvar a chave de Anthropic' }));

    expect(await screen.findByText('Chave de Anthropic guardada: o provedor a aceitou.')).toBeInTheDocument();
    expect(within(await rowOf('Anthropic')).getByText('Conectado')).toBeInTheDocument();
    expect(within(await rowOf('OpenAI')).getByText('Conectado')).toBeInTheDocument();
    expect(screen.getAllByText('Padrão')).toHaveLength(1);
    expect(within(await rowOf('OpenAI')).getByText('Padrão')).toBeInTheDocument();
  });

  it('com chave, o botão de guardar vira "Trocar chave"', async () => {
    renderPage(client(connected));

    const openai = await rowOf('OpenAI');

    expect(within(openai).getByRole('button', { name: 'Trocar a chave de OpenAI' })).toHaveTextContent('Trocar chave');
    expect(
      within(await rowOf('Anthropic')).getByRole('button', { name: 'Salvar a chave de Anthropic' })
    ).toHaveTextContent('Salvar chave');
  });

  it('tornar um provedor o padrão passa a marca para ele', async () => {
    const api = client(multi);
    renderPage(api);

    await userEvent.click(await screen.findByRole('button', { name: 'Tornar Anthropic o provedor padrão' }));

    expect(await screen.findByText('Anthropic é o provedor padrão.')).toBeInTheDocument();
    expect(within(await rowOf('Anthropic')).getByText('Padrão')).toBeInTheDocument();
    expect(screen.getAllByText('Padrão')).toHaveLength(1);
    expect(api.requests.find((request) => request.method === 'model.default')!.params).toEqual({
      provider: 'anthropic'
    });
  });

  it('remover um provedor tira só a chave dele', async () => {
    const api = client(multi);
    renderPage(api);

    await userEvent.click(await screen.findByRole('button', { name: 'Remover a chave de Anthropic' }));
    const confirm = screen.getByRole('group', { name: 'Remover a chave de Anthropic' });
    expect(confirm).toHaveTextContent('para de funcionar até você salvar outra chave');
    expect(api.requests.some((request) => request.method === 'model.remove')).toBe(false);
    await userEvent.click(within(confirm).getByRole('button', { name: 'Remover chave' }));

    expect(await screen.findByText('Chave de Anthropic removida deste computador.')).toBeInTheDocument();
    expect(within(await rowOf('Anthropic')).getByText('Sem chave')).toBeInTheDocument();
    expect(within(await rowOf('OpenAI')).getByText('Conectado')).toBeInTheDocument();
    expect(api.requests.find((request) => request.method === 'model.remove')!.params).toEqual({
      provider: 'anthropic'
    });
  });

  it('remover o último provedor deixa a tela sem chave e some com as finalidades', async () => {
    renderPage(client(connected));

    await userEvent.click(await screen.findByRole('button', { name: 'Remover a chave de OpenAI' }));
    await userEvent.click(screen.getByRole('button', { name: 'Remover chave' }));

    await waitFor(() => expect(screen.getAllByText('Sem chave neste computador.')).toHaveLength(3));
    expect(screen.queryByText('Modelo por finalidade')).not.toBeInTheDocument();
  });
});

describe('Minha IA · modelo por finalidade', () => {
  const selectOf = async (label: string) => await screen.findByRole('combobox', { name: label });

  it('mostra uma linha por finalidade, com modelos de todo provedor com chave e o padrão marcado', async () => {
    renderPage(client(multi));

    const judge = await selectOf('Juiz');

    expect(screen.getByText('Modelo por finalidade')).toBeInTheDocument();
    expect(await optionsOf(judge)).toEqual([
      'GPT-4o mini (OpenAI) (padrão)',
      'GPT-4o (OpenAI)',
      'Claude Haiku 5.5 (Anthropic)',
      'Claude Sonnet 5.5 (Anthropic)',
      'Claude Opus 5.5 (Anthropic)'
    ]);
    expect(judge).toHaveTextContent('Claude Opus 5.5 (Anthropic)');
    expect(await selectOf('Assistente gerencial')).toHaveTextContent('GPT-4o mini (OpenAI) (padrão)');
    expect(screen.getByText(/Dá uma segunda opinião sobre o resultado de uma execução/)).toBeInTheDocument();
  });

  it('escolher o modelo de outro provedor manda a finalidade e o modelo, avisa e mostra a escolha', async () => {
    const api = client(multi);
    renderPage(api);

    await chooseOption(await selectOf('Assistente gerencial'), /^Claude Sonnet 5.5 \(Anthropic\)$/);

    expect(await screen.findByText('Modelo de assistente gerencial atualizado.')).toBeInTheDocument();
    expect(api.requests.find((request) => request.method === 'model.purpose')!.params).toEqual({
      purpose: 'assistant',
      model: 'claude-sonnet-5-5'
    });
    expect(await selectOf('Assistente gerencial')).toHaveTextContent(/^Claude Sonnet 5.5 \(Anthropic\)$/);
    expect(await selectOf('Fluxos de engenharia')).toHaveTextContent('GPT-4o mini (OpenAI) (padrão)');
  });

  it('escolher o padrão volta a finalidade ao padrão sem mandar modelo', async () => {
    const api = client(multi);
    renderPage(api);

    await chooseOption(await selectOf('Juiz'), 'GPT-4o mini (OpenAI) (padrão)');

    await waitFor(() => expect(api.requests.some((request) => request.method === 'model.purpose')).toBe(true));
    expect(api.requests.find((request) => request.method === 'model.purpose')!.params).toEqual({ purpose: 'judge' });
    expect(await selectOf('Juiz')).toHaveTextContent('GPT-4o mini (OpenAI) (padrão)');
  });

  it('diz que a escolha é da pessoa e onde fica o teto de tokens', async () => {
    renderPage(client(connected));

    expect(await screen.findByText(/nenhum agente escolhe o modelo: a escolha é sua/)).toBeInTheDocument();
    expect(screen.getByText(/O teto de tokens de cada execução fica na seção seguinte/)).toBeInTheDocument();
  });

  it('sem chave a seção não aparece', async () => {
    renderPage(client(none));

    await screen.findByRole('heading', { name: 'OpenAI' });

    expect(screen.queryByText('Modelo por finalidade')).not.toBeInTheDocument();
  });

  it('se o motor recusa a escolha, mostra o motivo e mantém o modelo de antes', async () => {
    renderPage(client(multi, { refusePurpose: true }));

    await chooseOption(await selectOf('Assistente gerencial'), /^GPT-4o \(OpenAI\)$/);

    expect(await screen.findByRole('alert')).toHaveTextContent('Esse modelo não está disponível');
    expect(await selectOf('Assistente gerencial')).toHaveTextContent('GPT-4o mini (OpenAI) (padrão)');
  });

  it('conectar outro provedor acrescenta os modelos dele às opções', async () => {
    renderPage(client(connected));
    expect(await optionsOf(await selectOf('Juiz'))).toEqual(['GPT-4o mini (OpenAI) (padrão)', 'GPT-4o (OpenAI)']);

    const anthropic = await rowOf('Anthropic');
    await userEvent.type(within(anthropic).getByLabelText('Chave de API (Anthropic)'), testKey);
    await userEvent.click(within(anthropic).getByRole('button', { name: 'Salvar a chave de Anthropic' }));
    await screen.findByText('Chave de Anthropic guardada: o provedor a aceitou.');

    expect(await optionsOf(await selectOf('Juiz'))).toContain('Claude Opus 5.5 (Anthropic)');
  });

  it('remover o provedor de um modelo escolhido faz a finalidade voltar ao padrão', async () => {
    renderPage(client(multi));
    expect(await selectOf('Juiz')).toHaveTextContent('Claude Opus 5.5 (Anthropic)');

    await userEvent.click(await screen.findByRole('button', { name: 'Remover a chave de Anthropic' }));
    await userEvent.click(screen.getByRole('button', { name: 'Remover chave' }));

    await screen.findByText('Chave de Anthropic removida deste computador.');
    expect(await selectOf('Juiz')).toHaveTextContent('GPT-4o mini (OpenAI) (padrão)');
    expect(await optionsOf(await selectOf('Juiz'))).not.toContain('Claude Opus 5.5 (Anthropic)');
  });

  it('não tem violações estruturais de acessibilidade com vários provedores', async () => {
    const { container } = renderPage(client(multi));
    await selectOf('Juiz');

    await expectNoA11yViolations(container);
  });
});

describe('Minha IA · teto de tokens por finalidade', () => {
  const fieldOf = async (label: string) => screen.findByLabelText(`${label} (tokens por execução)`);

  it('aparece mesmo sem chave, com o teto de hoje, a faixa e o padrão', async () => {
    renderPage(client(none));

    expect(await screen.findByRole('heading', { name: 'Teto de tokens por finalidade' })).toBeInTheDocument();
    expect(await fieldOf('Fluxos de engenharia')).toHaveValue('8000');
    expect(screen.getByText('De 2.000 a 64.000 tokens. Padrão: 8.000.')).toBeInTheDocument();
    expect(await fieldOf('Investigação do assistente')).toHaveValue('30000');
    expect(screen.getByText(/segunda opinião do juiz/)).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /Voltar o teto/ })).not.toBeInTheDocument();
  });

  it('salvar manda a finalidade e o número, avisa e oferece voltar ao padrão', async () => {
    const api = client(connected);
    renderPage(api);

    const field = await fieldOf('Fluxos de engenharia');
    await userEvent.clear(field);
    await userEvent.type(field, '16000');
    await userEvent.click(screen.getByRole('button', { name: 'Salvar o teto de Fluxos de engenharia' }));

    expect(await screen.findByText('Teto de fluxos de engenharia: 16.000 tokens por execução.')).toBeInTheDocument();
    expect(api.requests.at(-1)).toEqual({ method: 'model.budget', params: { purpose: 'workflow', tokens: 16000 } });
    expect(await fieldOf('Fluxos de engenharia')).toHaveValue('16000');

    await userEvent.click(screen.getByRole('button', { name: 'Voltar o teto de Fluxos de engenharia ao padrão' }));

    expect(await screen.findByText('Teto de fluxos de engenharia: 8.000 tokens por execução.')).toBeInTheDocument();
    expect(api.requests.at(-1)).toEqual({ method: 'model.budget', params: { purpose: 'workflow', tokens: null } });
  });

  it('fora da faixa ou sem ser número, explica e não deixa salvar', async () => {
    const api = client(connected);
    renderPage(api);

    const field = await fieldOf('Investigação do assistente');
    for (const value of ['4999', '120001', '20.000', 'muito']) {
      await userEvent.clear(field);
      await userEvent.type(field, value);
      expect(screen.getByText('Use um número inteiro. De 5.000 a 120.000 tokens. Padrão: 30.000.')).toBeInTheDocument();
      expect(screen.getByRole('button', { name: 'Salvar o teto de Investigação do assistente' })).toBeDisabled();
    }
    expect(modelCalls(api)).not.toContain('model.budget');
  });

  it('se o motor recusa, mostra o motivo e o teto continua o de antes', async () => {
    renderPage(client(connected, { refuseBudget: true }));

    const field = await fieldOf('Fluxos de engenharia');
    await userEvent.clear(field);
    await userEvent.type(field, '9000');
    await userEvent.click(screen.getByRole('button', { name: 'Salvar o teto de Fluxos de engenharia' }));

    expect(await screen.findByRole('alert')).toHaveTextContent('vai de 2.000 a 64.000 tokens');
    expect(screen.queryByRole('button', { name: /Voltar o teto/ })).not.toBeInTheDocument();
  });

  it('não tem violações estruturais de acessibilidade', async () => {
    const { container } = renderPage(client(connected));
    await fieldOf('Fluxos de engenharia');

    await expectNoA11yViolations(container);
  });
});
