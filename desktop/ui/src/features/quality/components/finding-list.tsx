import { StatusChip } from '@/shared/components';
import type { FindingDecision, ReviewFinding } from '@/shared/schema';
import { Button, TextField, type Tone } from '@mariliz/ui-kit';
import { useState } from 'react';

import styles from '../quality-page.module.scss';

export const SEVERITY: Record<ReviewFinding['severity'], { tone: Tone; label: string }> = {
  critical: { tone: 'danger', label: 'Crítico' },
  high: { tone: 'danger', label: 'Grave' },
  medium: { tone: 'warning', label: 'Médio' },
  low: { tone: 'info', label: 'Baixo' },
  informational: { tone: 'neutral', label: 'Informativo' }
};

const DECISION: Record<FindingDecision['decision'], string> = {
  confirmed: 'Confirmado por você',
  dismissed_false_positive: 'Descartado como falso positivo',
  accepted_risk: 'Risco aceito',
  fix_requested: 'Correção pedida'
};

const CONFIDENCE = { confirmed: 'Confirmado', heuristic: 'Heurística', hypothesis: 'Hipótese' } as const;

export interface DecideFinding {
  (finding: ReviewFinding, decision: string, justification?: string): Promise<void>;
}

/** Cada achado mostra a evidência e deixa a pessoa decidir. Descartar e aceitar risco pedem a justificativa, e nada some. */
export function FindingList({
  findings,
  decisions,
  empty,
  onDecide,
  disabled
}: {
  findings: ReviewFinding[];
  decisions: FindingDecision[];
  empty: string;
  onDecide: DecideFinding;
  disabled: boolean;
}) {
  const [asking, setAsking] = useState<{ id: string; decision: string } | null>(null);
  const [text, setText] = useState('');
  if (findings.length === 0) return <p className={styles.note}>{empty}</p>;
  const byFinding = new Map(decisions.map((item) => [item.findingId, item]));

  const submit = async (finding: ReviewFinding, decision: string) => {
    await onDecide(finding, decision, text.trim() || undefined);
    setAsking(null);
    setText('');
  };

  return (
    <ul className={styles.findings}>
      {findings.map((finding) => {
        const decided = byFinding.get(finding.id);
        return (
          <li key={finding.id} className={styles.finding}>
            <div className={styles.findingHead}>
              <StatusChip tone={SEVERITY[finding.severity].tone}>{SEVERITY[finding.severity].label}</StatusChip>
              <strong>{finding.title}</strong>
              <span className={styles.rule}>{finding.ruleId}</span>
              {decided ? <StatusChip tone="info">{DECISION[decided.decision]}</StatusChip> : null}
            </div>
            <span className={styles.path}>
              {finding.path}
              {finding.startLine === null ? '' : `, linha ${finding.startLine}`}
              {finding.evidence.symbol ? ` · ${finding.evidence.symbol}` : ''}
            </span>
            <pre className={styles.excerpt} aria-label="Evidência">
              {finding.evidence.excerpt}
            </pre>
            <p className={styles.advice}>{finding.actualBehavior}</p>
            <p className={styles.advice}>
              <strong>Esperado:</strong> {finding.expectedBehavior}
            </p>
            <p className={styles.advice}>
              <strong>Como corrigir:</strong> {finding.suggestedCorrection}
            </p>
            <p className={styles.provenance}>
              {CONFIDENCE[finding.confidence]} · {finding.evidence.origin}
            </p>
            {decided?.justification ? (
              <p className={styles.advice}>
                <strong>Sua justificativa:</strong> {decided.justification}
              </p>
            ) : null}
            {asking?.id === finding.id ? (
              <div className={styles.decideForm}>
                <TextField
                  label="Justificativa"
                  multiline
                  minRows={2}
                  size="small"
                  fullWidth
                  value={text}
                  onChange={(event) => setText(event.target.value)}
                  helperText="De 10 a 500 caracteres. Fica registrada junto do achado, que continua no relatório."
                />
                <div className={styles.decideActions}>
                  <Button
                    type="button"
                    size="small"
                    variant="contained"
                    disabled={disabled || text.trim().length < 10}
                    onClick={() => void submit(finding, asking.decision)}
                  >
                    Registrar
                  </Button>
                  <Button type="button" size="small" variant="text" color="inherit" onClick={() => setAsking(null)}>
                    Cancelar
                  </Button>
                </div>
              </div>
            ) : (
              <div className={styles.decideActions}>
                <Button
                  type="button"
                  size="small"
                  variant="outlined"
                  color="inherit"
                  disabled={disabled}
                  onClick={() => void onDecide(finding, 'confirmed')}
                  aria-label={`Confirmar o achado ${finding.title}`}
                >
                  Confirmar
                </Button>
                <Button
                  type="button"
                  size="small"
                  variant="outlined"
                  color="inherit"
                  disabled={disabled}
                  onClick={() => setAsking({ id: finding.id, decision: 'dismissed_false_positive' })}
                  aria-label={`Descartar o achado ${finding.title}`}
                >
                  Descartar
                </Button>
                {finding.severity === 'critical' ? null : (
                  <Button
                    type="button"
                    size="small"
                    variant="outlined"
                    color="inherit"
                    disabled={disabled}
                    onClick={() => setAsking({ id: finding.id, decision: 'accepted_risk' })}
                    aria-label={`Aceitar o risco do achado ${finding.title}`}
                  >
                    Aceitar o risco
                  </Button>
                )}
              </div>
            )}
          </li>
        );
      })}
    </ul>
  );
}
