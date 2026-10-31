import { Button } from '@synthra.io/ui-kit';

import { EvidenceDisclosure, InlineNotice, PrimaryActionFooter, StatusChip } from '@/shared/components/ui';
import type { ConfigurationPlan } from '@/shared/schema';

import styles from '../../components/panels.module.scss';
import { StepHeader } from '../../components/step-header';
import { observationLabel, observationTone } from '../../domain/labels';
import { DecisionsTable } from './decisions-table';

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

      <InlineNotice
        tone="info"
        title={fromEngineer ? 'Plano com a proposta do Harness Engineer' : 'Plano pelas regras-base'}
      >
        {fromEngineer
          ? 'A proposta do agente foi validada pelo motor (formato, catálogo de componentes e ausência de segredos) antes de entrar no plano.'
          : 'O Harness Engineer ainda não gerou uma proposta. Depois de aplicar, o agente fica em .claude/agents/harness-engineer.md: peça a ele, no seu ambiente de IA, que escreva .mariliz/proposal.json e gere o plano de novo.'}
      </InlineNotice>

      <h3 className={styles.sectionTitle}>Decisões por componente</h3>
      <DecisionsTable decisions={plan.decisions} />

      {plan.limitations.length > 0 ? (
        <>
          <h3 className={styles.sectionTitle}>Limitações</h3>
          {plan.limitations.map((limitation) => (
            <InlineNotice key={limitation.code + limitation.message} tone="warning">
              {limitation.message}
            </InlineNotice>
          ))}
        </>
      ) : null}

      {plan.observations.length > 0 ? (
        <>
          <h3 className={styles.sectionTitle}>Observações</h3>
          <ul className={styles.list}>
            {plan.observations.map((observation) => (
              <li key={observation.kind + observation.text} className={styles.row}>
                <StatusChip tone={observationTone[observation.kind]}>{observationLabel[observation.kind]}</StatusChip>
                <span>{observation.text}</span>
                <EvidenceDisclosure
                  summary={`Evidências (${observation.evidence.length})`}
                  items={observation.evidence}
                />
              </li>
            ))}
          </ul>
        </>
      ) : null}

      {plan.risks.length > 0 ? (
        <>
          <h3 className={styles.sectionTitle}>Riscos</h3>
          <ul className={styles.list}>
            {plan.risks.map((risk) => (
              <li key={risk}>{risk}</li>
            ))}
          </ul>
        </>
      ) : null}

      <PrimaryActionFooter>
        <Button variant="outlined" color="inherit" onClick={onBack}>
          Voltar
        </Button>
        <Button variant="contained" onClick={onContinue}>
          Revisar alterações
        </Button>
      </PrimaryActionFooter>
    </section>
  );
}
