import { useEffect, useState } from 'react';
import { Outlet, useOutletContext } from 'react-router';

import { Glyph } from '../ui/glyph';

import { Rail } from './rail';
import styles from './shell.module.scss';

/** Largura da janela a partir da qual o painel de contexto cabe ao lado do conteúdo sem espremê-lo. */
const WIDE_QUERY = '(min-width: 1180px)';

export interface WorkbenchContext {
  wide: boolean;
  panelOpen: boolean;
  setPanelOpen: (open: boolean) => void;
}

export const useWorkbench = () => useOutletContext<WorkbenchContext>();

function useWide() {
  const [wide, setWide] = useState(() => window.matchMedia(WIDE_QUERY).matches);
  useEffect(() => {
    const query = window.matchMedia(WIDE_QUERY);
    const onChange = () => setWide(query.matches);
    query.addEventListener('change', onChange);
    return () => query.removeEventListener('change', onChange);
  }, []);
  return wide;
}

/** Moldura do workspace: menu lateral + área da tela. O painel de contexto é por tela e recolhível. */
export function Workbench() {
  const wide = useWide();
  const [panelOpen, setPanelOpen] = useState(wide);
  const [seenWide, setSeenWide] = useState(wide);

  // Ao estreitar a janela o painel vira gaveta e começa fechado; ao alargar, volta a ficar ao lado.
  if (seenWide !== wide) {
    setSeenWide(wide);
    setPanelOpen(wide);
  }

  return (
    <div className={styles.frame}>
      <div className={styles.demoStrip} role="note">
        <Glyph name="spark" size={13} />
        <span>
          <strong>Protótipo visual</strong> · dados de demonstração. Nada aqui lê, grava ou executa em um projeto.
        </span>
      </div>
      <div className={styles.body}>
        <Rail />
        <Outlet context={{ wide, panelOpen, setPanelOpen } satisfies WorkbenchContext} />
      </div>
    </div>
  );
}
