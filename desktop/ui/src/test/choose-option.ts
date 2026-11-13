import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

/**
 * Escolhe uma opção num `SelectField` do Synthra (um combobox com lista, não um `<select>` nativo): abre pelo nome do campo e
 * clica na opção pelo texto que a pessoa vê.
 */
export async function chooseOption(field: string | HTMLElement, option: string | RegExp) {
  const combobox = typeof field === 'string' ? screen.getByRole('combobox', { name: field }) : field;
  await userEvent.click(combobox);
  const list = await screen.findByRole('listbox');
  await userEvent.click(within(list).getByRole('option', { name: option }));
}

/** O que a pessoa vê na lista de opções de um `SelectField`, na ordem; abre a lista, lê e fecha sem escolher. */
export async function optionsOf(combobox: HTMLElement): Promise<string[]> {
  await userEvent.click(combobox);
  const list = await screen.findByRole('listbox');
  const labels = within(list)
    .getAllByRole('option')
    .map((option) => option.textContent ?? '');
  await userEvent.keyboard('{Escape}');
  await waitFor(() => expect(screen.queryByRole('listbox')).not.toBeInTheDocument());
  return labels;
}
