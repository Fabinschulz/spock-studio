import type { ConfigurationPlan, FileChange } from '@/shared/schema';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';

import { AppProviders } from '@/shared/components';

import { ReviewStep, changesSummary } from './review-step';

const change = (over: Partial<FileChange>): FileChange =>
  ({
    id: 'c1',
    path: '.claude/agents/harness-engineer.md',
    kind: 'agent',
    action: 'create',
    origin: 'baseline-rules',
    reason: 'Agente que planeja a configuração',
    destructive: false,
    ...over
  }) as FileChange;

const plan = (changes: FileChange[], over: Partial<ConfigurationPlan> = {}) =>
  ({
    changes,
    commands: [],
    validations: [],
    decisions: [],
    limitations: [],
    observations: [],
    risks: [],
    proposalOrigin: 'baseline-rules',
    ...over
  }) as unknown as ConfigurationPlan;

const renderStep = (p: ConfigurationPlan, props: Partial<Parameters<typeof ReviewStep>[0]> = {}) =>
  render(
    <ReviewStep
      plan={p}
      approvedChanges={[]}
      approvedCommands={[]}
      resolutions={{}}
      approvalCount={0}
      onToggleChange={vi.fn()}
      onToggleCommand={vi.fn()}
      onResolution={vi.fn()}
      onBack={vi.fn()}
      onCancel={vi.fn()}
      onApply={vi.fn()}
      {...props}
    />,
    // O TabBar da biblioteca lê a paleta do tema.
    { wrapper: AppProviders }
  );

