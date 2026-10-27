import { useId, type ReactNode } from 'react';

import { cn } from '@/shared/utils';

import styles from './ActionCard.module.scss';

interface ActionCardProps {
  title: string;
  description: string;
  icon: ReactNode;
  onClick?: () => void;
  /** Quando definido o cartão fica indisponível, mas continua focável (aria-disabled) para o leitor de tela anunciar o motivo. */
  unavailableReason?: string;
}

export function ActionCard({ title, description, icon, onClick, unavailableReason }: ActionCardProps) {
  const id = useId();
  const descriptionId = `${id}-descricao`;
  const reasonId = `${id}-motivo`;
  const unavailable = unavailableReason !== undefined;

  return (
    <button
      type="button"
      className={cn(styles.card, unavailable && styles.unavailable)}
      aria-disabled={unavailable || undefined}
      aria-describedby={unavailable ? `${descriptionId} ${reasonId}` : descriptionId}
      onClick={unavailable ? undefined : onClick}
    >
      <span className={styles.icon} aria-hidden="true">
        {icon}
      </span>
      <span className={styles.body}>
        <span className={styles.heading}>
          <span className={styles.title}>{title}</span>
          {unavailable ? (
            <span id={reasonId} className={styles.badge}>
              {unavailableReason}
            </span>
          ) : null}
        </span>
        <span id={descriptionId} className={styles.description}>
          {description}
        </span>
      </span>
    </button>
  );
}
