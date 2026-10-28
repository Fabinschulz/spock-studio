import { useRef, type ReactNode } from 'react';
import { Link } from 'react-router';

import { Container, Logo } from '@/shared/components';
import { app } from '@/shared/config/app';
import { pathTo } from '@/shared/routing';
import { useEngineStatus, type EngineStatus as Status } from '@/shared/services';
import { cn } from '@/shared/utils';

import styles from './AppShell.module.scss';
import { dotClass, EngineStatus } from './EngineStatus';
import { SideNav } from './SideNav';

export const MAIN_CONTENT_ID = 'conteudo';

/** O estado do cabeçalho vem do host; fora do aplicativo desktop não há motor e ele diz isso. */
function headerStatus({ status, hosted }: { status: Status; hosted: boolean }) {
  if (!hosted) return { label: 'Prévia', tone: 'idle' } as const;
  if (status === 'ready') return { label: 'Online', tone: 'ok' } as const;
  if (status === 'crashed') return { label: 'Indisponível', tone: 'bad' } as const;
  return { label: 'Iniciando…', tone: 'pending' } as const;
}

function AppHeader({ engine }: { engine: { status: Status; hosted: boolean } }) {
  const { label, tone } = headerStatus(engine);

  return (
    <header className={styles.topbar}>
      <Logo className={styles.topLogo} />
      <div className={styles.topText}>
        <p className={styles.topName}>
          Mariliz <span className={styles.topAccent}>Studio</span>
        </p>
        <p className={styles.topTagline}>{app.tagline}</p>
      </div>
      {/* O anúncio do estado fica no menu (região de status); aqui é só o resumo visual. */}
      <p className={styles.topStatus}>
        <span className={styles.topStatusItem}>
          <span className={cn(styles.dot, dotClass[tone])} aria-hidden="true" />
          {label}
        </span>
        <span className={styles.topVersion}>
          <span className="visually-hidden">Versão </span>v{app.version}
        </span>
      </p>
    </header>
  );
}

/**
 * Moldura da janela: menu lateral (marca, navegação e estado do motor), cabeçalho e a área de conteúdo.
 * A barra de título é a nativa do sistema; o menu encolhe para ícones quando a janela é estreita.
 */
export function AppShell({ children }: { children: ReactNode }) {
  const main = useRef<HTMLElement>(null);
  // Uma só escuta do host para o cabeçalho e para o menu: os dois sempre dizem o mesmo.
  const engine = useEngineStatus();

  return (
    <div className={styles.shell}>
      {/* Com o roteador por hash, âncoras (#conteudo) trocariam de rota: o atalho move o foco direto. */}
      <button type="button" className={styles.skip} onClick={() => main.current?.focus()}>
        Ir para o conteúdo
      </button>

      <aside className={styles.rail}>
        <Link to={pathTo('home')} className={styles.brand} aria-label="Mariliz Studio, página inicial">
          <Logo />
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
