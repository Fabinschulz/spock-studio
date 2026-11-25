import { Glyph, TrailNav, usePageTrail, WizardSteps } from '@/shared/components/ui';
import { Button } from '@mariliz/ui-kit';
import { useEffect, useRef } from 'react';
import { journeySteps } from '../../constants/journey-steps';
import { usePanelOpen } from '../../hooks/use-panel-open';
import { ImplementationModelBadge } from '../implementation-model-badge';
import styles from './journey-frame.module.scss';
import type { JourneyStepId } from '../../types/journey';
import type { JourneyFrameProps } from './types';

export function JourneyFrame({ step, issue, skipped, panel, live = false, onSelectStep, children }: JourneyFrameProps) {
  const { open, toggle, close } = usePanelOpen();
  const trail = usePageTrail();
  const toggleButton = useRef<HTMLButtonElement>(null);
  const panelElement = useRef<HTMLElement>(null);
  const hasPanel = panel !== undefined;
  const showPanel = hasPanel && open;
  const focusPanelWhenShown = useRef(false);

  // Abrir o painel pelo botão leva o foco para ele: em janelas estreitas ele cobre o conteúdo, e o painel só tem texto,
  // então sem isto o foco ficaria no botão, fora dele, e o Esc não teria a quem se dirigir. O painel que já nasce aberto
  // (janela larga) não toma o foco: ele fica ao lado do conteúdo, não por cima.
  useEffect(() => {
    if (!showPanel || !focusPanelWhenShown.current) return;
    focusPanelWhenShown.current = false;
    panelElement.current?.focus({ preventScroll: true });
  }, [showPanel]);

  // Esc, de dentro do painel, o fecha e devolve o foco ao botão que o abriu.
  // O ouvinte é registrado no elemento, e não como `onKeyDown` do <aside>, porque o <aside> não é um controle: o foco
  // chega a ele só por código, e o Esc sobe dos botões e links de dentro, se houver.
  useEffect(() => {
    const element = panelElement.current;
    if (!showPanel || !element) return;
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key !== 'Escape') return;
      close();
      toggleButton.current?.focus();
    };
    element.addEventListener('keydown', onKeyDown);
    return () => element.removeEventListener('keydown', onKeyDown);
  }, [showPanel, close]);

  return (
    <div className={styles.workbench} data-panel={showPanel ? 'open' : 'closed'}>
      <div className={styles.content}>
        <header className={styles.bar}>
          <div className={styles.heading}>
            <TrailNav trail={trail} />
            <ImplementationModelBadge />
          </div>
          {/* O título visível é o da etapa; o da tela fica na trilha, e o h1 o anuncia a quem usa leitor de tela. */}
          <h1 className="visually-hidden">Configurar projeto</h1>
          {hasPanel ? (
            <Button
              ref={toggleButton}
              variant="text"
              color="inherit"
              className={styles.toggle}
              aria-expanded={open}
              aria-controls="painel-contexto"
              aria-label={open ? 'Recolher o painel de contexto' : 'Abrir o painel de contexto'}
              onClick={() => {
                focusPanelWhenShown.current = !open;
                toggle();
              }}
            >
              <Glyph name="panel-right" size={17} />
              {live && !open ? <span className={styles.pip} aria-hidden="true" /> : null}
            </Button>
          ) : null}
        </header>
        <WizardSteps
          steps={journeySteps}
          currentId={step}
          skippedIds={skipped}
          issue={issue}
          onSelect={onSelectStep ? (id) => onSelectStep(id as JourneyStepId) : undefined}
        />
        {children}
      </div>

      {showPanel ? (
        <aside
          ref={panelElement}
          id="painel-contexto"
          className={styles.panel}
          aria-label="Contexto da configuração"
          tabIndex={-1}
        >
          <div className={styles.panelBody}>{panel}</div>
        </aside>
      ) : null}
    </div>
  );
}
