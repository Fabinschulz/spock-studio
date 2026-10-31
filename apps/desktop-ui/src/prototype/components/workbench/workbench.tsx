import { useState } from 'react';
import { Outlet } from 'react-router';

import { Glyph } from '@/shared/components/ui';

import { Rail } from '@/prototype/components/rail';
import { useWideWindow } from '@/prototype/hooks/use-wide-window';
import type { WorkbenchContext } from '@/prototype/types/shell';

import styles from './workbench.module.scss';

export function Workbench() {
  const wide = useWideWindow();
  const [panelOpen, setPanelOpen] = useState(wide);
  const [seenWide, setSeenWide] = useState(wide);

  // O painel acompanha a largura da janela: aberto quando cabe ao lado, fechado quando vira gaveta.
  if (seenWide !== wide) {
    setSeenWide(wide);
    setPanelOpen(wide);
  }

  const context: WorkbenchContext = { wide, panelOpen, setPanelOpen };

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
        <Outlet context={context} />
      </div>
    </div>
  );
}
