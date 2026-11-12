import type { ReactNode } from 'react';
import { Disclosure } from '../disclosure';
import styles from './EvidenceDisclosure.module.scss';

interface EvidenceDisclosureProps {
  summary: string;
  items: readonly string[];
  children?: ReactNode;
}

export function EvidenceDisclosure({ summary, items, children }: EvidenceDisclosureProps) {
  if (items.length === 0 && !children) return null;
  return (
    <Disclosure summary={summary}>
      <div className={styles.stack}>
        {children}
        <ul className={styles.list}>
          {/* A evidência pode repetir o mesmo texto (dois avisos iguais); a lista é estática, então a posição é a chave. */}
          {items.map((item, index) => (
            <li key={`${index}:${item}`} className={styles.item}>
              {item}
            </li>
          ))}
        </ul>
      </div>
    </Disclosure>
  );
}
