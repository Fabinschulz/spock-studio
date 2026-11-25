import { act, render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { StrictMode } from 'react';
import { MemoryRouter } from 'react-router';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { ProjectSelectionProvider, useProjectSelectionContext } from '@/features/project-selection';
import { AppProviders } from '@/shared/components';

import type { TerminalApi, TerminalEvent } from '../service/terminal-api';
import { TerminalPanel } from './TerminalPanel';

// O jsdom não desenha terminal: o xterm de mentira guarda o que a tela receberia e deixa simular o que a pessoa digita.
const screenText: string[] = [];
let typed: (data: string) => void = () => undefined;
let focused = 0;

vi.mock('@xterm/xterm', () => ({
  Terminal: class {
    cols = 80;
    rows = 24;
    loadAddon() {}
    open() {}
    focus() {
      focused += 1;
    }
    dispose() {}
    write(data: string) {
      screenText.push(data);
    }
    onData(listener: (data: string) => void) {
      typed = listener;
      return { dispose() {} };
    }
    onResize() {
      return { dispose() {} };
    }
  }
}));
vi.mock('@xterm/addon-fit', () => ({
  FitAddon: class {
    fit() {}
  }
}));

function fakeApi() {
  let emit: (event: TerminalEvent) => void = () => undefined;
  let opened = 0;
  const api = {
    open: vi.fn(async (_recentId: string, _size: unknown, onEvent: (event: TerminalEvent) => void) => {
      emit = onEvent;
      opened += 1;
      return { sessionId: `t-${opened}`, shell: 'PowerShell' };
    }),
    write: vi.fn(async () => undefined),
    resize: vi.fn(async () => undefined),
    close: vi.fn(async () => undefined)
  } satisfies TerminalApi;
  return { api, emit: (event: TerminalEvent) => act(() => emit(event)) };
}

const loja = { projectId: 'p-1', displayName: 'Loja', displayPath: '~\\Loja', recentId: 'r-loja' };

function Pick() {
  const { pick } = useProjectSelectionContext();
  return (
    <button type="button" onClick={() => void pick()}>
      escolher
    </button>
  );
}

/** `focusRequest` 1: a pessoa pediu o terminal. 0: o painel reabriu sozinho ao iniciar o aplicativo. */
async function renderOpenProject(api: TerminalApi, focusRequest = 1) {
  const view = render(
    <AppProviders>
      <ProjectSelectionProvider picker={{ pick: vi.fn().mockResolvedValue(loja) }}>
        <MemoryRouter>
          <Pick />
          <TerminalPanel hosted focusRequest={focusRequest} api={api} />
        </MemoryRouter>
      </ProjectSelectionProvider>
    </AppProviders>
  );
  await userEvent.click(screen.getByRole('button', { name: 'escolher' }));
  return view;
}

beforeEach(() => {
  screenText.length = 0;
  typed = () => undefined;
  focused = 0;
  // A WebView do aplicativo tem ResizeObserver; o jsdom, não.
  vi.stubGlobal(
    'ResizeObserver',
    class {
      observe() {}
      disconnect() {}
    }
  );
});

afterEach(() => {
  vi.unstubAllGlobals();
});

describe('TerminalPanel', () => {
  it('sem projeto aberto, diz que o terminal roda na pasta do projeto', () => {
    render(
      <AppProviders>
        <ProjectSelectionProvider picker={{ pick: vi.fn() }}>
          <TerminalPanel hosted focusRequest={0} api={fakeApi().api} />
        </ProjectSelectionProvider>
      </AppProviders>
    );

    expect(screen.getByText(/Abra um projeto para usar o terminal/)).toBeInTheDocument();
  });

  it('abre no projeto aberto, mostra a saída e manda o que a pessoa digita', async () => {
    const { api, emit } = fakeApi();
    await renderOpenProject(api);

    expect(await screen.findByText('PowerShell em Loja')).toBeInTheDocument();
    expect(api.open).toHaveBeenCalledWith('r-loja', { cols: 80, rows: 24 }, expect.any(Function));

    emit({ kind: 'output', data: 'PS C:\\Loja> ' });
    act(() => typed('dir\r'));

    expect(screenText).toContain('PS C:\\Loja> ');
    expect(api.write).toHaveBeenCalledWith('t-1', 'dir\r');
  });

  it('quando o shell sai, diz o código e deixa abrir de novo', async () => {
    const { api, emit } = fakeApi();
    await renderOpenProject(api);
    await screen.findByText('PowerShell em Loja');

    emit({ kind: 'exit', code: 1 });
    expect(screen.getByText('O terminal foi encerrado (código 1).')).toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: 'Abrir de novo' }));

    await waitFor(() => expect(api.open).toHaveBeenCalledTimes(2));
    expect(await screen.findByText('PowerShell em Loja')).toBeInTheDocument();
  });

  it('se o host recusa, mostra o motivo e deixa tentar de novo', async () => {
    const { api } = fakeApi();
    api.open.mockRejectedValueOnce(Object.assign(new Error('x'), { name: 'EngineError' }));
    await renderOpenProject(api);

    expect(await screen.findByRole('alert')).toHaveTextContent('Não foi possível abrir o terminal.');
    await userEvent.click(screen.getByRole('button', { name: 'Tentar de novo' }));

    expect(await screen.findByText('PowerShell em Loja')).toBeInTheDocument();
  });

  it('pedido pela pessoa, o terminal recebe o foco assim que fica pronto', async () => {
    const { api } = fakeApi();
    await renderOpenProject(api, 1);

    await screen.findByText('PowerShell em Loja');

    // O foco vem num efeito depois de o rótulo aparecer: com a máquina ocupada, ele ainda não rodou neste ponto.
    await waitFor(() => expect(focused).toBe(1));
  });

  it('reaberto sozinho ao iniciar o aplicativo, o terminal não toma o foco de ninguém', async () => {
    const { api } = fakeApi();
    await renderOpenProject(api, 0);

    await screen.findByText('PowerShell em Loja');

    expect(focused).toBe(0);
  });

  it('pedir de novo com o terminal já à vista leva o foco a ele', async () => {
    const { api } = fakeApi();
    const { rerender } = await renderOpenProject(api, 0);
    await screen.findByText('PowerShell em Loja');

    rerender(
      <AppProviders>
        <ProjectSelectionProvider picker={{ pick: vi.fn().mockResolvedValue(loja) }}>
          <MemoryRouter>
            <Pick />
            <TerminalPanel hosted focusRequest={1} api={api} />
          </MemoryRouter>
        </ProjectSelectionProvider>
      </AppProviders>
    );

    await waitFor(() => expect(focused).toBe(1));
  });

  it('o fim de uma sessão descartada não muda o estado da que a substituiu', async () => {
    const { api } = fakeApi();
    const emits: ((event: TerminalEvent) => void)[] = [];
    api.open.mockImplementation(async (_recentId, _size, onEvent) => {
      emits.push(onEvent);
      return { sessionId: `t-${emits.length}`, shell: 'PowerShell' };
    });
    // O StrictMode roda o efeito duas vezes na mesma instância, como qualquer reexecução do efeito: a primeira sessão
    // é descartada e a segunda fica.
    render(
      <StrictMode>
        <AppProviders>
          <ProjectSelectionProvider picker={{ pick: vi.fn().mockResolvedValue(loja) }}>
            <MemoryRouter>
              <Pick />
              <TerminalPanel hosted focusRequest={1} api={api} />
            </MemoryRouter>
          </ProjectSelectionProvider>
        </AppProviders>
      </StrictMode>
    );
    await userEvent.click(screen.getByRole('button', { name: 'escolher' }));
    await screen.findByText('PowerShell em Loja');
    expect(emits).toHaveLength(2);

    act(() => emits[0]!({ kind: 'exit', code: 1 }));

    expect(screen.queryByText(/O terminal foi encerrado/)).not.toBeInTheDocument();
    expect(screen.getByText('PowerShell em Loja')).toBeInTheDocument();
  });

  it('sair da tela encerra a sessão no host', async () => {
    const { api } = fakeApi();
    const { unmount } = await renderOpenProject(api);
    await screen.findByText('PowerShell em Loja');

    unmount();

    expect(api.close).toHaveBeenCalledWith('t-1');
  });
});
