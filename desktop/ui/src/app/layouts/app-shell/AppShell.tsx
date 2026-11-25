import { ACTIVITY_PANEL_ID, ActivityPanel, useRunActivity } from '@/features/runs';
import { OutputLog } from '@/features/settings';
import { TerminalPanel } from '@/features/terminal';
import { tauriEngineClient } from '@/shared/api';
import { Container, Logo, PageTabsProvider, PageTrailProvider } from '@/shared/components';
import { app } from '@/shared/config/app';
import { ContentAreaProvider, useEngineStatus, useScrollToTop } from '@/shared/hooks';
import { pathTo } from '@/shared/routing';
import { Button, LayoutLeftLineIcon } from '@mariliz/ui-kit';
import { useCallback, useMemo, useRef, useState, type ReactNode } from 'react';
import { Link, useLocation } from 'react-router';
import { ActivityBar } from './ActivityBar';
import { ACTIVITY_TOGGLE_ID, AppHeader } from './AppHeader';
import { AREA_PANEL_ID, AreaPanel } from './AreaPanel';
import styles from './AppShell.module.scss';
import { BOTTOM_PANEL_ID, BottomPanel } from './bottom-panel/BottomPanel';
import { useBottomPanel, type PanelTab } from './bottom-panel/use-bottom-panel';
import { CommandPalette } from './command-palette/CommandPalette';
import { useCommandPalette } from './command-palette/use-command-palette';
import { areaOf, describeRunActivity } from './navigation';
import { ShortcutSheet } from './shortcuts/ShortcutSheet';
import { useShortcuts } from './shortcuts/use-shortcuts';
import { StatusBar } from './StatusBar';
import { tabSetOf, trailOf } from './trail';
import { useActivityPanel } from './use-activity-panel';
import { useRail } from './use-rail';

const MAIN_CONTENT_ID = 'conteudo';

export function AppShell({ children }: { children: ReactNode }) {
  const main = useRef<HTMLElement>(null);
  const engine = useEngineStatus();
  const { pathname } = useLocation();
  const area = useMemo(() => areaOf(pathname), [pathname]);
  const trail = useMemo(() => trailOf(pathname), [pathname]);
  const tabs = useMemo(() => tabSetOf(pathname), [pathname]);
  const palette = useCommandPalette();
  const rail = useRail(pathname);
  const panel = useBottomPanel();
  const activity = useActivityPanel();
  const [sheetOpen, setSheetOpen] = useState(false);
  const showSheet = useCallback(() => setSheetOpen(true), []);
  const hideSheet = useCallback(() => setSheetOpen(false), []);
  const hasPanel = area !== null && area.items.length > 0;

  // Fechar com o foco dentro do painel devolve o foco a quem o abre, em vez de deixá-lo sumir com o painel.
  const closeActivity = () => {
    if (document.getElementById(ACTIVITY_PANEL_ID)?.contains(document.activeElement))
      document.getElementById(ACTIVITY_TOGGLE_ID)?.focus();
    activity.hide();
  };
  const toggleActivity = () => (activity.open ? closeActivity() : activity.show());

  // O terminal fica com o Tab (o shell completa comandos com ele): fechar o painel é a saída pelo teclado, e o foco
  // volta ao conteúdo em vez de sumir com o painel.
  const closePanel = () => {
    if (document.getElementById(BOTTOM_PANEL_ID)?.contains(document.activeElement)) main.current?.focus();
    panel.close();
  };

  // Pedir o terminal (atalho ou botão) leva o foco para ele; o painel que reabre sozinho ao iniciar, não.
  const [terminalRequests, setTerminalRequests] = useState(0);
  const openTerminal = () => {
    panel.show('terminal');
    setTerminalRequests((value) => value + 1);
  };
  const togglePanelTab = (tab: PanelTab) => {
    if (tab === 'terminal' && !(panel.open && panel.tab === 'terminal')) openTerminal();
    else panel.toggleTab(tab);
  };

  // Os mesmos gestos servem ao teclado e à paleta de comandos.
  const shellActions = {
    rail: hasPanel ? rail.toggle : undefined,
    activity: toggleActivity,
    terminal: openTerminal,
    shortcuts: () => setSheetOpen((value) => !value)
  };
  useShortcuts({
    ...shellActions,
    palette: palette.toggle,
    panel: () => {
      if (panel.open) closePanel();
      else panel.show(panel.tab);
    }
  });
  // Ao trocar de tela o indicador relê: uma aprovação pode ter chegado enquanto a pessoa olhava outra coisa.
  const runActivity = useRunActivity(tauriEngineClient, engine.status === 'ready', pathname);

  // Trocar de tela começa no topo e leva o foco para o conteúdo: o <main> é quem rola, e o foco não pode ficar num
  // item de menu que já não representa o que está na tela.
  useScrollToTop(pathname, { focus: true, container: main });

  const runNotice = describeRunActivity(runActivity);

  return (
    <div className={styles.shell}>
      <Button variant="outlined" color="inherit" className={styles.skip} onClick={() => main.current?.focus()}>
        Ir para o conteúdo
      </Button>

      <div className={styles.bar}>
        <Link to={pathTo('home')} className={styles.brand} aria-label={`${app.name}, página inicial`}>
          <Logo className={styles.logo} />
        </Link>
        <ActivityBar current={area?.id ?? null} activity={runNotice} compact={rail.compact} />
        {hasPanel ? (
          <Button
            variant="text"
            color="inherit"
            className={styles.area}
            aria-label={rail.collapsed ? 'Expandir menu' : 'Recolher menu'}
            aria-expanded={!rail.collapsed}
            aria-controls={rail.collapsed ? undefined : AREA_PANEL_ID}
            onClick={rail.toggle}
          >
            <span className={styles.areaIcon} aria-hidden="true">
              <LayoutLeftLineIcon fontSize="small" />
            </span>
            {/* O nome acessível completo contém o texto visível, como pede a regra de rótulo no nome. */}
            <span className={styles.areaLabel}>{rail.collapsed ? 'Expandir' : 'Recolher'}</span>
          </Button>
        ) : null}
      </div>

      {hasPanel && !rail.collapsed ? (
        <AreaPanel area={area} activity={runNotice} overlay={rail.overlay} onDismiss={rail.dismiss} />
      ) : null}

      <div className={styles.column}>
        <AppHeader
          onSearch={palette.show}
          activity={{ open: activity.open, onToggle: toggleActivity, notice: runNotice }}
        />
        <main id={MAIN_CONTENT_ID} ref={main} tabIndex={-1} className={styles.main}>
          <ContentAreaProvider value={main}>
            <PageTrailProvider value={trail}>
              <PageTabsProvider value={tabs}>
                <Container>{children}</Container>
              </PageTabsProvider>
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
          terminal={<TerminalPanel hosted={engine.hosted} focusRequest={terminalRequests} />}
          output={<OutputLog hosted={engine.hosted} active={panel.open && panel.tab === 'output'} />}
        />
      </div>

      {activity.open ? (
        <ActivityPanel
          client={tauriEngineClient}
          hosted={engine.hosted}
          ready={engine.status === 'ready'}
          focusRequest={activity.focusRequest}
          onClose={closeActivity}
          className={styles.activityPanel}
        />
      ) : null}

      <StatusBar engine={engine} panel={panel} onPanelTab={togglePanelTab} onShortcuts={showSheet} />

      <CommandPalette open={palette.open} onClose={palette.hide} onShellAction={(action) => shellActions[action]?.()} />
      <ShortcutSheet open={sheetOpen} onClose={hideSheet} />
    </div>
  );
}
