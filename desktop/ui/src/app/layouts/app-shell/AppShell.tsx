import { useRunActivity } from '@/features/runs';
import { OutputLog } from '@/features/settings';
import { TerminalPanel } from '@/features/terminal';
import { tauriEngineClient } from '@/shared/api';
import { Container, Logo, PageTrailProvider, Wordmark } from '@/shared/components';
import { app } from '@/shared/config/app';
import { ContentAreaProvider, useEngineStatus, useScrollToTop } from '@/shared/hooks';
import { isWithin, pathTo, paths } from '@/shared/routing';
import { cn } from '@/shared/utils';
import { Button } from '@mariliz/ui-kit';
import { useCallback, useMemo, useRef, useState, type ReactNode } from 'react';
import { Link, useLocation } from 'react-router';
import { AppHeader } from './AppHeader';
import styles from './AppShell.module.scss';
import { BOTTOM_PANEL_ID, BottomPanel } from './bottom-panel/BottomPanel';
import { useBottomPanel } from './bottom-panel/use-bottom-panel';
import { CommandPalette } from './command-palette/CommandPalette';
import { useCommandPalette } from './command-palette/use-command-palette';
import { RailCollapsedProvider } from './rail-context';
import { RailToggle } from './RailToggle';
import { SettingsNav } from './SettingsNav';
import { ShortcutSheet } from './shortcuts/ShortcutSheet';
import { useShortcuts } from './shortcuts/use-shortcuts';
import { SideNav } from './SideNav';
import { StatusBar } from './StatusBar';
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
  const panel = useBottomPanel();
  const [sheetOpen, setSheetOpen] = useState(false);
  const showSheet = useCallback(() => setSheetOpen(true), []);
  const hideSheet = useCallback(() => setSheetOpen(false), []);

  // O terminal fica com o Tab (o shell completa comandos com ele): fechar o painel é a saída pelo teclado, e o foco
  // volta ao conteúdo em vez de sumir com o painel.
  const closePanel = () => {
    if (document.getElementById(BOTTOM_PANEL_ID)?.contains(document.activeElement)) main.current?.focus();
    panel.close();
  };

  useShortcuts({
    palette: palette.toggle,
    rail: rail.canToggle ? rail.toggle : undefined,
    panel: () => {
      if (panel.open) closePanel();
      else panel.show(panel.tab);
    },
    terminal: () => panel.show('terminal'),
    shortcuts: () => setSheetOpen((value) => !value)
  });
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
        <BottomPanel
          open={panel.open}
          tab={panel.tab}
          height={panel.height}
          onSelectTab={panel.selectTab}
          onResize={panel.resize}
          onClose={closePanel}
          terminal={<TerminalPanel hosted={engine.hosted} active={panel.open && panel.tab === 'terminal'} />}
          output={<OutputLog hosted={engine.hosted} active={panel.open && panel.tab === 'output'} />}
        />
      </div>

      <StatusBar engine={engine} panel={panel} onPanelTab={panel.toggleTab} onShortcuts={showSheet} />

      <CommandPalette open={palette.open} onClose={palette.hide} />
      <ShortcutSheet open={sheetOpen} onClose={hideSheet} />
    </div>
  );
}
