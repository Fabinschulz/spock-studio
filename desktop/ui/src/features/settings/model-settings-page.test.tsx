import aiToolsFixture from '@contracts/fixtures/aitools/list.json';
import connectedFixture from '@contracts/fixtures/models/connection.json';
import failedFixture from '@contracts/fixtures/models/connection-error.json';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it } from 'vitest';

import { EngineError, type EngineClient } from '@/shared/api';
import { AppProviders } from '@/shared/components';
import { modelConnectionSchema, type ModelConnection } from '@/shared/schema';
import { expectNoA11yViolations } from '@/test/a11y';
import { chooseOption, optionsOf } from '@/test/choose-option';

import { ModelSettingsPage } from './model-settings-page';

const testKey = 'sk-test-chave-fase-f-7c2d41';

const connected = modelConnectionSchema.parse(connectedFixture);
const failed = modelConnectionSchema.parse(failedFixture);
const none: ModelConnection = {
  configured: false,
  provider: '',
  providerName: '',
  model: '',
  status: 'none',
  protection: null,
  checkedAt: null,
  lastError: null,
  available: connected.available,
  purposes: []
};

type Behavior = {
  refuseSave?: boolean;
  /** Sem isto, o provedor aceita. */
  testResult?: ModelConnection;
  refusePurpose?: boolean;
};

