import { Button, TextField } from '@mariliz/ui-kit';

import styles from '../infrastructure-page.module.scss';
import type { GenerateFlow } from '../hooks/use-generate-flow';

const MAX_NEED = 4000;
const MIN_NEED = 20;

/**
 * A IA do Spock propõe componentes e explica; os fatos da conta continuam os do formulário. Fica no documento mesmo
 * escondida, para a necessidade digitada não se perder ao trocar de nuvem e voltar.
 */
export function ArchitectDraftSection({ flow, hidden }: { flow: GenerateFlow; hidden: boolean }) {
  const { state, busy } = flow;
  const draft = state.draft;
  return (
    <section className={styles.consent} aria-label="Propor com IA" hidden={hidden}>
      <p className={styles.rowTitle}>Propor o desenho com IA (opcional)</p>
      <TextField
        label="O que o sistema precisa"
        multiline
        minRows={3}
        fullWidth
        value={state.need}
        disabled={busy}
        onChange={(event) => flow.dispatch({ type: 'need', need: event.target.value })}
        helperText="Ex.: uma API de pedidos com banco relacional e uma fila para avisar o estoque. Ids da conta não vão para o modelo."
        slotProps={{ htmlInput: { maxLength: MAX_NEED } }}
      />
      <div className={styles.actions}>
        <Button
          type="button"
          variant="outlined"
          disabled={busy || state.need.trim().length < MIN_NEED}
          onClick={() => void flow.propose()}
        >
          Propor com IA
        </Button>
      </div>
      {draft?.notes ? (
        <div aria-label="Por que este desenho">
          <ul className={styles.impacts}>
            {draft.notes.services.map((service) => (
              <li key={service.name}>
                <strong>{service.name}</strong>: {service.reason}
              </li>
            ))}
          </ul>
          {draft.notes.risks.length > 0 ? <p className={styles.hint}>Riscos: {draft.notes.risks.join(' · ')}</p> : null}
          {draft.notes.alternatives.length > 0 ? (
            <p className={styles.hint}>Alternativas: {draft.notes.alternatives.join(' · ')}</p>
          ) : null}
          {draft.notes.assumptions.length > 0 ? (
            <p className={styles.hint}>Premissas: {draft.notes.assumptions.join(' · ')}</p>
          ) : null}
          <p className={styles.hint}>
            {draft.attempts} tentativa(s), {draft.inputTokens + draft.outputTokens} tokens. Confira e complete os campos
            abaixo.
          </p>
        </div>
      ) : draft ? (
        <p className={styles.hint}>A resposta da IA não veio no formato esperado. Monte o desenho à mão abaixo.</p>
      ) : null}
    </section>
  );
}
