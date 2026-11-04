import { SectionCard } from '@/shared/components/ui';
import { SelectField } from '@synthra.io/ui-kit';

import { NodesGlyph } from '../../components/glyphs';
import styles from '../../components/panels.module.scss';
import { AI_ENVIRONMENTS, aiEnvironmentNames, isSupportedEnvironment } from '../../domain/ai-environments';

interface AiEnvironmentSelectProps {
  value: string | null;
  disabled: boolean;
  onChange: (id: string) => void;
}

const options = AI_ENVIRONMENTS.map((id) => ({
  value: id,
  label: `${aiEnvironmentNames[id]}${isSupportedEnvironment(id) ? '' : ' (sem suporte nesta fase)'}`
}));

export function AiEnvironmentSelect({ value, disabled, onChange }: AiEnvironmentSelectProps) {
  return (
    <SectionCard className={styles.fillCard} title="Ambiente de IA" icon={<NodesGlyph />}>
      <p className={styles.muted}>Selecione o ambiente que você utiliza para o desenvolvimento com IA.</p>
      <SelectField
        label="Ambiente de IA"
        options={options}
        value={value ?? ''}
        disabled={disabled}
        onChange={(event) => onChange(String(event.target.value))}
      />
    </SectionCard>
  );
}
