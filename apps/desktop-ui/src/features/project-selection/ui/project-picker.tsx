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
  /** Com uma pasta escolhida, a ação principal da tela. Escolher a pasta nunca é o mesmo que analisá-la. */
  onAnalyze?: () => void;
}

/**
 * Sem pasta: uma linha inteira clicável. Com pasta: o nome, o caminho abreviado pelo host, "Trocar" e a ação principal.
 * O caminho só é cortado visualmente (reticências); o texto completo continua no documento para quem usa leitor de tela.
 */
export function ProjectPicker({ selected, onPick, busy, unavailableReason, onAnalyze }: ProjectPickerProps) {
  const id = useId();
  const unavailable = unavailableReason !== undefined;
  const inactive = unavailable || busy;

  return (
    <section className={styles.picker} aria-labelledby={`${id}-rotulo`}>
      <h2 id={`${id}-rotulo`} className={styles.label}>
        Projeto
      </h2>

      {selected ? (
        <div className={styles.selected}>
          <span className={styles.icon} aria-hidden="true">
            <FolderIcon />
          </span>
          <span className={styles.text}>
            <span className={styles.title}>{selected.displayName}</span>
            <span className={styles.path} title={selected.displayPath}>
              {selected.displayPath}
            </span>
            <span className={styles.note}>
              Os arquivos permanecem no computador. A análise inicial é somente de leitura.
            </span>
          </span>
          <span className={styles.actions}>
            <button
              type="button"
              className={styles.change}
              aria-disabled={inactive || undefined}
              aria-label={`Trocar a pasta do projeto ${selected.displayName}`}
              onClick={inactive ? undefined : onPick}
            >
              {busy ? 'Abrindo…' : 'Trocar'}
            </button>
            {onAnalyze ? (
              <Button variant="contained" onClick={onAnalyze} endIcon={<ArrowRightIcon fontSize="small" />}>
                Analisar projeto
              </Button>
            ) : null}
          </span>
        </div>
      ) : (
        <button
          type="button"
          className={styles.row}
          aria-disabled={inactive || undefined}
          aria-describedby={`${id}-detalhe`}
          onClick={inactive ? undefined : onPick}
        >
          <span className={styles.icon} aria-hidden="true">
            <FolderIcon />
          </span>
          <span className={styles.text}>
            <span className={styles.title}>Selecionar pasta do projeto</span>
            <span id={`${id}-detalhe`} className={styles.path}>
              {unavailableReason ?? 'Os arquivos permanecem no computador.'}
            </span>
          </span>
          <span className={styles.chevron} aria-hidden="true">
            {busy ? '…' : <ArrowRightIcon />}
          </span>
        </button>
      )}
    </section>
  );
}
