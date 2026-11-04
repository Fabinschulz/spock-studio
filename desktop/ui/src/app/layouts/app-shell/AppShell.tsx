import { Container, Logo, Wordmark } from '@/shared/components';
import { useEngineStatus } from '@/shared/hooks';
import { pathTo } from '@/shared/routing';
import { useRef, type ReactNode } from 'react';
import { Link } from 'react-router';
import { AppHeader } from './AppHeader';
import styles from './AppShell.module.scss';
import { EngineStatus } from './EngineStatus';
import { SideNav } from './SideNav';

const MAIN_CONTENT_ID = 'conteudo';

export function AppShell({ children }: { children: ReactNode }) {
  const main = useRef<HTMLElement>(null);
  const engine = useEngineStatus();

  return (
    <div className={styles.shell}>
      <button type="button" className={styles.skip} onClick={() => main.current?.focus()}>
        Ir para o conteúdo
      </button>

      <aside className={styles.rail}>
        <Link to={pathTo('home')} className={styles.brand} aria-label="Mariliz Studio, página inicial">
          <Logo className={styles.logo} />
          <Wordmark className={styles.wordmark} />
        </Link>
        <SideNav />
        <EngineStatus {...engine} />
      </aside>

      <div className={styles.column}>
        <AppHeader engine={engine} />
        <main id={MAIN_CONTENT_ID} ref={main} tabIndex={-1} className={styles.main}>
          <Container>{children}</Container>
        </main>
      </div>
    </div>
  );
}
