import { useState, type ReactNode } from 'react';

import { Glyph, WizardSteps, type StepIssue } from '@/shared/components/ui';

import { journeySteps, type JourneyStepId } from '../domain/journey-state';
import { ImplementationModelBadge } from './implementation-model-badge';
import styles from './journey-frame.module.scss';

interface JourneyFrameProps {
  step: JourneyStepId;
  issue?: StepIssue;
  /** Conteúdo do painel de contexto (resumo, participantes, atividade). Sem painel, o botão não aparece. */
  panel?: ReactNode;
  /** Há uma execução em andamento: o botão do painel ganha um ponto de atividade quando o painel está fechado. */
  live?: boolean;
  children: ReactNode;
}

/** O painel começa aberto só quando cabe ao lado do conteúdo; senão é uma gaveta que a pessoa abre. */
const startsOpen = () => typeof window.matchMedia === 'function' && window.matchMedia('(min-width: 1180px)').matches;

export function JourneyFrame({ step, issue, panel, live = false, children }: JourneyFrameProps) {
  const [open, setOpen] = useState(startsOpen);
  const showPanel = panel !== undefined && open;

  return (
    <div className={styles.workbench} data-panel={showPanel ? 'open' : 'closed'}>
      <div className={styles.content}>
        <header className={styles.bar}>
          <div className={styles.heading}>
            <h1 className={styles.title}>Configurar projeto</h1>
            <ImplementationModelBadge />
          </div>
          {panel !== undefined ? (
            <button
              type="button"
              className={styles.toggle}
              aria-expanded={open}
              aria-controls="painel-contexto"
              aria-label={open ? 'Recolher o painel de contexto' : 'Abrir o painel de contexto'}
              onClick={() => setOpen((value) => !value)}
            >
              <Glyph name="panel-right" size={17} />
              {live && !open ? <span className={styles.pip} aria-hidden="true" /> : null}
            </button>
          ) : null}
        </header>
        <WizardSteps steps={journeySteps} currentId={step} issue={issue} />
        {children}
      </div>

      {showPanel ? (
        <aside id="painel-contexto" className={styles.panel} aria-label="Contexto da configuração">
          <div className={styles.panelBody}>{panel}</div>
        </aside>
      ) : null}
    </div>
  );
}
