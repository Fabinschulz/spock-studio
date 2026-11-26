import { Fragment } from 'react';

import { EvidenceDisclosure, type GlossaryId, Term } from '@/shared/components/ui';
import type { Discovery } from '@/shared/schema';
import { plural } from '@/shared/utils';

import styles from '../../components/rows.module.scss';
import { aiEnvironmentNames } from '../../domain/ai-environments';
import { specificationStateLabel } from '../../domain/labels';

const versionText = (version: string | null) => (version ? `versão ${version}` : 'não encontrado');

function gitText(git: Discovery['git']): string {
  if (!git.isRepository) return 'não é um repositório';
  return git.hasUncommittedChanges
    ? plural(git.changedPaths.length, 'alteração local não commitada', 'alterações locais não commitadas')
    : 'sem alterações locais';
}

const inventoryTotal = (inventory: Discovery['inventory']) =>
  Object.values(inventory).reduce((sum, items) => sum + items.length, 0);

/** A primeira coisa que a análise diz: de onde o projeto parte. */
export function discoveryHeadline(discovery: Discovery): string {
  if (discovery.hasDeclaredConfig) {
    const names = discovery.declaredAiEnvironments.map((id) => aiEnvironmentNames[id] ?? id);
    return names.length > 0 ? `Já configurado pelo Spock para ${names.join(' e ')}.` : 'Já configurado pelo Spock.';
  }
  return inventoryTotal(discovery.inventory) > 0
    ? 'Já tem configuração de IA, feita fora do Spock.'
    : 'Começa do zero: nenhuma configuração de IA encontrada.';
}

export function SystemRow({ tools }: { tools: Discovery['tools'] }) {
  return (
    <div className={styles.line}>
      <span className={styles.name}>Sistema</span>
      <span className={styles.end}>
        Node: {versionText(tools.node)} · Python: {versionText(tools.python)} · Git: {versionText(tools.git)}
      </span>
    </div>
  );
}

type InventoryKind = keyof Discovery['inventory'];

/** Os termos técnicos levam a explicação do glossário; agente e regra se entendem sozinhos. */
const INVENTORY_PARTS: readonly [InventoryKind, string, string, GlossaryId | null][] = [
  ['agents', 'agente', 'agentes', null],
  ['skills', 'skill', 'skills', 'skill'],
  ['rules', 'regra', 'regras', null],
  ['hooks', 'hook', 'hooks', 'hook'],
  ['mcpServers', 'servidor MCP', 'servidores MCP', 'mcp'],
  ['evals', 'arquivo de eval', 'arquivos de eval', 'eval']
];

function HarnessInventory({ inventory }: { inventory: Discovery['inventory'] }) {
  if (inventoryTotal(inventory) === 0) return 'nenhuma configuração encontrada: o projeto começa do zero';
  return INVENTORY_PARTS.map(([kind, singular, many, term], index) => {
    const count = inventory[kind].length;
    const word = count === 1 ? singular : many;
    return (
      <Fragment key={kind}>
        {index > 0 ? ', ' : ''}
        {count} {term ? <Term id={term}>{word}</Term> : word}
      </Fragment>
    );
  });
}

export function DiscoverySummary({ discovery }: { discovery: Discovery }) {
  const { specification, inventory, git } = discovery;
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
              <span className={styles.name}>
                <Term id="harness" /> existente
              </span>
              <span className={styles.meta}>
                {' '}
                <HarnessInventory inventory={inventory} />
              </span>
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
      </ul>
    </section>
  );
}
