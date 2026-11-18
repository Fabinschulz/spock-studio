import type { Discovery } from '@/shared/schema';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';

import { ModelStep } from './model-step';

const discovery = (
  state: Discovery['specification']['state'],
  version: string | null = null,
  declaredAiEnvironments: string[] = []
): Discovery => ({
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
  hasDeclaredConfig: declaredAiEnvironments.length > 0,
  declaredAiEnvironments,
  warnings: []
});

function renderStep(over: Partial<Parameters<typeof ModelStep>[0]> = {}) {
  const props = {
    discovery: discovery('not_installed'),
    aiEnvironments: [] as readonly string[],
    allowReinstall: false,
    updateSpecification: false,
    plan: { status: 'idle' } as const,
    onToggleEnvironment: vi.fn(),
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

  it('marcar e desmarcar IDEs avisa a jornada, uma de cada vez', async () => {
    const props = renderStep({ aiEnvironments: ['claude-code'] });
    expect((screen.getByRole('checkbox', { name: /Claude Code/ }) as HTMLInputElement).checked).toBe(true);
    await userEvent.click(screen.getByRole('checkbox', { name: /Cursor/ }));
    await userEvent.click(screen.getByRole('checkbox', { name: /Claude Code/ }));
    expect(props.onToggleEnvironment).toHaveBeenNthCalledWith(1, 'cursor');
    expect(props.onToggleEnvironment).toHaveBeenNthCalledWith(2, 'claude-code');
  });

  it('com ambiente escolhido, gerar o plano chama a jornada', async () => {
    const props = renderStep({ aiEnvironments: ['claude-code', 'codex'] });
    await userEvent.click(screen.getByRole('button', { name: 'Gerar plano' }));
    expect(props.onPlan).toHaveBeenCalledTimes(1);
  });

  it('num projeto configurado, o Claude Code não sai e a IDE desmarcada avisa o que o plano vai tirar', () => {
    renderStep({
      discovery: discovery('installed', '4.0.0', ['claude-code', 'cursor']),
      aiEnvironments: ['claude-code']
    });

    expect((screen.getByRole('checkbox', { name: /Claude Code/ }) as HTMLInputElement).disabled).toBe(true);
    expect((screen.getByRole('checkbox', { name: /Cursor/ }) as HTMLInputElement).disabled).toBe(false);
    expect(screen.getAllByText(/declarado/)).toHaveLength(2);
    expect(screen.getByRole('status').textContent).toMatch(/Cursor sai da lista/);
  });

  it('estrutura alterada pede permissão explícita para reinstalar', async () => {
    const props = renderStep({ discovery: discovery('drifted', '4.0.0'), aiEnvironments: ['claude-code'] });

    expect(screen.getByText(/Versão 4.0.0, validada/)).toBeTruthy();
    await userEvent.click(screen.getByRole('checkbox', { name: /Permitir reinstalar/ }));
    expect(props.onReinstall).toHaveBeenCalledWith(true);
  });
});
