import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { Button } from '@mariliz/ui-kit';
import { describe, expect, it, vi } from 'vitest';

import { AppProviders } from '../../../providers';
import { ConfirmGate } from './ConfirmGate';

function renderGate() {
  const onConfirm = vi.fn();
  render(
    <AppProviders>
      <ConfirmGate
        label="Desvincular a conexão"
        confirmLabel="Desvincular"
        message="O projeto deixa de ler desta conexão."
        onConfirm={onConfirm}
      >
        {(ask) => (
          <Button type="button" onClick={ask}>
            Desvincular conexão
          </Button>
        )}
      </ConfirmGate>
    </AppProviders>
  );
  return { onConfirm };
}

describe('ConfirmGate', () => {
  it('o botão abre a confirmação no lugar dele e desistir volta sem agir', async () => {
    const { onConfirm } = renderGate();

    await userEvent.click(screen.getByRole('button', { name: 'Desvincular conexão' }));
    expect(screen.getByRole('group', { name: 'Desvincular a conexão' })).toHaveTextContent('deixa de ler');
    expect(screen.queryByRole('button', { name: 'Desvincular conexão' })).not.toBeInTheDocument();

    await userEvent.click(screen.getByRole('button', { name: 'Manter' }));
    expect(screen.getByRole('button', { name: 'Desvincular conexão' })).toBeInTheDocument();
    expect(onConfirm).not.toHaveBeenCalled();
  });

  it('confirmar fecha a confirmação e então age', async () => {
    const { onConfirm } = renderGate();

    await userEvent.click(screen.getByRole('button', { name: 'Desvincular conexão' }));
    await userEvent.click(screen.getByRole('button', { name: 'Desvincular' }));

    expect(onConfirm).toHaveBeenCalledOnce();
    expect(screen.queryByRole('group', { name: 'Desvincular a conexão' })).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Desvincular conexão' })).toBeInTheDocument();
  });
});
