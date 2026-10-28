import { Alert, Button, Modal } from '@synthra.io/ui-kit';
import { useState } from 'react';

import { EvidenceDisclosure, PrimaryActionFooter, StatusChip } from '@/shared/components/ui';
import type { EvalResult, RunResult } from '@/shared/schemas';

import { HARNESS_ENGINEER_NOT_INVOKED, harnessEngineerPrompt } from '../../core/harness-engineer-prompt';

import type { JourneyResults, Loadable, RunView } from '../../core/journey-state';
import { evalStatusLabel, evalStatusTone, outcomeLabel, runKindLabel } from '../../core/labels';
import styles from '../../ui/panels.module.scss';
import { StepHeader } from '../../ui/step-header';

interface ResultStepProps {
  results: JourneyResults;
  /** Reversão ou validação em andamento. */
  run: RunView | null;
  onRevert: (runId: string) => void;
  onValidate: () => void;
  onDone: () => void;
  /** Gera o plano de novo, agora com a proposta do Harness Engineer (se a pessoa a escreveu). */
  onReplan: () => void;
  /** Estado do plano gerado a partir daqui; o erro dele precisa aparecer nesta etapa. */
  replan: Loadable<unknown>;
}

type Severity = 'success' | 'warning' | 'error' | 'info';

const severityOf = (o: RunResult['outcome']): Severity =>
  o === 'completed' ? 'success' : o === 'completed_with_limitations' ? 'warning' : o === 'cancelled' ? 'info' : 'error';

/** O texto diz o que de fato aconteceu com os arquivos; nunca um sucesso genérico. */
function explain(result: RunResult): string {
  switch (result.outcome) {
    case 'completed':
      return 'Todas as verificações obrigatórias foram aprovadas com evidência.';
    case 'completed_with_limitations':
      return 'A configuração foi concluída, mas há itens que não puderam ser verificados ou que dependem de você. Veja os detalhes abaixo.';
    case 'cancelled':
      return result.rolledBack
        ? 'A execução foi cancelada e as alterações já feitas foram desfeitas.'
        : 'A execução foi cancelada.';
    case 'requires_intervention':
      return 'A aplicação falhou e algumas alterações não puderam ser desfeitas sozinhas: os arquivos listados em pendências foram mantidos como estão.';
    case 'failed':
      if (result.error)
        return result.rolledBack
          ? 'A aplicação falhou e as alterações já feitas foram desfeitas.'
          : 'A aplicação falhou antes de alterar arquivos.';
      return result.changesApplied
        ? 'As alterações foram aplicadas, mas uma verificação obrigatória falhou. Revise os Evals reprovados ou reverta as alterações.'
        : 'A execução falhou.';
  }
}

