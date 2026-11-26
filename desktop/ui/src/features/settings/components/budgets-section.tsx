import { PageSection, Term } from '@/shared/components';
import type { ModelBudget, ModelConnection } from '@/shared/schema';
import { Button, TextField } from '@mariliz/ui-kit';
import { useState } from 'react';

import type { Feedback } from '../hooks/use-model-settings';
import { useRefocus } from '../hooks/use-refocus';
import { ActionFeedback } from './action-feedback';
import styles from './budgets-section.module.scss';

interface BudgetsSectionProps {
  connection: ModelConnection;
  busy: boolean;
  feedback: Feedback | null;
  onSave: (budget: ModelBudget, tokens: number | null) => Promise<boolean>;
}

const number = (value: number) => value.toLocaleString('pt-BR');

export function BudgetsSection({ connection, busy, feedback, onSave }: BudgetsSectionProps) {
  return (
    <PageSection
      title="Teto de tokens por finalidade"
      description={
        <>
          Quanto uma execução pode gastar do modelo, em <Term id="token" />, somando o que vai e o que volta. Se o teto
          acabar, o resto roda pelas regras do Spock Studio, sem o modelo. Um teto maior deixa o modelo ver e escrever
          mais, e custa mais.
        </>
      }
    >
      <ul className={styles.budgets}>
        {connection.budgets.map((budget) => (
          <BudgetRow key={budget.purpose} budget={budget} busy={busy} feedback={feedback} onSave={onSave} />
        ))}
      </ul>
    </PageSection>
  );
}

function BudgetRow({
  budget,
  busy,
  feedback,
  onSave
}: {
  budget: ModelBudget;
  busy: boolean;
  feedback: Feedback | null;
  onSave: BudgetsSectionProps['onSave'];
}) {
  const { target, after } = useRefocus<HTMLInputElement>(busy);
  const [text, setText] = useState(String(budget.tokens));
  const [shown, setShown] = useState(budget.tokens);
  // O motor devolveu outro valor (salvou ou voltou ao padrão): o campo passa a mostrar o que vale, sem remontar a linha.
  if (shown !== budget.tokens) {
    setShown(budget.tokens);
    setText(String(budget.tokens));
  }
  const tokens = /^\d+$/.test(text.trim()) ? Number(text.trim()) : null;
  const valid = tokens !== null && tokens >= budget.minTokens && tokens <= budget.maxTokens;
  const range = `De ${number(budget.minTokens)} a ${number(budget.maxTokens)} tokens. Padrão: ${number(budget.defaultTokens)}.`;

  return (
    <li className={styles.budget}>
      <TextField
        label={`${budget.label} (tokens por execução)`}
        size="small"
        fullWidth
        inputMode="numeric"
        inputRef={target}
        value={text}
        disabled={busy}
        error={!valid}
        helperText={valid ? range : `Use um número inteiro. ${range}`}
        onChange={(event) => setText(event.target.value)}
      />
      <p className={styles.hint}>{budget.description}</p>
      <div className={styles.actions}>
        <Button
          type="button"
          disabled={busy || !valid || tokens === budget.tokens}
          aria-label={`Salvar o teto de ${budget.label}`}
          onClick={() => void after(onSave(budget, tokens === budget.defaultTokens ? null : tokens))}
        >
          Salvar teto
        </Button>
        {budget.isDefault ? null : (
          <Button
            type="button"
            variant="outlined"
            disabled={busy}
            aria-label={`Voltar o teto de ${budget.label} ao padrão`}
            onClick={() => void after(onSave(budget, null))}
          >
            Voltar ao padrão
          </Button>
        )}
      </div>
      <ActionFeedback feedback={feedback} target={`budget:${budget.purpose}`} />
    </li>
  );
}
