import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { useState } from 'react';
import { describe, expect, it, vi } from 'vitest';

import { expectNoA11yViolations } from '@/test/a11y';

import { CountBadge, Radio, StatusChip, Tooltip } from './index';

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

describe('StatusChip', () => {
  it('o estado é o texto: a cor só reforça', () => {
    render(<StatusChip tone="danger">Falhou</StatusChip>);

    expect(screen.getByText('Falhou')).toBeInTheDocument();
  });
});

describe('CountBadge', () => {
  it('com rótulo, quem vê lê o número e o leitor de tela recebe a frase', () => {
    render(
      <a href="/execucoes">
        Execuções
        <CountBadge count={3} label="3 aguardando aprovação" tone="warning" />
      </a>
    );

    expect(screen.getByRole('link')).toHaveAccessibleName('Execuções , 3 aguardando aprovação');
    expect(screen.getByText('3')).toHaveAttribute('aria-hidden', 'true');
  });

  it('sem rótulo, o número é o que se lê', () => {
    render(<CountBadge count={7} />);

    expect(screen.getByText('7')).not.toHaveAttribute('aria-hidden');
  });
});

describe('Tooltip', () => {
  it('descreve o elemento sem trocar o nome acessível dele', async () => {
    render(
      <Tooltip title="Abre em outra janela">
        <button type="button">Abrir</button>
      </Tooltip>
    );
    const button = screen.getByRole('button', { name: 'Abrir' });

    await userEvent.hover(button);

    expect(await screen.findByRole('tooltip')).toHaveTextContent('Abre em outra janela');
    expect(button).toHaveAccessibleName('Abrir');
  });

  it('desativada, devolve o elemento intacto', async () => {
    render(
      <Tooltip title="Abre em outra janela" disabled>
        <button type="button">Abrir</button>
      </Tooltip>
    );

    await userEvent.hover(screen.getByRole('button', { name: 'Abrir' }));

    expect(screen.queryByRole('tooltip')).not.toBeInTheDocument();
  });
});
