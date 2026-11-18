import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';

import { AppProviders } from '@/shared/components';
import type { RunResult } from '@/shared/schema';

import { ResultStep } from './result-step';

const apply: RunResult = {
  runId: 'run-aaaaaaaaaaaa',
  projectId: 'p-aaaaaaaaaaaa',
  kind: 'apply',
  outcome: 'completed',
  rollbackAvailable: true
};

function renderStep(onRevert = vi.fn()) {
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
      />
    </AppProviders>
  );
  return onRevert;
}

describe('ResultStep · reverter', () => {
  it('pede confirmação na própria tela, com o foco na opção segura, antes de reverter', async () => {
    const onRevert = renderStep();

    await userEvent.click(screen.getByRole('button', { name: 'Reverter alterações' }));

    const confirm = screen.getByRole('group', { name: 'Reverter as alterações' });
    expect(confirm).toHaveTextContent('Arquivos que você editou depois da aplicação não são sobrescritos');
    expect(screen.getByRole('button', { name: 'Não reverter' })).toHaveFocus();
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();

    await userEvent.click(screen.getByRole('button', { name: 'Reverter agora' }));
    expect(onRevert).toHaveBeenCalledWith('run-aaaaaaaaaaaa');
  });

  it('desistir não reverte e devolve o botão', async () => {
    const onRevert = renderStep();

    await userEvent.click(screen.getByRole('button', { name: 'Reverter alterações' }));
    await userEvent.click(screen.getByRole('button', { name: 'Não reverter' }));

    expect(onRevert).not.toHaveBeenCalled();
    expect(screen.getByRole('button', { name: 'Reverter alterações' })).toBeInTheDocument();
  });
});
