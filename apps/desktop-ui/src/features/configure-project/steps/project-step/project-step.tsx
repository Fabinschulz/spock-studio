import { Alert, ArrowRightIcon, Button } from '@synthra.io/ui-kit';

import { RecentProjects, type RecentProject } from '@/features/project-selection';
import {
  Checklist,
  CommandDisplay,
  EvidenceDisclosure,
  FolderIcon,
  InlineNotice,
  PrimaryActionFooter,
  SectionCard,
  StatusChip
} from '@/shared/components/ui';
import type { Discovery } from '@/shared/schemas';

import type { Loadable } from '../../core/journey-state';
import { aiEnvironmentNames, confidenceLabel, confidenceTone, tlcStateLabel, tlcStateTone } from '../../core/labels';
import styles from '../../ui/panels.module.scss';

interface ProjectStepProps {
  /** `null` enquanto nenhuma pasta foi escolhida. O caminho é o abreviado pelo host: a UI nunca tem o completo. */
  project: { name: string; path: string } | null;
  discovery: Loadable<Discovery>;
  recents: readonly RecentProject[];
  busy: boolean;
  /** Falha ao usar a pasta escolhida. */
  error: string | null;
  /** A pessoa fechou o diálogo sem escolher. */
  cancelled: boolean;
  /** Presente quando a escolha da pasta não existe neste ambiente (ex.: navegador sem o host desktop). */
  unavailableReason?: string;
  onPick: () => void;
  onOpenRecent: (id: string) => void;
  onRetry: () => void;
  onContinue: () => void;
}

const present = (version: string | null) => (version ? `versão ${version}` : 'não encontrado');

/** Rótulos curtos dos tipos de operação que o motor detecta. */
const kindLabel: Record<string, string> = {
  build: 'Build',
  test: 'Teste',
  lint: 'Lint',
  typecheck: 'Tipos',
  run: 'Executar'
};

export function ProjectStep({
  project,
  discovery,
  recents,
  busy,
  error,
  cancelled,
  unavailableReason,
  onPick,
  onOpenRecent,
  onRetry,
  onContinue
}: ProjectStepProps) {
  const inactive = busy || unavailableReason !== undefined;
  const analyzing = project !== null && (discovery.status === 'loading' || discovery.status === 'idle');

  return (
    <section className={styles.step}>
      <h2 className="visually-hidden">{project ? `Projeto ${project.name}` : 'Projeto'}</h2>

      <div className={styles.projectWrap}>
        <div className={styles.projectGrid}>
          <SectionCard title="Selecionar pasta do projeto">
            <p className={styles.muted}>
              {unavailableReason ?? 'Escolha o diretório do seu projeto no computador. Os arquivos permanecem nele.'}
            </p>

            {error ? (
              <InlineNotice tone="danger" title="Não foi possível usar esta pasta">
                {error}
              </InlineNotice>
            ) : null}

            <div className={styles.pathField} role="group" aria-label="Pasta do projeto">
              <span className={project ? styles.pathValue : styles.pathEmpty} title={project?.path}>
                {project ? project.path : 'Nenhuma pasta selecionada'}
              </span>
              <button
                type="button"
                className={styles.pathButton}
                aria-disabled={inactive || undefined}
                aria-label={project ? `Trocar a pasta do projeto ${project.name}` : 'Selecionar pasta do projeto'}
                onClick={inactive ? undefined : onPick}
              >
                <FolderIcon />
              </button>
            </div>

            {project === null ? (
              <div>
                <Button variant="contained" onClick={onPick} disabled={inactive} endIcon={<ArrowRightIcon />}>
                  {busy ? 'Abrindo…' : 'Selecionar pasta'}
                </Button>
              </div>
            ) : null}

            {/* Região de status: o anúncio acontece quando o texto entra, sem tirar o foco de onde a pessoa está. */}
            <div role="status" aria-live="polite" className={styles.stack}>
              {cancelled && !error ? (
                <InlineNotice tone="info">
                  Seleção cancelada. Nenhuma pasta foi escolhida e nada foi alterado.
                </InlineNotice>
              ) : null}
              {busy ? <span className={styles.muted}>Abrindo o seletor de pastas…</span> : null}
              {analyzing ? <span className={styles.muted}>Analisando o projeto…</span> : null}
              {project && discovery.status === 'ready' ? (
                <span className={styles.doneLine}>Análise concluída: a pasta foi lida e nada foi alterado.</span>
              ) : null}
            </div>

            <RecentProjects recents={recents} selectedName={project?.name} busy={busy} onOpen={onOpenRecent} />
          </SectionCard>

          <div className={styles.stack}>
            <SectionCard title="O que vamos analisar?">
              <Checklist
                items={[
                  { text: 'Stack e tecnologias do projeto' },
                  { text: 'Ferramentas e dependências' },
                  { text: 'Agentes, skills e configurações existentes' },
                  { text: 'Alterações locais e conflitos' }
                ]}
              />
            </SectionCard>

            <SectionCard title="Ambiente de IA">
              <DetectedEnvironments discovery={discovery} />
            </SectionCard>
          </div>
        </div>
      </div>

      {analyzing ? <div aria-hidden="true" className={styles.progress} /> : null}

      {discovery.status === 'error' && project ? (
        <>
          <Alert severity="error" title="Não foi possível analisar o projeto" description={discovery.message} />
          <PrimaryActionFooter>
            <Button variant="contained" onClick={onRetry}>
              Tentar de novo
            </Button>
          </PrimaryActionFooter>
        </>
      ) : null}

      {project && discovery.status === 'ready' ? (
        <DiscoveryDetails discovery={discovery.data} onContinue={onContinue} />
      ) : null}
    </section>
  );
}

