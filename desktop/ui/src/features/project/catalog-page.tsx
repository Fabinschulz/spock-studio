import type { SelectedProject } from '@/features/project-selection';
import type { EngineClient } from '@/shared/api';
import { Disclosure, Page, PageSection, ResourceView, StatusChip } from '@/shared/components';
import { useAsyncResource } from '@/shared/hooks';
import { pathTo } from '@/shared/routing';
import type { CatalogEntry, HarnessCatalog } from '@/shared/schema';
import { useCallback } from 'react';
import { Link } from 'react-router';

import { McpSection } from './components/mcp-section';
import { PolicySection } from './components/policy-section';
import { HARNESS_STATE } from './domain/harness-state';
import styles from './project-page.module.scss';
import { readCatalog } from './service/project-api';

/** Um tipo novo do motor aparece depois, com o nome que veio. */
const GROUPS: { kind: string; title: string }[] = [
  { kind: 'agent', title: 'Agentes (assistentes de IA)' },
  { kind: 'skill', title: 'Skills (instruções prontas)' },
  { kind: 'rule', title: 'Regras' },
  { kind: 'hook', title: 'Hooks (checagens automáticas)' },
  { kind: 'tool', title: 'Conexões com a IDE' },
  { kind: 'specification', title: 'Ciclo de especificação' },
  { kind: 'config', title: 'Declaração do projeto' }
];

/** Só leitura: mudar é pela jornada Configurar. */
export function CatalogPage({ client, project }: { client: EngineClient; project: SelectedProject | null }) {
  // A chave remonta a tela ao trocar de projeto: nada de um projeto aparece, nem por um instante, em outro.
  return <CatalogView key={project?.projectId ?? 'sem-projeto'} client={client} project={project} />;
}

function CatalogView({ client, project }: { client: EngineClient; project: SelectedProject | null }) {
  const load = useCallback(() => readCatalog(client, project?.projectId ?? null), [client, project?.projectId]);
  const catalog = useAsyncResource(load);

  return (
    <Page
      title="Catálogo"
      description={
        project
          ? `${project.displayName} · o que cada componente do Harness faz no projeto e o que está aplicado nele.`
          : 'O que cada componente do Harness faz, quando age, o que lê e o que pode alterar.'
      }
    >
      <ResourceView resource={catalog} loading="Lendo o catálogo…" fallbackError="Não foi possível ler o catálogo.">
        {(value) => <CatalogBody catalog={value} projectOpen={project !== null} />}
      </ResourceView>
      <PolicySection client={client} />
      {project ? <McpSection client={client} projectId={project.projectId} /> : null}
    </Page>
  );
}

function CatalogBody({ catalog, projectOpen }: { catalog: HarnessCatalog; projectOpen: boolean }) {
  const known = new Set(GROUPS.map((group) => group.kind));
  const groups = [
    ...GROUPS,
    ...[...new Set(catalog.entries.map((entry) => entry.sheet.kind))]
      .filter((kind) => !known.has(kind))
      .map((kind) => ({ kind, title: kind }))
  ]
    .map((group) => ({ ...group, entries: catalog.entries.filter((entry) => entry.sheet.kind === group.kind) }))
    .filter((group) => group.entries.length > 0);

  return (
    <>
      {!projectOpen ? (
        <p className={styles.meta}>
          Nenhum projeto aberto: este é o catálogo geral. Abra um projeto na{' '}
          <Link to={pathTo('home')}>tela inicial</Link> para ver o que está aplicado nele.
        </p>
      ) : catalog.applied === false ? (
        <p className={styles.meta}>
          Este projeto ainda não tem o Harness aplicado. Use <Link to={pathTo('configure')}>Configurar</Link> para
          escolher e aprovar o que aplicar.
        </p>
      ) : null}
      {groups.map((group) => (
        <PageSection key={group.kind} title={group.title}>
          <ul className={styles.rows} aria-label={group.title}>
            {group.entries.map((entry) => (
              <EntryCard key={entry.sheet.id} entry={entry} />
            ))}
          </ul>
        </PageSection>
      ))}
      <p className={styles.meta}>
        Não há editor aqui: o que muda no projeto muda pela jornada <Link to={pathTo('configure')}>Configurar</Link>,
        que mostra a diferença, pede aprovação e permite reverter.
      </p>
    </>
  );
}

function Presence({ entry }: { entry: CatalogEntry }) {
  const state =
    entry.state !== null && entry.state in HARNESS_STATE
      ? HARNESS_STATE[entry.state as keyof typeof HARNESS_STATE]
      : null;
  if (entry.presence === 'applied')
    return (
      <>
        <StatusChip tone="success">Aplicado</StatusChip>
        {state && entry.state !== 'intact' ? <StatusChip tone={state.tone}>{state.label}</StatusChip> : null}
      </>
    );
  if (entry.presence === 'excluded')
    return (
      <>
        <StatusChip tone="neutral">Dispensado na declaração</StatusChip>
        {entry.state !== null ? <StatusChip tone="warning">Os arquivos saem na próxima aplicação</StatusChip> : null}
      </>
    );
  if (entry.presence === 'not_applied') return <StatusChip tone="neutral">Não aplicado neste projeto</StatusChip>;
  return null;
}

function EntryCard({ entry }: { entry: CatalogEntry }) {
  const { sheet } = entry;
  return (
    <li className={styles.catalogCard}>
      <div className={styles.catalogHead}>
        <h3 className={styles.catalogTitle}>{sheet.title}</h3>
        <span className={styles.catalogChips}>
          <Presence entry={entry} />
        </span>
      </div>
      <p>{sheet.whatItDoes}</p>
      <Disclosure summary="Quando age, o que lê, o que pode alterar e de onde veio">
        <dl className={styles.sheet}>
          <dt>Quando age</dt>
          <dd>{sheet.whenItActs}</dd>
          <dt>O que lê</dt>
          <dd>
            <ul className={styles.plainList}>
              {sheet.reads.map((item) => (
                <li key={item}>{item}</li>
              ))}
            </ul>
          </dd>
          <dt>O que pode alterar</dt>
          <dd>
            <ul className={styles.plainList}>
              {sheet.canChange.map((item) => (
                <li key={item}>{item}</li>
              ))}
            </ul>
          </dd>
          {sheet.tools.length > 0 ? (
            <>
              <dt>Ferramentas</dt>
              <dd>
                <ul className={styles.plainList}>
                  {sheet.tools.map((tool) => (
                    <li key={tool.name}>
                      <span className={styles.mono}>{tool.name}</span>: {tool.plain}
                    </li>
                  ))}
                </ul>
              </dd>
            </>
          ) : null}
          <dt>De onde veio</dt>
          <dd>{sheet.origin}</dd>
          <dt>Onde fica no projeto</dt>
          <dd>
            <ul className={styles.plainList}>
              {sheet.files.map((file) => (
                <li key={file} className={styles.mono}>
                  {file}
                </li>
              ))}
            </ul>
          </dd>
        </dl>
      </Disclosure>
    </li>
  );
}
