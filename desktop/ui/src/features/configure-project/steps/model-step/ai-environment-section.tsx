import { CheckboxOption } from '@/shared/components';
import type { Discovery } from '@/shared/schema';

import { EnvironmentGlyph } from '../../components/environment-glyph';
import {
  AI_ENVIRONMENTS,
  aiEnvironmentNames,
  isLockedEnvironment,
  isSupportedEnvironment,
  partialEnvironmentNote
} from '../../domain/ai-environments';
import styles from './model.module.scss';

interface AiEnvironmentSectionProps {
  discovery: Discovery;
  selected: readonly string[];
  onToggle: (id: string) => void;
}

export function AiEnvironmentSection({ discovery, selected, onToggle }: AiEnvironmentSectionProps) {
  const detectedIds = new Set(discovery.aiEnvironments.map((environment) => environment.id));
  const declared = new Set(discovery.declaredAiEnvironments);
  const leaving = discovery.declaredAiEnvironments.filter((id) => !selected.includes(id));

  return (
    <section aria-labelledby="ambiente-titulo" className={styles.section}>
      <h3 id="ambiente-titulo" className={styles.title}>
        Ambientes de IA
      </h3>
      <fieldset className={styles.fieldset}>
        <legend className="visually-hidden">Ambientes de IA do projeto</legend>
        <p className={styles.note}>
          Marque as IDEs que a equipe usa. Cada uma recebe só as peças dela, e a lista fica em spock.yaml.
        </p>
        <ul role="list" className={styles.rows}>
          {AI_ENVIRONMENTS.map((id) => {
            const supported = isSupportedEnvironment(id);
            const locked = isLockedEnvironment(discovery, id);
            const tags = [
              declared.has(id) ? 'declarado' : detectedIds.has(id) ? 'detectado' : '',
              supported ? (partialEnvironmentNote[id] ?? '') : 'ainda sem suporte'
            ];
            return (
              <li key={id}>
                <CheckboxOption
                  name="ai-environment"
                  value={id}
                  checked={selected.includes(id)}
                  disabled={locked}
                  onChange={() => onToggle(id)}
                  labelProps={{ className: styles.choice, 'aria-disabled': !supported || undefined }}
                  label={
                    <>
                      <EnvironmentGlyph id={id} />
                      <span className={styles.name}>{aiEnvironmentNames[id]}</span>
                      {supported ? <span className="visually-hidden">configuração suportada</span> : null}
                      <span className={styles.end}>{tags.filter(Boolean).join(' · ')}</span>
                    </>
                  }
                />
              </li>
            );
          })}
        </ul>
        {declared.has('claude-code') ? (
          <p className={styles.note}>
            O Claude Code continua marcado: tirá-lo de um projeto já configurado levaria agentes, regras e a estrutura
            de especificação, o que o plano ainda não faz.
          </p>
        ) : null}
        {leaving.length > 0 ? (
          <p className={styles.note} role="status">
            {leaving.map((id) => aiEnvironmentNames[id] ?? id).join(' e ')} {leaving.length > 1 ? 'saem' : 'sai'} da
            lista: o plano vai propor tirar o que o Spock gravou para {leaving.length > 1 ? 'elas' : 'ela'}, e o que é
            da equipe fica.
          </p>
        ) : null}
      </fieldset>
    </section>
  );
}
