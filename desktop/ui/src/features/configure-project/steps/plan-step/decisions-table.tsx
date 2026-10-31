import { Fragment } from 'react';

import { EvidenceDisclosure, StatusChip } from '@/shared/components/ui';
import type { ConfigurationPlan } from '@/shared/schema';

import styles from '../../components/panels.module.scss';
import { ScrollableRegion } from '../../components/scrollable-region';
import { componentKindLabel, decisionLabel, decisionTone, originLabel } from '../../domain/labels';

/** Permite a quebra de linha depois de cada ponto do id; `<wbr>` só sugere o ponto, o texto copiado é o id exato. */
function BreakableId({ id }: { id: string }) {
  return id.split('.').map((part, index) => (
    <Fragment key={index}>
      {index > 0 ? (
        <>
          .<wbr />
        </>
      ) : null}
      {part}
    </Fragment>
  ));
}

export function DecisionsTable({ decisions }: { decisions: ConfigurationPlan['decisions'] }) {
  return (
    <ScrollableRegion label="Decisões por componente">
      <table className={styles.table}>
        <thead>
          <tr>
            <th scope="col">Componente</th>
            <th scope="col">Decisão</th>
            <th scope="col">Origem e justificativa</th>
          </tr>
        </thead>
        <tbody>
          {decisions.map((decision) => (
            <tr key={decision.componentId}>
              <td>
                <div>{componentKindLabel[decision.kind]}</div>
                <div className={styles.mono}>
                  <BreakableId id={decision.componentId} />
                </div>
              </td>
              <td>
                <StatusChip tone={decisionTone[decision.decision] ?? 'neutral'}>
                  {decisionLabel[decision.decision] ?? decision.decision}
                </StatusChip>
              </td>
              <td>
                <div>{decision.justification}</div>
                <div className={styles.muted}>Origem: {originLabel[decision.origin] ?? decision.origin}</div>
                <EvidenceDisclosure summary={`Evidências (${decision.evidence.length})`} items={decision.evidence} />
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </ScrollableRegion>
  );
}
