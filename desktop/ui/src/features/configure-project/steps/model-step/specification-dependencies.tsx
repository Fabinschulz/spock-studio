import { StatusChip } from '@/shared/components/ui';
import type { Discovery } from '@/shared/schema';
import { cn } from '@/shared/utils';

import rows from '../../components/rows.module.scss';
import styles from './model.module.scss';

type Tools = Discovery['tools'];

const dependencies = (tools: Tools) => [
  { id: 'node', text: tools.node ? `Node.js ${tools.node}` : 'Node.js: não encontrado', ok: Boolean(tools.node) },
  {
    id: 'python',
    text: tools.python ? `Python ${tools.python}` : 'Python 3: não encontrado',
    ok: Boolean(tools.python)
  },
  { id: 'git', text: tools.git ? `Git ${tools.git}` : 'Git: não encontrado (recomendado)', ok: Boolean(tools.git) }
];

export function SpecificationDependencies({ tools }: { tools: Tools }) {
  return (
    <ul role="list" aria-label="Dependências da estrutura de especificação" className={rows.rows}>
      {dependencies(tools).map((dep) => (
        <li key={dep.id}>
          <div className={rows.line}>
            <span className={cn(rows.name, !dep.ok && styles.missing)}>{dep.text}</span>
            <StatusChip tone={dep.ok ? 'success' : 'warning'}>{dep.ok ? 'encontrado' : 'ausente'}</StatusChip>
          </div>
        </li>
      ))}
    </ul>
  );
}
