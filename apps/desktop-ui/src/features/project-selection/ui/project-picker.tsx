import { ArrowRightIcon } from '@synthra.io/ui-kit';
import { useId } from 'react';

import type { SelectedProject } from '../core/types';
import styles from './project-picker.module.scss';

interface ProjectPickerProps {
  selected: SelectedProject | null;
  onPick: () => void;
  busy?: boolean;
  /** Presente quando a seleção não está disponível neste ambiente (ex.: navegador sem o host desktop). */
  unavailableReason?: string;
}

/** O ícone de pasta ainda não existe na Synthra (docs/experience-blueprint.md, seção 11.2): SVG provisório. */
function FolderGlyph() {
  return (
    <svg width="24" height="24" viewBox="0 0 24 24" fill="none" aria-hidden="true" focusable="false">
      <path
        d="M3 6.5A1.5 1.5 0 0 1 4.5 5h4.2l2 2H19.5A1.5 1.5 0 0 1 21 8.5v9a1.5 1.5 0 0 1-1.5 1.5h-15A1.5 1.5 0 0 1 3 17.5v-11Z"
        stroke="currentColor"
        strokeWidth="1.6"
        strokeLinejoin="round"
      />
    </svg>
  );
}

export function ProjectPicker({ selected, onPick, busy, unavailableReason }: ProjectPickerProps) {
  const id = useId();
  const unavailable = unavailableReason !== undefined;
  const inactive = unavailable || busy;

  return (
    <section className={styles.picker} aria-labelledby={`${id}-rotulo`}>
      <h2 id={`${id}-rotulo`} className={styles.label}>
        Projeto
      </h2>
      <button
        type="button"
        className={styles.row}
        aria-disabled={inactive || undefined}
        aria-describedby={`${id}-detalhe`}
        onClick={inactive ? undefined : onPick}
      >
        <span className={styles.icon} aria-hidden="true">
          <FolderGlyph />
        </span>
        <span className={styles.text}>
          <span className={styles.title}>{selected ? selected.displayName : 'Selecionar pasta do projeto'}</span>
          <span id={`${id}-detalhe`} className={styles.detail}>
            {selected ? selected.displayPath : (unavailableReason ?? 'Os arquivos permanecem no computador.')}
          </span>
        </span>
        {selected ? (
          <span className={styles.action}>Trocar</span>
        ) : (
          <span className={styles.chevron} aria-hidden="true">
            <ArrowRightIcon />
          </span>
        )}
      </button>
      {selected ? <p className={styles.note}>Os arquivos permanecem no computador.</p> : null}
    </section>
  );
}
