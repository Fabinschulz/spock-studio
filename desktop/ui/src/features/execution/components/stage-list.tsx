import { StatusGlyph } from '@/shared/components/ui';

import type { StageView } from '../domain/run-workspace';
import styles from './workspace.module.scss';

const GLYPH_TEXT = {
  pending: 'Ainda não começou',
  running: 'Em andamento',
  done: 'Concluída',
  warning: 'Concluída com ressalvas',
  failed: 'Falhou',
  skipped: 'Não concluída'
} as const;

/**
 * As etapas do grafo que o motor já anunciou, na ordem em que o grafo as percorre. O estado de cada uma está no texto
 * (o ícone sozinho não carrega o significado) e o nome acessível do ícone repete o que o texto diz.
 */
export function StageList({ stages, outcome }: { stages: readonly StageView[]; outcome: string }) {
  return (
    <ol className={styles.stages}>
      {stages.map((stage) => {
        // O estado final já está escrito no cabeçalho da página; aqui a linha diz que a execução terminou e o ícone o nomeia.
        const state = stage.id === 'outcome' ? 'Encerrada' : GLYPH_TEXT[stage.glyph];
        return (
          <li key={stage.id} className={styles.stage}>
            <StatusGlyph state={stage.glyph} label={stage.id === 'outcome' ? outcome : state} />
            <div>
              <p className={styles.stageName}>{stage.label}</p>
              <p className={styles.meta}>{[state, stage.detail].filter(Boolean).join(' · ')}</p>
            </div>
          </li>
        );
      })}
    </ol>
  );
}
