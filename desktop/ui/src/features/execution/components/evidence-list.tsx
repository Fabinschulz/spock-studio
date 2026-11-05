import type { EvidenceView } from '../domain/run-workspace';
import styles from './workspace.module.scss';

/**
 * Cada trecho com a origem que o motor conferiu: arquivo, trecho (chunk), versão do conteúdo (hash) e pontuação da busca.
 * O texto do trecho só existe no resultado final; antes dele a lista mostra a origem sem o texto.
 */
export function EvidenceList({ items }: { items: readonly EvidenceView[] }) {
  return (
    <ul className={styles.list}>
      {items.map((item) => (
        <li key={item.chunkId} className={styles.row}>
          <span className={styles.path}>{item.filePath}</span>
          <p className={styles.meta}>
            Trecho <span className={styles.mono}>{item.chunkId}</span>
            {item.score !== null ? ` · pontuação ${item.score.toFixed(2)}` : ''}
            {item.hash ? (
              <>
                {' · versão '}
                <span className={styles.mono}>{item.hash}</span>
              </>
            ) : null}
            {item.citedByImpact ? ' · citado no impacto técnico' : ''}
          </p>
          {item.excerpt ? <p className={styles.excerpt}>{item.excerpt}</p> : null}
        </li>
      ))}
    </ul>
  );
}
