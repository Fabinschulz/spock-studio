import { Checkbox } from '@mariliz/ui-kit';

import type { PendingApproval } from '../domain/run-timeline';
import styles from './workspace.module.scss';

/**
 * As ações que o motor pediu para aprovar, todas desmarcadas: executar uma ação é uma escolha explícita de quem aprova.
 * O conteúdo de cada ação é do motor e não é mostrado aqui; a tela mostra o que a política mostra (resumo e alvo).
 */
export function ProposedActions({
  pending,
  chosen,
  busy,
  onToggle
}: {
  pending: PendingApproval;
  chosen: readonly string[];
  busy: boolean;
  onToggle: (id: string) => void;
}) {
  return (
    <section aria-labelledby="acoes-titulo">
      <h2 id="acoes-titulo">Ações propostas</h2>
      <p>
        Nada é gravado no projeto sem a sua aprovação. O conteúdo é gerado pelo motor a partir do impacto já validado,
        não pelo agente. Marque o que quer executar.
      </p>
      <ul className={styles.list}>
        {pending.actions.map((action) => (
          <li key={action.id} className={styles.row}>
            <Checkbox
              label={action.summary}
              checked={chosen.includes(action.id)}
              onChange={() => onToggle(action.id)}
              disabled={busy}
            />
            {action.target ? <p className={styles.path}>{action.target}</p> : null}
          </li>
        ))}
      </ul>
    </section>
  );
}
