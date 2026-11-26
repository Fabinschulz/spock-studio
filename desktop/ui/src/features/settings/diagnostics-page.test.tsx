import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import aiToolsFixture from '@contracts/fixtures/aitools/list.json';
import connectionFixture from '@contracts/fixtures/models/connection.json';

import type { EngineClient } from '@/shared/api';
import { AppProviders } from '@/shared/components';
import { expectNoA11yViolations } from '@/test/a11y';

import { DiagnosticsPage } from './diagnostics-page';

const invoke = vi.fn();
vi.mock('@tauri-apps/api/core', () => ({ invoke: (...args: unknown[]) => invoke(...args) }));
vi.mock('@tauri-apps/api/event', () => ({ listen: vi.fn().mockResolvedValue(() => undefined) }));

const motor = (atMs: number, message: string) => ({ atMs, source: 'motor', message });

const engineClient: EngineClient = {
  async request(method, _params, schema) {
    if (method === 'model.connection') return schema.parse(connectionFixture);
    if (method === 'aitool.list') return schema.parse(aiToolsFixture);
    throw new Error(method);
  },
  async onEvent() {
    return () => undefined;
  }
};

function renderPage() {
  return render(
    <AppProviders>
      <MemoryRouter>
        <DiagnosticsPage client={engineClient} />
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
  it('fora do aplicativo as verificações dizem que não há motor, sem fingir que está pronto', () => {
    renderPage();

    expect(screen.getByRole('heading', { level: 1, name: 'Diagnóstico' })).toBeInTheDocument();
    const checks = screen.getByRole('region', { name: 'Verificações' });
    expect(within(checks).getAllByText('Indisponível na prévia do navegador.').length).toBeGreaterThanOrEqual(2);
    expect(within(checks).queryByText('Certo')).not.toBeInTheDocument();
    expect(screen.getByText('Aplicativo')).toBeInTheDocument();
  });

  it('no aplicativo, cada verificação diz o estado, o que quer dizer e leva aonde se corrige', async () => {
    hostWith([]);
    renderPage();

    const checks = screen.getByRole('region', { name: 'Verificações' });
    const model = (await within(checks).findByText('OpenAI conectado, com o modelo gpt-4o-mini.')).closest('li')!;
    expect(within(model).getByText('Certo')).toBeInTheDocument();
    const storage = within(checks).getByText('Onde a chave fica').closest('li')!;
    expect(within(storage).getByText('Atenção')).toBeInTheDocument();
    expect(within(storage).getByRole('link', { name: 'Abrir Conexões › Serviços' })).toHaveAttribute(
      'href',
      '/integracoes'
    );
    const tools = within(checks)
      .getByText(/^3 de 4 encontradas/)
      .closest('li')!;
    expect(within(tools).getByText('Cursor Agent')).toBeInTheDocument();
    expect(within(tools).getByText(/Ele não confere o login de nenhuma delas/)).toBeInTheDocument();

    await expectNoA11yViolations(document.body);
  });

  it('copia as linhas da tela e confirma', async () => {
    const writeText = vi.fn().mockResolvedValue(undefined);
    Object.defineProperty(navigator, 'clipboard', { value: { writeText }, configurable: true });
    renderPage();

    await userEvent.click(screen.getByRole('button', { name: 'Copiar informações' }));

    expect(writeText).toHaveBeenCalledWith(expect.stringContaining('Verificações:\nMotor: '));
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

    expect(items[0]).toHaveTextContent('Motor');
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
