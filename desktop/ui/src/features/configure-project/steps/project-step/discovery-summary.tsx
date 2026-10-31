import { EvidenceDisclosure, SectionCard, StatusChip } from '@/shared/components/ui';
import type { Discovery } from '@/shared/schema';

import styles from '../../components/panels.module.scss';
import { specificationStateLabel, specificationStateTone } from '../../domain/labels';

const versionText = (version: string | null) => (version ? `versão ${version}` : 'não encontrado');

function SpecificationSummary({ specification }: { specification: Discovery['specification'] }) {
  return (
    <SectionCard
      title="Estrutura de especificação"
      aside={
        <StatusChip tone={specificationStateTone[specification.state]}>
          {specificationStateLabel[specification.state]}
        </StatusChip>
      }
    >
      {specification.version ? <span className={styles.muted}>versão {specification.version}</span> : null}
      {specification.problems.map((problem) => (
        <span key={problem} className={styles.muted}>
          {problem}
        </span>
      ))}
    </SectionCard>
  );
}

function ExistingHarnessSummary({ inventory }: { inventory: Discovery['inventory'] }) {
  const existing = [
    ...inventory.agents,
    ...inventory.skills,
    ...inventory.rules,
    ...inventory.hooks,
    ...inventory.mcpServers,
    ...inventory.evals
  ];

  return (
    <SectionCard title="Harness já existente">
      <span className={styles.muted}>
        {existing.length === 0
          ? 'Nenhuma configuração do Harness foi encontrada: o projeto começa do zero.'
          : `${inventory.agents.length} agente(s), ${inventory.skills.length} skill(s), ${inventory.rules.length} regra(s), ${inventory.hooks.length} hook(s), ${inventory.mcpServers.length} servidor(es) MCP, ${inventory.evals.length} arquivo(s) de eval`}
      </span>
      <EvidenceDisclosure
        summary="Ver o que existe"
        items={existing.map((item) => (item.issue ? `${item.path} — aviso: ${item.issue}` : item.path))}
      />
    </SectionCard>
  );
}

function gitText(git: Discovery['git']): string {
  if (!git.isRepository) return 'Não é um repositório git';
  return git.hasUncommittedChanges
    ? `Git: ${git.changedPaths.length} alteração(ões) local(is) não commitada(s)`
    : 'Git: sem alterações locais';
}

function RepositorySummary({ git, tools }: Pick<Discovery, 'git' | 'tools'>) {
  return (
    <SectionCard title="Repositório e sistema">
      <span className={styles.muted}>{gitText(git)}</span>
      <div className={styles.chips}>
        <StatusChip tone={tools.node ? 'success' : 'warning'}>Node: {versionText(tools.node)}</StatusChip>
        <StatusChip tone={tools.python ? 'success' : 'warning'}>Python: {versionText(tools.python)}</StatusChip>
        <StatusChip tone={tools.git ? 'success' : 'neutral'}>Git: {versionText(tools.git)}</StatusChip>
      </div>
    </SectionCard>
  );
}

export function DiscoverySummary({ discovery }: { discovery: Discovery }) {
  return (
    <div className={styles.grid}>
      <SpecificationSummary specification={discovery.specification} />
      <ExistingHarnessSummary inventory={discovery.inventory} />
      <RepositorySummary git={discovery.git} tools={discovery.tools} />
    </div>
  );
}
