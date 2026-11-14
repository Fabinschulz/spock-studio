import { useRunActivity } from '@/features/runs';
import { tauriEngineClient } from '@/shared/api';
import { Container, Logo, PageTrailProvider, Wordmark } from '@/shared/components';
import { app } from '@/shared/config/app';
import { ContentAreaProvider, useEngineStatus, useScrollToTop } from '@/shared/hooks';
import { pathTo } from '@/shared/routing';
import { Button } from '@mariliz/ui-kit';
import { useMemo, useRef, type ReactNode } from 'react';
import { Link, useLocation } from 'react-router';
import { AppHeader } from './AppHeader';
import styles from './AppShell.module.scss';
import { EngineStatus } from './EngineStatus';
import { useEnvironment } from './use-environment';
import { SideNav } from './SideNav';
import { trailOf } from './trail';

const MAIN_CONTENT_ID = 'conteudo';

export function AppShell({ children }: { children: ReactNode }) {
  const main = useRef<HTMLElement>(null);
  const engine = useEngineStatus();
  const environment = useEnvironment();
  const { pathname } = useLocation();
  const trail = useMemo(() => trailOf(pathname), [pathname]);
  // Ao trocar de tela o indicador relê: uma aprovação pode ter chegado enquanto a pessoa olhava outra coisa.
  const runActivity = useRunActivity(tauriEngineClient, engine.status === 'ready', pathname);

  // Trocar de tela começa no topo e leva o foco para o conteúdo: o <main> é quem rola, e o foco não pode ficar num
  // item de menu que já não representa o que está na tela.
  useScrollToTop(pathname, { focus: true, container: main });

  return (
    <div className={styles.shell}>
      <Button variant="outlined" color="inherit" className={styles.skip} onClick={() => main.current?.focus()}>
        Ir para o conteúdo
      </Button>

      <aside className={styles.rail}>
        <Link to={pathTo('home')} className={styles.brand} aria-label={`${app.name}, página inicial`}>
          <Logo className={styles.logo} />
          <Wordmark className={styles.wordmark} />
        </Link>
        <SideNav environment={environment} runActivity={runActivity} />
        <EngineStatus {...engine} />
        <p className={styles.version}>
          <span className="visually-hidden">Versão </span>v{app.version}
        </p>
      </aside>

      <div className={styles.column}>
        <AppHeader environment={environment} />
        <main id={MAIN_CONTENT_ID} ref={main} tabIndex={-1} className={styles.main}>
          <ContentAreaProvider value={main}>
            <PageTrailProvider value={trail}>
              <Container>{children}</Container>
            </PageTrailProvider>
          </ContentAreaProvider>
        </main>
      </div>
    </div>
  );
}
