import type { Discovery } from '@/shared/schema';

import { ChoiceField } from '../../components/choice-field';
import { RefreshGlyph } from '../../components/glyphs';
import styles from '../../components/panels.module.scss';

interface TlcFooterProps {
  state: Discovery['tlc']['state'];
  updateTlc: boolean;
  allowReinstall: boolean;
  onUpdateTlc: (value: boolean) => void;
  onReinstall: (value: boolean) => void;
}

export function TlcFooter({ state, updateTlc, allowReinstall, onUpdateTlc, onReinstall }: TlcFooterProps) {
  const needsForce = state === 'drifted' || state === 'invalid';

  return (
    <div className={styles.tlcFooter}>
      {state === 'not_installed' ? (
        <span className={styles.muted}>
          O TLC será instalado pelo instalador oficial (@tech-leads-club/agent-skills) somente se você aprovar o comando
          na revisão.
        </span>
      ) : null}
      {state === 'installed' ? (
        <label className={styles.subCard}>
          <input type="checkbox" checked={updateTlc} onChange={(event) => onUpdateTlc(event.target.checked)} />
          <span className={styles.subCardIcon} aria-hidden="true">
            <RefreshGlyph />
          </span>
          <strong className={styles.subCardTitle}>Buscar a versão mais recente do TLC</strong>
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
            Permitir reinstalar o TLC
            <br />
            <span className={styles.muted}>
              Sobrescreve as edições locais da skill. Sem esta permissão, o TLC não é alterado.
            </span>
          </span>
        </ChoiceField>
      ) : null}
    </div>
  );
}
