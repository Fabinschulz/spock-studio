import { ArrowRightIcon } from '@synthra.io/ui-kit';
import { useId, type ReactNode } from 'react';

import { cn } from '@/shared/utils';

import styles from './ActionCard.module.scss';

export type CardAccent = 'teal' | 'indigo' | 'blue' | 'orange';

interface ActionCardProps {
  title: string;
  description: string;
  icon: ReactNode;
  onClick?: () => void;
  variant?: 'primary' | 'default';
  accent?: CardAccent;
  highlight?: string;
  /** Quando definido o cartão fica indisponível, mas continua focável (aria-disabled) para o leitor de tela anunciar o motivo. */
  unavailableReason?: string;
}

export function ActionCard({
  title,
  description,
  icon,
  onClick,
  variant = 'default',
  accent = 'teal',
  highlight,
  unavailableReason
}: ActionCardProps) {
  const id = useId();
  const descriptionId = `${id}-descricao`;
  const reasonId = `${id}-motivo`;
  const unavailable = unavailableReason !== undefined;

  return (
    <button
      type="button"
      className={cn(
        styles.card,
        variant === 'primary' && !unavailable && styles.primary,
        unavailable && styles.unavailable
      )}
      aria-disabled={unavailable || undefined}
      aria-describedby={unavailable ? `${descriptionId} ${reasonId}` : descriptionId}
      onClick={unavailable ? undefined : onClick}
    >
      <span className={cn(styles.icon, styles[accent])} aria-hidden="true">
        {icon}
      </span>
      <span className={styles.body}>
        <span className={styles.heading}>
          <span className={styles.title}>{title}</span>
          {unavailable ? (
            <span id={reasonId} className={styles.badge}>
              {unavailableReason}
              <span className="visually-hidden">: esta atividade ainda não está disponível.</span>
            </span>
          ) : null}
        </span>
        <span id={descriptionId} className={styles.description}>
          {description}
        </span>
        {highlight && !unavailable ? <span className={styles.highlight}>{highlight}</span> : null}
      </span>
      {unavailable ? null : (
        <span className={styles.arrow} aria-hidden="true">
          <ArrowRightIcon fontSize="small" />
        </span>
      )}
    </button>
  );
}
