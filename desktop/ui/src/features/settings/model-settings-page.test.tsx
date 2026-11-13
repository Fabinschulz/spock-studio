import connectedFixture from '@contracts/fixtures/models/connection.json';
import failedFixture from '@contracts/fixtures/models/connection-error.json';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it } from 'vitest';

import { EngineError, type EngineClient } from '@/shared/api';
import { AppProviders } from '@/shared/components';
import { modelConnectionSchema, type ModelConnection } from '@/shared/schema';
import { expectNoA11yViolations } from '@/test/a11y';

import { ModelSettingsPage } from './model-settings-page';

const testKey = 'sk-test-chave-fase-f-7c2d41';

const connected = modelConnectionSchema.parse(connectedFixture);
const failed = modelConnectionSchema.parse(failedFixture);
const none: ModelConnection = {
  configured: false,
  provider: '',
  model: connected.model,
  status: 'none',
  protection: null,
  checkedAt: null,
  lastError: null
};

type Behavior = {
  /** Se o provedor recusa a chave ao guardar. */
  refuseSave?: boolean;
  /** O estado que o teste devolve. Sem isto, o provedor aceita. */
  testResult?: ModelConnection;
};

/** O motor de mentira do provedor de IA: guarda o estado em memória e responde no formato do contrato. */
function client(
  initial: ModelConnection,
  behavior: Behavior = {}
): EngineClient & { calls: string[]; saved: string[] } {
  const calls: string[] = [];
  const saved: string[] = [];
  let state = initial;
  return {
    calls,
    saved,
    async request(method, params, schema) {
      calls.push(method);
      if (method === 'model.connection') return schema.parse(state);
      if (method === 'model.save') {
        if (behavior.refuseSave) throw new EngineError('MODEL_AUTH_FAILED', 'O provedor recusou a chave.');
        saved.push(String((params as { key: string }).key));
        state = { ...connected };
        return schema.parse({ configured: true, provider: 'openai' });
      }
      if (method === 'model.test') {
        state = behavior.testResult ?? { ...state, status: 'connected', lastError: null };
        return schema.parse(state);
      }
      if (method === 'model.clear') {
        state = none;
        return schema.parse({ configured: false, provider: '' });
      }
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
    expect(api.calls).toEqual(['model.connection', 'model.save', 'model.connection']);
    expect(api.saved).toEqual([testKey]);

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
    expect(screen.getByText('gpt-4o-mini')).toBeInTheDocument();
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
    expect(await screen.findByText(/sem a proteção do sistema: este sistema ainda não tem cofre/)).toBeInTheDocument();
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

    await waitFor(() => expect(screen.getByText('Nenhuma chave configurada.')).toBeInTheDocument());
    expect(screen.getByText('Chave removida deste computador.')).toBeInTheDocument();
    expect(api.calls).toContain('model.clear');
  });

  it('trocar a chave guarda a nova e mantém o estado conectado', async () => {
    const api = client(connected);
    renderPage(api);

    await userEvent.type(await screen.findByLabelText('Chave de API'), testKey);
    await userEvent.click(screen.getByRole('button', { name: 'Salvar chave' }));

    expect(await screen.findByText('Chave guardada: o provedor a aceitou.')).toBeInTheDocument();
    expect(api.saved).toEqual([testKey]);
  });
});
