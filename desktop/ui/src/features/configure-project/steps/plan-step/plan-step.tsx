import { Button } from '@mariliz/ui-kit';

import { EvidenceDisclosure, InlineNotice, PrimaryActionFooter } from '@/shared/components/ui';
import type { ConfigurationPlan } from '@/shared/schema';

import styles from '../../components/panels.module.scss';
import rows from '../../components/rows.module.scss';
import { StepHeader } from '../../components/step-header';
import { observationLabel } from '../../domain/labels';
import { DecisionsList } from './decisions-list';

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
          : 'O Harness Engineer ainda não gerou uma proposta. Depois de aplicar, o agente fica em .claude/agents/spock-harness-engineer.md: peça a ele, no seu ambiente de IA, que escreva a proposta no arquivo que ele indica e gere o plano de novo.'}
      </InlineNotice>

      <DecisionsList decisions={plan.decisions} />

      {plan.limitations.length > 0 ? (
        <section aria-labelledby="limitacoes-titulo" className={rows.section}>
          <h3 id="limitacoes-titulo" className={rows.title}>
            Limitações
          </h3>
          {plan.limitations.map((limitation) => (
            <InlineNotice key={limitation.code + limitation.message} tone="warning">
              {limitation.message}
            </InlineNotice>
          ))}
        </section>
      ) : null}

      {plan.observations.length > 0 ? (
        <section aria-labelledby="observacoes-titulo" className={rows.section}>
          <h3 id="observacoes-titulo" className={rows.title}>
            Observações
          </h3>
          <ul role="list" className={rows.rows}>
            {plan.observations.map((observation) => (
              <li key={observation.kind + observation.text}>
                <div className={rows.line}>
                  <span>{observation.text}</span>
                  <span className={rows.end}>{observationLabel[observation.kind]}</span>
                </div>
                <EvidenceDisclosure
                  summary={`Evidências (${observation.evidence.length})`}
                  items={observation.evidence}
                />
              </li>
            ))}
          </ul>
        </section>
      ) : null}

      {plan.risks.length > 0 ? (
        <section aria-labelledby="riscos-titulo" className={rows.section}>
          <h3 id="riscos-titulo" className={rows.title}>
            Riscos
          </h3>
          <ul role="list" className={rows.rows}>
            {plan.risks.map((risk) => (
              <li key={risk}>{risk}</li>
            ))}
          </ul>
        </section>
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
