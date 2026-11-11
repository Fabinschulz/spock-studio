import { StatusChip } from '@/shared/components';
import type { ProjectRisk, ProjectRisks } from '@/shared/schema';

import { SEVERITY_LABEL } from '../domain/delivery-labels';
import styles from '../management-page.module.scss';

/** Riscos detectados por regra fixa, do mais grave ao mais leve, cada um com as evidências e o que fazer. */
export function RisksSection({ risks }: { risks: ProjectRisks }) {
  if (!risks.hasData) return null;

  return (
    <section aria-labelledby="riscos">
      <h2 id="riscos" className={styles.section}>
        Riscos
      </h2>
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
    </section>
  );
}

function RiskRow({ risk }: { risk: ProjectRisk }) {
  const severity = SEVERITY_LABEL[risk.severity];

  return (
    <li className={styles.portfolioRow}>
      <div className={styles.portfolioHead}>
        <span className={styles.rowTitle}>{risk.title}</span>
        <StatusChip tone={severity.tone}>Severidade {severity.label}</StatusChip>
      </div>
      {risk.evidence.map((line) => (
        <p key={line} className={styles.muted}>
          {line}
        </p>
      ))}
      {risk.trend ? <p className={styles.muted}>Tendência: {risk.trend}</p> : null}
      {risk.items.length > 0 ? (
        <p className={styles.muted}>
          Itens: {risk.items.map((item) => `${item.externalId} (${item.title})`).join(', ')}
        </p>
      ) : null}
      <p>
        <span className={styles.kindLabel}>O que fazer:</span> {risk.recommendation}
      </p>
      <p className={styles.ruleNote}>Regra: {risk.rule}</p>
    </li>
  );
}
