import { CloseIcon } from '@mariliz/ui-kit';
import { useId, useRef, useState, type KeyboardEvent, type ReactNode } from 'react';

import { PanelResizer } from './PanelResizer';
import type { PanelTab } from './use-bottom-panel';
import styles from './BottomPanel.module.scss';

export const BOTTOM_PANEL_ID = 'painel-inferior';

const TABS: readonly { id: PanelTab; label: string }[] = [
  { id: 'terminal', label: 'Terminal' },
  { id: 'output', label: 'Saída' }
];

interface BottomPanelProps {
  open: boolean;
  tab: PanelTab;
  height: number;
  onSelectTab: (tab: PanelTab) => void;
  onResize: (height: number) => void;
  onClose: () => void;
  terminal: ReactNode;
  output: ReactNode;
}

/**
 * Abas no padrão do ARIA: setas trocam a aba, e só a aba atual entra na ordem do Tab. Uma aba só monta quando é vista
 * pela primeira vez e depois só se esconde: o terminal continua vivo ao trocar de aba ou fechar o painel.
 */
export function BottomPanel({ open, tab, height, onSelectTab, onResize, onClose, terminal, output }: BottomPanelProps) {
  const baseId = useId();
  const tabs = useRef<Partial<Record<PanelTab, HTMLButtonElement | null>>>({});
  const tabId = (id: PanelTab) => `${baseId}-${id}`;
  const [visited, setVisited] = useState<readonly PanelTab[]>([]);
  if (open && !visited.includes(tab)) setVisited([...visited, tab]);
  const content: Record<PanelTab, ReactNode> = { terminal, output };

  const onTabKeyDown = (event: KeyboardEvent<HTMLButtonElement>) => {
    if (event.key !== 'ArrowLeft' && event.key !== 'ArrowRight') return;
    event.preventDefault();
    const index = TABS.findIndex((entry) => entry.id === tab);
    const next = TABS[(index + (event.key === 'ArrowRight' ? 1 : -1) + TABS.length) % TABS.length]!;
    onSelectTab(next.id);
    tabs.current[next.id]?.focus();
  };

  return (
    <section
      id={BOTTOM_PANEL_ID}
      className={styles.panel}
      style={{ height }}
      aria-label="Painel inferior"
      hidden={!open}
    >
      <PanelResizer height={height} onResize={onResize} />
      <div className={styles.bar}>
        <div role="tablist" aria-label="Painel inferior" className={styles.tabs}>
          {TABS.map((entry) => (
            <button
              key={entry.id}
              ref={(element) => {
                tabs.current[entry.id] = element;
              }}
              type="button"
              role="tab"
              id={tabId(entry.id)}
              aria-selected={entry.id === tab}
              aria-controls={`${tabId(entry.id)}-conteudo`}
              tabIndex={entry.id === tab ? 0 : -1}
              className={styles.tab}
              onClick={() => onSelectTab(entry.id)}
              onKeyDown={onTabKeyDown}
            >
              {entry.label}
            </button>
          ))}
        </div>
        <button type="button" className={styles.close} aria-label="Fechar o painel inferior" onClick={onClose}>
          <CloseIcon fontSize="small" aria-hidden="true" />
        </button>
      </div>
      {TABS.filter((entry) => visited.includes(entry.id)).map((entry) => (
        <div
          key={entry.id}
          role="tabpanel"
          id={`${tabId(entry.id)}-conteudo`}
          aria-labelledby={tabId(entry.id)}
          className={styles.body}
          hidden={entry.id !== tab}
        >
          {content[entry.id]}
        </div>
      ))}
    </section>
  );
}
