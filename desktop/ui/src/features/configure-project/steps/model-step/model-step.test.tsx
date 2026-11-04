import type { Discovery } from '@/shared/schema';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';

import { ModelStep } from './model-step';

const discovery = (state: Discovery['specification']['state'], version: string | null = null): Discovery => ({
  projectId: 'p-aaaaaaaaaaaa',
  projectName: 'Loja',
  stacks: [],
  tooling: [],
  aiEnvironments: [{ id: 'claude-code', name: 'Claude Code', supported: true, confidence: 'high', evidence: [] }],
  inventory: { agents: [], skills: [], hooks: [], rules: [], mcpServers: [], evals: [] },
  specification: {
    state,
    version,
    lockedHash: null,
    computedHash: null,
    versionTested: version !== null,
    problems: []
  },
  git: { isRepository: true, hasUncommittedChanges: false, changedPaths: [] },
  tools: { git: '2.45', node: null, python: '3.12', pythonCommand: 'python' },
  hasDeclaredConfig: false,
  warnings: []
});

function renderStep(over: Partial<Parameters<typeof ModelStep>[0]> = {}) {
  const props = {
    discovery: discovery('not_installed'),
    aiEnvironment: null,
    allowReinstall: false,
    updateSpecification: false,
    plan: { status: 'idle' } as const,
    onEnvironment: vi.fn(),
    onReinstall: vi.fn(),
    onUpdateSpecification: vi.fn(),
    onBack: vi.fn(),
    onPlan: vi.fn(),
    ...over
  };
  render(<ModelStep {...props} />);
  return props;
}

describe('ModelStep', () => {
  it('diz o que falta em texto e só libera o plano com um ambiente escolhido', () => {
    renderStep();

    expect(screen.getByText(/Node.js: não encontrado/)).toBeTruthy();
    expect(screen.getByText('ausente')).toBeTruthy();
    expect(screen.getByText('detectado')).toBeTruthy();
    expect((screen.getByRole('button', { name: 'Gerar plano' }) as HTMLButtonElement).disabled).toBe(true);
  });

  it('escolher um ambiente avisa a jornada', async () => {
    const props = renderStep();
    await userEvent.click(screen.getByRole('radio', { name: /Claude Code/ }));
    expect(props.onEnvironment).toHaveBeenCalledWith('claude-code');
  });

  it('com ambiente escolhido, gerar o plano chama a jornada', async () => {
    const props = renderStep({ aiEnvironment: 'claude-code' });
    await userEvent.click(screen.getByRole('button', { name: 'Gerar plano' }));
    expect(props.onPlan).toHaveBeenCalledTimes(1);
  });

  it('estrutura alterada pede permissão explícita para reinstalar', async () => {
    const props = renderStep({ discovery: discovery('drifted', '4.0.0'), aiEnvironment: 'claude-code' });

    expect(screen.getByText(/Versão 4.0.0, validada/)).toBeTruthy();
    await userEvent.click(screen.getByRole('checkbox', { name: /Permitir reinstalar/ }));
    expect(props.onReinstall).toHaveBeenCalledWith(true);
  });
});
