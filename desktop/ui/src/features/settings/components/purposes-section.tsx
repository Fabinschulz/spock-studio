import { PageSection } from '@/shared/components';
import type { ModelConnection, ModelPurpose } from '@/shared/schema';
import { SelectField } from '@mariliz/ui-kit';
import { useMemo } from 'react';

import { defaultModelOf, purposeModelOptions, type PurposeOption } from '../domain/provider-labels';
import type { Feedback } from '../hooks/use-model-settings';
import { useRefocus } from '../hooks/use-refocus';
import { ActionFeedback } from './action-feedback';
import styles from './purposes-section.module.scss';

interface PurposesSectionProps {
  connection: ModelConnection;
  busy: boolean;
  feedback: Feedback | null;
  onChoose: (purpose: ModelPurpose, model: string | null) => Promise<boolean>;
}

export function PurposesSection({ connection, busy, feedback, onChoose }: PurposesSectionProps) {
  const options = useMemo(() => purposeModelOptions(connection), [connection]);
  const defaultModel = defaultModelOf(connection);

  return (
    <PageSection
      title="Modelo por finalidade"
      description="Cada tarefa pode usar um modelo diferente, de qualquer provedor com chave. Um modelo maior costuma responder melhor, custa mais e demora mais. O teto de tokens de cada execução fica na seção seguinte, e nenhum agente escolhe o modelo: a escolha é sua."
    >
      <ul className={styles.purposes}>
        {connection.purposes.map((purpose) => (
          <PurposeRow
            key={purpose.id}
            purpose={purpose}
            options={options}
            busy={busy}
            feedback={feedback}
            onChoose={(model) => onChoose(purpose, model === defaultModel ? null : model)}
          />
        ))}
      </ul>
    </PageSection>
  );
}

interface PurposeRowProps {
  purpose: ModelPurpose;
  options: PurposeOption[];
  busy: boolean;
  feedback: Feedback | null;
  onChoose: (model: string) => Promise<boolean>;
}

function PurposeRow({ purpose, options, busy, feedback, onChoose }: PurposeRowProps) {
  const { target, after } = useRefocus<{ focus: () => void }>(busy);

  return (
    <li className={styles.purpose}>
      <SelectField
        label={purpose.label}
        size="small"
        fullWidth
        inputRef={target}
        value={purpose.model}
        disabled={busy}
        onChange={(event) => void after(onChoose(String(event.target.value)))}
        options={options}
      />
      <p className={styles.hint}>{purpose.description}</p>
      <ActionFeedback feedback={feedback} target={`purpose:${purpose.id}`} />
    </li>
  );
}
