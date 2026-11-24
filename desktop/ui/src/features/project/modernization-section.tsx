import type { EngineClient } from '@/shared/api';
import { InlineNotice, PageSection, ResourceView, StatusChip } from '@/shared/components';
import { useAction, useAsyncResource, useCopyToClipboard } from '@/shared/hooks';
import type { ModernizationStep, ProjectModernization } from '@/shared/schema';
import { formatEngineDate } from '@/shared/utils';
import { Button, TextField } from '@mariliz/ui-kit';
import { useCallback, useState } from 'react';

import styles from './project-page.module.scss';
import { decideModernizationStep, readModernization } from './service/project-api';

type StepState = ProjectModernization['steps'][number];

const STATUS: Record<StepState['status'], { label: string; tone: 'success' | 'danger' | 'warning' | 'neutral' }> = {
  pending: { label: 'Aguardando você', tone: 'neutral' },
  approved: { label: 'Aprovado', tone: 'success' },
  rejected: { label: 'Recusado', tone: 'danger' },
  changed: { label: 'Mudou depois da decisão', tone: 'warning' }
};

/** Pedido pronto para colar no agente que tem o MCP do Spock; o roteiro completo vem da ferramenta. */
export const modernizationAgentPrompt = [
  'Use a ferramenta spock_modernization_plan_prepare do Spock para propor o plano de modernização deste projeto.',
  'Grave o plano onde a ferramenta indicar e confira com spock_modernization_plan_check até passar.',
  'Não mude o código: eu aprovo cada passo no Spock, e você só executa os aprovados quando eu pedir.'
].join('\n');

/**
 * W4-2 no modo Sessão: o agente propõe passos pequenos a partir da avaliação, o Spock confere sem modelo e a pessoa aprova
 * ou recusa cada um. A decisão vai para a memória de decisões do projeto e vale só para o texto do passo como foi lido.
 */
export function ModernizationSection({ client, projectId }: { client: EngineClient; projectId: string }) {
  const modernization = useAsyncResource(
    useCallback(() => readModernization(client, projectId), [client, projectId]),
    { keepPrevious: true }
  );
  const { state: copyState, copy } = useCopyToClipboard();

  return (
    <PageSection
      title="Plano de modernização"
      action={
        <Button type="button" variant="outlined" size="small" onClick={() => modernization.reload?.()}>
          Ler de novo
        </Button>
      }
    >
      <p className={styles.meta}>
        Depois da avaliação, ou para sair de uma versão atrás da recomendada, peça ao seu agente um plano de passos
        pequenos que se desfazem. O Spock confere que o plano caracteriza o comportamento de hoje antes de mudar, que
        nenhuma reescrita aparece sem custo, risco e plano de convivência, e que cada salto de versão segue o caminho de
        LTS em LTS com a página oficial de mudanças incompatíveis. Você aprova cada passo aqui; o agente só executa o
        que estiver aprovado.
      </p>
      <TextField
        label="Pedido para o agente"
        multiline
        minRows={3}
        fullWidth
        value={modernizationAgentPrompt}
        onFocus={(event) => event.target.select()}
        slotProps={{ input: { readOnly: true } }}
      />
      <Button type="button" variant="outlined" color="inherit" onClick={() => void copy(modernizationAgentPrompt)}>
        {copyState === 'copied' ? 'Copiado' : 'Copiar pedido'}
      </Button>
      <ResourceView
        resource={modernization}
        loading="Lendo o plano de modernização…"
        fallbackError="Não foi possível ler o plano de modernização."
      >
        {(value) => (
          <PlanView
            client={client}
            projectId={projectId}
            modernization={value}
            onChange={() => modernization.reload?.()}
          />
        )}
      </ResourceView>
    </PageSection>
  );
}

function PlanView({
  client,
  projectId,
  modernization,
  onChange
}: {
  client: EngineClient;
  projectId: string;
  modernization: ProjectModernization;
  onChange: () => void;
}) {
  const action = useAction(onChange);
  const plan = modernization.plan;
  if (!plan) {
    return (
      <>
        <Versions versions={modernization.versions} />
        <p className={styles.meta}>
          Nenhum plano gravado em <span className={styles.mono}>{modernization.planPath}</span>.
        </p>
      </>
    );
  }

  const failed = plan.checks.filter((check) => !check.passed);
  const divergences = new Map((modernization.assessment?.divergences ?? []).map((item) => [item.id, item.title]));
  const states = new Map(modernization.steps.map((state) => [state.stepId, state]));
  const decide = (step: ModernizationStep, decision: 'approved' | 'rejected', rationale: string) =>
    action.run(
      () => decideModernizationStep(client, projectId, step.id, decision, rationale),
      'Não foi possível registrar a decisão.',
      {
        target: step.id,
        success: () =>
          `${step.id} ${decision === 'approved' ? 'aprovado' : 'recusado'} e registrado nas decisões do projeto.`
      }
    );

  return (
    <div aria-label="Plano gravado">
      <p className={styles.meta}>
        {plan.valid ? (
          <StatusChip tone="success">Conferido pelo Spock</StatusChip>
        ) : (
          <StatusChip tone="warning">{failed.length} verificação(ões) falhando</StatusChip>
        )}{' '}
        {plan.goal}
      </p>
      {failed.length > 0 ? (
        <ul className={styles.plainList} aria-label="Verificações do plano">
          {failed.map((check) => (
            <li key={check.id} className={styles.warning}>
              {check.description}
              {check.detail ? ` (${check.detail})` : ''}
            </li>
          ))}
        </ul>
      ) : null}
      {action.error ? <InlineNotice tone="danger">{action.error}</InlineNotice> : null}
      <Versions versions={modernization.versions} />
      {plan.characterization.length > 0 ? (
        <>
          <h3 className={styles.subsection}>Antes de mudar: o que continua igual</h3>
          <ul className={styles.plainList} aria-label="Caracterização">
            {plan.characterization.map((item) => (
              <li key={item.behavior}>
                {item.behavior} <span className={styles.meta}>· preso por: {item.test}</span>{' '}
                <span className={styles.mono}>{item.files.join(' · ')}</span>
              </li>
            ))}
          </ul>
        </>
      ) : null}
      <h3 className={styles.subsection}>Passos</h3>
      <ol className={styles.rows} aria-label="Passos do plano">
        {plan.steps.map((step) => (
          <StepRow
            key={step.id || step.title}
            step={step}
            state={states.get(step.id)}
            divergences={divergences}
            decidable={plan.valid}
            busy={action.busy}
            notice={action.notice?.target === step.id ? action.notice.text : null}
            onDecide={(decision, rationale) => decide(step, decision, rationale)}
          />
        ))}
      </ol>
    </div>
  );
}

