import { StatusChip, PageSection } from '@/shared/components';
import type { ProjectRisk, ProjectRisks } from '@/shared/schema';

import { severityLabel } from '../domain/delivery-labels';
import styles from '../management-page.module.scss';

export function RisksSection({ risks }: { risks: ProjectRisks }) {
  if (!risks.hasData) return null;

  return (
    <PageSection title="Riscos">
      {risks.risks.length === 0 ? (
        <p>Nenhuma das {risks.rulesEvaluated.length} regras de risco disparou com os dados sincronizados.</p>
      ) : (
        <ul className={styles.rows} aria-label="Riscos detectados">
          {risks.risks.map((risk) => (
            <RiskRow key={risk.id} risk={risk} />
          ))}
        </ul>
      )}
      <p className={styles.muted}>
        Detectados por regras fixas sobre os dados sincronizados: são contagens, não previsão nem opinião.
        {risks.complete ? '' : ' Ao menos uma fonte está incompleta, então podem faltar riscos.'}
      </p>
    </PageSection>
  );
}

function RiskRow({ risk }: { risk: ProjectRisk }) {
  const severity = severityLabel(risk.severity);

  return (
    <li className={styles.portfolioRow}>
      <div className={styles.portfolioHead}>
        <span className={styles.rowTitle}>{risk.title}</span>
        <StatusChip tone={severity.tone}>Gravidade {severity.label}</StatusChip>
      </div>
      {risk.evidence.map((line) => (
        <p key={line} className={styles.muted}>
          {line}
        </p>
      ))}
      {risk.trend ? <p className={styles.muted}>Tendência: {risk.trend}</p> : null}
      {risk.items.length > 0 ? (
        <>
          <p className={styles.muted}>Itens:</p>
          <ul className={styles.notes} aria-label={`Itens de ${risk.title}`}>
            {risk.items.map((item) => (
              <li key={item.externalId}>{item.title ? `${item.externalId} (${item.title})` : item.externalId}</li>
            ))}
          </ul>
        </>
      ) : null}
      <p>
        <span className={styles.kindLabel}>O que fazer:</span> {risk.recommendation}
      </p>
      <p className={styles.ruleNote}>Regra: {risk.rule}</p>
    </li>
  );
}
