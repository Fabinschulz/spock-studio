import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { useState } from 'react';
import { describe, expect, it, vi } from 'vitest';

import { expectNoA11yViolations } from '@/test/a11y';

import { Radio } from './Radio';

function Group() {
  const [value, setValue] = useState('manter');
  return (
    <fieldset>
      <legend>O que fazer com o arquivo</legend>
      <Radio
        name="arquivo"
        value="manter"
        label="Manter o meu"
        checked={value === 'manter'}
        onChange={() => setValue('manter')}
      />
      <Radio
        name="arquivo"
        value="trocar"
        label="Trocar pelo do Spock Studio"
        checked={value === 'trocar'}
        onChange={() => setValue('trocar')}
      />
    </fieldset>
  );
}

describe('Radio', () => {
  it('o rótulo é o nome acessível e o estado vem do `checked`', () => {
    render(<Group />);

    expect(screen.getByRole('radio', { name: 'Manter o meu' })).toBeChecked();
    expect(screen.getByRole('radio', { name: 'Trocar pelo do Spock Studio' })).not.toBeChecked();
  });

  it('clicar no rótulo escolhe a opção e desmarca a outra do mesmo grupo', async () => {
    render(<Group />);

    await userEvent.click(screen.getByText('Trocar pelo do Spock Studio'));

    expect(screen.getByRole('radio', { name: 'Trocar pelo do Spock Studio' })).toBeChecked();
    expect(screen.getByRole('radio', { name: 'Manter o meu' })).not.toBeChecked();
  });

  it('as setas do teclado andam entre as opções do grupo', async () => {
    render(<Group />);
    await userEvent.tab();
    expect(screen.getByRole('radio', { name: 'Manter o meu' })).toHaveFocus();

    await userEvent.keyboard('{ArrowDown}');

    expect(screen.getByRole('radio', { name: 'Trocar pelo do Spock Studio' })).toBeChecked();
  });

  it('o rótulo pode ser mais que texto e recebe os atributos da linha', () => {
    const onChange = vi.fn();
    render(
      <Radio
        name="ambiente"
        checked={false}
        onChange={onChange}
        label={
          <>
            <span>Claude Code</span>
            <span>detectado</span>
          </>
        }
        labelProps={{ className: 'linha', 'aria-disabled': true }}
      />
    );

    const radio = screen.getByRole('radio', { name: /Claude Code/ });
    expect(radio.closest('label')).toHaveClass('linha');
    expect(radio.closest('label')).toHaveAttribute('aria-disabled', 'true');
  });

  it('sem violações de acessibilidade num grupo', async () => {
    const { container } = render(<Group />);

    await expectNoA11yViolations(container);
  });
});
