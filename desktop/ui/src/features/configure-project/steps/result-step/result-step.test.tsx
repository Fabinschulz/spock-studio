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

const renderStep = (onRevert = vi.fn()) => {
  const apply = { ...runResultSchema.parse(applyResult), rollbackAvailable: true };
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
});
