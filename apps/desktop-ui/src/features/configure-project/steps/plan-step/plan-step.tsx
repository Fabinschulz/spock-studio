import { Alert, Button } from '@synthra.io/ui-kit';

import { StatusChip } from '@/shared/components/ui';
import type { ConfigurationPlan } from '@/shared/schemas';

import { decisionLabel, decisionTone, kindLabel, observationLabel, originLabel } from '../../core/labels';
import { EvidenceList } from '../../ui/evidence-list';
import styles from '../../ui/panels.module.scss';
import { StepHeader } from '../../ui/step-header';

interface PlanStepProps {
  plan: ConfigurationPlan;
  onBack: () => void;
  onContinue: () => void;
}

export function PlanStep({ plan, onBack, onContinue }: PlanStepProps) {
  const fromEngineer = plan.proposalOrigin === 'harness-engineer';

  return (
    <section className={styles.step}>
      <StepHeader title="Composição do Harness">
        Cada componente tem uma decisão, a origem dela e a justificativa. O plano é uma proposta: nada é aplicado antes
        da revisão.
      </StepHeader>

      <Alert
        severity="info"
        title={fromEngineer ? 'Plano com a proposta do Harness Engineer' : 'Plano pelas regras-base'}
        description={
          fromEngineer
            ? 'A proposta do agente foi validada pelo motor (formato, catálogo de componentes e ausência de segredos) antes de entrar no plano.'
            : 'O Harness Engineer ainda não gerou uma proposta. Depois de aplicar, o agente fica em .claude/agents/harness-engineer.md: peça a ele, no seu ambiente de IA, que escreva .mariliz/proposal.json e gere o plano de novo.'
        }
      />

      <h3 className={styles.sectionTitle}>Decisões por componente</h3>
      {/* Região rolável: precisa de foco para quem navega só pelo teclado. */}
      {/* eslint-disable-next-line jsx-a11y/no-noninteractive-tabindex */}
      <div role="region" aria-label="Decisões por componente" tabIndex={0} style={{ overflowX: 'auto' }}>
        <table className={styles.table}>
          <thead>
            <tr>
              <th scope="col">Componente</th>
              <th scope="col">Decisão</th>
              <th scope="col">Origem e justificativa</th>
            </tr>
          </thead>
          <tbody>
            {plan.decisions.map((d) => (
              <tr key={d.componentId}>
                <td>
                  <div>{kindLabel[d.kind]}</div>
                  <div className={styles.mono}>{d.componentId}</div>
                </td>
                <td>
                  <StatusChip tone={decisionTone(d.decision)}>{decisionLabel[d.decision] ?? d.decision}</StatusChip>
                </td>
                <td>
                  <div>{d.justification}</div>
                  <div className={styles.muted}>Origem: {originLabel[d.origin] ?? d.origin}</div>
                  <EvidenceList summary={`Evidências (${d.evidence.length})`} items={d.evidence} />
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {plan.limitations.length > 0 ? (
        <>
          <h3 className={styles.sectionTitle}>Limitações</h3>
          {plan.limitations.map((l) => (
            <Alert key={l.code + l.message} severity="warning" description={l.message} />
          ))}
        </>
      ) : null}

      {plan.observations.length > 0 ? (
        <>
          <h3 className={styles.sectionTitle}>Observações</h3>
          <ul className={styles.list}>
            {plan.observations.map((o) => (
              <li key={o.kind + o.text} className={styles.row}>
                <StatusChip tone={o.kind === 'fact' ? 'success' : o.kind === 'inference' ? 'info' : 'warning'}>
                  {observationLabel[o.kind]}
                </StatusChip>
                <span>{o.text}</span>
                <EvidenceList summary={`Evidências (${o.evidence.length})`} items={o.evidence} />
              </li>
            ))}
          </ul>
        </>
      ) : null}

      {plan.risks.length > 0 ? (
        <>
          <h3 className={styles.sectionTitle}>Riscos</h3>
          <ul className={styles.list}>
            {plan.risks.map((r) => (
              <li key={r}>{r}</li>
            ))}
          </ul>
        </>
      ) : null}

      <div className={styles.actions}>
        <Button variant="outlined" color="inherit" onClick={onBack}>
          Voltar
        </Button>
        <Button variant="contained" onClick={onContinue}>
          Revisar alterações
        </Button>
      </div>
    </section>
  );
}
