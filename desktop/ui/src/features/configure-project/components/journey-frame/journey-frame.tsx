import { Glyph, WizardSteps } from '@/shared/components/ui';
import { Button } from '@mariliz/ui-kit';
import { journeySteps } from '../../constants/journey-steps';
import { usePanelOpen } from '../../hooks/use-panel-open';
import { ImplementationModelBadge } from '../implementation-model-badge';
import styles from './journey-frame.module.scss';
import type { JourneyFrameProps } from './types';

export function JourneyFrame({ step, issue, panel, live = false, children }: JourneyFrameProps) {
  const { open, toggle } = usePanelOpen();
  const hasPanel = panel !== undefined;

  return (
    <div className={styles.workbench} data-panel={hasPanel && open ? 'open' : 'closed'}>
      <div className={styles.content}>
        <header className={styles.bar}>
          <div className={styles.heading}>
            <h1 className={styles.title}>Configurar projeto</h1>
            <ImplementationModelBadge />
          </div>
          {hasPanel ? (
            <Button
              variant="text"
              color="inherit"
              className={styles.toggle}
              aria-expanded={open}
              aria-controls="painel-contexto"
              aria-label={open ? 'Recolher o painel de contexto' : 'Abrir o painel de contexto'}
              onClick={toggle}
            >
              <Glyph name="panel-right" size={17} />
              {live && !open ? <span className={styles.pip} aria-hidden="true" /> : null}
            </Button>
          ) : null}
        </header>
        <WizardSteps steps={journeySteps} currentId={step} issue={issue} />
        {children}
      </div>

      {hasPanel && open ? (
        <aside id="painel-contexto" className={styles.panel} aria-label="Contexto da configuração">
          <div className={styles.panelBody}>{panel}</div>
        </aside>
      ) : null}
    </div>
  );
}
