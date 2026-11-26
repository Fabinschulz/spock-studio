import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, useLocation } from 'react-router';
import { describe, expect, it, vi } from 'vitest';

import { ProjectSelectionProvider, type ProjectPickerPort } from '@/features/project-selection';
import type { EngineClient } from '@/shared/api';
import { AppProviders } from '@/shared/components';
import { readHandoff } from '@/shared/routing';
import { expectNoA11yViolations } from '@/test/a11y';

import { CommandPalette } from './CommandPalette';

const recentProject = {
  id: 'r-1',
  displayName: 'loja-api',
  displayPath: '~\\dev\\loja-api',
  lastOpenedMs: 1,
  available: true
};

function Where() {
  const { pathname, state } = useLocation();
  return (
    <>
      <p data-testid="where">{pathname}</p>
      <p data-testid="handoff">{readHandoff(state) ?? ''}</p>
    </>
  );
}

const run = (runId: string, state: string) => ({
  runId,
  projectId: 'p-1',
  projectName: 'loja-api',
  kind: 'validate',
  state,
  startedAt: '2026-11-20T10:00:00Z',
  finishedAt: null,
  live: false,
  interrupted: false,
  changesApplied: false,
  reverted: false,
  canRevert: false,
  revertsRunId: null,
  error: null,
  operations: 0,
  pending: 0,
  notApplied: 0,
  tokens: null
});

interface Feed {
  /** O que espera a pessoa ou está andando. */
  tasks?: unknown[];
  recent?: unknown[];
  /** O motor não responde. */
  down?: boolean;
}

/** O motor de mentira responde só a lista de execuções, a mesma que o painel de atividade lê. */
function engine({ tasks = [], recent = [], down = false }: Feed = {}): EngineClient {
  return {
    request: vi.fn(async (method: string, params: Record<string, unknown>, schema) => {
      if (down) throw new Error('motor fora do ar');
      if (method !== 'run.list') throw new Error(`método inesperado: ${method}`);
      const runs = params.states ? tasks : [...tasks, ...recent];
      return schema.parse({ runs, nextCursor: null, summary: { total: runs.length, running: 0, awaitingApproval: 0 } });
    }) as EngineClient['request'],
    onEvent: vi.fn().mockResolvedValue(() => undefined)
  };
}

function renderPalette(picker: Partial<ProjectPickerPort> = {}, client: EngineClient = engine()) {
  const onClose = vi.fn();
  const onShellAction = vi.fn();
  const port: ProjectPickerPort = { pick: vi.fn().mockResolvedValue(null), ...picker };
  render(
    <AppProviders>
      <ProjectSelectionProvider picker={port}>
        <MemoryRouter>
          <Where />
          <CommandPalette open onClose={onClose} onShellAction={onShellAction} client={client} />
        </MemoryRouter>
      </ProjectSelectionProvider>
    </AppProviders>
  );
  return { onClose, onShellAction, port, client };
}

