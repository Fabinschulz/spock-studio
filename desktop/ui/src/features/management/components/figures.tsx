import type { ReactNode } from 'react';

import styles from './figures.module.scss';

/** Uma lista de indicadores: número em destaque, rótulo e, quando houver, a nota de onde ele vem. */
export function Figures({ children }: { children: ReactNode }) {
  return <dl className={styles.figures}>{children}</dl>;
}

interface FigureProps {
  label: ReactNode;
  /** `null` é ausência, nunca zero: aparece como "sem dado", com o motivo no lugar da nota. */
  value: ReactNode | null;
  note?: ReactNode;
  unavailable?: string | null;
}

export function Figure({ label, value, note, unavailable }: FigureProps) {
  const detail = value === null ? (unavailable ?? 'Sem dado.') : note;
  return (
    <div className={styles.figure}>
      <dt>
        {label}
        {detail ? <span className={styles.note}>{detail}</span> : null}
      </dt>
      {value === null ? <dd className={styles.absent}>sem dado</dd> : <dd>{value}</dd>}
    </div>
  );
}
