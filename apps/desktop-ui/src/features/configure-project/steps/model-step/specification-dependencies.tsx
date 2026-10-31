import type { Discovery } from '@/shared/schema';
import { cn } from '@/shared/utils';
import { GitGlyph, NodeGlyph, PythonGlyph } from '../../components/glyphs';
import styles from '../../components/panels.module.scss';

type Tools = Discovery['tools'];

const dependencies = (tools: Tools) => [
  {
    id: 'node',
    icon: <NodeGlyph />,
    text: tools.node ? `Node.js ${tools.node}` : 'Node.js: não encontrado',
    ok: Boolean(tools.node)
  },
  {
    id: 'python',
    icon: <PythonGlyph />,
    text: tools.python ? `Python ${tools.python}` : 'Python 3: não encontrado',
    ok: Boolean(tools.python)
  },
  {
    id: 'git',
    icon: <GitGlyph />,
    text: tools.git ? `Git ${tools.git}` : 'Git: não encontrado (recomendado)',
    ok: Boolean(tools.git)
  }
];

export function SpecificationDependencies({ tools }: { tools: Tools }) {
  return (
    <ul className={styles.deps} aria-label="Dependências da estrutura de especificação">
      {dependencies(tools).map((dep) => (
        <li key={dep.id} className={cn(styles.dep, !dep.ok && styles.depMissing)}>
          <span className={styles.depIcon} aria-hidden="true">
            {dep.icon}
          </span>
          {dep.text}
        </li>
      ))}
    </ul>
  );
}
