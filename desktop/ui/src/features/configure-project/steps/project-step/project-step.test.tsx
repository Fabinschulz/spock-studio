import discoveryFixture from '@contracts/fixtures/golden/discovery.json';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router';
import { describe, expect, it, vi } from 'vitest';

import { ProjectSelectionProvider, ProjectSwitcher } from '@/features/project-selection';
import { AppProviders } from '@/shared/components';
import { discoverySchema, type HarnessStatus, type RunSummary } from '@/shared/schema';

import { ProjectStep } from './project-step';

const project = { name: 'Loja', path: '~\\Loja' };
const discovery = {
  ...discoverySchema.parse(discoveryFixture),
  hasDeclaredConfig: false,
  inventory: { agents: [], skills: [], hooks: [], rules: [], mcpServers: [], evals: [] }
};

function renderStep(props: Partial<Parameters<typeof ProjectStep>[0]>) {
  render(
    <AppProviders>
      <MemoryRouter>
        <ProjectSelectionProvider picker={{ pick: vi.fn() }}>
          <ProjectStep
            project={null}
            discovery={{ status: 'idle' }}
            onAnalyze={vi.fn()}
            onContinue={vi.fn()}
            {...props}
          />
        </ProjectSelectionProvider>
      </MemoryRouter>
    </AppProviders>
  );
  return within(screen.getByRole('group', { name: 'Ações da etapa' }));
}

describe('ProjectStep · por que ainda não dá para seguir', () => {
  it('sem pasta, pede a pasta', () => {
    const actions = renderStep({});

    expect(actions.getByRole('button', { name: /Próximo/ })).toBeDisabled();
    expect(actions.getByText('Escolha a pasta do projeto para continuar.')).toBeInTheDocument();
  });

  it('com pasta e sem análise, pede a análise', () => {
    const actions = renderStep({ project });

    expect(actions.getByText('Analise o projeto para continuar.')).toBeInTheDocument();
  });

  it('enquanto analisa, não repete o pedido', () => {
    const actions = renderStep({ project, discovery: { status: 'loading' } });

    expect(actions.queryByText(/para continuar/)).not.toBeInTheDocument();
  });
});

const harnessStatus = (over: Partial<HarnessStatus> = {}): HarnessStatus => ({
  projectId: 'p-1',
  inspectedAt: '2026-11-25T10:00:00Z',
  applied: true,
  lockReadable: true,
  engine: '1.0.0',
  aiEnvironment: 'claude-code',
  specificationVersion: null,
  components: [],
  excluded: [],
  intact: 0,
  kept: 0,
  modified: 0,
  missing: 0,
  ...over
});

const applyRun: RunSummary = {
  runId: 'run-apply',
  kind: 'apply',
  state: 'completed',
  startedAt: '2026-11-25T10:00:00Z',
  changesApplied: true,
  reverted: false
};

describe('ProjectStep · com projeto aberto', () => {
  it('mostra o projeto numa linha, e Trocar abre o seletor do topo em vez de repetir a pasta e os recentes', async () => {
    render(
      <AppProviders>
        <MemoryRouter>
          <ProjectSelectionProvider picker={{ pick: vi.fn() }}>
            <ProjectSwitcher />
            <ProjectStep project={project} discovery={{ status: 'idle' }} onAnalyze={vi.fn()} onContinue={vi.fn()} />
          </ProjectSelectionProvider>
        </MemoryRouter>
      </AppProviders>
    );

    expect(screen.queryByRole('group', { name: 'Pasta do projeto' })).not.toBeInTheDocument();
    expect(within(screen.getByRole('group', { name: 'Projeto' })).getByText(project.path)).toBeInTheDocument();

    await userEvent.click(screen.getByRole('button', { name: 'Trocar o projeto Loja' }));
    expect(screen.getByRole('group', { name: 'Trocar de projeto' })).toBeInTheDocument();
  });

  it('depois da análise, diz de onde o projeto parte e o Próximo passa a ser o único botão principal', () => {
    renderStep({ project, discovery: { status: 'ready', data: discovery } });

    expect(screen.getByRole('status')).toHaveTextContent(/^Começa do zero/);
    const again = screen.getByRole('button', { name: 'Analisar de novo' });
    expect(again).toHaveClass('MuiButton-outlined');
    expect(screen.getByRole('button', { name: /Próximo/ })).toHaveClass('MuiButton-contained');
  });
});

describe('ProjectStep · segunda visita', () => {
  it('resume o que está aplicado e valida sem passar pelo plano', async () => {
    const onValidate = vi.fn();
    renderStep({
      project,
      configured: {
        status: harnessStatus({ components: [{}, {}] as HarnessStatus['components'], intact: 2 }),
        revertible: null
      },
      onValidate
    });

    expect(screen.getByRole('heading', { name: 'Este projeto já está configurado' })).toBeInTheDocument();
    expect(screen.getByText(/2 componentes: 2 íntegros · ambiente de IA: Claude Code/)).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Reverter a última aplicação' })).not.toBeInTheDocument();

    await userEvent.click(screen.getByRole('button', { name: 'Validar de novo' }));
    expect(onValidate).toHaveBeenCalledTimes(1);
  });

  it('reverter a última aplicação pede confirmação e reverte a execução que o motor aceita', async () => {
    const onRevert = vi.fn();
    renderStep({ project, configured: { status: harnessStatus(), revertible: applyRun }, onRevert });

    await userEvent.click(screen.getByRole('button', { name: 'Reverter a última aplicação' }));
    expect(screen.getByRole('button', { name: 'Não reverter' })).toHaveFocus();

    await userEvent.click(screen.getByRole('button', { name: 'Reverter agora' }));
    expect(onRevert).toHaveBeenCalledExactlyOnceWith('run-apply');
  });
});
