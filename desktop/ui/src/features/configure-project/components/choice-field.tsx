import type { ReactNode } from 'react';
import styles from './panels.module.scss';

interface ChoiceFieldProps {
  type: 'checkbox' | 'radio';
  checked: boolean;
  onChange: (checked: boolean) => void;
  name?: string;
  children: ReactNode;
}

export function ChoiceField({ type, checked, onChange, name, children }: ChoiceFieldProps) {
  return (
    <label className={styles.field}>
      <input type={type} name={name} checked={checked} onChange={(event) => onChange(event.target.checked)} />
      {children}
    </label>
  );
}
