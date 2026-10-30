import { Container, Logo } from '@/shared/components';
import { app } from '@/shared/config/app';
import { useEngineStatus, type EngineStatus as Status } from '@/shared/hooks';
import { pathTo } from '@/shared/routing';
import { cn } from '@/shared/utils';
import { useRef, type ReactNode } from 'react';
import { Link } from 'react-router';
import styles from './AppShell.module.scss';
import { dotClass, EngineStatus } from './EngineStatus';
import { SideNav } from './SideNav';

export const MAIN_CONTENT_ID = 'conteudo';

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