describe('CommandPalette', () => {
  it('abre com o campo focado e todas as telas à escolha', () => {
    renderPalette();

    const field = screen.getByRole('combobox', { name: 'Tela, projeto, comando ou pedido' });
    expect(field).toHaveFocus();
    expect(screen.getByRole('option', { name: /^Padrões do projeto/ })).toBeInTheDocument();
    expect(screen.getByRole('option', { name: /^Portfólio/ })).toBeInTheDocument();
    // A aba vira opção com o nome dela, então o nome que a tela tinha no menu continua achável.
    expect(screen.getByRole('option', { name: /^Auditoria/ })).toBeInTheDocument();
    expect(screen.getByRole('option', { name: /^Da ideia ao projeto/ })).toBeInTheDocument();
  });

  it('filtra ao digitar, sem exigir acento', async () => {
    renderPalette();

    await userEvent.type(screen.getByRole('combobox'), 'catalogo');

    expect(screen.getAllByRole('option')).toHaveLength(1);
    expect(screen.getByRole('option', { name: /Catálogo/ })).toBeInTheDocument();
  });

  it('diz quando nada combina, em vez de mostrar uma lista vazia', async () => {
    renderPalette();

    await userEvent.type(screen.getByRole('combobox'), 'zzz');

    expect(screen.queryByRole('option')).not.toBeInTheDocument();
    expect(screen.getByRole('status')).toHaveTextContent('Nada encontrado para "zzz".');
  });

  it('Enter abre a opção ativa e fecha a paleta', async () => {
    const { onClose } = renderPalette();

    await userEvent.type(screen.getByRole('combobox'), 'harness{Enter}');

    expect(screen.getByTestId('where')).toHaveTextContent('/projeto/harness');
    expect(onClose).toHaveBeenCalledTimes(1);
  });

  it('as setas mudam a opção ativa, que o campo aponta por aria-activedescendant', async () => {
    renderPalette();
    const field = screen.getByRole('combobox');
    const [first, second] = screen.getAllByRole('option');
    expect(first).toHaveAttribute('aria-selected', 'true');

    await userEvent.keyboard('{ArrowDown}');

    expect(second).toHaveAttribute('aria-selected', 'true');
    expect(field).toHaveAttribute('aria-activedescendant', second!.id);
    await userEvent.keyboard('{ArrowUp}{ArrowUp}');
    expect(screen.getAllByRole('option').at(-1)).toHaveAttribute('aria-selected', 'true');
  });

  it('o clique também abre a opção', async () => {
    renderPalette();

    await userEvent.click(screen.getByRole('option', { name: /Auditoria/ }));

    expect(screen.getByTestId('where')).toHaveTextContent('/auditoria');
  });

  it('oferece os projetos recentes e abre o escolhido, sem mandar caminho', async () => {
    const openRecent = vi.fn().mockResolvedValue({ projectId: 'p', displayName: 'loja-api', displayPath: '~' });
    renderPalette({ recents: vi.fn().mockResolvedValue([recentProject]), openRecent });

    await userEvent.type(screen.getByRole('combobox'), 'dev loja');
    await userEvent.click(await screen.findByRole('option', { name: /loja-api/ }));

    expect(openRecent).toHaveBeenCalledWith('r-1');
  });

  it('agrupa o que oferece: ações do aplicativo, telas e projetos', async () => {
    renderPalette({ recents: vi.fn().mockResolvedValue([recentProject]) });

    const actions = screen.getByRole('group', { name: 'Ações' });
    expect(within(actions).getByRole('option', { name: /Abrir pasta de projeto/ })).toBeInTheDocument();
    expect(
      within(screen.getByRole('group', { name: 'Telas' })).getByRole('option', { name: /^Portfólio/ })
    ).toBeInTheDocument();
    expect(
      within(await screen.findByRole('group', { name: 'Projetos' })).getByRole('option', { name: /loja-api/ })
    ).toBeInTheDocument();
  });

  it('oferece as tarefas e as execuções recentes, o que espera a pessoa primeiro', async () => {
    renderPalette({}, engine({ tasks: [run('run-t', 'awaiting_approval')], recent: [run('run-r', 'completed')] }));

    const activity = await screen.findByRole('group', { name: 'Atividade' });

    const options = within(activity).getAllByRole('option');
    expect(options).toHaveLength(2);
    expect(options[0]).toHaveTextContent('Validação · loja-api');
    expect(options[0]).toHaveTextContent('Aguardando aprovação');
    expect(options[1]).toHaveTextContent('Concluído');
  });

  it('acha a execução pelo estado e abre o detalhe dela pelo endereço, sem texto na URL', async () => {
    renderPalette({}, engine({ tasks: [run('run-t', 'awaiting_approval')] }));

    await userEvent.type(screen.getByRole('combobox'), 'tarefa pendente');
    await userEvent.click(await screen.findByRole('option', { name: /Validação · loja-api/ }));

    expect(screen.getByTestId('where')).toHaveTextContent('/execucao/run-t');
    expect(screen.getByTestId('handoff')).toBeEmptyDOMElement();
  });

  it('sem resposta do motor, a paleta segue sem o grupo de atividade', async () => {
    const { client } = renderPalette({}, engine({ down: true }));

    await waitFor(() => expect(client.request).toHaveBeenCalled());

    expect(screen.queryByRole('group', { name: 'Atividade' })).not.toBeInTheDocument();
    expect(screen.getByRole('option', { name: /^Portfólio/ })).toBeInTheDocument();
  });

  it('acha a tela pelo nome técnico e diz para que ela serve', async () => {
    renderPalette();

    await userEvent.type(screen.getByRole('combobox'), 'harness');

    const option = screen.getByRole('option', { name: /^Padrões do projeto/ });
    expect(option).toHaveTextContent('Regras, verificações e assistentes de IA aplicados ao projeto.');
  });

  it('uma ação do aplicativo é entregue ao shell', async () => {
    const { onShellAction, onClose } = renderPalette();

    await userEvent.type(screen.getByRole('combobox'), 'terminal{Enter}');

    expect(onShellAction).toHaveBeenCalledWith('terminal');
    expect(onClose).toHaveBeenCalled();
  });

  it('abrir pasta de projeto chama o seletor de pasta', async () => {
    const { port } = renderPalette();

    await userEvent.type(screen.getByRole('combobox'), 'abrir pasta{Enter}');

    expect(port.pick).toHaveBeenCalled();
  });

  it('uma frase vira pedido: oferece os caminhos, o mais provável primeiro, e as telas relacionadas', async () => {
    renderPalette();

    await userEvent.type(screen.getByRole('combobox'), 'mostre os riscos dos meus projetos');

    const ask = screen.getByRole('group', { name: 'Pedir ao Spock Studio' });
    // A frase é um pedido: o caminho mais provável já vem marcado, e Enter o segue.
    const first = screen.getAllByRole('option')[0]!;
    expect(first).toHaveAttribute('aria-selected', 'true');
    expect(first).toHaveTextContent('Pergunta sobre o andamento');
    expect(
      within(ask)
        .getAllByRole('option')
        .map((option) => option.textContent)
    ).toEqual([
      expect.stringContaining('Pergunta sobre o andamento'),
      expect.stringContaining('Pedido de engenharia'),
      expect.stringContaining('Pergunta sobre o código'),
      expect.stringContaining('Nova ideia')
    ]);
    expect(
      within(screen.getByRole('group', { name: 'Telas' })).getByRole('option', { name: /^Portfólio/ })
    ).toBeInTheDocument();
  });

  it('uma frase que é o nome de um comando continua sendo esse comando', async () => {
    const { port } = renderPalette();

    await userEvent.type(screen.getByRole('combobox'), 'abrir pasta de projeto{Enter}');

    expect(port.pick).toHaveBeenCalled();
    expect(screen.getByTestId('where')).toHaveTextContent(/^\/$/);
  });

  it('escolher um caminho leva o texto à tela que o trata, depois de garantir um projeto', async () => {
    const project = { projectId: 'p', displayName: 'loja-api', displayPath: '~' };
    const { port } = renderPalette({ pick: vi.fn().mockResolvedValue(project) });

    await userEvent.type(screen.getByRole('combobox'), 'trocar o contrato de autenticação');
    await userEvent.click(screen.getByRole('option', { name: /^Pedido de engenharia/ }));

    expect(port.pick).toHaveBeenCalled();
    await waitFor(() => expect(screen.getByTestId('where')).toHaveTextContent('/pedido'));
    expect(screen.getByTestId('handoff')).toHaveTextContent('trocar o contrato de autenticação');
  });

  it('sem projeto e sem escolher uma pasta, o pedido não segue', async () => {
    renderPalette();

    await userEvent.type(screen.getByRole('combobox'), 'trocar o contrato de autenticação');
    await userEvent.click(screen.getByRole('option', { name: /^Pedido de engenharia/ }));

    expect(screen.getByTestId('where')).toHaveTextContent(/^\/$/);
  });

  it('Esc fecha', async () => {
    const { onClose } = renderPalette();

    await userEvent.keyboard('{Escape}');

    expect(onClose).toHaveBeenCalled();
  });

  it('não tem violações estruturais de acessibilidade', async () => {
    renderPalette();

    await expectNoA11yViolations(document.body);
  });
});
