import { SectionCard } from '@/shared/components/ui';

import { ChevronDownGlyph, NodesGlyph } from '../../components/glyphs';
import styles from '../../components/panels.module.scss';
import { AI_ENVIRONMENTS, aiEnvironmentNames, isSupportedEnvironment } from '../../domain/ai-environments';

interface AiEnvironmentSelectProps {
  value: string | null;
  disabled: boolean;
  onChange: (id: string) => void;
}

export function AiEnvironmentSelect({ value, disabled, onChange }: AiEnvironmentSelectProps) {
  return (
    <SectionCard className={styles.fillCard} title="Ambiente de IA" icon={<NodesGlyph />}>
      <p className={styles.muted}>Selecione o ambiente que você utiliza para o desenvolvimento com IA.</p>
      <span className={styles.selectWrap}>
        <select
          className={styles.select}
          aria-label="Ambiente de IA"
          value={value ?? ''}
          disabled={disabled}
          onChange={(event) => onChange(event.target.value)}
        >
          <option value="" disabled>
            Selecione o ambiente
          </option>
          {AI_ENVIRONMENTS.map((id) => (
            <option key={id} value={id}>
              {aiEnvironmentNames[id]}
              {isSupportedEnvironment(id) ? '' : ' (sem suporte nesta fase)'}
            </option>
          ))}
        </select>
        <ChevronDownGlyph />
      </span>
    </SectionCard>
  );
}