function client(
  initial: ModelConnection,
  behavior: Behavior = {}
): EngineClient & {
  calls: string[];
  saved: { provider: string; key: string }[];
  purposeCalls: Record<string, unknown>[];
} {
  const calls: string[] = [];
  const saved: { provider: string; key: string }[] = [];
  const purposeCalls: Record<string, unknown>[] = [];
  let state = initial;
  return {
    calls,
    saved,
    purposeCalls,
    async request(method, params, schema) {
      calls.push(method);
      if (method === 'model.connection') return schema.parse(state);
      if (method === 'model.save') {
        if (behavior.refuseSave) throw new EngineError('MODEL_AUTH_FAILED', 'O provedor recusou a chave.');
        const { provider, key } = params as { provider: string; key: string };
        saved.push({ provider, key });
        const chosen = connected.available.find((entry) => entry.id === provider)!;
        // Trocar de provedor volta cada finalidade ao padrão do novo.
        state = {
          ...connected,
          provider,
          providerName: chosen.name,
          model: chosen.model,
          purposes: connected.purposes.map((entry) => ({ ...entry, model: chosen.model, isDefault: true }))
        };
        return schema.parse({ configured: true, provider: 'openai' });
      }
      if (method === 'model.purpose') {
        if (behavior.refusePurpose)
          throw new EngineError('MODEL_CHOICE_DENIED', 'Esse modelo não está disponível neste provedor.');
        const { purpose, model } = params as { purpose: string; model?: string };
        purposeCalls.push(params as Record<string, unknown>);
        state = {
          ...state,
          purposes: state.purposes.map((entry) =>
            entry.id === purpose
              ? { ...entry, model: model ?? state.model, isDefault: model === undefined || model === state.model }
              : entry
          )
        };
        return schema.parse(state);
      }
      if (method === 'model.test') {
        state = behavior.testResult ?? { ...state, status: 'connected', lastError: null };
        return schema.parse(state);
      }
      if (method === 'model.clear') {
        state = none;
        return schema.parse({ configured: false, provider: '' });
      }
      if (method === 'aitool.list') return schema.parse(aiToolsFixture);
      throw new Error(`${method} ${JSON.stringify(params)}`);
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

describe('Configurações · provedor de IA', () => {
  it('mostra o estado e guarda sem ecoar a chave na interface', async () => {
    const api = client(none);
    const { container } = renderPage(api);

    await waitFor(() => expect(screen.getByText('Nenhuma chave configurada.')).toBeInTheDocument());

    const field = screen.getByLabelText('Chave de API');
    await userEvent.type(field, testKey);
    await userEvent.click(screen.getByRole('button', { name: 'Salvar chave' }));

    await waitFor(() => expect(screen.getByText(/Provedor configurado: OpenAI/)).toBeInTheDocument());
    expect(screen.getByText('Conectado')).toBeInTheDocument();
    expect(screen.getByText('Chave guardada: o provedor a aceitou.')).toBeInTheDocument();
    expect(field).toHaveValue('');
    expect(container.textContent).not.toContain(testKey);
    expect(api.calls.filter((call) => call.startsWith('model.'))).toEqual([
      'model.connection',
      'model.save',
      'model.connection'
    ]);
    expect(api.saved).toEqual([{ provider: 'openai', key: testKey }]);

    await expectNoA11yViolations(container);
  });

  it('se o provedor recusa ao guardar, diz o motivo e continua sem chave', async () => {
    const api = client(none, { refuseSave: true });
    renderPage(api);

    await userEvent.type(await screen.findByLabelText('Chave de API'), testKey);
    await userEvent.click(screen.getByRole('button', { name: 'Salvar chave' }));

    expect(await screen.findByRole('alert')).toHaveTextContent('O provedor recusou a chave.');
    expect(screen.getByText('Nenhuma chave configurada.')).toBeInTheDocument();
    expect(screen.queryByText('Chave guardada: o provedor a aceitou.')).not.toBeInTheDocument();
  });

  it('diz que a chave é conferida antes de ser guardada e que a atual continua valendo se o provedor recusar', async () => {
    renderPage(client(connected));

    expect(await screen.findByText(/conferida com o provedor antes de ser guardada/)).toBeInTheDocument();
    expect(screen.getByText(/se ele recusar, a atual continua valendo/)).toBeInTheDocument();
  });

  it('mostra o modelo, quando foi conferida e que o provedor aceitou', async () => {
    renderPage(client(connected));

    expect(await screen.findByText('Conectado')).toBeInTheDocument();
    expect(screen.getByText(/Modelo das conversas dos agentes:/)).toHaveTextContent('gpt-4o-mini');
    expect(screen.getByText(/Conferida .*/)).toBeInTheDocument();
  });

  it('uma chave que nunca foi conferida diz isso, sem fingir que funciona', async () => {
    renderPage(client({ ...connected, status: 'untested', checkedAt: null }));

    expect(await screen.findByText('Ainda não conferida')).toBeInTheDocument();
    expect(screen.getByText(/Nunca foi conferida com o provedor/)).toBeInTheDocument();
    expect(screen.queryByText('Conectado')).not.toBeInTheDocument();
  });

  it('diz onde a chave está guardada, sem rodeio', async () => {
    const vault = renderPage(client({ ...connected, protection: 'vault' }));
    expect(await screen.findByText('Guardada com a proteção do sistema operacional.')).toBeInTheDocument();
    vault.unmount();

    renderPage(client({ ...connected, protection: 'file' }));
    expect(await screen.findByText(/sem a proteção do sistema: não há cofre disponível aqui/)).toBeInTheDocument();
  });

  it('conferir com o provedor confirma quando ele aceita', async () => {
    const api = client({ ...connected, status: 'untested', checkedAt: null });
    renderPage(api);

    await userEvent.click(await screen.findByRole('button', { name: 'Conferir com o provedor' }));

    expect(await screen.findByText('O provedor aceitou a chave.')).toBeInTheDocument();
    expect(screen.getByText('Conectado')).toBeInTheDocument();
    expect(api.calls).toContain('model.test');
  });

  it('conferir não confirma nada quando o provedor recusa: a recusa aparece no estado, não como sucesso', async () => {
    renderPage(client(connected, { testResult: failed }));

    await userEvent.click(await screen.findByRole('button', { name: 'Conferir com o provedor' }));

    expect(await screen.findByText('Com erro')).toBeInTheDocument();
    expect(screen.getByText('O provedor recusou a chave.')).toBeInTheDocument();
    expect(screen.queryByText('O provedor aceitou a chave.')).not.toBeInTheDocument();
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });

  it('só oferece conferir e remover quando há chave', async () => {
    renderPage(client(none));

    await screen.findByText('Nenhuma chave configurada.');

    expect(screen.queryByRole('button', { name: 'Conferir com o provedor' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Remover chave' })).not.toBeInTheDocument();
  });

  it('remove a chave configurada', async () => {
    const api = client(connected);
    renderPage(api);

    await userEvent.click(await screen.findByRole('button', { name: 'Remover chave' }));
    const confirm = screen.getByRole('group', { name: 'Remover a chave' });
    expect(confirm).toHaveTextContent('o Assistente, a pergunta ao portfólio');
    expect(api.calls).not.toContain('model.clear');
    await userEvent.click(within(confirm).getByRole('button', { name: 'Remover chave' }));

    await waitFor(() => expect(screen.getByText('Nenhuma chave configurada.')).toBeInTheDocument());
    expect(screen.getByText('Chave removida deste computador.')).toBeInTheDocument();
    expect(api.calls).toContain('model.clear');
  });

  it('oferece os provedores do motor, começa pelo primeiro sem chave e diz o modelo de cada um', async () => {
    renderPage(client(none));

    const select = await screen.findByRole('combobox', { name: 'Provedor' });

    expect(await optionsOf(select)).toEqual(['OpenAI', 'Anthropic']);
    expect(select).toHaveTextContent('OpenAI');
    expect(screen.getByText(/Com OpenAI, as conversas dos agentes usam o modelo/)).toBeInTheDocument();
    await chooseOption(select, 'Anthropic');
    expect(screen.getByText(/Com Anthropic, as conversas dos agentes usam o modelo/)).toBeInTheDocument();
    expect(screen.getByText('claude-haiku-5-5')).toBeInTheDocument();
  });

  it('guarda a chave no provedor escolhido e passa a mostrar esse provedor', async () => {
    const api = client(none);
    renderPage(api);

    await chooseOption(await screen.findByRole('combobox', { name: 'Provedor' }), 'Anthropic');
    await userEvent.type(screen.getByLabelText('Chave de API'), testKey);
    await userEvent.click(screen.getByRole('button', { name: 'Salvar chave' }));

    expect(await screen.findByText(/Provedor configurado: Anthropic/)).toBeInTheDocument();
    expect(api.saved).toEqual([{ provider: 'anthropic', key: testKey }]);
    expect(screen.getByRole('combobox', { name: 'Provedor' })).toHaveTextContent('Anthropic');
  });

  it('trocar de provedor avisa que a chave atual é substituída e que ela fica se o novo recusar', async () => {
    renderPage(client(connected));

    const select = await screen.findByRole('combobox', { name: 'Provedor' });
    expect(select).toHaveTextContent('OpenAI');
    expect(screen.queryByText(/Trocar de provedor substitui a chave/)).not.toBeInTheDocument();

    await chooseOption(select, 'Anthropic');

    expect(
      screen.getByText(/Trocar de provedor substitui a chave de OpenAI: só uma chave fica ativa/)
    ).toBeInTheDocument();
    expect(screen.getByText(/Se o novo provedor recusar a chave, a atual continua valendo/)).toBeInTheDocument();
  });

  it('se o novo provedor recusa, continua mostrando o provedor de antes', async () => {
    renderPage(client(connected, { refuseSave: true }));

    await chooseOption(await screen.findByRole('combobox', { name: 'Provedor' }), 'Anthropic');
    await userEvent.type(screen.getByLabelText('Chave de API'), testKey);
    await userEvent.click(screen.getByRole('button', { name: 'Salvar chave' }));

    expect(await screen.findByRole('alert')).toHaveTextContent('O provedor recusou a chave.');
    expect(screen.getByText(/Provedor configurado: OpenAI/)).toBeInTheDocument();
  });

  describe('modelo por finalidade', () => {
    const rowOf = async (label: string) => await screen.findByRole('combobox', { name: label });

    it('mostra uma linha por finalidade, com o modelo de hoje e o padrão marcado', async () => {
      renderPage(client(connected));

      const judge = await rowOf('Juiz');

      expect(screen.getByText('Modelo por finalidade')).toBeInTheDocument();
      expect(judge).toHaveTextContent('GPT-4o mini (padrão)');
      expect(await optionsOf(judge)).toEqual(['GPT-4o mini (padrão)', 'GPT-4o']);
      expect(await rowOf('Assistente gerencial')).toBeInTheDocument();
      expect(await rowOf('Fluxos de engenharia')).toBeInTheDocument();
      expect(screen.getByText(/Dá uma segunda opinião sobre o resultado de uma execução/)).toBeInTheDocument();
    });

    it('escolher um modelo manda a finalidade e o modelo, avisa e mostra a escolha', async () => {
      const api = client(connected);
      renderPage(api);

      await chooseOption(await rowOf('Juiz'), /^GPT-4o$/);

      expect(await screen.findByText('Modelo de juiz atualizado.')).toBeInTheDocument();
      expect(api.purposeCalls).toEqual([{ purpose: 'judge', model: 'gpt-4o' }]);
      expect(await rowOf('Juiz')).toHaveTextContent(/^GPT-4o$/);
      expect(await rowOf('Assistente gerencial')).toHaveTextContent('GPT-4o mini (padrão)');
    });

    it('escolher o padrão volta a finalidade ao padrão sem mandar modelo', async () => {
      const api = client(connected);
      renderPage(api);
      await chooseOption(await rowOf('Juiz'), /^GPT-4o$/);
      await screen.findByText('Modelo de juiz atualizado.');

      await chooseOption(await rowOf('Juiz'), 'GPT-4o mini (padrão)');

      await waitFor(() => expect(api.purposeCalls).toHaveLength(2));
      expect(api.purposeCalls[1]).toEqual({ purpose: 'judge' });
      expect(await rowOf('Juiz')).toHaveTextContent('GPT-4o mini (padrão)');
    });

    it('diz que a escolha é da pessoa e que o teto de tokens não muda', async () => {
      renderPage(client(connected));

      expect(await screen.findByText(/nenhum agente escolhe o modelo: a escolha é sua/)).toBeInTheDocument();
      expect(screen.getByText(/O teto de tokens de cada execução continua o mesmo/)).toBeInTheDocument();
    });

    it('sem chave a seção não aparece', async () => {
      renderPage(client(none));

      await screen.findByText('Nenhuma chave configurada.');

      expect(screen.queryByText('Modelo por finalidade')).not.toBeInTheDocument();
    });

    it('se o motor recusa a escolha, mostra o motivo e mantém o modelo de antes', async () => {
      renderPage(client(connected, { refusePurpose: true }));

      await chooseOption(await rowOf('Juiz'), /^GPT-4o$/);

      expect(await screen.findByRole('alert')).toHaveTextContent('Esse modelo não está disponível neste provedor.');
      expect(await rowOf('Juiz')).toHaveTextContent('GPT-4o mini (padrão)');
    });

    it('trocar de provedor mostra os modelos do novo, cada finalidade no padrão dele', async () => {
      renderPage(client(connected));
      await chooseOption(await screen.findByRole('combobox', { name: 'Provedor' }), 'Anthropic');
      await userEvent.type(screen.getByLabelText('Chave de API'), testKey);
      await userEvent.click(screen.getByRole('button', { name: 'Salvar chave' }));

      await screen.findByText(/Provedor configurado: Anthropic/);
      const judge = await rowOf('Juiz');

      expect(await optionsOf(judge)).toEqual(['Claude Haiku 5.5 (padrão)', 'Claude Sonnet 5.5', 'Claude Opus 5.5']);
      expect(judge).toHaveTextContent('Claude Haiku 5.5 (padrão)');
    });
  });

  it('trocar a chave guarda a nova e mantém o estado conectado', async () => {
    const api = client(connected);
    renderPage(api);

    await userEvent.type(await screen.findByLabelText('Chave de API'), testKey);
    await userEvent.click(screen.getByRole('button', { name: 'Salvar chave' }));

    expect(await screen.findByText('Chave guardada: o provedor a aceitou.')).toBeInTheDocument();
    expect(api.saved).toEqual([{ provider: 'openai', key: testKey }]);
  });
});
