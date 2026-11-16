import { act, render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router';
import { describe, expect, it, vi } from 'vitest';

import type { EngineClient } from '@/shared/api';
import { AppProviders } from '@/shared/components';
import type { RunEvent } from '@/shared/schema';
import { expectNoA11yViolations } from '@/test/a11y';

import { AgentsPage } from './agents-page';
import { scopedServerName } from './domain/ide-setup';

const loja = { projectId: 'p-1', displayName: 'Loja', displayPath: '~\\Loja' };

const TOOLS = [
  { id: 'claude-code', name: 'Claude Code', binary: 'claude', installed: true, version: '2.1.0', harnessSupport: true },
  { id: 'codex', name: 'Codex CLI', binary: 'codex', installed: false, version: null, harnessSupport: false }
];

const ENGINE = 'C:\\Program Files\\Spock\\mariliz-runtime.exe';

const IDE = {
  command: ENGINE,
  args: ['mcp'],
  projectArgument: '--project',
  tools: [{ name: 'project_briefing', title: 'Resumo do projeto', description: 'Tecnologias, comandos e decisões.' }]
};

/** Um motor que inicia a execução e deixa o teste emitir as linhas e o fim, na ordem do motor real. */
function fakeEngine(
  options: {
    tools?: typeof TOOLS;
    error?: string | null;
    ide?: Omit<typeof IDE, 'projectArgument'> | { command: null };
  } = {}
) {
  const calls: { method: string; params: Record<string, unknown> }[] = [];
  let emit: (event: RunEvent) => void = () => undefined;
  let finished = false;
  let seq = 1;
  const snapshot = () => ({
    runId: 'run-1',
    projectId: 'p-1',
    kind: 'agent',
    state: finished ? (options.error ? 'failed' : 'completed') : 'running',
    lastSeq: finished ? seq : 1,
    events: [{ seq: 1, type: 'run.state', data: { state: 'running' } }],
    result: finished
      ? {
          kind: 'agent',
          tool: 'claude-code',
          toolName: 'Claude Code',
          exitCode: options.error ? 1 : 0,
          error: options.error ?? null,
          answer: options.error ? '' : 'É uma loja virtual em .NET.'
        }
      : null
  });

  const client: EngineClient = {
    async request(method, params, schema) {
      calls.push({ method, params: params as Record<string, unknown> });
      const reply: Record<string, unknown> = {
        'aitool.list': { tools: options.tools ?? TOOLS },
        'agent.run': { runId: 'run-1' },
        'run.snapshot': snapshot(),
        'run.cancel': { cancelled: true },
        'ide.connection': { ...IDE, ...options.ide }
      };
      return schema.parse(reply[method]);
    },
    async onEvent(handler) {
      emit = handler;
      return () => undefined;
    }
  };
  const send = (type: string, data: Record<string, unknown>) =>
    act(() => emit({ v: 1, kind: 'event', runId: 'run-1', type, seq: ++seq, data } as RunEvent));
  return {
    client,
    calls,
    line: (text: string) => send('agent.output', { line: text }),
    complete: () => {
      finished = true;
      return send('run.completed', { state: options.error ? 'failed' : 'completed' });
    }
  };
}

function renderPage(client: EngineClient, project: typeof loja | null = loja) {
  return render(
    <AppProviders>
      <MemoryRouter>
        <AgentsPage client={client} project={project} />
      </MemoryRouter>
    </AppProviders>
  );
}

async function ask(text = 'O que este projeto faz?') {
  const group = await screen.findByRole('group', { name: 'Ferramenta' });
  await userEvent.click(await within(group).findByRole('radio', { name: /Claude Code/ }));
  await userEvent.type(screen.getByLabelText(/O que você quer saber/), text);
  await userEvent.click(screen.getByRole('button', { name: 'Pedir ao agente' }));
}

describe('Agentes de IA', () => {
  it('só deixa escolher o que está instalado e diz por que o resto não está', async () => {
    const { container } = renderPage(fakeEngine().client);

    expect(await screen.findByRole('radio', { name: /Claude Code.*versão 2\.1\.0/ })).toBeEnabled();
    expect(screen.getByRole('radio', { name: /Codex CLI.*não instalada neste computador/ })).toBeDisabled();
    expect(screen.getByRole('button', { name: 'Pedir ao agente' })).toBeDisabled();
    expect(screen.getByText('Escolha uma ferramenta instalada e escreva o pedido para enviar.')).toBeInTheDocument();
    await expectNoA11yViolations(container);
  });

  it('pede pelo projeto aberto, mostra a saída ao vivo e depois a resposta', async () => {
    const engine = fakeEngine();
    renderPage(engine.client);

    await ask();
    expect(await screen.findByText('A ferramenta está escrevendo…')).toBeInTheDocument();
    engine.line('Lendo o projeto…');
    expect(screen.getByText('Lendo o projeto…')).toBeInTheDocument();

    engine.complete();

    expect(await screen.findByText('É uma loja virtual em .NET.')).toBeInTheDocument();
    expect(screen.getByText('Concluído')).toBeInTheDocument();
    expect(engine.calls.find((call) => call.method === 'agent.run')?.params).toEqual({
      projectId: 'p-1',
      tool: 'claude-code',
      prompt: 'O que este projeto faz?'
    });
    expect(screen.getByRole('link', { name: 'Execuções' })).toHaveAttribute('href', '/execucoes');
  });

  it('quando a ferramenta falha, explica em português e mostra o que ela escreveu', async () => {
    const engine = fakeEngine({ error: 'AGENT_FAILED' });
    renderPage(engine.client);

    await ask();
    engine.line('Error: not logged in');
    engine.complete();

    const notice = await screen.findByText(/A ferramenta terminou com erro/);
    expect(notice).toHaveTextContent('muitas vezes é falta de login');
    expect(screen.getByText('Error: not logged in')).toBeInTheDocument();
  });

  it('enquanto roda, dá para cancelar', async () => {
    const engine = fakeEngine();
    renderPage(engine.client);

    await ask();
    await userEvent.click(await screen.findByRole('button', { name: 'Cancelar' }));

    expect(engine.calls.some((call) => call.method === 'run.cancel' && call.params.runId === 'run-1')).toBe(true);
  });

  it('mostra como ligar o Spock em cada ferramenta e copia o passo', async () => {
    const writeText = vi.fn().mockResolvedValue(undefined);
    Object.assign(navigator, { clipboard: { writeText } });
    const { container } = renderPage(fakeEngine().client);

    const section = await screen.findByRole('region', { name: 'O Spock dentro da sua ferramenta' });
    const snippet = await within(section).findByLabelText('Configuração para Claude Code');
    expect(snippet).toHaveTextContent(
      `claude mcp add --scope user ${scopedServerName(loja)} -- "${ENGINE}" mcp --project ${loja.projectId}`
    );
    expect(within(section).getByText('Resumo do projeto')).toBeInTheDocument();

    await userEvent.click(within(section).getByRole('radio', { name: 'Cursor' }));
    const cursor = within(section).getByLabelText('Configuração para Cursor');
    expect(JSON.parse(cursor.textContent ?? '')).toEqual({
      mcpServers: { [scopedServerName(loja)]: { command: ENGINE, args: ['mcp', '--project', loja.projectId] } }
    });

    await userEvent.click(within(section).getByRole('button', { name: 'Copiar' }));
    expect(writeText).toHaveBeenCalledWith(cursor.textContent);
    expect(within(section).getByRole('button', { name: 'Copiado' })).toBeInTheDocument();
    await expectNoA11yViolations(container);
  });

  // Validação real M3 e H0-1 (16/11): a conexão lê só o projeto aberto, e não existe mais a opção de ler todos.
  it('com projeto aberto, a configuração lê só ele, sem opção de todos', async () => {
    renderPage(fakeEngine().client);

    const section = await screen.findByRole('region', { name: 'O Spock dentro da sua ferramenta' });
    await within(section).findByLabelText('Configuração para Claude Code');
    expect(within(section).queryByRole('group', { name: 'O que ela pode ler' })).not.toBeInTheDocument();
    expect(within(section).queryByText(/Todos os projetos/)).not.toBeInTheDocument();
    expect(within(section).getByText(new RegExp(`que só\\s+enxergam ${loja.displayName}`))).toBeInTheDocument();
  });

  it('o nome do servidor restrito sai do nome do projeto, sem acento nem espaço', () => {
    expect(scopedServerName({ projectId: 'p-aaaaaaaaaaaa', displayName: 'Gestão de Loja' })).toBe(
      'spock-gestao-de-loja'
    );
    expect(scopedServerName({ projectId: 'p-aaaaaaaaaaaa', displayName: '***' })).toBe('spock-p-aaaaaaaaaaaa');
  });

  // H0-1 (16/11): sem projeto, o comando sairia sem --project, e o servidor não sobe assim.
  it('sem projeto aberto, pede para abrir um em vez de mostrar um comando que não sobe', async () => {
    renderPage(fakeEngine().client, null);

    expect(await screen.findByText('Abra um projeto para ligar a sua ferramenta')).toBeInTheDocument();
    expect(screen.queryByLabelText(/Configuração para/)).not.toBeInTheDocument();
  });

  it('sem saber onde o motor está, não inventa um caminho', async () => {
    renderPage(fakeEngine({ ide: { command: null } }).client);

    expect(await screen.findByText(/não conseguiu descobrir onde está instalado/)).toBeInTheDocument();
    expect(screen.queryByLabelText(/Configuração para/)).not.toBeInTheDocument();
  });

  it('sem ferramenta instalada, diz o que fazer', async () => {
    renderPage(fakeEngine({ tools: TOOLS.map((tool) => ({ ...tool, installed: false })) }).client);

    expect(await screen.findByText(/Nenhuma ferramenta de IA foi encontrada/)).toBeInTheDocument();
    expect(
      within(screen.getByRole('group', { name: 'Ferramenta' }))
        .getAllByRole('radio')
        .every((radio) => (radio as HTMLInputElement).disabled)
    ).toBe(true);
  });
});
