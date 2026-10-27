import { Alert, Button } from '@synthra.io/ui-kit';

import { StatusChip } from '@/shared/components/ui';
import type { Discovery } from '@/shared/schemas';

import { aiEnvironmentNames, confidenceLabel, confidenceTone, tlcStateLabel, tlcStateTone } from '../../core/labels';
import type { Loadable } from '../../core/journey-state';
import { EvidenceList } from '../../ui/evidence-list';
import styles from '../../ui/panels.module.scss';
import { StepHeader } from '../../ui/step-header';

interface ProjectStepProps {
  projectName: string;
  discovery: Loadable<Discovery>;
  onRetry: () => void;
  onContinue: () => void;
}

const present = (version: string | null) => (version ? `versão ${version}` : 'não encontrado');

export function ProjectStep({ projectName, discovery, onRetry, onContinue }: ProjectStepProps) {
  return (
    <section className={styles.step}>
      <StepHeader title={`Projeto ${projectName}`}>
        A análise é somente de leitura: nada no repositório é alterado nesta etapa.
      </StepHeader>

      {discovery.status === 'loading' || discovery.status === 'idle' ? (
        <div role="status" aria-live="polite">
          <p className={styles.muted}>Analisando o projeto…</p>
          <div className={styles.progress} />
        </div>
      ) : null}

      {discovery.status === 'error' ? (
        <>
          <Alert severity="error" title="Não foi possível analisar o projeto" description={discovery.message} />
          <div className={styles.actions}>
            <Button variant="contained" onClick={onRetry}>
              Tentar de novo
            </Button>
          </div>
        </>
      ) : null}

      {discovery.status === 'ready' ? <DiscoveryDetails discovery={discovery.data} onContinue={onContinue} /> : null}
    </section>
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

  return (
    <>
      {discovery.warnings.map((w) => (
        <Alert
          key={w.code + w.message}
          severity="warning"
          description={<WarningBody message={w.message} paths={w.paths} />}
        />
      ))}

      <h3 className={styles.sectionTitle}>Stack detectada</h3>
      {discovery.stacks.length === 0 ? (
        <p className={styles.muted}>Nenhuma stack conhecida foi detectada pelos arquivos de configuração.</p>
      ) : (
        <ul className={styles.list} aria-label="Stacks detectadas">
          {discovery.stacks.map((s) => (
            <li key={s.id + s.root} className={styles.card}>
              <div className={styles.cardTitle}>
                {s.name} {s.version ?? 'versão desconhecida'}
                <StatusChip tone={confidenceTone(s.confidence)}>confiança {confidenceLabel[s.confidence]}</StatusChip>
              </div>
              <span className={styles.muted}>Em {s.root === '' ? 'raiz do projeto' : s.root}</span>
              {s.notes.map((n) => (
                <span key={n} className={styles.muted}>
                  {n}
                </span>
              ))}
              <EvidenceList
                summary={`Evidências (${s.evidence.length})`}
                items={s.evidence.map((e) => (e.detail ? `${e.path}: ${e.detail}` : e.path))}
              />
            </li>
          ))}
        </ul>
      )}

      {discovery.tooling.length > 0 ? (
        <>
          <h3 className={styles.sectionTitle}>Ferramentas e comandos</h3>
          <ul className={styles.list} aria-label="Ferramentas detectadas">
            {discovery.tooling.map((t) => (
              <li key={t.kind + t.name} className={styles.row}>
                <StatusChip>{t.kind}</StatusChip>
                <span className={styles.mono}>{t.command ?? t.name}</span>
              </li>
            ))}
          </ul>
        </>
      ) : null}

      <div className={styles.grid}>
        <div className={styles.card}>
          <h3 className={styles.cardTitle}>Ambiente de IA</h3>
          {discovery.aiEnvironments.length === 0 ? (
            <span className={styles.muted}>Nenhum detectado; você escolhe na próxima etapa.</span>
          ) : (
            discovery.aiEnvironments.map((e) => (
              <div key={e.id} className={styles.row}>
                <span>{aiEnvironmentNames[e.id] ?? e.name}</span>
                <StatusChip tone={e.supported ? 'success' : 'warning'}>
                  {e.supported ? 'suportado' : 'sem suporte nesta fase'}
                </StatusChip>
                <StatusChip tone={confidenceTone(e.confidence)}>confiança {confidenceLabel[e.confidence]}</StatusChip>
              </div>
            ))
          )}
        </div>

        <div className={styles.card}>
          <h3 className={styles.cardTitle}>TLC (método SDD)</h3>
          <div className={styles.row}>
            <StatusChip tone={tlcStateTone(tlc.state)}>{tlcStateLabel[tlc.state]}</StatusChip>
            {tlc.version ? <span className={styles.muted}>versão {tlc.version}</span> : null}
          </div>
          {tlc.problems.map((p) => (
            <span key={p} className={styles.muted}>
              {p}
            </span>
          ))}
        </div>

        <div className={styles.card}>
          <h3 className={styles.cardTitle}>Harness já existente</h3>
          <span className={styles.muted}>
            {inventory.agents.length} agente(s), {inventory.skills.length} skill(s), {inventory.rules.length} regra(s),{' '}
            {inventory.hooks.length} hook(s), {inventory.mcpServers.length} servidor(es) MCP, {inventory.evals.length}{' '}
            arquivo(s) de eval
          </span>
          <EvidenceList
            summary="Ver o que existe"
            items={existing.map((i) => (i.issue ? `${i.path} — aviso: ${i.issue}` : i.path))}
          />
        </div>

        <div className={styles.card}>
          <h3 className={styles.cardTitle}>Repositório e sistema</h3>
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
        </div>
      </div>

      <div className={styles.actions}>
        <Button variant="contained" onClick={onContinue}>
          Continuar
        </Button>
      </div>
    </>
  );
}

function WarningBody({ message, paths }: { message: string; paths: readonly string[] }) {
  return (
    <>
      {message}
      {paths.length > 0 ? <EvidenceList summary={`Arquivos (${paths.length})`} items={paths} /> : null}
    </>
  );
}
