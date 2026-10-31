import { CheckGlyph, FolderIcon } from '@/shared/components/ui';
import { Button, CopyIcon } from '@synthra.io/ui-kit';
import { useId, useState } from 'react';
import type { SelectedProject } from '../domain/types';
import styles from './project-picker.module.scss';

interface ProjectPickerProps {
  selected: SelectedProject | null;
  onPick: () => void;
  busy?: boolean;
  unavailableReason?: string;
}

export function ProjectPicker({ selected, onPick, busy, unavailableReason }: ProjectPickerProps) {
  const id = useId();
  const unavailable = unavailableReason !== undefined;
  const inactive = unavailable || busy;
  const [copied, setCopied] = useState(false);

  const copyPath = async (path: string) => {
    try {
      await navigator.clipboard.writeText(path);
      setCopied(true);
      window.setTimeout(() => setCopied(false), 2000);
    } catch {
      setCopied(false);
    }
  };

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
          <Button variant="contained" onClick={onPick} disabled={inactive} startIcon={<FolderIcon size={18} />}>
            {busy ? 'Abrindo…' : 'Selecionar pasta'}
          </Button>
        </div>

        {selected ? (
          <div className={styles.selected}>
            <span className={styles.check} aria-hidden="true">
              <CheckGlyph size={20} />
            </span>
            <span className={styles.text}>
              <span className={styles.title}>{selected.displayName}</span>
              <span className="visually-hidden">Pasta selecionada</span>
              <span className={styles.pathRow}>
                <svg
                  className={styles.sync}
                  width="16"
                  height="16"
                  viewBox="0 0 16 16"
                  fill="none"
                  aria-hidden="true"
                  focusable="false"
                >
                  <path
                    d="M13.5 8a5.5 5.5 0 0 1-9.4 3.9M2.5 8a5.5 5.5 0 0 1 9.4-3.9M12 1.8v2.6H9.4M4 14.2v-2.6h2.6"
                    stroke="currentColor"
                    strokeWidth="1.4"
                    strokeLinecap="round"
                    strokeLinejoin="round"
                  />
                </svg>
                <span className={styles.path} title={selected.displayPath}>
                  {selected.displayPath}
                </span>
                <button
                  type="button"
                  className={styles.copy}
                  aria-label="Copiar o caminho exibido"
                  onClick={() => void copyPath(selected.displayPath)}
                >
                  <CopyIcon fontSize="small" aria-hidden="true" />
                </button>
                <span role="status" className="visually-hidden">
                  {copied ? 'Caminho copiado' : ''}
                </span>
              </span>
            </span>
            <span className={styles.aside}>
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
              <span className={styles.lockNote}>
                <svg width="16" height="16" viewBox="0 0 16 16" fill="none" aria-hidden="true" focusable="false">
                  <rect x="3" y="7" width="10" height="7" rx="1.5" stroke="currentColor" strokeWidth="1.4" />
                  <path d="M5.5 7V5a2.5 2.5 0 0 1 5 0v2" stroke="currentColor" strokeWidth="1.4" />
                </svg>
                Arquivos permanecem no computador
              </span>
            </span>
          </div>
        ) : null}
      </div>

      <p className={styles.info}>
        <InlineNoticeGlyph />
        <span>
          Você pode trocar a pasta a qualquer momento. A análise inicial é somente de leitura e não altera seus
          arquivos.
        </span>
      </p>
    </section>
  );
}

function InlineNoticeGlyph() {
  return (
    <svg width="16" height="16" viewBox="0 0 20 20" fill="none" aria-hidden="true" focusable="false">
      <circle cx="10" cy="10" r="8" stroke="currentColor" strokeWidth="1.6" />
      <path d="M10 9v5M10 6.2v.1" stroke="currentColor" strokeWidth="1.6" strokeLinecap="round" />
    </svg>
  );
}
