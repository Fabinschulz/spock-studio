import { CheckGlyph, FolderIcon, InfoGlyph, LockGlyph } from '@/shared/components/ui';
import { Button, CopyIcon } from '@mariliz/ui-kit';
import { useCopyToClipboard } from '@/shared/hooks';
import { useId } from 'react';
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
  const { state: copyState, copy } = useCopyToClipboard({ resetAfterMs: 2000 });

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
                <Button
                  variant="text"
                  color="inherit"
                  className={styles.copy}
                  aria-label="Copiar o caminho exibido"
                  onClick={() => void copy(selected.displayPath)}
                >
                  <CopyIcon fontSize="small" aria-hidden="true" />
                </Button>
                <span role="status" className="visually-hidden">
                  {copyState === 'copied' ? 'Caminho copiado' : ''}
                </span>
              </span>
            </span>
            <span className={styles.aside}>
              <Button
                variant="text"
                color="inherit"
                aria-disabled={inactive || undefined}
                aria-label={`Trocar a pasta do projeto ${selected.displayName}`}
                startIcon={
                  <svg width="16" height="16" viewBox="0 0 16 16" fill="none" aria-hidden="true" focusable="false">
                    <path
                      d="M2.5 6h10M10 3l3 3-3 3M13.5 10h-10M6 7l-3 3 3 3"
                      stroke="currentColor"
                      strokeWidth="1.5"
                      strokeLinecap="round"
                      strokeLinejoin="round"
                    />
                  </svg>
                }
                onClick={inactive ? undefined : onPick}
              >
                Trocar
              </Button>
              <span className={styles.lockNote}>
                <LockGlyph />
                Arquivos permanecem no computador
              </span>
            </span>
          </div>
        ) : null}
      </div>

      <p className={styles.info}>
        <InfoGlyph />
        <span>
          Você pode trocar a pasta a qualquer momento. A análise inicial é somente de leitura e não altera seus
          arquivos.
        </span>
      </p>
    </section>
  );
}
