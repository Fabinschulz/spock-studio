import { useRunActivity } from '@/features/runs';
import { tauriEngineClient } from '@/shared/api';
import { Container, Logo, PageTrailProvider, Wordmark } from '@/shared/components';
import { app } from '@/shared/config/app';
import { ContentAreaProvider, useEngineStatus, useScrollToTop } from '@/shared/hooks';
import { isWithin, pathTo, paths } from '@/shared/routing';
import { cn } from '@/shared/utils';
import { Button } from '@mariliz/ui-kit';
import { useMemo, useRef, type ReactNode } from 'react';
import { Link, useLocation } from 'react-router';
import { AppHeader } from './AppHeader';
import styles from './AppShell.module.scss';
import { CommandPalette } from './command-palette/CommandPalette';
import { useCommandPalette } from './command-palette/use-command-palette';
import { EngineStatus } from './EngineStatus';
import { RailCollapsedProvider } from './rail-context';
import { RailToggle } from './RailToggle';
import { SettingsNav } from './SettingsNav';
import { SideNav } from './SideNav';
import { trailOf } from './trail';
import { useEnvironment } from './use-environment';
import { useRail } from './use-rail';

const MAIN_CONTENT_ID = 'conteudo';

export function AppShell({ children }: { children: ReactNode }) {
  const main = useRef<HTMLElement>(null);
  const engine = useEngineStatus();
  const environment = useEnvironment();
  const { pathname } = useLocation();
  const trail = useMemo(() => trailOf(pathname), [pathname]);
  const palette = useCommandPalette();
  const rail = useRail();
  // Ao trocar de tela o indicador relê: uma aprovação pode ter chegado enquanto a pessoa olhava outra coisa.
  const runActivity = useRunActivity(tauriEngineClient, engine.status === 'ready', pathname);

  // Trocar de tela começa no topo e leva o foco para o conteúdo: o <main> é quem rola, e o foco não pode ficar num
  // item de menu que já não representa o que está na tela.
  useScrollToTop(pathname, { focus: true, container: main });

  return (
    <div className={cn(styles.shell, rail.collapsed && styles.collapsed)}>
      <Button variant="outlined" color="inherit" className={styles.skip} onClick={() => main.current?.focus()}>
        Ir para o conteúdo
      </Button>

      <aside className={styles.rail}>
        <Link to={pathTo('home')} className={styles.brand} aria-label={`${app.name}, página inicial`}>
          <Logo className={styles.logo} />
          <Wordmark className={styles.wordmark} />
        </Link>
        <RailCollapsedProvider value={rail.collapsed}>
          {isWithin(pathname, paths.settings) ? (
            <SettingsNav environment={environment} />
          ) : (
            <SideNav environment={environment} runActivity={runActivity} />
          )}
          {rail.canToggle ? <RailToggle collapsed={rail.collapsed} onToggle={rail.toggle} /> : null}
        </RailCollapsedProvider>
      </aside>

      <div className={styles.column}>
        <AppHeader environment={environment} onSearch={palette.show} />
        <main id={MAIN_CONTENT_ID} ref={main} tabIndex={-1} className={styles.main}>
          <ContentAreaProvider value={main}>
            <PageTrailProvider value={trail}>
              <Container>{children}</Container>
            </PageTrailProvider>
          </ContentAreaProvider>
        </main>
      </div>

      <footer className={styles.statusbar}>
        <EngineStatus {...engine} />
        <p className={styles.version}>
          <span className="visually-hidden">Versão </span>v{app.version}
        </p>
      </footer>

      <CommandPalette open={palette.open} onClose={palette.hide} />
    </div>
  );
}
