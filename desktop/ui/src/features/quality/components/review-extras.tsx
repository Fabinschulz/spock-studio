import { PageSection, StatusChip } from '@/shared/components';
import type { GateResult, Review } from '@/shared/schema';
import type { Tone } from '@mariliz/ui-kit';

import styles from '../quality-page.module.scss';

const GATE: Record<GateResult['state'], { tone: Tone; label: string }> = {
  passed: { tone: 'success', label: 'Passou' },
  failed: { tone: 'danger', label: 'Falhou' },
  blocked: { tone: 'warning', label: 'Bloqueada' },
  not_run: { tone: 'neutral', label: 'Não rodou' },
  timed_out: { tone: 'warning', label: 'Estourou o prazo' },
  cancelled: { tone: 'neutral', label: 'Cancelada' }
};

const REQUIREMENT = {
  met: 'Atendido',
  partial: 'Em parte',
  not_met: 'Não atendido',
  unknown: 'Sem evidência'
} as const;

/** O que a pessoa pediu além da revisão simples: o que o modelo viu, a conferência dos critérios e o resultado de cada verificação. */
export function ReviewExtras({ review }: { review: Review }) {
  const gates = review.gates ?? [];
  const requirements = review.requirements ?? [];
  const runs = review.modelRuns ?? [];
  return (
    <>
      {gates.length > 0 ? (
        <PageSection
          title="Verificações do projeto"
          description="Comandos que o projeto define, rodados com a sua autorização. Uma verificação que não rodou nunca conta como aprovada."
        >
          <ul className={styles.judges}>
            {gates.map((gate) => (
              <li key={gate.id} className={styles.judge}>
                <div className={styles.findingHead}>
                  <StatusChip tone={GATE[gate.state].tone}>{GATE[gate.state].label}</StatusChip>
                  <strong>{gate.command}</strong>
                  <span className={styles.rule}>{gate.origin}</span>
                </div>
                <p className={styles.advice}>{gate.summary}</p>
                {gate.logTail ? (
                  <pre className={styles.excerpt} aria-label={`Saída de ${gate.command}`}>
                    {gate.logTail}
                  </pre>
                ) : null}
              </li>
            ))}
          </ul>
        </PageSection>
      ) : null}

      {requirements.length > 0 ? (
        <PageSection
          title="Critérios de aceite"
          description="Conferência feita por modelo. Um critério só conta como atendido com a linha do diff que o atende."
        >
          <ul className={styles.judges}>
            {requirements.map((check) => (
              <li key={check.index} className={styles.judge}>
                <div className={styles.findingHead}>
                  <StatusChip
                    tone={check.status === 'met' ? 'success' : check.status === 'unknown' ? 'neutral' : 'warning'}
                  >
                    {REQUIREMENT[check.status]}
                  </StatusChip>
                  <strong>{`${check.index}. ${check.text}`}</strong>
                </div>
                {check.path ? (
                  <span className={styles.path}>
                    {check.path}
                    {check.line === null ? '' : `, linha ${check.line}`}
                  </span>
                ) : null}
                {check.note ? <p className={styles.advice}>{check.note}</p> : null}
              </li>
            ))}
          </ul>
        </PageSection>
      ) : null}

      {runs.length > 0 ? (
        <PageSection
          title="Uso do modelo de IA"
          description="O que foi enviado e o que voltou. O modelo só propõe: o que não citou uma linha do diff foi descartado."
        >
          <ul className={styles.plain}>
            {runs.map((run) => (
              <li key={run.purpose}>
                {run.purpose === 'requirements' ? 'Critérios de aceite' : 'Revisão do diff'}:{' '}
                {run.state === 'used'
                  ? `${run.model ?? 'modelo'}, ${run.inputTokens} tokens de entrada e ${run.outputTokens} de saída, ${run.accepted} resposta(s) aproveitada(s) e ${run.discarded} descartada(s).`
                  : run.state === 'skipped'
                    ? 'não rodou.'
                    : `falhou (${run.failureCode ?? 'erro'}).`}
              </li>
            ))}
          </ul>
        </PageSection>
      ) : null}
    </>
  );
}