export function ResultStep({ results, run, onRevert, onValidate, onDone, onReplan, replan }: ResultStepProps) {
  const [confirming, setConfirming] = useState(false);
  const apply = results.apply;
  const revert = results.revert;
  const busy = run !== null && run.error === null;
  const validation = results.validate?.validation ?? apply?.validation;
  const canRevert = apply?.rollbackAvailable === true && revert?.outcome !== 'completed';
  // O agente só existe no projeto depois de aplicado; enquanto o plano ainda usou as regras-base, falta pedir a proposta.
  const awaitingProposal =
    apply !== null &&
    apply.changesApplied &&
    !apply.rolledBack &&
    revert?.outcome !== 'completed' &&
    (apply.limitations ?? []).some((l) => l.code === HARNESS_ENGINEER_NOT_INVOKED);

  return (
    <section className={styles.step}>
      <StepHeader title="Resultado" />

      {apply ? (
        <Alert
          severity={severityOf(apply.outcome)}
          title={outcomeLabel[apply.outcome]}
          description={
            <>
              {explain(apply)}
              {apply.error ? (
                <div className={styles.mono}>
                  {apply.error.code}: {apply.error.message}
                </div>
              ) : null}
            </>
          }
        />
      ) : null}

      {apply?.files ? <FilesSummary files={apply.files} /> : null}

      {apply && apply.pending && apply.pending.length > 0 ? (
        <div className={styles.card}>
          <h3 className={styles.cardTitle}>Pendências e itens mantidos</h3>
          <ul className={styles.list}>
            {apply.pending.map((p) => (
              <li key={p}>{p}</li>
            ))}
          </ul>
        </div>
      ) : null}

      {validation ? <ValidationTable report={validation} /> : null}

      {apply?.limitations && apply.limitations.length > 0 ? (
        <>
          <h3 className={styles.sectionTitle}>Limitações do plano</h3>
          {apply.limitations.map((l) => (
            <Alert key={l.code + l.message} severity="warning" description={l.message} />
          ))}
        </>
      ) : null}

      {apply?.risks && apply.risks.length > 0 ? (
        <EvidenceDisclosure summary={`Riscos remanescentes (${apply.risks.length})`} items={apply.risks} />
      ) : null}

      {awaitingProposal ? <ProposalGuide onReplan={onReplan} replan={replan} /> : null}

      {revert ? <RevertSummary result={revert} /> : null}

      {run?.error ? (
        <Alert severity="error" title={`${runKindLabel[run.kind]} não concluída`} description={run.error} />
      ) : null}

      {busy ? (
        <div role="status" aria-live="polite" className={styles.card}>
          <span>{runKindLabel[run.kind]} em andamento…</span>
          <div className={styles.progress} />
        </div>
      ) : null}

      <PrimaryActionFooter>
        {canRevert ? (
          <Button variant="outlined" color="inherit" disabled={busy} onClick={() => setConfirming(true)}>
            Reverter alterações
          </Button>
        ) : null}
        <Button variant="outlined" color="inherit" disabled={busy} onClick={onValidate}>
          Validar de novo
        </Button>
        <Button variant="contained" disabled={busy} onClick={onDone}>
          Concluir
        </Button>
      </PrimaryActionFooter>

      <Modal
        open={confirming}
        onClose={() => setConfirming(false)}
        title="Reverter as alterações?"
        description="Isto restaura o estado anterior dos arquivos que a aplicação alterou. Arquivos que você editou depois da aplicação não são sobrescritos: ficam como estão e aparecem como pendência."
        closeLabel="Fechar"
      >
        <div className={styles.actions}>
          <Button variant="outlined" color="inherit" onClick={() => setConfirming(false)}>
            Não reverter
          </Button>
          <Button
            variant="contained"
            onClick={() => {
              setConfirming(false);
              if (apply) onRevert(apply.runId);
            }}
          >
            Reverter agora
          </Button>
        </div>
      </Modal>
    </section>
  );
}

type CopyState = 'idle' | 'copied' | 'failed';

/** Próximo passo guiado: o prompt pronto para o agente e o botão para planejar de novo com a proposta dele. */
function ProposalGuide({ onReplan, replan }: { onReplan: () => void; replan: Loadable<unknown> }) {
  const [copy, setCopy] = useState<CopyState>('idle');

  const copyPrompt = async () => {
    try {
      await navigator.clipboard.writeText(harnessEngineerPrompt);
      setCopy('copied');
    } catch {
      setCopy('failed');
    }
  };

  return (
    <div className={styles.card}>
      <h3 className={styles.cardTitle}>Próximo passo: peça a proposta ao Harness Engineer</h3>
      <span className={styles.muted}>
        O plano usou as regras-base. O agente <span className={styles.mono}>harness-engineer</span> já está no projeto (
        <span className={styles.mono}>.claude/agents/harness-engineer.md</span>). Abra a pasta no Claude Code, cole o
        pedido abaixo e deixe que ele escreva <span className={styles.mono}>.mariliz/proposal.json</span>. O motor
        valida a proposta antes de usá-la e você revisa tudo de novo antes de aplicar.
      </span>
      <textarea
        className={styles.prompt}
        readOnly
        aria-label="Pedido para o Harness Engineer"
        value={harnessEngineerPrompt}
        onFocus={(event) => event.currentTarget.select()}
      />
      <div className={styles.actions}>
        <Button variant="outlined" color="inherit" onClick={() => void copyPrompt()}>
          {copy === 'copied' ? 'Copiado' : 'Copiar pedido'}
        </Button>
        <Button variant="contained" onClick={onReplan} disabled={replan.status === 'loading'}>
          {replan.status === 'loading' ? 'Gerando plano…' : 'Gerar o plano de novo'}
        </Button>
      </div>
      <div role="status" aria-live="polite">
        {copy === 'copied' ? <span className={styles.muted}>Pedido copiado para a área de transferência.</span> : null}
        {copy === 'failed' ? (
          <span className={styles.muted}>
            Não foi possível copiar automaticamente. Selecione o texto acima e copie com Ctrl+C.
          </span>
        ) : null}
      </div>
      {replan.status === 'error' ? (
        <Alert severity="error" title="Não foi possível gerar o plano" description={replan.message} />
      ) : null}
    </div>
  );
}

