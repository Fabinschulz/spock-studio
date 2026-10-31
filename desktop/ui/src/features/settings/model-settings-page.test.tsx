import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it } from 'vitest';

import type { EngineClient } from '@/shared/api';
import { AppProviders } from '@/shared/components';
import { expectNoA11yViolations } from '@/test/a11y';

import { ModelSettingsPage } from './model-settings-page';

const testKey = 'sk-test-chave-fase-j-9f3a';

function client(initial: { configured: boolean; provider: string }): EngineClient & { calls: string[] } {
  const calls: string[] = [];
  let status = initial;
  return {
    calls,
    async request(method, params, schema) {
      calls.push(method);
      if (method === 'model.status') return schema.parse(status);
      if (method === 'model.save') {
        status = { configured: true, provider: 'openai' };
        return schema.parse(status);
      }
      if (method === 'model.clear') {
        status = { configured: false, provider: '' };
        return schema.parse(status);
      }
      throw new Error(`${method} ${JSON.stringify(params)}`);
    },
    async onEvent() {
      return () => undefined;
    }
  };
}

describe('Configurações · chave de modelo', () => {
  it('mostra o estado e guarda sem ecoar a chave na interface', async () => {
    const api = client({ configured: false, provider: '' });
    const { container } = render(
      <AppProviders>
        <ModelSettingsPage client={api} />
      </AppProviders>
    );

    await waitFor(() => expect(screen.getByText('Nenhuma chave configurada.')).toBeInTheDocument());

    const field = screen.getByLabelText('Chave de API');
    await userEvent.type(field, testKey);
    await userEvent.click(screen.getByRole('button', { name: 'Guardar chave' }));

    await waitFor(() => expect(screen.getByText(/Provedor configurado: OpenAI/)).toBeInTheDocument());
    expect(field).toHaveValue('');
    expect(screen.queryByText(testKey)).not.toBeInTheDocument();
    expect(api.calls).toEqual(['model.status', 'model.save']);

    await expectNoA11yViolations(container);
  });

  it('remove a chave configurada', async () => {
    const api = client({ configured: true, provider: 'openai' });
    render(
      <AppProviders>
        <ModelSettingsPage client={api} />
      </AppProviders>
    );

    await waitFor(() => expect(screen.getByRole('button', { name: 'Remover chave' })).toBeInTheDocument());
    await userEvent.click(screen.getByRole('button', { name: 'Remover chave' }));

    await waitFor(() => expect(screen.getByText('Nenhuma chave configurada.')).toBeInTheDocument());
    expect(api.calls).toContain('model.clear');
  });
});