describe('ReviewStep', () => {
  it('resume o plano só com o que existe, sem listar zeros', () => {
    expect(
      changesSummary([
        change({}),
        change({ id: 'b' }),
        change({ id: 'c', action: 'conflict' }),
        change({ id: 'd', action: 'preserve' })
      ])
    ).toBe('2 a criar · 1 conflito · 1 preservado');
    expect(changesSummary([])).toBe('Nenhuma alteração no plano');
  });

  it('cancelar pede confirmação antes de descartar o plano e as aprovações', async () => {
    const onCancel = vi.fn();
    renderStep(plan([change({})]), { onCancel });

    await userEvent.click(screen.getByRole('button', { name: 'Cancelar sem aplicar' }));
    expect(onCancel).not.toHaveBeenCalled();
    await userEvent.click(screen.getByRole('button', { name: 'Continuar revisando' }));
    expect(onCancel).not.toHaveBeenCalled();

    await userEvent.click(screen.getByRole('button', { name: 'Cancelar sem aplicar' }));
    await userEvent.click(screen.getByRole('button', { name: 'Descartar o plano' }));
    expect(onCancel).toHaveBeenCalledTimes(1);
  });

  it('cada arquivo é uma linha com a aprovação, o estado em texto e o motivo', async () => {
    const onToggleChange = vi.fn();
    renderStep(plan([change({})]), { onToggleChange });

    expect(screen.getByText('Criar')).toBeTruthy();
    expect(screen.getByText('Agente que planeja a configuração')).toBeTruthy();
    await userEvent.click(screen.getByRole('checkbox', { name: '.claude/agents/harness-engineer.md' }));
    expect(onToggleChange).toHaveBeenCalledWith('c1');
  });

  it('destrutivo aparece em texto e as diferenças ficam recolhidas', async () => {
    const { container } = renderStep(plan([change({ action: 'remove', destructive: true, diff: '-a\n+b' })]));

    expect(screen.getByText('Remover · destrutivo')).toBeTruthy();
    const details = container.querySelector('details')!;
    expect(details.open).toBe(false);
    await userEvent.click(screen.getByText('Ver diferenças'));
    expect(details.open).toBe(true);
  });

  it('conflito não tem caixa de aprovação: a decisão é manter ou substituir, e manter é o padrão', async () => {
    const onResolution = vi.fn();
    renderStep(plan([change({ action: 'conflict', conflictReason: 'Você editou o arquivo' })]), { onResolution });

    expect(screen.queryByRole('checkbox')).toBeNull();
    expect(screen.getByText('Conflito')).toBeTruthy();
    expect(screen.getByText('Você editou o arquivo')).toBeTruthy();
    expect((screen.getByRole('radio', { name: /Manter o meu arquivo/ }) as HTMLInputElement).checked).toBe(true);
    await userEvent.click(screen.getByRole('radio', { name: /Substituir pelo proposto/ }));
    expect(onResolution).toHaveBeenCalledWith('c1', 'overwrite');
  });

  it('comando exige aprovação própria e mostra o que executa e o que toca', async () => {
    const onToggleCommand = vi.fn();
    renderStep(
      plan([], {
        commands: [
          {
            id: 'cmd1',
            description: 'Instalar a estrutura',
            executable: 'npx',
            arguments: ['spec', 'init'],
            effects: ['process', 'network'],
            affectedPaths: ['.specs'],
            destructive: false
          }
        ] as unknown as ConfigurationPlan['commands']
      }),
      { onToggleCommand }
    );

    expect(screen.getByText('Executa um programa · Acessa a rede')).toBeTruthy();
    expect(screen.getByText('npx spec init')).toBeTruthy();
    await userEvent.click(screen.getByRole('checkbox', { name: 'Aprovar a execução: Instalar a estrutura' }));
    expect(onToggleCommand).toHaveBeenCalledWith('cmd1');
  });

  it('aplicar fica desabilitado sem aprovação e o rótulo conta os aprovados', () => {
    const { rerender } = renderStep(plan([change({})]));
    expect((screen.getByRole('button', { name: 'Nada aprovado para aplicar' }) as HTMLButtonElement).disabled).toBe(
      true
    );

    rerender(
      <ReviewStep
        plan={plan([change({})])}
        approvedChanges={['c1']}
        approvedCommands={[]}
        resolutions={{}}
        approvalCount={1}
        onToggleChange={vi.fn()}
        onToggleCommand={vi.fn()}
        onResolution={vi.fn()}
        onBack={vi.fn()}
        onCancel={vi.fn()}
        onApply={vi.fn()}
      />
    );
    expect(
      (screen.getByRole('button', { name: 'Aplicar configuração (1 aprovado)' }) as HTMLButtonElement).disabled
    ).toBe(false);
  });

  it('arquivos agrupados por tipo, com aprovar tudo ou nada no grupo; conflito fica fora do lote', async () => {
    const onSetChanges = vi.fn();
    renderStep(
      plan([
        change({ id: 'a1' }),
        change({ id: 'a2', path: '.claude/agents/verifier.md' }),
        change({ id: 'a3', path: '.claude/agents/revisor.md', action: 'conflict' }),
        change({ id: 'r1', kind: 'rule', path: '.claude/rules/geral.md' })
      ]),
      { approvedChanges: ['a1'], onSetChanges }
    );

    const agents = screen.getByRole('checkbox', { name: 'Agentes (3)' }) as HTMLInputElement;
    expect(agents.checked).toBe(false);
    expect(agents.dataset.indeterminate ?? agents.indeterminate.toString()).toBe('true');
    expect(screen.getByRole('checkbox', { name: 'Regras (1)' })).toBeTruthy();

    await userEvent.click(agents);
    expect(onSetChanges).toHaveBeenCalledWith(['a1', 'a2'], true);
  });

  it('o comando da estrutura de especificação diz o que acontece sem aprovar', () => {
    renderStep(
      plan([], {
        commands: [
          {
            id: 'specification-install',
            description: 'Instalar a estrutura de especificação',
            executable: 'npx',
            arguments: [],
            effects: ['network'],
            affectedPaths: [],
            destructive: false
          }
        ] as unknown as ConfigurationPlan['commands']
      })
    );
    expect(screen.getByText(/Sem aprovar, a estrutura de especificação não é instalada/)).toBeTruthy();
  });

  it('Por quê agrupa as decisões: bloqueadas e as que entram abertas, as que não se aplicam recolhidas', async () => {
    const decision = (componentId: string, value: string, justification: string) => ({
      componentId,
      kind: 'rule',
      decision: value,
      origin: 'baseline-rules',
      justification,
      evidence: []
    });
    renderStep(
      plan([change({})], {
        decisions: [
          decision('a', 'enable', 'Stack .NET detectada.'),
          decision('b', 'skip', 'Nenhuma stack React.'),
          decision('c', 'blocked', 'Falta o package.json.')
        ] as unknown as ConfigurationPlan['decisions']
      })
    );

    await userEvent.click(screen.getByRole('tab', { name: 'Por quê (1 bloqueada)' }));
    expect(screen.getByText('Origem do plano: as regras-base do Spock.')).toBeTruthy();
    const group = (name: string) => screen.getByText(name).closest('details')!;
    expect(group('1 Bloqueada').open).toBe(true);
    expect(group('Entram (1)').open).toBe(true);
    expect(group('Não se aplicam (1)').open).toBe(false);
    expect(screen.getByText('Stack .NET detectada.')).toBeTruthy();
  });

  it('Riscos junta limitações, observações e riscos num lugar só', async () => {
    renderStep(
      plan([change({})], {
        limitations: [{ code: 'X', message: 'sem proposta do agente' }],
        observations: [{ kind: 'inference', text: 'Usa .NET', evidence: [] }],
        risks: ['pode sobrescrever regras']
      } as unknown as Partial<ConfigurationPlan>)
    );

    await userEvent.click(screen.getByRole('tab', { name: 'Riscos (3)' }));
    expect(screen.getByText('sem proposta do agente')).toBeTruthy();
    expect(screen.getByText('Inferência')).toBeTruthy();
    expect(screen.getByText('pode sobrescrever regras')).toBeTruthy();
  });
});
