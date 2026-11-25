import applyResult from '@contracts/fixtures/golden/apply-result.json';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';

import { AppProviders } from '@/shared/components';
import { runResultSchema, type HarnessNames } from '@/shared/schema';

import { ResultStep } from './result-step';

const naming: HarnessNames = {
  declaration: 'spock.yaml',
  lock: 'spock.lock',
  stateDirectory: '.spock',
  proposal: '.spock/proposal.json',
  proposalSchema: '.spock/schemas/harness-proposal.v1.schema.json',
  harnessEngineer: 'spock-harness-engineer',
  harnessEngineerAgent: '.claude/agents/spock-harness-engineer.md'
};

const renderStep = (onRevert = vi.fn(), over: Partial<Parameters<typeof ResultStep>[0]> = {}) => {
  const apply = { ...runResultSchema.parse(applyResult), rollbackAvailable: true, limitations: [] };
  render(
    <AppProviders>
      <ResultStep
        results={{ apply, revert: null, validate: null }}
        run={null}
        onRevert={onRevert}
        onValidate={vi.fn()}
        onDone={vi.fn()}
        onReplan={vi.fn()}
        replan={{ status: 'idle' }}
        naming={naming}
        {...over}
      />
    </AppProviders>
  );
  return apply;
};

describe('ResultStep', () => {
  it('reverter pede confirmação na própria página, com o foco na opção que não reverte', async () => {
    const onRevert = vi.fn();
    const apply = renderStep(onRevert);

    await userEvent.click(screen.getByRole('button', { name: 'Reverter alterações' }));

    expect(screen.getByRole('group', { name: 'Reverter as alterações' })).toBeInTheDocument();
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Não reverter' })).toHaveFocus();

    await userEvent.click(screen.getByRole('button', { name: 'Reverter agora' }));
    expect(onRevert).toHaveBeenCalledExactlyOnceWith(apply.runId);
    expect(screen.queryByRole('group', { name: 'Reverter as alterações' })).not.toBeInTheDocument();
  });

  it('desistir fecha a confirmação sem reverter', async () => {
    const onRevert = vi.fn();
    renderStep(onRevert);

    await userEvent.click(screen.getByRole('button', { name: 'Reverter alterações' }));
    await userEvent.click(screen.getByRole('button', { name: 'Não reverter' }));

    expect(onRevert).not.toHaveBeenCalled();
    expect(screen.queryByRole('group', { name: 'Reverter as alterações' })).not.toBeInTheDocument();
  });

  it('instalador sem aprovação: o alerta diz a causa e a saída vira o único botão principal', async () => {
    const onInstallSpecification = vi.fn();
    renderStep(vi.fn(), { specificationSkipped: true, onInstallSpecification });

    expect(screen.getByText(/Causa: a estrutura de especificação não foi instalada/)).toBeInTheDocument();
    const exit = screen.getByRole('button', { name: 'Instalar a estrutura e gerar o plano de novo' });
    expect(exit).toHaveClass('MuiButton-contained');
    expect(screen.getByRole('button', { name: 'Concluir' })).toHaveClass('MuiButton-outlined');

    await userEvent.click(exit);
    expect(onInstallSpecification).toHaveBeenCalledTimes(1);
  });

  it('sem nada pendente, Concluir é o principal e não há causa no alerta', () => {
    renderStep();

    expect(screen.queryByText(/^Causa:/)).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Concluir' })).toHaveClass('MuiButton-contained');
  });
});
