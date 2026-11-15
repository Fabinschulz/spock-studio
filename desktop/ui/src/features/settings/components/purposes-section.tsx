import { PageSection } from '@/shared/components';
import type { ModelConnection, ModelPurpose } from '@/shared/schema';
import { SelectField } from '@mariliz/ui-kit';
import { useMemo } from 'react';

import { defaultModelOf, purposeModelOptions } from '../domain/provider-labels';
import styles from './purposes-section.module.scss';

interface PurposesSectionProps {
  connection: ModelConnection;
  busy: boolean;
  onChoose: (purpose: ModelPurpose, model: string | null) => void;
}

export function PurposesSection({ connection, busy, onChoose }: PurposesSectionProps) {
  const options = useMemo(() => purposeModelOptions(connection), [connection]);
  const defaultModel = defaultModelOf(connection);

  return (
    <PageSection
      title="Modelo por finalidade"
      description="Cada tarefa pode usar um modelo diferente, de qualquer provedor com chave. Um modelo maior costuma responder melhor, custa mais e demora mais. O teto de tokens de cada execução fica na seção seguinte, e nenhum agente escolhe o modelo: a escolha é sua."
    >
      <ul className={styles.purposes}>
        {connection.purposes.map((purpose) => (
          <li key={purpose.id} className={styles.purpose}>
            <SelectField
              label={purpose.label}
              size="small"
              fullWidth
              value={purpose.model}
              disabled={busy}
              onChange={(event) => {
                const model = String(event.target.value);
                onChoose(purpose, model === defaultModel ? null : model);
              }}
              options={options}
            />
            <p className={styles.hint}>{purpose.description}</p>
          </li>
        ))}
      </ul>
    </PageSection>
  );
}
