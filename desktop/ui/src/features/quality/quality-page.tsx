import type { SelectedProject } from '@/features/project-selection';
import { errorMessage, type EngineClient } from '@/shared/api';
import { InlineNotice, NoProjectState, Page, PageSection, ResourceView, StatusChip } from '@/shared/components';
import { useAsyncResource } from '@/shared/hooks';
import type { Review, ReviewFinding, ReviewSummary, ReviewVerdict } from '@/shared/schema';
import { Button, TextField, type Tone } from '@mariliz/ui-kit';
import { useCallback, useMemo, useState, type FormEvent } from 'react';

import styles from './quality-page.module.scss';
import { createQualityApi, type QualityApi } from './service/quality-api';

const VERDICT: Record<ReviewVerdict, { tone: Tone; label: string }> = {
  approved: { tone: 'success', label: 'Aprovada' },
  approved_with_warnings: { tone: 'warning', label: 'Aprovada com ressalvas' },
  rejected: { tone: 'danger', label: 'Reprovada' },
  inconclusive: { tone: 'warning', label: 'Inconclusiva' },
  not_run: { tone: 'neutral', label: 'Não executada' }
};

const SEVERITY: Record<ReviewFinding['severity'], { tone: Tone; label: string }> = {
  critical: { tone: 'danger', label: 'Crítico' },
  high: { tone: 'danger', label: 'Grave' },
  medium: { tone: 'warning', label: 'Médio' },
  low: { tone: 'info', label: 'Baixo' },
  informational: { tone: 'neutral', label: 'Informativo' }
};

const SKIPPED: Record<string, string> = {
  binary: 'binário',
  generated: 'gerado',
  'third-party': 'de terceiros',
  'too-large': 'grande demais',
  conflict: 'com conflito',
  unreadable: 'ilegível'
};

type Run = { status: 'idle' } | { status: 'running'; what: string } | { status: 'failed'; message: string };

export function QualityPage({ client, project }: { client: EngineClient; project: SelectedProject | null }) {
  const api = useMemo(() => createQualityApi(client), [client]);
  if (!project) {
    return (
      <Page title="Revisão de código">
        <NoProjectState />
      </Page>
    );
  }
  return <ProjectQuality key={project.projectId} api={api} project={project} />;
}

function ProjectQuality({ api, project }: { api: QualityApi; project: SelectedProject }) {
  const history = useAsyncResource(
    useCallback(() => api.list(project.projectId), [api, project.projectId]),
    {
      keepPrevious: true
    }
  );
  const [run, setRun] = useState<Run>({ status: 'idle' });
  const [review, setReview] = useState<Review | null>(null);
  const [base, setBase] = useState('');
  const busy = run.status === 'running';

  const execute = useCallback(
    async (what: string, start: () => Promise<Review>) => {
      setRun({ status: 'running', what });
      try {
        setReview(await start());
        setRun({ status: 'idle' });
        history.reload();
      } catch (error) {
        setRun({ status: 'failed', message: errorMessage(error, 'Não foi possível revisar as alterações.') });
      }
    },
    [history]
  );

  const open = useCallback(
    async (id: string) => {
      setRun({ status: 'running', what: 'Abrindo a revisão…' });
      try {
        setReview(await api.detail(project.projectId, id));
        setRun({ status: 'idle' });
      } catch (error) {
        setRun({ status: 'failed', message: errorMessage(error, 'Não foi possível abrir a revisão.') });
      }
    },
    [api, project.projectId]
  );

  const submitBranch = (event: FormEvent) => {
    event.preventDefault();
    if (base.trim()) void execute('Revisando a branch…', () => api.reviewBranch(project.projectId, base.trim()));
  };

  return (
    <Page
      title="Revisão de código"
      description={`Lê o que mudou em ${project.displayName} e procura defeitos com verificações do próprio Spock, sem alterar nenhum arquivo nem rodar comandos do projeto. Cada achado mostra a evidência e o quanto o Spock consegue provar; o que não foi verificado aparece como limitação.`}
      actions={
        <Button
          type="button"
          variant="contained"
          disabled={busy}
          onClick={() => void execute('Revisando as alterações…', () => api.reviewWorkingTree(project.projectId))}
        >
          Revisar alterações locais
        </Button>
      }
    >
      <form className={styles.branchForm} aria-label="Revisar uma branch" onSubmit={submitBranch}>
        <TextField
          label="Comparar com a base"
          size="small"
          value={base}
          disabled={busy}
          onChange={(event) => setBase(event.target.value)}
          helperText="Uma branch, tag ou commit. O Spock não adivinha a base."
        />
        <Button type="submit" variant="outlined" color="inherit" disabled={busy || !base.trim()}>
          Revisar a branch
        </Button>
      </form>

      {run.status === 'running' ? (
        <p role="status" className={styles.note}>
          {run.what}
        </p>
      ) : null}
      {run.status === 'failed' ? (
        <InlineNotice tone="danger" title="A revisão não foi feita">
          {run.message}
        </InlineNotice>
      ) : null}

      {review ? <ReviewReport review={review} /> : null}

      <PageSection title="Revisões anteriores" description="O histórico fica só neste computador.">
        <ResourceView resource={history} loading="Lendo o histórico…" fallbackError="Não foi possível ler o histórico.">
          {(list) => <History list={list} selected={review?.id} disabled={busy} onOpen={(id) => void open(id)} />}
        </ResourceView>
      </PageSection>
    </Page>
  );
}

