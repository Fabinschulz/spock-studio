import type { SelectedProject } from '@/features/project-selection';
import { errorMessage, type EngineClient } from '@/shared/api';
import { InlineNotice, NoProjectState, Page, PageSection, ResourceView, StatusChip } from '@/shared/components';
import { useAsyncResource } from '@/shared/hooks';
import type { CorrectionBrief, ReviewFinding, ReviewSummary, ReviewVerdict, ReviewView } from '@/shared/schema';
import { Button, TextField, type Tone } from '@mariliz/ui-kit';
import { useCallback, useMemo, useState, type FormEvent } from 'react';

import { DeliveryPanel } from './components/delivery-panel';
import { FindingList } from './components/finding-list';
import { ReviewExtras } from './components/review-extras';
import { ReviewOptionsForm } from './components/review-options';
import styles from './quality-page.module.scss';
import { createQualityApi, SIMPLE_REVIEW, type QualityApi, type ReviewRequest } from './service/quality-api';

const VERDICT: Record<ReviewVerdict, { tone: Tone; label: string }> = {
  approved: { tone: 'success', label: 'Aprovada' },
  approved_with_warnings: { tone: 'warning', label: 'Aprovada com ressalvas' },
  rejected: { tone: 'danger', label: 'Reprovada' },
  inconclusive: { tone: 'warning', label: 'Inconclusiva' },
  not_run: { tone: 'neutral', label: 'Não executada' }
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
    { keepPrevious: true }
  );
  const options = useAsyncResource(useCallback(() => api.gates(project.projectId), [api, project.projectId]));
  const [run, setRun] = useState<Run>({ status: 'idle' });
  const [view, setView] = useState<ReviewView | null>(null);
  const [brief, setBrief] = useState<CorrectionBrief | null>(null);
  const [request, setRequest] = useState<ReviewRequest>(SIMPLE_REVIEW);
  const [base, setBase] = useState('');
  const busy = run.status === 'running';
  const gatesNeedApproval = request.gates.length > 0 && !request.gatesApproved;
  const info = options.status === 'ready' ? options.value : null;

  const execute = useCallback(
    async (what: string, start: () => Promise<ReviewView>) => {
      setRun({ status: 'running', what });
      // O relatório anterior sai da tela: depois de um erro, ele pareceria o resultado deste pedido.
      setView(null);
      setBrief(null);
      try {
        setView(await start());
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
      setView(null);
      setBrief(null);
      try {
        setView(await api.detail(project.projectId, id));
        setRun({ status: 'idle' });
      } catch (error) {
        setRun({ status: 'failed', message: errorMessage(error, 'Não foi possível abrir a revisão.') });
      }
    },
    [api, project.projectId]
  );

  // Uma decisão devolve a revisão com o que ainda bloqueia; um erro (justificativa curta, risco crítico) aparece sem tirar o relatório.
  const decide = useCallback(async (change: () => Promise<ReviewView>, fallback: string) => {
    setRun({ status: 'running', what: 'Registrando a decisão…' });
    try {
      setView(await change());
      setRun({ status: 'idle' });
    } catch (error) {
      setRun({ status: 'failed', message: errorMessage(error, fallback) });
    }
  }, []);

  const submitBranch = (event: FormEvent) => {
    event.preventDefault();
    if (base.trim())
      void execute('Revisando a branch…', () =>
        api.reviewBranch(project.projectId, base.trim(), { ...request, gates: [], gatesApproved: false })
      );
  };

  return (
    <Page
      title="Revisão de código"
      description={`Lê o que mudou em ${project.displayName} e procura defeitos com verificações do próprio Spock. A revisão simples não altera nenhum arquivo nem roda comandos do projeto; o modelo de IA e as verificações do projeto só entram se você marcar. Cada achado mostra a evidência e o quanto o Spock consegue provar; o que não foi verificado aparece como limitação.`}
      actions={
        <Button
          type="button"
          variant="contained"
          disabled={busy || gatesNeedApproval}
          onClick={() =>
            void execute('Revisando as alterações…', () => api.reviewWorkingTree(project.projectId, request))
          }
        >
          Revisar alterações locais
        </Button>
      }
    >
      <ReviewOptionsForm info={info} request={request} onChange={setRequest} disabled={busy} />
      {gatesNeedApproval ? (
        <p className={styles.note}>Marque a autorização para rodar as verificações escolhidas, ou desmarque-as.</p>
      ) : null}

      <form className={styles.branchForm} aria-label="Revisar uma branch" onSubmit={submitBranch}>
        <TextField
          label="Comparar com a base"
          size="small"
          value={base}
          disabled={busy}
          onChange={(event) => setBase(event.target.value)}
          helperText="Uma branch, tag ou commit. O Spock não adivinha a base. As verificações do projeto não rodam numa branch."
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
        <InlineNotice tone="danger" title="Não foi possível concluir">
          {run.message}
        </InlineNotice>
      ) : null}

      {view ? (
        <ReviewReport
          view={view}
          brief={brief}
          busy={busy}
          onDecideFinding={(finding, decision, justification) =>
            decide(
              () => api.decideFinding(project.projectId, view.review.id, finding.id, decision, justification),
              'Não foi possível registrar a decisão.'
            )
          }
          onDecideDelivery={(decision, justification) =>
            decide(
              () => api.decideDelivery(project.projectId, view.review.id, decision, justification),
              'Não foi possível registrar a decisão sobre a entrega.'
            )
          }
          onBrief={async () => {
            try {
              setBrief(await api.brief(project.projectId, view.review.id));
              // O pedido marca "correção pedida" nos achados: a revisão volta com isso.
              setView(await api.detail(project.projectId, view.review.id));
            } catch (error) {
              setRun({ status: 'failed', message: errorMessage(error, 'Não foi possível montar o pedido.') });
            }
          }}
        />
      ) : null}

      <PageSection title="Revisões anteriores" description="O histórico fica só neste computador.">
        <ResourceView resource={history} loading="Lendo o histórico…" fallbackError="Não foi possível ler o histórico.">
          {(list) => <History list={list} selected={view?.review.id} disabled={busy} onOpen={(id) => void open(id)} />}
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

