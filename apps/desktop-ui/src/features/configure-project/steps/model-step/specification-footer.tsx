import type { Discovery } from '@/shared/schema';

import { ChoiceField } from '../../components/choice-field';
import { RefreshGlyph } from '../../components/glyphs';
import styles from '../../components/panels.module.scss';

interface SpecificationFooterProps {
  state: Discovery['specification']['state'];
  updateSpecification: boolean;
  allowReinstall: boolean;
  onUpdateSpecification: (value: boolean) => void;
  onReinstall: (value: boolean) => void;
}

export function SpecificationFooter({
  state,
  updateSpecification,
  allowReinstall,
  onUpdateSpecification,
  onReinstall
}: SpecificationFooterProps) {
  const needsForce = state === 'drifted' || state === 'invalid';

  return (
    <div className={styles.specificationFooter}>
      {state === 'not_installed' ? (
        <span className={styles.muted}>
          A estrutura de especificação será instalada pelo instalador oficial somente se você aprovar o comando na
          revisão.
        </span>
      ) : null}
      {state === 'installed' ? (
        <label className={styles.subCard}>
          <input
            type="checkbox"
            checked={updateSpecification}
            onChange={(event) => onUpdateSpecification(event.target.checked)}
          />
          <span className={styles.subCardIcon} aria-hidden="true">
            <RefreshGlyph />
          </span>
          <strong className={styles.subCardTitle}>Buscar a versão mais recente da estrutura de especificação</strong>
          <span className={styles.subCardText}>
            O plano já propõe a atualização quando a versão instalada é anterior à mais recente conhecida. Marque para
            pedir ao instalador oficial a versão mais nova disponível. Usa a rede e só executa se você aprovar o
            comando.
          </span>
        </label>
      ) : null}
      {needsForce ? (
        <ChoiceField type="checkbox" checked={allowReinstall} onChange={onReinstall}>
          <span>
            Permitir reinstalar a estrutura de especificação
            <br />
            <span className={styles.muted}>
              Sobrescreve as edições locais. Sem esta permissão, a estrutura de especificação não é alterada.
            </span>
          </span>
        </ChoiceField>
      ) : null}
    </div>
  );
}
