import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';

import { AppProviders } from '@/shared/components';
import { EngineError } from '@/shared/api';

import type { RunView } from '../../types/run';
import { RunStep } from './run-step';

const run: RunView = {
  kind: 'apply',
  runId: 'run-1',
  state: 'running',
  steps: [],
  files: [],
  evals: [],
  feed: [],
  startedAt: Date.now(),
  error: null
};

describe('Etapa de execução', () => {
  it('cancelamento que falha devolve o botão e mostra o motivo', async () => {
    const onCancel = vi
      .fn()
      .mockRejectedValue(new EngineError('ENGINE_TIMEOUT', 'O motor demorou demais para responder.'));
    render(
      <AppProviders>
        <RunStep run={run} onCancel={onCancel} onBack={() => undefined} />
      </AppProviders>
    );

    await userEvent.click(screen.getByRole('button', { name: 'Cancelar e desfazer' }));
    await userEvent.click(screen.getByRole('button', { name: 'Parar e desfazer' }));

    expect(await screen.findByText('O motor demorou demais para responder.')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Cancelar e desfazer' })).toBeEnabled();
  });

  it('cancelamento aceito mantém o botão em espera até a execução terminar', async () => {
    render(
      <AppProviders>
        <RunStep run={run} onCancel={vi.fn().mockResolvedValue(undefined)} onBack={() => undefined} />
      </AppProviders>
    );

    await userEvent.click(screen.getByRole('button', { name: 'Cancelar e desfazer' }));
    await userEvent.click(screen.getByRole('button', { name: 'Parar e desfazer' }));

    expect(await screen.findByRole('button', { name: 'Cancelando…' })).toBeDisabled();
  });

  it('pede confirmação antes de parar, e desistir não cancela nada', async () => {
    const onCancel = vi.fn();
    render(
      <AppProviders>
        <RunStep run={run} onCancel={onCancel} onBack={() => undefined} />
      </AppProviders>
    );

    await userEvent.click(screen.getByRole('button', { name: 'Cancelar e desfazer' }));
    expect(screen.getByRole('group', { name: 'Parar e desfazer' })).toHaveTextContent(
      'o que ela já gravou no projeto é desfeito'
    );
    await userEvent.click(screen.getByRole('button', { name: 'Continuar a execução' }));

    expect(onCancel).not.toHaveBeenCalled();
    expect(screen.getByRole('button', { name: 'Cancelar e desfazer' })).toHaveFocus();
  });
});
