import { act, render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { AppProviders } from '@/shared/components';
import { expectNoA11yViolations } from '@/test/a11y';

import { RestartEngine } from './restart-engine';

const invoke = vi.fn();
let emitStatus: (status: string) => void = () => undefined;
vi.mock('@tauri-apps/api/core', () => ({ invoke: (...args: unknown[]) => invoke(...args) }));
vi.mock('@tauri-apps/api/event', () => ({
  listen: vi.fn(async (_event: string, handler: (event: { payload: unknown }) => void) => {
    emitStatus = (status) => handler({ payload: status });
    return () => undefined;
  })
}));

function hostWith(status: string, restart: () => Promise<void> = async () => undefined) {
  (window as unknown as Record<string, unknown>).__TAURI_INTERNALS__ = {};
  invoke.mockImplementation((command: string) => {
    if (command === 'engine_status') return Promise.resolve(status);
    if (command === 'restart_engine') return restart();
    return Promise.resolve(null);
  });
}

const renderButton = (onReady?: () => void) =>
  render(
    <AppProviders>
      <RestartEngine onReady={onReady} />
    </AppProviders>
  );

beforeEach(() => invoke.mockReset());
afterEach(() => {
  delete (window as unknown as Record<string, unknown>).__TAURI_INTERNALS__;
});

describe('RestartEngine', () => {
  it('com o motor de pé não aparece', async () => {
    hostWith('ready');
    renderButton();

    await waitFor(() => expect(invoke).toHaveBeenCalledWith('engine_status'));
    expect(screen.queryByRole('button', { name: 'Reiniciar o motor' })).not.toBeInTheDocument();
  });

  it('com o motor parado, reinicia e avisa quando ele fica pronto', async () => {
    hostWith('crashed');
    const onReady = vi.fn();
    const { container } = renderButton(onReady);

    await userEvent.click(await screen.findByRole('button', { name: 'Reiniciar o motor' }));

    expect(invoke).toHaveBeenCalledWith('restart_engine');
    await act(async () => emitStatus('starting'));
    expect(screen.queryByRole('button', { name: 'Reiniciar o motor' })).not.toBeInTheDocument();
    expect(onReady).not.toHaveBeenCalled();
    await act(async () => emitStatus('ready'));
    expect(onReady).toHaveBeenCalledTimes(1);
    await expectNoA11yViolations(container);
  });

  it('se o host recusa, diz o que fazer e deixa tentar de novo', async () => {
    hostWith('crashed', () => Promise.reject({ code: 'ENGINE_NOT_STOPPED', message: 'não' }));
    renderButton();

    await userEvent.click(await screen.findByRole('button', { name: 'Reiniciar o motor' }));

    expect(await screen.findByRole('alert')).toHaveTextContent('Não foi possível reiniciar o motor.');
    expect(screen.getByRole('button', { name: 'Reiniciar o motor' })).toBeEnabled();
  });
});
