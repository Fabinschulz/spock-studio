import { ContextDrawer } from '@/prototype/components/context-drawer';
import { PanelToggle } from '@/prototype/components/panel-toggle';
import { useWorkbench } from '@/prototype/hooks/use-workbench';

import styles from './stage.module.scss';
import type { StageProps } from './types';

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
            <PanelToggle open={panelOpen} title={panelTitle} live={live} onToggle={() => setPanelOpen(!panelOpen)} />
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
        <ContextDrawer title={panelTitle} overlay={!wide} onClose={() => setPanelOpen(false)}>
          {panel}
        </ContextDrawer>
      ) : null}
    </div>
  );
}
