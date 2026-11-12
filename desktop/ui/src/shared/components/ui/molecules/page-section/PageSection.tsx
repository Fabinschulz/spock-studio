import { cn } from '@/shared/utils';
import { useId, type ReactNode } from 'react';
import styles from './PageSection.module.scss';

interface PageSectionProps {
  title: ReactNode;
  /** Uma frase sob o título. */
  description?: ReactNode;
  /** Ação da seção (por exemplo, "Atualizar"), alinhada ao título. */
  action?: ReactNode;
  className?: string;
  children?: ReactNode;
}

/**
 * Uma região da página com título. O nome acessível da região é o título, ligado por um id que o componente mesmo
 * cria: ninguém digita `aria-labelledby` à mão nem corre o risco de repetir um id.
 */
export function PageSection({ title, description, action, className, children }: PageSectionProps) {
  const titleId = useId();

  return (
    <section aria-labelledby={titleId} className={cn(styles.section, className)}>
      <div className={styles.header}>
        <h2 id={titleId} className={styles.title}>
          {title}
        </h2>
        {action ? <div className={styles.action}>{action}</div> : null}
      </div>
      {description ? <p className={styles.description}>{description}</p> : null}
      {children}
    </section>
  );
}
