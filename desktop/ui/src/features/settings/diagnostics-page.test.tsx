import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { AppProviders } from '@/shared/components';
import { expectNoA11yViolations } from '@/test/a11y';

import { DiagnosticsPage } from './diagnostics-page';

const invoke = vi.fn();
vi.mock('@tauri-apps/api/core', () => ({ invoke: (...args: unknown[]) => invoke(...args) }));
vi.mock('@tauri-apps/api/event', () => ({ listen: vi.fn().mockResolvedValue(() => undefined) }));

const motor = (atMs: number, message: string) => ({ atMs, source: 'motor', message });

function renderPage() {
  return render(
    <AppProviders>
      <MemoryRouter>
        <DiagnosticsPage />
      </MemoryRouter>
    </AppProviders>
  );
}

function hostWith(log: unknown) {
  (window as unknown as Record<string, unknown>).__TAURI_INTERNALS__ = {};
  invoke.mockImplementation((command: string) => {
    if (command === 'engine_status') return Promise.resolve('ready');
    return log instanceof Error ? Promise.reject({ code: 'HOST_ERROR', message: log.message }) : Promise.resolve(log);
  });
}

beforeEach(() => {
  invoke.mockReset();
});

afterEach(() => {
  delete (window as unknown as Record<string, unknown>).__TAURI_INTERNALS__;
  vi.restoreAllMocks();
});

describe('DiagnosticsPage · este computador', () => {
  it('mostra as informações do computador e diz que o motor não existe fora do aplicativo', () => {
    renderPage();

    expect(screen.getByRole('heading', { level: 1, name: 'Diagnóstico' })).toBeInTheDocument();
    expect(screen.getByText('Aplicativo')).toBeInTheDocument();
    expect(screen.getAllByText('Indisponível na prévia do navegador')).toHaveLength(2);
  });

  it('copia as linhas da tela e confirma', async () => {
    const writeText = vi.fn().mockResolvedValue(undefined);
    Object.defineProperty(navigator, 'clipboard', { value: { writeText }, configurable: true });
    renderPage();

    await userEvent.click(screen.getByRole('button', { name: 'Copiar informações' }));

    expect(writeText).toHaveBeenCalledWith(expect.stringContaining('Aplicativo: Spock Studio v'));
    expect(await screen.findByText('Informações copiadas.')).toBeInTheDocument();
  });

  it('se o sistema recusa a cópia, diz como seguir', async () => {
    const writeText = vi.fn().mockRejectedValue(new Error('negado'));
    Object.defineProperty(navigator, 'clipboard', { value: { writeText }, configurable: true });
    renderPage();

    await userEvent.click(screen.getByRole('button', { name: 'Copiar informações' }));

    expect(await screen.findByText(/Não foi possível copiar/)).toBeInTheDocument();
  });

  it('não tem violações estruturais de acessibilidade', async () => {
    const { container } = renderPage();

    await expectNoA11yViolations(container);
  });
});

describe('DiagnosticsPage · registro recente', () => {
  it('fora do aplicativo não há registro, e a tela diz isso', () => {
    renderPage();

    const section = screen.getByRole('region', { name: 'Registro recente' });
    expect(within(section).getByText('Sem motor local, não há registro nesta prévia.')).toBeInTheDocument();
    expect(within(section).queryByRole('button', { name: 'Atualizar' })).not.toBeInTheDocument();
  });

  it('lista as linhas da mais nova para a mais antiga e conta as que se repetem', async () => {
    hostWith([motor(1, 'iniciou'), motor(2, 'falhou'), motor(3, 'falhou'), motor(4, 'caiu')]);
    renderPage();

    const section = await screen.findByRole('region', { name: 'Registro recente' });
    const items = await within(section).findAllByRole('listitem');

    expect(items.map((item) => item.textContent)).toEqual([
      expect.stringContaining('caiu'),
      expect.stringContaining('falhou (×2)'),
      expect.stringContaining('iniciou')
    ]);
  });

  it('sem linhas diz que ainda não há nenhuma', async () => {
    hostWith([]);
    renderPage();

    expect(await screen.findByText('Nenhuma linha ainda.')).toBeInTheDocument();
  });

  it('o texto copiado leva o registro recente junto', async () => {
    const writeText = vi.fn().mockResolvedValue(undefined);
    Object.defineProperty(navigator, 'clipboard', { value: { writeText }, configurable: true });
    hostWith([motor(Date.UTC(2026, 10, 13, 22, 0, 0), 'falhou')]);
    renderPage();
    await screen.findByText(/falhou/);

    await userEvent.click(screen.getByRole('button', { name: 'Copiar informações' }));

    expect(writeText).toHaveBeenCalledWith(
      expect.stringContaining('Registro recente:\n2026-11-13T22:00:00.000Z [motor] falhou')
    );
  });

  it('se o host não consegue ler, mostra o erro e deixa tentar de novo', async () => {
    hostWith(new Error('sem acesso'));
    renderPage();

    expect(await screen.findByText('Não foi possível ler o registro.')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Tentar de novo' })).toBeInTheDocument();
  });

  it('Atualizar lê o registro de novo', async () => {
    hostWith([motor(1, 'primeira')]);
    renderPage();
    await screen.findByText(/primeira/);
    hostWith([motor(1, 'primeira'), motor(2, 'segunda')]);

    await userEvent.click(screen.getByRole('button', { name: 'Atualizar' }));

    expect(await screen.findByText(/segunda/)).toBeInTheDocument();
  });
});
