import { app } from '@/shared/config/app';
import type { AiRuntimeStatus, EngineStatus as Status } from '@/shared/hooks';
import { paths } from '@/shared/routing';
import { cn } from '@/shared/utils';
import { Link } from 'react-router';

import styles from './AppShell.module.scss';
import { BOTTOM_PANEL_ID } from './bottom-panel/BottomPanel';
import type { PanelTab } from './bottom-panel/use-bottom-panel';
import { EngineStatus } from './EngineStatus';
import { shortcutLabel } from './shortcuts/shortcuts';

interface StatusBarProps {
  engine: { status: Status; hosted: boolean; aiRuntime?: AiRuntimeStatus };
  panel: { open: boolean; tab: PanelTab };
  onPanelTab: (tab: PanelTab) => void;
  onShortcuts: () => void;
}

const PANEL_BUTTONS: readonly { tab: PanelTab; label: string }[] = [
  { tab: 'terminal', label: 'Terminal' },
  { tab: 'output', label: 'Saída' }
];

/** O estado do motor, os atalhos para o painel inferior e a versão: o que a pessoa consulta sem sair da tela. */
export function StatusBar({ engine, panel, onPanelTab, onShortcuts }: StatusBarProps) {
  return (
    <footer className={styles.statusbar}>
      <EngineStatus {...engine} />
      <div className={styles.statusActions}>
        {PANEL_BUTTONS.map(({ tab, label }) => {
          const showing = panel.open && panel.tab === tab;
          return (
            <button
              key={tab}
              type="button"
              className={cn(styles.statusButton, showing && styles.statusButtonActive)}
              aria-expanded={showing}
              aria-controls={showing ? BOTTOM_PANEL_ID : undefined}
              onClick={() => onPanelTab(tab)}
            >
              {label}
            </button>
          );
        })}
        <button
          type="button"
          className={styles.statusButton}
          aria-label={`Atalhos de teclado (${shortcutLabel('shortcuts')})`}
          onClick={onShortcuts}
        >
          Atalhos
        </button>
        {/* Configurações saiu do menu: é preferência do aplicativo, não tela de trabalho. */}
        <Link to={paths.settings} className={styles.statusButton}>
          Configurações
        </Link>
        <p className={styles.version}>
          <span className="visually-hidden">Versão </span>v{app.version}
        </p>
      </div>
    </footer>
  );
}
