import { Radio } from '@/shared/components';
import { useId, type ReactNode } from 'react';

import askStyles from './ask.module.scss';

export interface Choice<T extends string> {
  value: T;
  label: string;
  /** A consequência da escolha; quando a opção está desabilitada, diz por quê. */
  note: ReactNode;
  disabled?: boolean;
}

interface ChoiceGroupProps<T extends string> {
  legend: string;
  name: string;
  value: T;
  choices: readonly Choice<T>[];
  disabled?: boolean;
  onChange: (value: T) => void;
}

/** Cada opção leva a nota como descrição: quem usa leitor de tela ouve a consequência junto com o nome. */
export function ChoiceGroup<T extends string>({
  legend,
  name,
  value,
  choices,
  disabled,
  onChange
}: ChoiceGroupProps<T>) {
  const baseId = useId();

  return (
    <fieldset className={askStyles.choices}>
      <legend>{legend}</legend>
      {choices.map((choice) => {
        const noteId = `${baseId}-${choice.value}`;
        return (
          <div key={choice.value} className={askStyles.choice}>
            <Radio
              name={name}
              checked={value === choice.value}
              disabled={disabled || choice.disabled}
              onChange={() => onChange(choice.value)}
              label={choice.label}
              slotProps={{ input: { 'aria-describedby': noteId } }}
            />
            <p id={noteId} className={askStyles.choiceNote}>
              {choice.note}
            </p>
          </div>
        );
      })}
    </fieldset>
  );
}
