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
  naming: {
    declaration: 'spock.yaml',
    lock: 'spock.lock',
    stateDirectory: '.spock',
    proposal: '.spock/proposal.json',
    proposalSchema: '.spock/schemas/harness-proposal.v1.schema.json',
    harnessEngineer: 'spock-harness-engineer',
    harnessEngineerAgent: '.claude/agents/spock-harness-engineer.md'
  },
  warnings: []
});

function renderStep(over: Partial<Parameters<typeof ModelStep>[0]> = {}) {
  const props = {
    discovery: discovery('not_installed'),
    aiEnvironments: [] as readonly string[],
    allowReinstall: false,
    updateSpecification: false,
    installSpecification: false,
    plan: { status: 'idle' } as const,
    onToggleEnvironment: vi.fn(),
    onReinstall: vi.fn(),
    onUpdateSpecification: vi.fn(),
    onInstallSpecification: vi.fn(),
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
    expect(screen.getByText('Escolha ao menos um ambiente de IA para gerar o plano.')).toBeTruthy();
  });

  it('a frase sobre o instalador aparece uma vez só e a lista fixa do que seria configurado não volta', () => {
    renderStep();
    expect(screen.getAllByText(/instalador oficial/)).toHaveLength(1);
    expect(screen.queryByRole('heading', { name: 'O que será configurado' })).toBeNull();
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
    expect(screen.queryByText(/Escolha ao menos um ambiente/)).toBeNull();
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

  it('a escolha obrigatória vem primeiro, na etapa que leva o nome dela', () => {
    renderStep();

    expect(screen.getByRole('heading', { level: 2, name: 'Ambiente de IA' })).toBeTruthy();
    const headings = screen.getAllByRole('heading', { level: 3 }).map((heading) => heading.textContent);
    expect(headings.indexOf('Ambientes de IA')).toBeLessThan(headings.indexOf('Estrutura de especificação'));
  });

  it('sem a estrutura instalada, não instalar é o padrão e cada opção diz a consequência', async () => {
    const props = renderStep();

    const skip = screen.getByRole('radio', { name: 'Não instalar agora' });
    expect(skip).toBeChecked();
    expect(skip).toHaveAccessibleDescription(/verificações que dependem dela ficam bloqueadas/);
    const install = screen.getByRole('radio', { name: 'Instalar agora' });
    expect(install).toHaveAccessibleDescription(/usa a rede.*vem marcado na revisão/);
    expect(screen.getByText('O que a instalação precisa (falta Node.js)')).toBeTruthy();

    await userEvent.click(install);
    expect(props.onInstallSpecification).toHaveBeenCalledWith(true);
  });

  it('a nota do ambiente parcial explica o que ele recebe, fora do nome da caixa', () => {
    renderStep();

    const cursor = screen.getByRole('checkbox', { name: /Cursor/ });
    expect(cursor).toHaveAccessibleName(expect.not.stringMatching(/skill/));
    expect(cursor).toHaveAccessibleDescription(/ligação com a IDE e a skill do contrato/);
  });

  it('estrutura alterada pede permissão explícita para reinstalar', async () => {
    const props = renderStep({ discovery: discovery('drifted', '4.0.0'), aiEnvironments: ['claude-code'] });

    expect(screen.getByText(/Versão 4.0.0, validada/)).toBeTruthy();
    await userEvent.click(screen.getByRole('checkbox', { name: /Permitir reinstalar/ }));
    expect(props.onReinstall).toHaveBeenCalledWith(true);
  });
});
