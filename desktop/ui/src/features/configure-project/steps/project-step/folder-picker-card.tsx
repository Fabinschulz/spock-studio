import { Button } from '@synthra.io/ui-kit';

import { RecentProjects, useProjectSelectionContext } from '@/features/project-selection';
import { FolderIcon, InlineNotice, SectionCard } from '@/shared/components/ui';

import { TerminalGlyph } from '../../components/glyphs';
import styles from '../../components/panels.module.scss';

export interface PickedProject {
  name: string;
  /** O caminho abreviado pelo host: a UI nunca tem o completo. */
  path: string;
}

interface FolderPickerCardProps {
  project: PickedProject | null;
  analyzing: boolean;
  onAnalyze: () => void;
}

/** Escolha da pasta: o estado da seleção (recentes, erro, ambiente sem host) vem do contexto de seleção de projeto. */
export function FolderPickerCard({ project, analyzing, onAnalyze }: FolderPickerCardProps) {
  const { busy, error, cancelled, recents, pick, openRecent, unavailableReason } = useProjectSelectionContext();
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
        <span className={project ? styles.pathValue : styles.pathEmpty} title={project?.path}>
          {project ? project.path : 'Nenhuma pasta selecionada'}
        </span>
        <button
          type="button"
          className={styles.pathButton}
          aria-disabled={inactive || undefined}
          aria-label={project ? `Trocar a pasta do projeto ${project.name}` : 'Selecionar pasta do projeto'}
          onClick={inactive ? undefined : onPick}
        >
          <FolderIcon />
        </button>
      </div>

      <div className={styles.analyzeRow}>
        <Button variant="contained" onClick={onAnalyze} disabled={project === null || analyzing}>
          {analyzing ? 'Analisando…' : 'Analisar projeto'}
        </Button>
      </div>

      <div role="status" aria-live="polite" className={styles.stack}>
        {cancelled && !error ? (
          <InlineNotice tone="info">Seleção cancelada. Nenhuma pasta foi escolhida e nada foi alterado.</InlineNotice>
        ) : null}
        {busy ? <span className={styles.muted}>Abrindo o seletor de pastas…</span> : null}
        {analyzing ? <span className={styles.muted}>Analisando o projeto…</span> : null}
      </div>

      <RecentProjects recents={recents} selectedName={project?.name} busy={busy} onOpen={onOpenRecent} />
    </SectionCard>
  );
}
