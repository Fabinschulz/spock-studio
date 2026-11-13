import { Radio } from '@/shared/components';
import { Checkbox } from '@mariliz/ui-kit';
import { useId, type ReactNode } from 'react';
import styles from './panels.module.scss';

interface CheckboxFieldProps {
  checked: boolean;
  onChange: (checked: boolean) => void;
  label: string;
  /** Explicação que o leitor de tela lê junto com o rótulo. */
  description?: string;
  /** Sem moldura: para dentro de uma linha de lista, que já tem o seu separador. */
  plain?: boolean;
}

/** Caixa de seleção do Spock Studio com o rótulo curto; a explicação fica abaixo, ligada por `aria-describedby`. */
export function CheckboxField({ checked, onChange, label, description, plain }: CheckboxFieldProps) {
  const descriptionId = useId();

  return (
    <div className={plain ? styles.choicePlain : styles.choice}>
      <Checkbox
        label={label}
        checked={checked}
        onChange={(_, value) => onChange(value)}
        slotProps={{ input: { 'aria-describedby': description ? descriptionId : undefined } }}
      />
      {description ? (
        <span id={descriptionId} className={styles.muted}>
          {description}
        </span>
      ) : null}
    </div>
  );
}

interface RadioFieldProps {
  name: string;
  checked: boolean;
  onChange: () => void;
  children: ReactNode;
}

/** Opção da jornada, com a moldura do campo: o botão vem do átomo `Radio` (o Synthra não tem um). */
export function RadioField({ name, checked, onChange, children }: RadioFieldProps) {
  return (
    <Radio
      name={name}
      checked={checked}
      onChange={onChange}
      label={children}
      labelProps={{ className: styles.field }}
    />
  );
}
