import { Button, TextField } from '@mariliz/ui-kit';

import { useCopyToClipboard } from '@/shared/hooks';

import styles from '../../components/panels.module.scss';
import { PlanError } from '../../components/plan-error';
import { harnessEngineerPrompt } from '../../domain/harness-engineer-prompt';
import type { Loadable } from '../../types/journey';

interface ProposalGuideProps {
  onReplan: () => void;
  replan: Loadable<unknown>;
  /** Uma reversão ou validação em andamento: gerar outro plano agora tiraria a pessoa do resultado no meio dela. */
  busy: boolean;
}

/** Próximo passo guiado: o prompt pronto para o agente e o botão para planejar de novo com a proposta dele. */
export function ProposalGuide({ onReplan, replan, busy }: ProposalGuideProps) {
  const { state: copyState, copy } = useCopyToClipboard();

  return (
    <div className={styles.card}>
      <h3 className={styles.cardTitle}>Próximo passo: peça a proposta ao Harness Engineer</h3>
      <span className={styles.muted}>
        O plano usou as regras-base. O agente <span className={styles.mono}>harness-engineer</span> já está no projeto (
        <span className={styles.mono}>.claude/agents/harness-engineer.md</span>). Abra a pasta no Claude Code, cole o
        pedido abaixo e deixe que ele escreva <span className={styles.mono}>.mariliz/proposal.json</span>. O motor
        valida a proposta antes de usá-la e você revisa tudo de novo antes de aplicar.
      </span>
      <TextField
        label="Pedido para o Harness Engineer"
        multiline
        minRows={8}
        fullWidth
        value={harnessEngineerPrompt}
        onFocus={(event) => event.target.select()}
        slotProps={{ input: { readOnly: true }, htmlInput: { className: styles.prompt } }}
      />
      <div className={styles.actions}>
        <Button variant="outlined" color="inherit" onClick={() => void copy(harnessEngineerPrompt)}>
          {copyState === 'copied' ? 'Copiado' : 'Copiar pedido'}
        </Button>
        <Button variant="contained" onClick={onReplan} disabled={busy || replan.status === 'loading'}>
          {replan.status === 'loading' ? 'Gerando plano…' : 'Gerar o plano de novo'}
        </Button>
      </div>
      <div role="status" aria-live="polite">
        {copyState === 'copied' ? (
          <span className={styles.muted}>Pedido copiado para a área de transferência.</span>
        ) : null}
        {copyState === 'failed' ? (
          <span className={styles.muted}>
            Não foi possível copiar automaticamente. Selecione o texto acima e copie com Ctrl+C.
          </span>
        ) : null}
      </div>
      <PlanError plan={replan} />
    </div>
  );
}
