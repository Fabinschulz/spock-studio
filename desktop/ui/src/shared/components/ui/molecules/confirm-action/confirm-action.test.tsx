import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type { ComponentProps } from 'react';
import { describe, expect, it, vi } from 'vitest';

import { expectNoA11yViolations } from '@/test/a11y';

import { AppProviders } from '../../../providers';
import { ConfirmAction } from './ConfirmAction';

function renderConfirm(props: Partial<ComponentProps<typeof ConfirmAction>> = {}) {
  const onConfirm = vi.fn();
  const onCancel = vi.fn();
  const view = render(
    <AppProviders>
      <ConfirmAction
        label="Remover GitHub da equipe"
        confirmLabel="Remover conexão"
        onConfirm={onConfirm}
        onCancel={onCancel}
        {...props}
      >
        A credencial será apagada deste computador.
      </ConfirmAction>
    </AppProviders>
  );
  return { onConfirm, onCancel, ...view };
}

describe('ConfirmAction', () => {
  it('é um grupo com o nome do que se confirma e diz o que vai acontecer', () => {
    renderConfirm();

    const group = screen.getByRole('group', { name: 'Remover GitHub da equipe' });
    expect(group).toHaveTextContent('A credencial será apagada deste computador.');
  });

  it('o foco vai para a opção segura ao aparecer, e Enter ali nunca apaga nada', async () => {
    const { onConfirm, onCancel } = renderConfirm();

    expect(screen.getByRole('button', { name: 'Manter' })).toHaveFocus();
    await userEvent.keyboard('{Enter}');

    expect(onCancel).toHaveBeenCalledTimes(1);
    expect(onConfirm).not.toHaveBeenCalled();
  });

  it('só o botão de perigo confirma', async () => {
    const { onConfirm, onCancel } = renderConfirm();

    await userEvent.click(screen.getByRole('button', { name: 'Remover conexão' }));

    expect(onConfirm).toHaveBeenCalledTimes(1);
    expect(onCancel).not.toHaveBeenCalled();
  });

  it('Esc desiste, de qualquer botão de dentro', async () => {
    const { onConfirm, onCancel } = renderConfirm();
    screen.getByRole('button', { name: 'Remover conexão' }).focus();

    await userEvent.keyboard('{Escape}');

    expect(onCancel).toHaveBeenCalledTimes(1);
    expect(onConfirm).not.toHaveBeenCalled();
  });

  it('o texto de desistir pode ser outro', () => {
    renderConfirm({ cancelLabel: 'Voltar' });

    expect(screen.getByRole('button', { name: 'Voltar' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Manter' })).not.toBeInTheDocument();
  });

  it('ocupado, os dois botões ficam desligados', () => {
    renderConfirm({ busy: true });

    expect(screen.getByRole('button', { name: 'Remover conexão' })).toBeDisabled();
    expect(screen.getByRole('button', { name: 'Manter' })).toBeDisabled();
  });

  it('não tem violações estruturais de acessibilidade', async () => {
    const { container } = renderConfirm();

    await expectNoA11yViolations(container);
  });
});
