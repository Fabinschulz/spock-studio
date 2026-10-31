import type { ReactNode } from 'react';

import { Glyph } from '../ui/glyph';

import styles from './shell.module.scss';
import { useWorkbench } from './workbench';

interface StageProps {
  /** Conteúdo da barra superior (título, trilha, progresso). */
  bar: ReactNode;
  /** Painel de contexto da tela. Sem painel, o botão de painel não aparece. */
  panel?: ReactNode;
  panelTitle?: string;
  /** Há algo em andamento: o botão do painel ganha um ponto de atividade (e um texto para leitor de tela). */
  live?: boolean;
  /** Ações da tela, ancoradas ao pé (nunca sobre o conteúdo). */
  actions?: ReactNode;
  actionsNote?: ReactNode;
  children: ReactNode;
}

export function Stage({
  bar,
  panel,
  panelTitle = 'Contexto',
  live = false,
  actions,
  actionsNote,
  children
}: StageProps) {
  const { wide, panelOpen, setPanelOpen } = useWorkbench();
  const showPanel = Boolean(panel) && panelOpen;

  return (
    <div className={styles.stage}>
      <div className={styles.main}>
        <header className={styles.bar}>
          <div className={styles.barMain}>{bar}</div>
          {panel ? (
            <button
              type="button"
              className={styles.iconButton}
              aria-expanded={panelOpen}
              aria-controls="painel-contexto"
              aria-label={panelOpen ? `Recolher ${panelTitle.toLowerCase()}` : `Abrir ${panelTitle.toLowerCase()}`}
              onClick={() => setPanelOpen(!panelOpen)}
            >
              <Glyph name="panel-right" size={17} />
              {live && !panelOpen ? <span className={styles.livePip} aria-hidden="true" /> : null}
            </button>
          ) : null}
        </header>

        <div className={styles.scroll}>
          <div className={styles.column}>{children}</div>
        </div>

        {actions ? (
          <footer className={styles.actions}>
            {actionsNote ? <p className={styles.actionsNote}>{actionsNote}</p> : null}
            {actions}
          </footer>
        ) : null}
      </div>

      {showPanel ? (
        <aside
          id="painel-contexto"
          className={`${styles.panel} ${wide ? '' : styles.panelOverlay}`}
          aria-label={panelTitle}
        >
          <div className={styles.panelHead}>
            <h2 className={styles.panelTitle}>{panelTitle}</h2>
            <button
              type="button"
              className={styles.iconButton}
              aria-label={`Recolher ${panelTitle.toLowerCase()}`}
              onClick={() => setPanelOpen(false)}
            >
              <Glyph name="x" size={15} />
            </button>
          </div>
          <div className={styles.panelBody}>{panel}</div>
        </aside>
      ) : null}
    </div>
  );
}
