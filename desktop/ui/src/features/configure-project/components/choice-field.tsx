import { Checkbox } from '@mariliz/ui-kit';
import { useId } from 'react';
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
