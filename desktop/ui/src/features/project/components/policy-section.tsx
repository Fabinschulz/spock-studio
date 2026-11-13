import type { EngineClient } from '@/shared/api';
import { PageSection, ResourceView } from '@/shared/components';
import { useAsyncResource } from '@/shared/hooks';
import type { AgentPolicy } from '@/shared/schema';
import { useCallback } from 'react';

import styles from '../project-page.module.scss';
import { readPolicy } from '../service/project-api';

/** As três respostas do hook, na ordem do que mais importa. Uma decisão nova do script aparece depois, como veio. */
const DECISIONS: { decision: string; title: string }[] = [
  { decision: 'deny', title: 'Bloqueia' },
  { decision: 'ask', title: 'Pergunta a você antes' },
  { decision: 'allow', title: 'Libera, como exceção' }
];

/**
 * O que o hook de política faz, em palavras simples. As regras são lidas do próprio script do hook e só o texto é escrito à
 * mão, preso a elas por testes. Não depende de projeto: é o hook que o Spock Studio aplica.
 */
export function PolicySection({ client }: { client: EngineClient }) {
  const load = useCallback(() => readPolicy(client), [client]);
  const policy = useAsyncResource(load);

  return (
    <PageSection title="O que o hook de política faz">
      <ResourceView resource={policy} loading="Lendo a política…" fallbackError="Não foi possível ler a política.">
        {(value) => <PolicyBody policy={value} />}
      </ResourceView>
    </PageSection>
  );
}

/** "a, b e c": uma lista em português corrido. */
function listed(items: string[]): string {
  return items.length < 2 ? items.join('') : `${items.slice(0, -1).join(', ')} e ${items.at(-1)}`;
}

function PolicyBody({ policy }: { policy: AgentPolicy }) {
  const known = new Set(DECISIONS.map((item) => item.decision));
  const groups = [
    ...DECISIONS,
    ...[...new Set(policy.rules.map((rule) => rule.decision))]
      .filter((decision) => !known.has(decision))
      .map((decision) => ({ decision, title: decision }))
  ]
    .map((group) => ({ ...group, rules: policy.rules.filter((rule) => rule.decision === group.decision) }))
    .filter((group) => group.rules.length > 0);

  return (
    <>
      <p className={styles.meta}>
        O hook <span className={styles.mono}>{policy.script}</span> confere o que o Claude Code vai fazer, antes de
        acontecer: {listed(policy.watches)}. Ele só lê: não grava nada, não usa a rede e não chama outro agente.
      </p>
      {groups.map((group) => (
        <div key={group.decision}>
          <h3 className={styles.subsection}>{group.title}</h3>
          <ul className={styles.plainList} aria-label={group.title}>
            {group.rules.map((rule) => (
              <li key={rule.plain}>{rule.plain}</li>
            ))}
          </ul>
        </div>
      ))}
      <h3 className={styles.subsection}>O que isso não garante</h3>
      <ul className={styles.plainList} aria-label="Limites da política">
        {policy.limits.map((limit) => (
          <li key={limit}>{limit}</li>
        ))}
      </ul>
    </>
  );
}
