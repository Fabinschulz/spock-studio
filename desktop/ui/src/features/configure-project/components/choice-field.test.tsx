import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';

import { AppProviders } from '@/shared/components';

import { CheckboxField, RadioField } from './choice-field';

describe('CheckboxField', () => {
  it('usa o rótulo como nome, liga a explicação ao campo e avisa a mudança', async () => {
    const onChange = vi.fn();
    render(
      <AppProviders>
        <CheckboxField
          checked={false}
          onChange={onChange}
          label="Aprovar a execução deste comando"
          description="Desmarcado por padrão."
        />
      </AppProviders>
    );

    const box = screen.getByRole('checkbox', { name: 'Aprovar a execução deste comando' });
    expect(box).toHaveAccessibleDescription('Desmarcado por padrão.');
    expect(box).not.toBeChecked();

    await userEvent.click(box);
    expect(onChange).toHaveBeenCalledExactlyOnceWith(true);
  });
});

describe('RadioField', () => {
  it('marca a opção e avisa quando a pessoa a escolhe', async () => {
    const onChange = vi.fn();
    render(
      <RadioField name="resolucao" checked={false} onChange={onChange}>
        <span>Manter o meu arquivo</span>
      </RadioField>
    );

    await userEvent.click(screen.getByRole('radio', { name: 'Manter o meu arquivo' }));
    expect(onChange).toHaveBeenCalledTimes(1);
  });
});