function History({
  list,
  selected,
  disabled,
  onOpen
}: {
  list: ReviewSummary[];
  selected?: string;
  disabled: boolean;
  onOpen: (id: string) => void;
}) {
  if (list.length === 0) return <p className={styles.note}>Nenhuma revisão feita neste projeto ainda.</p>;
  return (
    <ul className={styles.history}>
      {list.map((item) => (
        <li key={item.id} className={styles.historyRow}>
          <StatusChip tone={VERDICT[item.verdict].tone}>{VERDICT[item.verdict].label}</StatusChip>
          <span>
            {item.mode === 'branch' ? `Branch contra ${item.base ?? '?'}` : 'Alterações locais'} ·{' '}
            {new Date(item.createdAt).toLocaleString('pt-BR')} · {item.counts.findings} achados
          </span>
          <Button
            type="button"
            size="small"
            variant="text"
            disabled={disabled || item.id === selected}
            onClick={() => onOpen(item.id)}
            aria-label={`Abrir a revisão de ${new Date(item.createdAt).toLocaleString('pt-BR')}`}
          >
            {item.id === selected ? 'Aberta' : 'Abrir'}
          </Button>
        </li>
      ))}
    </ul>
  );
}

function ReviewReport({ review }: { review: Review }) {
  const confirmed = review.findings.filter((f) => f.severity !== 'informational' && f.confidence === 'confirmed');
  const toCheck = review.findings.filter((f) => f.severity !== 'informational' && f.confidence !== 'confirmed');
  const recommendations = review.findings.filter((f) => f.severity === 'informational');
  const verdict = VERDICT[review.verdict];

  return (
    <>
      <InlineNotice tone={verdict.tone === 'neutral' ? 'info' : verdict.tone} title={verdict.label}>
        {review.summary}
      </InlineNotice>
      <p className={styles.note}>
        {review.mode === 'branch'
          ? `Branch ${review.head ?? review.branch ?? ''} contra ${review.base ?? ''}.`
          : `Alterações locais${review.branch ? ` na branch ${review.branch}` : ''}.`}{' '}
        {review.counts.filesAnalyzed} de {review.counts.files} arquivos analisados linha a linha.
      </p>

      <PageSection
        title="Problemas confirmados"
        description="O padrão encontrado já é o defeito, e a linha está no que foi alterado."
      >
        <FindingList findings={confirmed} empty="Nenhum problema confirmado." />
      </PageSection>
      <PageSection
        title="A conferir"
        description="O Spock achou o padrão, mas só uma pessoa sabe se é defeito naquele contexto. Não bloqueiam a entrega."
      >
        <FindingList findings={toCheck} empty="Nada para conferir." />
      </PageSection>
      <PageSection title="Recomendações">
        <FindingList findings={recommendations} empty="Nenhuma recomendação." />
      </PageSection>

      <PageSection
        title="Quem avaliou"
        description="Cada juiz aplica critérios escritos; nenhum deles é um modelo de IA."
      >
        <ul className={styles.judges}>
          {review.judges.map((judge) => (
            <li key={judge.id} className={styles.judge}>
              <div className={styles.findingHead}>
                <StatusChip tone={VERDICT[judge.verdict].tone}>{VERDICT[judge.verdict].label}</StatusChip>
                <strong>{judge.name}</strong>
              </div>
              <p className={styles.advice}>{judge.reason}</p>
            </li>
          ))}
        </ul>
      </PageSection>

      <PageSection title="O que não foi verificado">
        <ul className={styles.plain}>
          {review.limitations.map((text) => (
            <li key={text}>{text}</li>
          ))}
        </ul>
      </PageSection>

      {review.nextActions.length > 0 ? (
        <PageSection title="Próximos passos">
          <ul className={styles.plain}>
            {review.nextActions.map((text) => (
              <li key={text}>{text}</li>
            ))}
          </ul>
        </PageSection>
      ) : null}

      <PageSection title={`Arquivos (${review.files.length})`}>
        <ul className={styles.plain}>
          {review.files.map((file) => (
            <li key={`${file.status}:${file.path}`}>
              <span className={styles.path}>{file.path}</span>
              {` — ${file.status}`}
              {file.analyzed
                ? `, +${file.added} −${file.removed}`
                : `, não analisado (${SKIPPED[file.skippedReason ?? ''] ?? 'sem leitura'})`}
            </li>
          ))}
        </ul>
      </PageSection>
    </>
  );
}

function FindingList({ findings, empty }: { findings: ReviewFinding[]; empty: string }) {
  if (findings.length === 0) return <p className={styles.note}>{empty}</p>;
  return (
    <ul className={styles.findings}>
      {findings.map((finding) => (
        <li key={finding.id} className={styles.finding}>
          <div className={styles.findingHead}>
            <StatusChip tone={SEVERITY[finding.severity].tone}>{SEVERITY[finding.severity].label}</StatusChip>
            <strong>{finding.title}</strong>
            <span className={styles.rule}>{finding.ruleId}</span>
          </div>
          <span className={styles.path}>
            {finding.path}
            {finding.startLine === null ? '' : `, linha ${finding.startLine}`}
            {finding.evidence.symbol ? ` · ${finding.evidence.symbol}` : ''}
          </span>
          <pre className={styles.excerpt} aria-label="Evidência">
            {finding.evidence.excerpt}
          </pre>
          <p className={styles.advice}>{finding.actualBehavior}</p>
          <p className={styles.advice}>
            <strong>Esperado:</strong> {finding.expectedBehavior}
          </p>
          <p className={styles.advice}>
            <strong>Como corrigir:</strong> {finding.suggestedCorrection}
          </p>
          <p className={styles.provenance}>
            {finding.confidence === 'confirmed'
              ? 'Confirmado'
              : finding.confidence === 'heuristic'
                ? 'Heurística'
                : 'Hipótese'}{' '}
            · {finding.evidence.origin}
          </p>
        </li>
      ))}
    </ul>
  );
}
