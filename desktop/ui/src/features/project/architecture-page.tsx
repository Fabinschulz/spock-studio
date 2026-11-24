import type { SelectedProject } from '@/features/project-selection';
import type { EngineClient } from '@/shared/api';
import { Page, PageSection, ResourceView, StatusChip } from '@/shared/components';
import { useAsyncResource, useCopyToClipboard } from '@/shared/hooks';
import type { ArchitectureFinding, ProjectArchitecture } from '@/shared/schema';
import { Button, TextField } from '@mariliz/ui-kit';
import { useCallback } from 'react';

import { ProjectPageFrame } from './project-page-frame';
import styles from './project-page.module.scss';
import { readArchitecture } from './service/project-api';

type Assessment = NonNullable<ProjectArchitecture['assessment']>;
type Divergence = Assessment['divergences'][number];

const CLASSIFICATIONS: readonly { id: string; label: string; tone: 'danger' | 'warning' | 'neutral' }[] = [
  { id: 'risk', label: 'Risco', tone: 'danger' },
  { id: 'cost', label: 'Custo', tone: 'warning' },
  { id: 'style', label: 'Estilo', tone: 'neutral' }
];

/** Pedido pronto para colar no agente que tem o MCP do Spock; o roteiro completo vem da ferramenta. */
export const assessmentAgentPrompt = [
  'Use a ferramenta spock_architecture_assessment_prepare do Spock para avaliar a arquitetura deste projeto.',
  'Leia os arquivos que for citar, grave a avaliação onde a ferramenta indicar e confira com spock_architecture_assessment_check até passar.',
  'Não mude o código: eu leio a avaliação no Spock e decido os próximos passos.'
].join('\n');

/**
 * W4-1 no modo Sessão: o Spock descreve o código sem modelo, o agente que a pessoa já usa avalia contra as referências e
 * grava no projeto, e esta tela mostra a avaliação conferida. Nada aqui muda o código.
 */
export function ArchitecturePage({ client, project }: { client: EngineClient; project: SelectedProject | null }) {
  return (
    <ProjectPageFrame project={project} title="Arquitetura">
      {(selected) => (
        <Page
          title="Arquitetura"
          description={`${selected.displayName} · o que o código é hoje e a avaliação do seu agente contra as referências.`}
        >
          <ArchitectureContent client={client} projectId={selected.projectId} />
        </Page>
      )}
    </ProjectPageFrame>
  );
}

function ArchitectureContent({ client, projectId }: { client: EngineClient; projectId: string }) {
  const architecture = useAsyncResource(
    useCallback(() => readArchitecture(client, projectId), [client, projectId]),
    { keepPrevious: true }
  );

  return (
    <ResourceView
      resource={architecture}
      loading="Lendo a arquitetura do projeto…"
      fallbackError="Não foi possível ler a arquitetura do projeto."
    >
      {(value) => (
        <>
          <AssessmentSection architecture={value} onReload={() => architecture.reload?.()} />
          <AnalysisSection report={value.report} />
        </>
      )}
    </ResourceView>
  );
}

function AnalysisSection({ report }: { report: ProjectArchitecture['report'] }) {
  const warnings = report.findings.filter((finding) => finding.severity === 'warning');
  const info = report.findings.filter((finding) => finding.severity !== 'warning');
  return (
    <PageSection title="Análise do Spock">
      <p className={styles.meta}>
        Feita sem modelo, direto do código: descreve o que existe, com evidência; não é regra nem recomendação.{' '}
        {report.dotnetProjects.length} projeto(s) .NET e {report.frontends.length} frontend(s).
      </p>
      <h3 className={styles.subsection}>Violações de direção</h3>
      {warnings.length === 0 ? (
        <p className={styles.meta}>Nenhuma violação de direção de dependência encontrada.</p>
      ) : (
        <FindingList findings={warnings} label="Violações de direção" />
      )}
      {info.length > 0 ? (
        <>
          <h3 className={styles.subsection}>Tamanho e testes</h3>
          <FindingList findings={info} label="Tamanho e testes" />
        </>
      ) : null}
    </PageSection>
  );
}

