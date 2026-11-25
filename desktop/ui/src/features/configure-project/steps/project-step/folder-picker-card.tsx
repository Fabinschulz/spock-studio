import { Button } from '@mariliz/ui-kit';

import { RecentProjects, useProjectSelectionContext } from '@/features/project-selection';
import { FolderIcon, InlineNotice, SectionCard } from '@/shared/components/ui';

import { TerminalGlyph } from '../../components/glyphs';
import styles from '../../components/panels.module.scss';

export interface PickedProject {
  name: string;
  /** O caminho abreviado pelo host: a UI nunca tem o completo. */
  path: string;
}

/** Só sem projeto aberto: com projeto, a etapa mostra a linha do projeto e a troca é pelo seletor do topo. */
export function FolderPickerCard() {
  const { selected, busy, error, cancelled, recents, pick, openRecent, unavailableReason } =
    useProjectSelectionContext();
  const onPick = () => void pick();
  const onOpenRecent = (id: string) => void openRecent(id);
  const inactive = busy || unavailableReason !== undefined;

  return (
    <SectionCard title="Selecionar pasta do projeto" icon={<TerminalGlyph />}>
      <p className={styles.muted}>{unavailableReason ?? 'Escolha o diretório do seu projeto no computador.'}</p>

      {error ? (
        <InlineNotice tone="danger" title="Não foi possível usar esta pasta">
          {error}
        </InlineNotice>
      ) : null}

      <div className={styles.pathField} role="group" aria-label="Pasta do projeto">
        <span className={styles.pathEmpty}>Nenhuma pasta selecionada</span>
        <Button
          variant="text"
          className={styles.pathButton}
          aria-disabled={inactive || undefined}
          aria-label="Selecionar pasta do projeto"
          onClick={inactive ? undefined : onPick}
        >
          <FolderIcon />
        </Button>
      </div>

      <div role="status" aria-live="polite" className={styles.stack}>
        {cancelled && !error ? (
          <InlineNotice tone="info">Seleção cancelada. Nenhuma pasta foi escolhida e nada foi alterado.</InlineNotice>
        ) : null}
        {busy ? <span className={styles.muted}>Abrindo o seletor de pastas…</span> : null}
      </div>

      <RecentProjects recents={recents} currentId={selected?.recentId} busy={busy} onOpen={onOpenRecent} />
    </SectionCard>
  );
}