/** O que o motor encontrou, só depois da análise. A escolha do ambiente é da próxima etapa. */
function DetectedEnvironments({ discovery }: { discovery: Loadable<Discovery> }) {
  if (discovery.status !== 'ready') {
    return <span className={styles.muted}>Detectado durante a análise. Você escolhe o ambiente na próxima etapa.</span>;
  }
  if (discovery.data.aiEnvironments.length === 0) {
    return <span className={styles.muted}>Nenhum detectado; você escolhe na próxima etapa.</span>;
  }
  return (
    <>
      {discovery.data.aiEnvironments.map((e) => (
        <div key={e.id} className={styles.row}>
          <span>{aiEnvironmentNames[e.id] ?? e.name}</span>
          <StatusChip tone={e.supported ? 'success' : 'warning'}>
            {e.supported ? 'suportado' : 'sem suporte nesta fase'}
          </StatusChip>
          <StatusChip tone={confidenceTone(e.confidence)}>confiança {confidenceLabel[e.confidence]}</StatusChip>
        </div>
      ))}
    </>
  );
}

function DiscoveryDetails({ discovery, onContinue }: { discovery: Discovery; onContinue: () => void }) {
  const { inventory, tlc, git, tools } = discovery;
  const existing = [
    ...inventory.agents,
    ...inventory.skills,
    ...inventory.rules,
    ...inventory.hooks,
    ...inventory.mcpServers,
    ...inventory.evals
  ];
  const commands = discovery.tooling.filter((t) => t.command);
  const otherTools = discovery.tooling.filter((t) => !t.command);
  const harnessCount = existing.length;

  return (
    <>
      {discovery.warnings.map((warning) => (
        <InlineNotice
          key={warning.code + warning.message}
          // Sem Git a análise e a configuração seguem; só a reversão por controle de versão não existe.
          tone={warning.code === 'NOT_A_GIT_REPOSITORY' ? 'info' : 'warning'}
        >
          <span>{warning.message}</span>
          {warning.code === 'NOT_A_GIT_REPOSITORY' ? (
            <span>
              {' '}
              A reversão continua possível pelo backup que a aplicação guarda antes de alterar qualquer arquivo.
            </span>
          ) : null}
          {warning.paths.length > 0 ? (
            <EvidenceDisclosure summary={`Arquivos (${warning.paths.length})`} items={warning.paths} />
          ) : null}
        </InlineNotice>
      ))}

      <SectionCard title="Stack detectada">
        {discovery.stacks.length === 0 ? (
          <p className={styles.muted}>Nenhuma stack conhecida foi detectada pelos arquivos de configuração.</p>
        ) : (
          <ul className={styles.list} aria-label="Stacks detectadas">
            {discovery.stacks.map((s) => (
              <li key={s.id + s.root} className={styles.stack}>
                <div className={styles.row}>
                  <span className={styles.cardTitle}>{s.name}</span>
                  <StatusChip tone="info">{s.version ?? 'versão desconhecida'}</StatusChip>
                  <StatusChip tone={confidenceTone(s.confidence)}>confiança {confidenceLabel[s.confidence]}</StatusChip>
                </div>
                <span className={styles.muted}>Em {s.root === '' ? 'raiz do projeto' : s.root}</span>
                {s.notes.length > 0 ? (
                  <div className={styles.chips}>
                    {s.notes.map((note) => (
                      <StatusChip key={note}>{note}</StatusChip>
                    ))}
                  </div>
                ) : null}
                <EvidenceDisclosure
                  summary={`Evidências (${s.evidence.length})`}
                  items={s.evidence.map((e) => (e.detail ? `${e.path}: ${e.detail}` : e.path))}
                />
              </li>
            ))}
          </ul>
        )}
      </SectionCard>

      {discovery.tooling.length > 0 ? (
        <SectionCard title="Ferramentas e comandos">
          <p className={styles.muted}>Detectados nos arquivos do projeto. Nada é executado por ter sido detectado.</p>
          {commands.length > 0 ? (
            <ul className={styles.list} aria-label="Comandos detectados">
              {commands.map((t) => (
                <li key={t.kind + t.name}>
                  <CommandDisplay command={t.command!} kind={kindLabel[t.kind] ?? t.kind} />
                </li>
              ))}
            </ul>
          ) : null}
          {otherTools.length > 0 ? (
            <ul className={styles.chips} aria-label="Outras ferramentas detectadas">
              {otherTools.map((t) => (
                <li key={t.kind + t.name}>
                  <StatusChip>
                    {kindLabel[t.kind] ?? t.kind}: {t.name}
                  </StatusChip>
                </li>
              ))}
            </ul>
          ) : null}
        </SectionCard>
      ) : null}

      <div className={styles.grid}>
        <SectionCard
          title="TLC (método SDD)"
          aside={<StatusChip tone={tlcStateTone(tlc.state)}>{tlcStateLabel[tlc.state]}</StatusChip>}
        >
          {tlc.version ? <span className={styles.muted}>versão {tlc.version}</span> : null}
          {tlc.problems.map((p) => (
            <span key={p} className={styles.muted}>
              {p}
            </span>
          ))}
        </SectionCard>

        <SectionCard title="Harness já existente">
          <span className={styles.muted}>
            {harnessCount === 0
              ? 'Nenhuma configuração do Harness foi encontrada: o projeto começa do zero.'
              : `${inventory.agents.length} agente(s), ${inventory.skills.length} skill(s), ${inventory.rules.length} regra(s), ${inventory.hooks.length} hook(s), ${inventory.mcpServers.length} servidor(es) MCP, ${inventory.evals.length} arquivo(s) de eval`}
          </span>
          <EvidenceDisclosure
            summary="Ver o que existe"
            items={existing.map((i) => (i.issue ? `${i.path} — aviso: ${i.issue}` : i.path))}
          />
        </SectionCard>

        <SectionCard title="Repositório e sistema">
          <span className={styles.muted}>
            {git.isRepository
              ? git.hasUncommittedChanges
                ? `Git: ${git.changedPaths.length} alteração(ões) local(is) não commitada(s)`
                : 'Git: sem alterações locais'
              : 'Não é um repositório git'}
          </span>
          <div className={styles.chips}>
            <StatusChip tone={tools.node ? 'success' : 'warning'}>Node: {present(tools.node)}</StatusChip>
            <StatusChip tone={tools.python ? 'success' : 'warning'}>Python: {present(tools.python)}</StatusChip>
            <StatusChip tone={tools.git ? 'success' : 'neutral'}>Git: {present(tools.git)}</StatusChip>
          </div>
        </SectionCard>
      </div>

      <PrimaryActionFooter>
        <Button variant="contained" onClick={onContinue} endIcon={<ArrowRightIcon />}>
          Próximo
        </Button>
      </PrimaryActionFooter>
    </>
  );
}
