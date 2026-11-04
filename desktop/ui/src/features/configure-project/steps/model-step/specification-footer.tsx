import type { Discovery } from '@/shared/schema';
import { Checkbox } from '@synthra.io/ui-kit';
import { useId } from 'react';
import { CheckboxField } from '../../components/choice-field';
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
  const updateHintId = useId();

  return (
    <div className={styles.specificationFooter}>
      {state === 'not_installed' ? (
        <span className={styles.muted}>
          A estrutura de especificação será instalada pelo instalador oficial somente se você aprovar o comando na
          revisão.
        </span>
      ) : null}
      {state === 'installed' ? (
        <div className={styles.subCard}>
          <span className={styles.subCardIcon} aria-hidden="true">
            <RefreshGlyph />
          </span>
          <div className={styles.subCardBody}>
            <Checkbox
              label="Buscar a versão mais recente da estrutura de especificação"
              checked={updateSpecification}
              onChange={(_, value) => onUpdateSpecification(value)}
              slotProps={{ input: { 'aria-describedby': updateHintId } }}
            />
            <span id={updateHintId} className={styles.subCardText}>
              O plano já propõe a atualização quando a versão instalada é anterior à mais recente conhecida. Marque para
              pedir ao instalador oficial a versão mais nova disponível. Usa a rede e só executa se você aprovar o
              comando.
            </span>
          </div>
        </div>
      ) : null}
      {needsForce ? (
        <CheckboxField
          checked={allowReinstall}
          onChange={onReinstall}
          label="Permitir reinstalar a estrutura de especificação"
          description="Sobrescreve as edições locais. Sem esta permissão, a estrutura de especificação não é alterada."
        />
      ) : null}
    </div>
  );
}