function FilesSummary({ files }: { files: NonNullable<RunResult['files']> }) {
  const groups: [string, readonly string[], 'success' | 'info' | 'warning' | 'neutral'][] = [
    ['criados', files.created, 'success'],
    ['atualizados', files.updated, 'info'],
    ['removidos', files.removed, 'warning'],
    ['preservados', files.preserved, 'neutral'],
    ['mantidos (seus)', files.kept, 'neutral'],
    ['não aplicados', files.notApplied, 'warning']
  ];

  return (
    <div className={styles.card}>
      <h3 className={styles.cardTitle}>Arquivos</h3>
      <div className={styles.chips}>
        {groups.map(([label, items, tone]) => (
          <StatusChip key={label} tone={tone}>
            {items.length} {label}
          </StatusChip>
        ))}
      </div>
      {groups.map(([label, items]) =>
        items.length > 0 ? (
          <EvidenceDisclosure key={label} summary={`${label} (${items.length})`} items={items} />
        ) : null
      )}
      {files.commandEffects.length > 0 ? (
        <EvidenceDisclosure
          summary={`Efeitos do instalador (${files.commandEffects.length})`}
          items={files.commandEffects}
        />
      ) : null}
    </div>
  );
}

function ValidationTable({ report }: { report: NonNullable<RunResult['validation']> }) {
  const count = (s: EvalResult['status']) => report.results.filter((r) => r.status === s).length;

  return (
    <div className={styles.card}>
      <h3 className={styles.cardTitle}>Evals</h3>
      <div className={styles.chips} aria-label="Resumo dos Evals">
        {(['passed', 'failed', 'blocked', 'not_run'] as const).map((s) => (
          <StatusChip key={s} tone={evalStatusTone(s)}>
            {count(s)} {evalStatusLabel[s].toLowerCase()}
          </StatusChip>
        ))}
      </div>
      {/* Região rolável: precisa de foco para quem navega só pelo teclado. */}
      {/* eslint-disable-next-line jsx-a11y/no-noninteractive-tabindex */}
      <div role="region" aria-label="Resultados dos Evals" tabIndex={0} style={{ overflowX: 'auto' }}>
        <table className={styles.table}>
          <thead>
            <tr>
              <th scope="col">Eval</th>
              <th scope="col">Estado</th>
              <th scope="col">Evidência e limites</th>
            </tr>
          </thead>
          <tbody>
            {report.results.map((r) => (
              <tr key={r.id}>
                <td>
                  <div>{r.name}</div>
                  <div className={styles.mono}>
                    {r.id} · {r.kind === 'model' ? 'juiz por modelo' : 'determinístico'} ·{' '}
                    {r.required ? 'obrigatório' : 'opcional'}
                  </div>
                </td>
                <td>
                  <StatusChip tone={evalStatusTone(r.status)}>{evalStatusLabel[r.status]}</StatusChip>
                </td>
                <td>
                  {r.reason ? <div>{r.reason}</div> : null}
                  {r.limitations.map((l) => (
                    <div key={l} className={styles.muted}>
                      Limite: {l}
                    </div>
                  ))}
                  <EvidenceDisclosure summary={`Critério e evidências (${r.evidence.length})`} items={r.evidence}>
                    <p className={styles.muted}>Critério: {r.criteria}</p>
                  </EvidenceDisclosure>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}

function RevertSummary({ result }: { result: RunResult }) {
  return (
    <div className={styles.card}>
      <h3 className={styles.cardTitle}>
        Reversão{' '}
        <StatusChip tone={result.outcome === 'completed' ? 'success' : 'warning'}>
          {outcomeLabel[result.outcome]}
        </StatusChip>
      </h3>
      {result.error ? <Alert severity="error" description={`${result.error.code}: ${result.error.message}`} /> : null}
      <span className={styles.muted}>
        {result.verified
          ? 'Conferido: os arquivos restaurados voltaram ao estado anterior.'
          : 'Os arquivos restaurados não foram todos conferidos.'}
      </span>
      {result.reverted ? (
        <EvidenceDisclosure summary={`Restaurados (${result.reverted.length})`} items={result.reverted} />
      ) : null}
      {result.conflicts && result.conflicts.length > 0 ? (
        <Alert
          severity="warning"
          title="Mantidos como estão"
          description={
            <EvidenceDisclosure summary={`Arquivos (${result.conflicts.length})`} items={result.conflicts} />
          }
        />
      ) : null}
    </div>
  );
}
