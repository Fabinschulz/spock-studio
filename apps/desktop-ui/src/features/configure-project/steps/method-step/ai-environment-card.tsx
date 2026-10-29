import { SectionCard } from '@/shared/components/ui';
import type { Discovery } from '@/shared/schema';
import { cn } from '@/shared/utils';

import { EnvironmentGlyph } from '../../components/environment-glyph';
import { LockGlyph, NodesGlyph } from '../../components/glyphs';
import styles from '../../components/panels.module.scss';
import { AI_ENVIRONMENTS, aiEnvironmentNames, isSupportedEnvironment } from '../../domain/ai-environments';

interface AiEnvironmentCardProps {
  detected: Discovery['aiEnvironments'];
  selected: string | null;
  onSelect: (id: string) => void;
}

export function AiEnvironmentCard({ detected, selected, onSelect }: AiEnvironmentCardProps) {
  const detectedIds = new Set(detected.map((environment) => environment.id));

  return (
    <SectionCard className={cn(styles.areaEnv, styles.fillCard)} title="Ambiente de IA" icon={<NodesGlyph />}>
      <fieldset className={styles.fieldset}>
        <legend className="visually-hidden">Ambiente de IA do projeto</legend>
        <p className={styles.muted}>Selecione o ambiente que será utilizado.</p>
        {AI_ENVIRONMENTS.map((id) => (
          <label key={id} className={cn(styles.envItem, !isSupportedEnvironment(id) && styles.envMuted)}>
            <input
              type="radio"
              name="ai-environment"
              value={id}
              checked={selected === id}
              onChange={() => onSelect(id)}
            />
            <EnvironmentGlyph id={id} />
            <span className={styles.envName}>{aiEnvironmentNames[id]}</span>
            {detectedIds.has(id) ? <span className={styles.envTag}>detectado</span> : null}
            {isSupportedEnvironment(id) ? (
              <span className="visually-hidden">configuração suportada</span>
            ) : (
              <span className={cn(styles.envTag, styles.envTagWarn)}>
                sem suporte<span className="visually-hidden"> nesta fase</span>
              </span>
            )}
          </label>
        ))}
        <p className={styles.cardNote}>
          <LockGlyph />
          <span>
            O ambiente selecionado será usado para configurar os agentes, skills, hooks e demais componentes do Harness.
          </span>
        </p>
      </fieldset>
    </SectionCard>
  );
}