function FindingList({ findings, label }: { findings: ArchitectureFinding[]; label: string }) {
  return (
    <ul className={styles.plainList} aria-label={label}>
      {findings.map((finding) => (
        <li key={`${finding.code}-${finding.title}`}>
          {finding.title} <span className={styles.mono}>[{finding.code}]</span>
          {finding.evidence.length > 0 ? (
            <span className={styles.meta}>
              {' '}
              · {finding.evidence.map((item) => (item.detail ? `${item.path} (${item.detail})` : item.path)).join('; ')}
            </span>
          ) : null}
        </li>
      ))}
    </ul>
  );
}

function AssessmentSection({ architecture, onReload }: { architecture: ProjectArchitecture; onReload: () => void }) {
  const { state: copyState, copy } = useCopyToClipboard();
  const assessment = architecture.assessment;
  return (
    <PageSection
      title="Avaliação do seu agente"
      action={
        <Button type="button" variant="outlined" size="small" onClick={onReload}>
          Ler de novo
        </Button>
      }
    >
      <p className={styles.meta}>
        Sem chave de IA no Spock: no Claude Code ou em outro agente com o MCP do Spock, cole o pedido abaixo. O agente
        avalia o código contra as referências da pasta Organização e grava em{' '}
        <span className={styles.mono}>{architecture.assessmentPath}</span>; o Spock confere cada arquivo, referência e
        achado citado.
      </p>
      <TextField
        label="Pedido para o agente"
        multiline
        minRows={3}
        fullWidth
        value={assessmentAgentPrompt}
        onFocus={(event) => event.target.select()}
        slotProps={{ input: { readOnly: true } }}
      />
      <Button type="button" variant="outlined" color="inherit" onClick={() => void copy(assessmentAgentPrompt)}>
        {copyState === 'copied' ? 'Copiado' : 'Copiar pedido'}
      </Button>
      {assessment ? <AssessmentView assessment={assessment} /> : null}
    </PageSection>
  );
}

function AssessmentView({ assessment }: { assessment: Assessment }) {
  const failed = assessment.checks.filter((check) => !check.passed);
  return (
    <div aria-label="Avaliação gravada">
      <p className={styles.meta}>
        {assessment.valid ? (
          <StatusChip tone="success">Conferida pelo Spock</StatusChip>
        ) : (
          <StatusChip tone="warning">{failed.length} verificação(ões) falhando</StatusChip>
        )}{' '}
        {assessment.summary}
      </p>
      {failed.length > 0 ? (
        <ul className={styles.plainList} aria-label="Verificações da avaliação">
          {failed.map((check) => (
            <li key={check.id} className={styles.warning}>
              {check.description}
              {check.detail ? ` (${check.detail})` : ''}
            </li>
          ))}
        </ul>
      ) : null}
      {CLASSIFICATIONS.map((kind) => (
        <DivergenceGroup
          key={kind.id}
          label={kind.label}
          tone={kind.tone}
          divergences={assessment.divergences.filter((divergence) => divergence.classification === kind.id)}
        />
      ))}
      {assessment.conventions.length > 0 ? (
        <>
          <h3 className={styles.subsection}>Convenções do projeto</h3>
          <ul className={styles.plainList} aria-label="Convenções do projeto">
            {assessment.conventions.map((convention) => (
              <li key={convention.title}>
                {convention.title} <span className={styles.meta}>· {convention.files.join(', ')}</span>
              </li>
            ))}
          </ul>
        </>
      ) : null}
    </div>
  );
}

function DivergenceGroup({
  label,
  tone,
  divergences
}: {
  label: string;
  tone: 'danger' | 'warning' | 'neutral';
  divergences: Divergence[];
}) {
  if (divergences.length === 0) return null;
  return (
    <>
      <h3 className={styles.subsection}>
        <StatusChip tone={tone}>{label}</StatusChip> {divergences.length} divergência(s)
      </h3>
      <ul className={styles.rows} aria-label={`Divergências de ${label.toLowerCase()}`}>
        {divergences.map((divergence) => (
          <li key={divergence.title} className={styles.row}>
            <span>
              <span className={styles.rowTitle}>{divergence.title}</span>
              <span className={styles.meta}> · {divergence.description}</span>
              <br />
              <span className={styles.meta}>Próximo passo: {divergence.recommendation}</span>
              <br />
              <span className={styles.mono}>
                {[
                  ...divergence.files,
                  ...divergence.references,
                  ...divergence.findings.map((code) => `[${code}]`)
                ].join(' · ')}
              </span>
            </span>
          </li>
        ))}
      </ul>
    </>
  );
}
