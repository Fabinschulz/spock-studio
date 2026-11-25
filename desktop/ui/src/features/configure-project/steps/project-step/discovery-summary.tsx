import { EvidenceDisclosure } from '@/shared/components/ui';
import type { Discovery } from '@/shared/schema';
import { plural } from '@/shared/utils';

import { specificationStateLabel } from '../../domain/labels';
import styles from '../../components/rows.module.scss';

const versionText = (version: string | null) => (version ? `versão ${version}` : 'não encontrado');

function gitText(git: Discovery['git']): string {
  if (!git.isRepository) return 'não é um repositório';
  return git.hasUncommittedChanges
    ? plural(git.changedPaths.length, 'alteração local não commitada', 'alterações locais não commitadas')
    : 'sem alterações locais';
}

function harnessText(inventory: Discovery['inventory']): string {
  const total = Object.values(inventory).reduce((sum, items) => sum + items.length, 0);
  if (total === 0) return 'nenhuma configuração encontrada: o projeto começa do zero';
  return [
    plural(inventory.agents.length, 'agente', 'agentes'),
    plural(inventory.skills.length, 'skill', 'skills'),
    plural(inventory.rules.length, 'regra', 'regras'),
    plural(inventory.hooks.length, 'hook', 'hooks'),
    plural(inventory.mcpServers.length, 'servidor MCP', 'servidores MCP'),
    plural(inventory.evals.length, 'arquivo de eval', 'arquivos de eval')
  ].join(', ');
}

export function DiscoverySummary({ discovery }: { discovery: Discovery }) {
  const { specification, inventory, git, tools } = discovery;
  const existing = Object.values(inventory)
    .flat()
    .map((item) => (item.issue ? `${item.path} (aviso: ${item.issue})` : item.path));

  return (
    <section aria-labelledby="estado-titulo" className={styles.section}>
      <h3 id="estado-titulo" className={styles.title}>
        Estado atual
      </h3>
      <ul role="list" className={styles.rows}>
        <li>
          <div className={styles.line}>
            <span className={styles.name}>Estrutura de especificação</span>
            <span className={styles.end}>
              {specificationStateLabel[specification.state]}
              {specification.version ? ` · versão ${specification.version}` : ''}
            </span>
          </div>
          {specification.problems.map((problem) => (
            <div key={problem} className={styles.meta}>
              {problem}
            </div>
          ))}
        </li>
        <li>
          <div className={styles.line}>
            <span>
              <span className={styles.name}>Harness existente</span>
              <span className={styles.meta}> {harnessText(inventory)}</span>
            </span>
          </div>
          <EvidenceDisclosure summary="Ver o que existe" items={existing} />
        </li>
        <li>
          <div className={styles.line}>
            <span className={styles.name}>Git</span>
            <span className={styles.end}>{gitText(git)}</span>
          </div>
        </li>
        <li>
          <div className={styles.line}>
            <span className={styles.name}>Sistema</span>
            <span className={styles.end}>
              Node: {versionText(tools.node)} · Python: {versionText(tools.python)} · Git: {versionText(tools.git)}
            </span>
          </div>
        </li>
      </ul>
    </section>
  );
}
