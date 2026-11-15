import { PageSection } from '@/shared/components';
import type { ModelBudget, ModelConnection } from '@/shared/schema';
import { Button, TextField } from '@mariliz/ui-kit';
import { useState } from 'react';

import styles from './budgets-section.module.scss';

interface BudgetsSectionProps {
  connection: ModelConnection;
  busy: boolean;
  onSave: (budget: ModelBudget, tokens: number | null) => void;
}

const number = (value: number) => value.toLocaleString('pt-BR');

export function BudgetsSection({ connection, busy, onSave }: BudgetsSectionProps) {
  return (
    <PageSection
      title="Teto de tokens por finalidade"
      description="Quanto uma execução pode gastar do modelo, entrada e resposta somadas. O motor cobra o teto antes e depois de cada chamada: o que não couber roda pelas regras do motor, sem o modelo. Um teto maior deixa o modelo ver e escrever mais, e custa mais."
    >
      <ul className={styles.budgets}>
        {connection.budgets.map((budget) => (
          // Remonta quando o motor devolve outro valor: o campo volta a mostrar o que vale.
          <BudgetRow key={`${budget.purpose}-${budget.tokens}`} budget={budget} busy={busy} onSave={onSave} />
        ))}
      </ul>
    </PageSection>
  );
}

function BudgetRow({
  budget,
  busy,
  onSave
}: {
  budget: ModelBudget;
  busy: boolean;
  onSave: BudgetsSectionProps['onSave'];
}) {
  const [text, setText] = useState(String(budget.tokens));
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
          onClick={() => onSave(budget, tokens === budget.defaultTokens ? null : tokens)}
        >
          Salvar teto
        </Button>
        {budget.isDefault ? null : (
          <Button
            type="button"
            variant="outlined"
            disabled={busy}
            aria-label={`Voltar o teto de ${budget.label} ao padrão`}
            onClick={() => onSave(budget, null)}
          >
            Voltar ao padrão
          </Button>
        )}
      </div>
    </li>
  );
}