function ReviewReport({
  view,
  brief,
  busy,
  onDecideFinding,
  onDecideDelivery,
  onBrief
}: {
  view: ReviewView;
  brief: CorrectionBrief | null;
  busy: boolean;
  onDecideFinding: (finding: ReviewFinding, decision: string, justification?: string) => Promise<void>;
  onDecideDelivery: (decision: 'approved' | 'rejected', justification: string) => Promise<void>;
  onBrief: () => Promise<void>;
}) {
  const { review, decisions } = view;
  const confirmed = review.findings.filter((f) => f.severity !== 'informational' && f.confidence === 'confirmed');
  const toCheck = review.findings.filter((f) => f.severity !== 'informational' && f.confidence !== 'confirmed');
  const recommendations = review.findings.filter((f) => f.severity === 'informational');
  const verdict = VERDICT[review.verdict];
  // O resumo do motor começa pelo veredito ("Reprovada: ..."), que já é o título do aviso.
  const reason = review.summary.startsWith(`${verdict.label}: `)
    ? review.summary.slice(verdict.label.length + 2)
    : review.summary;
  const list = (findings: ReviewFinding[], empty: string) => (
    <FindingList findings={findings} decisions={decisions} empty={empty} onDecide={onDecideFinding} disabled={busy} />
  );

  return (
    <>
      <InlineNotice tone={verdict.tone === 'neutral' ? 'info' : verdict.tone} title={verdict.label}>
        {reason}
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
        {list(confirmed, 'Nenhum problema confirmado.')}
      </PageSection>
      <PageSection
        title="A conferir"
        description="O Spock achou o padrão, mas só uma pessoa sabe se é defeito naquele contexto. Não bloqueiam a entrega."
      >
        {list(toCheck, 'Nada para conferir.')}
      </PageSection>
      <PageSection title="Recomendações">{list(recommendations, 'Nenhuma recomendação.')}</PageSection>

      <PageSection
        title="Decisão sobre a entrega"
        description="O veredito do Spock não muda com as suas decisões. Elas dizem o que ainda bloqueia, e a assinatura da entrega é sua."
      >
        <DeliveryPanel view={view} brief={brief} busy={busy} onDecide={onDecideDelivery} onBrief={onBrief} />
      </PageSection>

      <ReviewExtras review={review} />

      <PageSection
        title="Quem avaliou"
        description="Os juízes aplicam critérios escritos. O de revisão por modelo, quando você o pede, só propõe hipóteses."
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
              {`: ${file.status}`}
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
