import { FolderIcon } from '@/shared/components/ui';
import { pathTo } from '@/shared/routing';
import { cn } from '@/shared/utils';
import { Button, DownIcon } from '@mariliz/ui-kit';
import { useEffect, useId, useRef, useState } from 'react';
import { useNavigate } from 'react-router';
import { useProjectSelectionContext } from '../context/project-selection-context';
import { formatRecentDate } from '../domain/format-recent-date';
import { RecentProjectButton } from './recent-project-button';
import styles from './project-switcher.module.scss';

const recentClasses = {
  button: styles.recent,
  icon: styles.recentIcon,
  text: styles.recentText,
  name: styles.recentName,
  path: styles.recentPath,
  meta: styles.recentMeta
};

/**
 * Painel de disclosure (não um menu de comandos): cada projeto é um botão comum, então o teclado e o leitor de tela
 * usam o que já conhecem. Esc e clique fora fecham, e o foco volta ao botão que abriu.
 */
export function ProjectSwitcher() {
  const panelId = useId();
  const recentsId = useId();
  const hintId = useId();
  const { selected, recents, busy, error, pick, openRecent, unavailableReason } = useProjectSelectionContext();
  const navigate = useNavigate();
  const [open, setOpen] = useState(false);
  const root = useRef<HTMLDivElement>(null);
  const trigger = useRef<HTMLButtonElement>(null);
  // O texto visível do botão fica dentro do nome acessível ("Projeto: loja"), como pede a regra de rótulo no nome.
  const triggerName = selected ? selected.displayName : 'Nenhum projeto aberto';

  useEffect(() => {
    if (!open) return;
    const onPointerDown = (event: PointerEvent) => {
      if (!root.current?.contains(event.target as Node)) setOpen(false);
    };
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key !== 'Escape') return;
      setOpen(false);
      trigger.current?.focus();
    };
    document.addEventListener('pointerdown', onPointerDown);
    document.addEventListener('keydown', onKeyDown);
    return () => {
      document.removeEventListener('pointerdown', onPointerDown);
      document.removeEventListener('keydown', onKeyDown);
    };
  }, [open]);

  async function openProject(recentId: string) {
    if (await openRecent(recentId)) setOpen(false);
  }

  async function chooseFolder() {
    if (await pick()) setOpen(false);
  }

  function showPortfolio() {
    setOpen(false);
    navigate(pathTo('portfolio'));
  }

  return (
    <div ref={root} className={styles.root}>
      <Button
        ref={trigger}
        type="button"
        variant="text"
        color="inherit"
        className={cn(styles.trigger, !selected && styles.empty)}
        aria-label={`Projeto: ${triggerName}`}
        aria-expanded={open}
        aria-controls={open ? panelId : undefined}
        title={selected?.displayPath}
        onClick={() => setOpen((value) => !value)}
      >
        <span className={styles.triggerIcon} aria-hidden="true">
          <FolderIcon size={18} />
        </span>
        <span className={styles.triggerName}>{triggerName}</span>
        <span className={styles.caret} aria-hidden="true">
          <DownIcon fontSize="small" />
        </span>
      </Button>

      {open ? (
        <div id={panelId} role="group" aria-label="Trocar de projeto" className={styles.panel}>
          {selected ? (
            <p className={styles.current}>
              <span className={styles.label}>Projeto aberto</span>
              <span className={styles.currentName}>{selected.displayName}</span>
              <span className={styles.currentPath}>{selected.displayPath}</span>
            </p>
          ) : (
            <p className={styles.hint}>Nenhum projeto aberto. Escolha um recente ou abra uma pasta.</p>
          )}

          {recents.length > 0 ? (
            <>
              <p id={recentsId} className={styles.label}>
                Projetos recentes
              </p>
              <ul className={styles.list} aria-labelledby={recentsId}>
                {recents.map((project) => (
                  <li key={project.id}>
                    <RecentProjectButton
                      project={project}
                      current={project.id === selected?.recentId}
                      busy={busy}
                      formatDate={formatRecentDate}
                      classes={recentClasses}
                      onOpen={(recentId) => void openProject(recentId)}
                    />
                  </li>
                ))}
              </ul>
            </>
          ) : null}

          {error ? (
            <p role="alert" className={styles.error}>
              {error}
            </p>
          ) : null}

          <div className={styles.actions}>
            <Button
              type="button"
              variant="text"
              size="small"
              aria-disabled={busy || unavailableReason ? true : undefined}
              aria-describedby={unavailableReason ? hintId : undefined}
              onClick={busy || unavailableReason ? undefined : () => void chooseFolder()}
            >
              Abrir outra pasta
            </Button>
            <Button type="button" variant="text" size="small" onClick={showPortfolio}>
              Todos os projetos
            </Button>
          </div>
          {unavailableReason ? (
            <p id={hintId} className={styles.hint}>
              {unavailableReason}
            </p>
          ) : null}
        </div>
      ) : null}
    </div>
  );
}
