import type { Discovery } from '@/shared/schema';

import { EnvironmentGlyph } from '../../components/environment-glyph';
import { AI_ENVIRONMENTS, aiEnvironmentNames, isSupportedEnvironment } from '../../domain/ai-environments';
import styles from './model.module.scss';

interface AiEnvironmentSectionProps {
  detected: Discovery['aiEnvironments'];
  selected: string | null;
  onSelect: (id: string) => void;
}

export function AiEnvironmentSection({ detected, selected, onSelect }: AiEnvironmentSectionProps) {
  const detectedIds = new Set(detected.map((environment) => environment.id));

  return (
    <section aria-labelledby="ambiente-titulo" className={styles.section}>
      <h3 id="ambiente-titulo" className={styles.title}>
        Ambiente de IA
      </h3>
      <fieldset className={styles.fieldset}>
        <legend className="visually-hidden">Ambiente de IA do projeto</legend>
        <p className={styles.note}>
          Escolha o ambiente em que você trabalha. Ele define onde agentes, skills, hooks e regras serão escritos.
        </p>
        <ul role="list" className={styles.rows}>
          {AI_ENVIRONMENTS.map((id) => {
            const supported = isSupportedEnvironment(id);
            const tags = [detectedIds.has(id) ? 'detectado' : '', supported ? '' : 'sem suporte nesta fase'];
            return (
              <li key={id}>
                <label className={styles.choice} data-checked={selected === id} aria-disabled={!supported || undefined}>
                  <input
                    type="radio"
                    name="ai-environment"
                    value={id}
                    checked={selected === id}
                    onChange={() => onSelect(id)}
                  />
                  <EnvironmentGlyph id={id} />
                  <span className={styles.name}>{aiEnvironmentNames[id]}</span>
                  {supported ? <span className="visually-hidden">configuração suportada</span> : null}
                  <span className={styles.end}>{tags.filter(Boolean).join(' · ')}</span>
                </label>
              </li>
            );
          })}
        </ul>
      </fieldset>
    </section>
  );
}
