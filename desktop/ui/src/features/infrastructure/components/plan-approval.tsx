import { ConfirmAction } from '@/shared/components';
import type { IacApplyRequirements, IacOperation, IacPlanSummary } from '@/shared/schema';
import { Button, Checkbox, TextField } from '@mariliz/ui-kit';
import { useState } from 'react';

import styles from '../infrastructure-page.module.scss';

interface PlanApprovalProps {
  plan: IacOperation;
  summary: IacPlanSummary;
  /** O motor decide o que precisa ser confirmado; a tela só pergunta. */
  requirements: IacApplyRequirements;
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
export function PlanApproval({ plan, summary, requirements, busy, onApply }: PlanApprovalProps) {
  const [destroyConfirmed, setDestroyConfirmed] = useState(false);
  const [risksAccepted, setRisksAccepted] = useState(false);
  const [typed, setTyped] = useState('');
  const [confirming, setConfirming] = useState(false);

  const destructive = summary.changes.filter((change) => change.action === 'delete' || change.action === 'replace');
  const workspace = plan.workspace ?? 'default';
  const production = requirements.workspaceConfirmation;
  const ready =
    (requirements.destructive === 0 || destroyConfirmed) &&
    (requirements.risks === 0 || risksAccepted) &&
    (production === null || typed.trim() === production);

  return (
    <section className={styles.approval} aria-label={`Aprovar o plano ${requirements.planId}`}>
      <p className={styles.rowTitle}>Aprovar e aplicar este plano</p>
      <p>
        Aplicar <strong>muda a nuvem</strong>. O Spock aplica exatamente o plano {requirements.planId} no ambiente{' '}
        {workspace} e confere o resultado com um plano novo. Se a configuração do projeto mudar, esta aprovação deixa de
        valer.
      </p>

      {requirements.destructive > 0 ? (
        <div className={styles.danger}>
          <p className={styles.rowTitle}>{requirements.destructive} recurso(s) serão destruídos ou substituídos:</p>
          <ul className={styles.impacts}>
            {destructive.map((change) => (
              <li key={change.address}>
                {change.action === 'delete' ? 'destruir' : 'substituir'}: <code>{change.address}</code>
              </li>
            ))}
          </ul>
          <Checkbox
            label={`Confirmo destruir ou substituir ${requirements.destructive} recurso(s)`}
            checked={destroyConfirmed}
            disabled={busy}
            onChange={(_, checked) => setDestroyConfirmed(checked)}
          />
        </div>
      ) : null}

      {requirements.risks > 0 ? (
        <Checkbox
          label={`Revisei os ${requirements.risks} achado(s) grave(s) de segurança e aceito aplicar`}
          checked={risksAccepted}
          disabled={busy}
          onChange={(_, checked) => setRisksAccepted(checked)}
        />
      ) : null}

      {production !== null ? (
        <TextField
          label={`Digite ${production} para confirmar o ambiente de produção`}
          value={typed}
          disabled={busy}
          onChange={(event) => setTyped(event.target.value)}
          slotProps={{ htmlInput: { spellCheck: false } }}
        />
      ) : null}

      {confirming ? (
        <ConfirmAction
          label={`Aplicar o plano ${requirements.planId}`}
          confirmLabel="Aplicar agora"
          cancelLabel="Não aplicar"
          busy={busy}
          onConfirm={() => {
            setConfirming(false);
            onApply({
              destroyConfirmed,
              risksAccepted,
              ...(production !== null ? { workspaceConfirmation: typed.trim() } : {})
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
