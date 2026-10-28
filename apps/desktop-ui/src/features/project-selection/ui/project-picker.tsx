import { ArrowRightIcon, Button } from '@synthra.io/ui-kit';
import { useId } from 'react';

import { FolderIcon } from '@/shared/components/ui';

import type { SelectedProject } from '../core/types';
import styles from './project-picker.module.scss';

interface ProjectPickerProps {
  selected: SelectedProject | null;
  onPick: () => void;
  busy?: boolean;
  /** Presente quando a seleção não está disponível neste ambiente (ex.: navegador sem o host desktop). */
  unavailableReason?: string;
}

/**
 * Cartão do projeto: a ação de escolher a pasta e, depois de escolhida, o nome, o caminho abreviado pelo host e "Trocar".
 * O caminho só é cortado visualmente (reticências); o texto completo continua no documento para quem usa leitor de tela.
 * Escolher a pasta nunca é o mesmo que analisá-la: a análise começa na atividade "Configurar projeto".
 */
export function ProjectPicker({ selected, onPick, busy, unavailableReason }: ProjectPickerProps) {
  const id = useId();
  const unavailable = unavailableReason !== undefined;
  const inactive = unavailable || busy;

  return (
    <section className={styles.picker} aria-labelledby={`${id}-rotulo`}>
      <h2 id={`${id}-rotulo`} className={styles.label}>
        Projeto
      </h2>

      <div className={`${styles.card} ${selected ? styles.hasProject : ''}`}>
        <div className={styles.header}>
          <span className={styles.icon} aria-hidden="true">
            <FolderIcon />
          </span>
          <span className={styles.text}>
            <span className={styles.title}>Selecionar pasta do projeto</span>
            <span className={styles.note}>{unavailableReason ?? 'Os arquivos permanecem no computador.'}</span>
          </span>
          <Button
            variant="contained"
            onClick={onPick}
            disabled={inactive}
            startIcon={<FolderIcon size={18} />}
            endIcon={<ArrowRightIcon fontSize="small" />}
          >
            {busy ? 'Abrindo…' : 'Selecionar pasta'}
          </Button>
        </div>

        {selected ? (
          <div className={styles.selected}>
            <span className={styles.icon} aria-hidden="true">
              <FolderIcon />
            </span>
            <span className={styles.text}>
              <span className={styles.name}>
                <span className={styles.title}>{selected.displayName}</span>
                <span className={styles.chip}>Pasta selecionada</span>
              </span>
              <span className={styles.path} title={selected.displayPath}>
                {selected.displayPath}
              </span>
            </span>
            <button
              type="button"
              className={styles.change}
              aria-disabled={inactive || undefined}
              aria-label={`Trocar a pasta do projeto ${selected.displayName}`}
              onClick={inactive ? undefined : onPick}
            >
              <svg width="16" height="16" viewBox="0 0 16 16" fill="none" aria-hidden="true" focusable="false">
                <path
                  d="M2.5 6h10M10 3l3 3-3 3M13.5 10h-10M6 7l-3 3 3 3"
                  stroke="currentColor"
                  strokeWidth="1.5"
                  strokeLinecap="round"
                  strokeLinejoin="round"
                />
              </svg>
              Trocar
            </button>
            <p className={styles.info}>
              <InlineNoticeGlyph />
              <span>Os arquivos permanecem no computador. A análise inicial é somente de leitura.</span>
            </p>
          </div>
        ) : null}
      </div>
    </section>
  );
}

/** Pequeno "i" em círculo, no mesmo traço dos avisos. Decorativo. */
function InlineNoticeGlyph() {
  return (
    <svg width="16" height="16" viewBox="0 0 20 20" fill="none" aria-hidden="true" focusable="false">
      <circle cx="10" cy="10" r="8" stroke="currentColor" strokeWidth="1.6" />
      <path d="M10 9v5M10 6.2v.1" stroke="currentColor" strokeWidth="1.6" strokeLinecap="round" />
    </svg>
  );
}