// MV-7: o caminho vem calculado pelo Spock; o plano só pode seguir os trechos daqui.
function Versions({ versions }: { versions: ProjectModernization['versions'] }) {
  if (versions.length === 0) return null;
  return (
    <>
      <h3 className={styles.subsection}>Versões atrás da recomendada</h3>
      <ul className={styles.plainList} aria-label="Versões atrás da recomendada">
        {versions.map((finding) => (
          <li key={finding.id}>
            {finding.id} · {finding.root === '' || finding.root === '.' ? 'raiz' : finding.root}: {finding.name}{' '}
            {finding.version ?? finding.cycle}{' '}
            <StatusChip tone={finding.status === 'end-of-life' ? 'danger' : 'warning'}>
              {finding.statusLabel}
            </StatusChip>{' '}
            <span className={styles.meta}>
              · caminho {[finding.cycle, ...finding.hops.map((hop) => hop.to)].join(' → ')} (recomendada{' '}
              {finding.target})
            </span>
          </li>
        ))}
      </ul>
    </>
  );
}

function StepRow({
  step,
  state,
  divergences,
  decidable,
  busy,
  notice,
  onDecide
}: {
  step: ModernizationStep;
  state: StepState | undefined;
  divergences: Map<string, string>;
  decidable: boolean;
  busy: boolean;
  notice: string | null;
  onDecide: (decision: 'approved' | 'rejected', rationale: string) => Promise<boolean>;
}) {
  const [rationale, setRationale] = useState('');
  const status = STATUS[state?.status ?? 'pending'];
  const submit = async (decision: 'approved' | 'rejected') => {
    if (await onDecide(decision, rationale)) setRationale('');
  };

  return (
    <li className={styles.row} aria-label={`Passo ${step.id}`}>
      <div>
        <span className={styles.rowTitle}>
          {step.id} · {step.title}
        </span>{' '}
        <StatusChip tone={status.tone}>{status.label}</StatusChip>{' '}
        {step.kind === 'rewrite' ? <StatusChip tone="warning">Reescrita</StatusChip> : null}
        {step.kind === 'upgrade' ? <StatusChip tone="info">Salto de versão</StatusChip> : null}
        {state?.recordedAt ? (
          <span className={styles.meta}> · decidido em {formatEngineDate(state.recordedAt)}</span>
        ) : null}
        <br />
        <span>{step.change}</span>
        <br />
        <span className={styles.meta}>
          {step.upgrade
            ? `Salto: ${step.upgrade.finding} ${step.upgrade.from} → ${step.upgrade.to}`
            : `Trata: ${step.divergences.map((id) => `${id} ${divergences.get(id) ?? ''}`.trim()).join('; ')}`}
          {step.dependsOn.length > 0 ? ` · depende de ${step.dependsOn.join(', ')}` : ''}
        </span>
        {step.upgrade ? (
          <>
            <br />
            <span className={styles.meta}>
              Mudanças incompatíveis: <span className={styles.mono}>{step.upgrade.breakingChanges}</span>
            </span>
          </>
        ) : null}
        <br />
        <span className={styles.meta}>
          Desfazer: {step.undo} · Verificar: {step.verification} · Risco: {step.risk}
          {step.cost ? ` · Custo: ${step.cost}` : ''}
          {step.coexistence ? ` · Convivência: ${step.coexistence}` : ''}
        </span>
        <br />
        <span className={styles.mono}>{step.files.join(' · ')}</span>
        {decidable ? (
          <div className={styles.meta}>
            <TextField
              label={`Motivo da decisão de ${step.id} (opcional)`}
              size="small"
              fullWidth
              value={rationale}
              onChange={(event) => setRationale(event.target.value)}
            />
            <Button
              type="button"
              variant="contained"
              size="small"
              disabled={busy}
              onClick={() => void submit('approved')}
            >
              Aprovar {step.id}
            </Button>{' '}
            <Button
              type="button"
              variant="outlined"
              color="inherit"
              size="small"
              disabled={busy}
              onClick={() => void submit('rejected')}
            >
              Recusar {step.id}
            </Button>
          </div>
        ) : null}
        {notice ? <InlineNotice tone="success">{notice}</InlineNotice> : null}
      </div>
    </li>
  );
}
