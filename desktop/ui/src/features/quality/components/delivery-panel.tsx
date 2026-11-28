import { InlineNotice, StatusChip } from '@/shared/components';
import type { CorrectionBrief, ReviewView } from '@/shared/schema';
import { Button, TextField } from '@mariliz/ui-kit';
import { useState } from 'react';

import styles from '../quality-page.module.scss';

/**
 * A assinatura da entrega é da pessoa: o veredito do motor não muda, e um juiz que aprova nunca a substitui. Aprovar só abre quando
 * nada bloqueante está em aberto. O pedido de correção devolve ao agente o que ficou aberto, com as regras que ele não pode quebrar.
 */
export function DeliveryPanel({
  view,
  brief,
  busy,
  onDecide,
  onBrief
}: {
  view: ReviewView;
  brief: CorrectionBrief | null;
  busy: boolean;
  onDecide: (decision: 'approved' | 'rejected', justification: string) => Promise<void>;
  onBrief: () => Promise<void>;
}) {
  const [text, setText] = useState('');
  const [copied, setCopied] = useState(false);
  const { status } = view;
  const signed = status.delivery;
  const decide = async (decision: 'approved' | 'rejected') => {
    await onDecide(decision, text.trim());
    setText('');
  };
  const copy = async () => {
    if (!brief) return;
    try {
      await navigator.clipboard.writeText(brief.text);
      setCopied(true);
    } catch {
      setCopied(false);
    }
  };

  return (
    <>
      <p className={styles.note} role="status">
        {status.openBlocking === 0
          ? 'Nenhum problema confirmado e grave em aberto.'
          : `${status.openBlocking} problema(s) confirmado(s) e grave(s) em aberto: corrija, ou descarte dizendo por que o Spock errou.`}{' '}
        {status.openNeedsCheck > 0 ? `${status.openNeedsCheck} ponto(s) ainda para conferir.` : ''}
      </p>

      {signed ? (
        <InlineNotice
          tone={signed.decision === 'approved' ? 'success' : 'warning'}
          title={signed.decision === 'approved' ? 'Entrega aprovada por você' : 'Entrega recusada por você'}
        >
          {signed.justification} Assinado em {new Date(signed.decidedAt).toLocaleString('pt-BR')}. O veredito do Spock
          continua sendo o dele.
        </InlineNotice>
      ) : null}

      <TextField
        label="Justificativa da decisão sobre a entrega"
        multiline
        minRows={2}
        size="small"
        fullWidth
        value={text}
        disabled={busy}
        onChange={(event) => setText(event.target.value)}
        helperText="De 10 a 500 caracteres. É a sua assinatura: nenhum juiz a substitui."
      />
      <div className={styles.decideActions}>
        <Button
          type="button"
          variant="contained"
          disabled={busy || !status.readyForDelivery || text.trim().length < 10}
          onClick={() => void decide('approved')}
        >
          Aprovar a entrega
        </Button>
        <Button
          type="button"
          variant="outlined"
          color="inherit"
          disabled={busy || text.trim().length < 10}
          onClick={() => void decide('rejected')}
        >
          Recusar a entrega
        </Button>
        <Button type="button" variant="outlined" color="inherit" disabled={busy} onClick={() => void onBrief()}>
          Montar o pedido de correção
        </Button>
      </div>
      {!status.readyForDelivery ? (
        <p className={styles.note}>
          Aprovar só abre quando não há problema bloqueante em aberto e a revisão foi concluída (nem inconclusiva, nem
          vazia).
        </p>
      ) : null}

      {brief ? (
        <div className={styles.brief}>
          <div className={styles.findingHead}>
            <StatusChip tone={brief.stopped ? 'danger' : 'info'}>
              {brief.stopped ? 'Limite de tentativas' : `Tentativa ${brief.attempt} de ${brief.maxAttempts}`}
            </StatusChip>
            <strong>Pedido de correção para o agente</strong>
          </div>
          <pre className={styles.excerpt} aria-label="Pedido de correção">
            {brief.text}
          </pre>
          {brief.stopped ? null : (
            <Button type="button" size="small" variant="outlined" color="inherit" onClick={() => void copy()}>
              {copied ? 'Copiado' : 'Copiar o pedido'}
            </Button>
          )}
        </div>
      ) : null}
    </>
  );
}
