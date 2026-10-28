import { useRef, type ReactNode } from 'react';
import { Link } from 'react-router';

import { Container, Logo, Wordmark } from '@/shared/components';
import { app } from '@/shared/config/app';
import { pathTo } from '@/shared/routing';

import styles from './AppShell.module.scss';
import { EngineStatus } from './EngineStatus';
import { SideNav } from './SideNav';

export const MAIN_CONTENT_ID = 'conteudo';

/**
 * Moldura da janela: menu lateral (marca, navegação e estado do motor) e a área de conteúdo.
 * A barra de título é a nativa do sistema; o menu encolhe para ícones quando a janela é estreita.
 */
export function AppShell({ children }: { children: ReactNode }) {
  const main = useRef<HTMLElement>(null);

  return (
    <div className={styles.shell}>
      {/* Com o roteador por hash, âncoras (#conteudo) trocariam de rota: o atalho move o foco direto. */}
      <button type="button" className={styles.skip} onClick={() => main.current?.focus()}>
        Ir para o conteúdo
      </button>

      <aside className={styles.rail}>
        <div className={styles.brandBlock}>
          <Link to={pathTo('home')} className={styles.brand} aria-label="Mariliz Studio, página inicial">
            <Logo />
            <span className={styles.brandText} aria-hidden="true">
              <Wordmark />
              <span className={styles.studio}>Studio</span>
            </span>
          </Link>
          <p className={styles.tagline}>{app.tagline}</p>
        </div>
        <SideNav />
        <EngineStatus />
      </aside>

      <main id={MAIN_CONTENT_ID} ref={main} tabIndex={-1} className={styles.main}>
        <Container>{children}</Container>
      </main>
    </div>
  );
}
