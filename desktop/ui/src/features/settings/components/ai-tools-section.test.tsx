import aiToolsFixture from '@contracts/fixtures/aitools/list.json';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';

import { EngineError, type EngineClient } from '@/shared/api';
import { AppProviders } from '@/shared/components';
import { expectNoA11yViolations } from '@/test/a11y';

import { AiToolsSection } from './ai-tools-section';

function client(reply: () => unknown): EngineClient & { request: ReturnType<typeof vi.fn> } {
  return {
    request: vi.fn(async (_method, _params, schema) => schema.parse(reply())),
    onEvent: async () => () => undefined
  } as EngineClient & { request: ReturnType<typeof vi.fn> };
}

const renderSection = (api: EngineClient) =>
  render(
    <AppProviders>
      <AiToolsSection client={api} />
    </AppProviders>
  );

describe('AiToolsSection', () => {
  it('lista cada ferramenta com o estado, a versão e o que o Spock faz com ela', async () => {
    const api = client(() => aiToolsFixture);
    renderSection(api);

    const claude = (await screen.findByText('Claude Code')).closest('li')!;
    expect(within(claude).getByText('Instalada')).toBeInTheDocument();
    expect(within(claude).getByText('Versão 2.0.5.')).toBeInTheDocument();
    expect(within(claude).getByText(/O Spock configura o/)).toHaveTextContent(
      'O Spock configura o Harness para esta ferramenta.'
    );

    const cursor = screen.getByText('Cursor Agent').closest('li')!;
    expect(within(cursor).getByText('Não encontrada')).toBeInTheDocument();
    expect(within(cursor).getByText(/ainda são só do Claude Code/)).toBeInTheDocument();
    expect(within(cursor).getByRole('button', { name: 'MCP' })).toBeInTheDocument();
    const codex = screen.getByText('Codex CLI').closest('li')!;
    expect(within(codex).getByText('Instalada, mas não informou a versão.')).toBeInTheDocument();
    expect(api.request).toHaveBeenCalledWith('aitool.list', {}, expect.anything());
  });

  it('diz que não confere o login, para ninguém achar que "instalada" é "logada"', async () => {
    renderSection(client(() => aiToolsFixture));

    expect(await screen.findByText(/Ele não confere o login de nenhuma delas/)).toBeInTheDocument();
  });

  it('Verificar de novo procura outra vez', async () => {
    const api = client(() => aiToolsFixture);
    renderSection(api);
    await screen.findByText('Claude Code');

    await userEvent.click(screen.getByRole('button', { name: 'Verificar de novo' }));

    await vi.waitFor(() => expect(api.request).toHaveBeenCalledTimes(2));
  });

  it('se o motor falha, mostra o erro e deixa tentar de novo', async () => {
    const api = client(() => aiToolsFixture);
    api.request.mockRejectedValueOnce(new EngineError('ENGINE_STOPPED', 'O motor está parado.'));
    renderSection(api);

    expect(await screen.findByRole('alert')).toHaveTextContent(
      'O motor está parado. Tente de novo em instantes; se continuar, feche e abra o aplicativo.'
    );
    expect(screen.getByRole('button', { name: 'Tentar de novo' })).toBeInTheDocument();
  });

  it('não tem violações estruturais de acessibilidade', async () => {
    const { container } = renderSection(client(() => aiToolsFixture));
    await screen.findByText('Claude Code');

    await expectNoA11yViolations(container);
  });
});
