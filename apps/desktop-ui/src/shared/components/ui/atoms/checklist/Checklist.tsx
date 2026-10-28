import type { ReactNode } from 'react';

import { cn } from '@/shared/utils';

import { CheckGlyph } from '../icons';
import styles from './Checklist.module.scss';

export interface ChecklistItem {
  text: ReactNode;
  /** `ok` = presente/incluído; `warn` = falta ou exige atenção; `neutral` = informativo. */
  state?: 'ok' | 'warn' | 'neutral';
}

interface ChecklistProps {
  items: readonly ChecklistItem[];
  label?: string;
}

const spoken = { ok: '', warn: ' (atenção)', neutral: '' } as const;

/** Lista de itens com marca. O estado de cada item também é dito em texto: a marca e a cor só reforçam. */
export function Checklist({ items, label }: ChecklistProps) {
  return (
    <ul className={styles.list} role="list" aria-label={label}>
      {items.map((item, index) => {
        const state = item.state ?? 'ok';
        return (
          <li key={index} className={cn(styles.item, styles[state])}>
            <span className={styles.mark} aria-hidden="true">
              {state === 'ok' ? <CheckGlyph size={12} /> : state === 'warn' ? '!' : '•'}
            </span>
            <span>
              {item.text}
              {spoken[state] ? <span className="visually-hidden">{spoken[state]}</span> : null}
            </span>
          </li>
        );
      })}
    </ul>
  );
}
