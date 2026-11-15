import { ConfirmAction } from '@/shared/components';
import type { IacOperation } from '@/shared/schema';
import { Button, Checkbox, TextField } from '@mariliz/ui-kit';
import { useState } from 'react';

import styles from '../infrastructure-page.module.scss';

const PRODUCTION = new Set(['prd', 'prod', 'production', 'producao']);

interface PlanApprovalProps {
  plan: IacOperation;
  busy: boolean;
  onApply: (confirmations: {
    destroyConfirmed: boolean;
    risksAccepted: boolean;
    workspaceConfirmation?: string;
  }) => void;
}

/**
 * A aprovação é daquele plano: o Spock aplica exatamente o arquivo mostrado. Destruir, achado grave e produção pedem
 * confirmações a mais, e o clique final ainda passa por uma confirmação que diz que a nuvem vai mudar.
 */
export function PlanApproval({ plan, busy, onApply }: PlanApprovalProps) {
  const [destroyConfirmed, setDestroyConfirmed] = useState(false);
  const [risksAccepted, setRisksAccepted] = useState(false);
  const [typed, setTyped] = useState('');
  const [confirming, setConfirming] = useState(false);

  const summary = plan.summary!;
  const destructive = summary.changes.filter((change) => change.action === 'delete' || change.action === 'replace');
  const risks = (plan.findings ?? []).filter(
    (finding) => finding.severity === 'danger' && finding.rule !== 'destroy' && finding.rule !== 'replace'
  );
  const workspace = plan.workspace ?? 'default';
  const production = PRODUCTION.has(workspace);
  const ready =
    (destructive.length === 0 || destroyConfirmed) &&
    (risks.length === 0 || risksAccepted) &&
    (!production || typed.trim() === workspace);

  return (
    <section className={styles.approval} aria-label={`Aprovar o plano ${plan.planId}`}>
      <p className={styles.rowTitle}>Aprovar e aplicar este plano</p>
      <p>
        Aplicar <strong>muda a nuvem</strong>. O Spock aplica exatamente o plano {plan.planId} no ambiente {workspace} e
        confere o resultado com um plano novo. Se a configuração do projeto mudar, esta aprovação deixa de valer.
      </p>

      {destructive.length > 0 ? (
        <div className={styles.danger}>
          <p className={styles.rowTitle}>{destructive.length} recurso(s) serão destruídos ou substituídos:</p>
          <ul className={styles.impacts}>
            {destructive.map((change) => (
              <li key={change.address}>
                {change.action === 'delete' ? 'destruir' : 'substituir'}: <code>{change.address}</code>
              </li>
            ))}
          </ul>
          <Checkbox
            label={`Confirmo destruir ou substituir ${destructive.length} recurso(s)`}
            checked={destroyConfirmed}
            disabled={busy}
            onChange={(_, checked) => setDestroyConfirmed(checked)}
          />
        </div>
      ) : null}

      {risks.length > 0 ? (
        <Checkbox
          label={`Revisei os ${risks.length} achado(s) grave(s) de segurança e aceito aplicar`}
          checked={risksAccepted}
          disabled={busy}
          onChange={(_, checked) => setRisksAccepted(checked)}
        />
      ) : null}

      {production ? (
        <TextField
          label={`Digite ${workspace} para confirmar o ambiente de produção`}
          value={typed}
          disabled={busy}
          onChange={(event) => setTyped(event.target.value)}
          slotProps={{ htmlInput: { spellCheck: false } }}
        />
      ) : null}

      {confirming ? (
        <ConfirmAction
          label={`Aplicar o plano ${plan.planId}`}
          confirmLabel="Aplicar agora"
          cancelLabel="Não aplicar"
          busy={busy}
          onConfirm={() => {
            setConfirming(false);
            onApply({
              destroyConfirmed,
              risksAccepted,
              ...(production ? { workspaceConfirmation: typed.trim() } : {})
            });
          }}
          onCancel={() => setConfirming(false)}
        >
          Isto muda a infraestrutura de {workspace}: criar {summary.create}, alterar {summary.update}, substituir{' '}
          {summary.replace}, destruir {summary.delete}.
        </ConfirmAction>
      ) : (
        <div className={styles.actions}>
          <Button type="button" disabled={busy || !ready} onClick={() => setConfirming(true)}>
            Aprovar e aplicar
          </Button>
        </div>
      )}
    </section>
  );
}
