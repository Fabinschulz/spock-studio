import { StatusChip } from '@/shared/components';

import styles from '../infrastructure-page.module.scss';
import { LIFECYCLE, LIFECYCLE_MODE } from '../domain/labels';

/** As oito etapas, com o que cada uma faz na nuvem; as que ainda não existem aparecem como próximas entregas. */
export function LifecycleStrip() {
  return (
    <ol className={styles.lifecycle} aria-label="Ciclo de uma mudança de infraestrutura">
      {LIFECYCLE.map((stage, index) => (
        <li key={stage.id} className={styles.stage} data-available={stage.available}>
          <span className={styles.stageIndex}>{index + 1}</span>
          <span className={styles.rowTitle}>{stage.label}</span>
          <StatusChip tone={LIFECYCLE_MODE[stage.mode].tone}>{LIFECYCLE_MODE[stage.mode].label}</StatusChip>
          <span className={styles.hint}>{stage.description}</span>
          <span className={styles.hint}>{stage.available ? 'Disponível' : 'Próxima entrega'}</span>
        </li>
      ))}
    </ol>
  );
}
