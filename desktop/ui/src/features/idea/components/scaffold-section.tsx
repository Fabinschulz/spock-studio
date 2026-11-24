import { InlineNotice, PageSection, ResourceView, StatusChip } from '@/shared/components';
import { useAsyncResource, useCopyToClipboard } from '@/shared/hooks';
import type { ProjectScaffold } from '@/shared/schema';
import { Button, TextField } from '@mariliz/ui-kit';
import { useCallback } from 'react';

import styles from '../idea-page.module.scss';
import type { IdeaApi } from '../service/idea-api';

/** Pedido pronto para colar no agente que tem o MCP do Spock; o plano completo vem da ferramenta. */
export const scaffoldAgentPrompt = [
  'Use a ferramenta spock_project_scaffold_prepare do Spock para criar o esqueleto deste projeto.',
  'Siga os passos na ordem, sem trocar versão, template nem nome, e grave os arquivos com o conteúdo exato.',
  'Depois rode a verificação, confira com spock_project_scaffold_check e me conte o resultado do build e dos testes.',
  'Não implemente funcionalidade: só o esqueleto.'
].join('\n');

/**
 * MV-6: da arquitetura aprovada ao esqueleto do projeto. O Spock escolhe as versões pela política e monta os comandos
 * oficiais; o agente da sessão executa e o motor confere os arquivos. Build e testes ficam com o agente.
 */
export function ScaffoldSection({ api, projectId }: { api: IdeaApi; projectId: string }) {
  const scaffold = useAsyncResource(
    useCallback(() => api.readScaffold(projectId), [api, projectId]),
    { keepPrevious: true }
  );

  return (
    <PageSection
      title="Criar o projeto"
      action={
        <Button type="button" variant="outlined" size="small" onClick={() => scaffold.reload?.()}>
          Ler de novo
        </Button>
      }
    >
      <ResourceView
        resource={scaffold}
        loading="Montando o plano de criação…"
        fallbackError="Não foi possível montar o plano de criação."
      >
        {(value) => <ScaffoldView scaffold={value} />}
      </ResourceView>
    </PageSection>
  );
}

function ScaffoldView({ scaffold }: { scaffold: ProjectScaffold }) {
  const { state: copyState, copy } = useCopyToClipboard();
  const { plan } = scaffold;
  if (!plan.supported) {
    return (
      <InlineNotice tone="info" title="Ainda não dá para criar o projeto">
        <ul className={styles.checks} aria-label="O que impede a criação">
          {plan.problems.map((problem) => (
            <li key={problem}>{problem}</li>
          ))}
        </ul>
      </InlineNotice>
    );
  }

  const passed = scaffold.checks.filter((check) => check.passed).length;
  return (
    <div className={styles.artifact}>
      <p className={styles.note}>
        Com a arquitetura aprovada, o Spock escolhe as versões pela política de suporte e monta os comandos oficiais de
        criação. O agente da sua sessão executa, roda o build e os testes, e o Spock confere os arquivos.
      </p>
      <h3 className={styles.heading}>Versões de {plan.projectName}</h3>
      <ul className={styles.list} aria-label="Versões escolhidas">
        {plan.technologies.map((technology) => (
          <li key={technology.id}>
            <strong>
              {technology.name} {technology.version}
            </strong>{' '}
            (linha {technology.cycle}, {technology.channel}
            {technology.eol ? `, suporte até ${technology.eol}` : ''}): {technology.reason}
          </li>
        ))}
      </ul>
      {plan.notes.length > 0 ? (
        <>
          <h4 className={styles.listTitle}>Notas</h4>
          <ul className={styles.criteria}>
            {plan.notes.map((note) => (
              <li key={note}>{note}</li>
            ))}
          </ul>
        </>
      ) : null}
      <h4 className={styles.listTitle}>Passos que o agente executa</h4>
      <ol className={styles.list} aria-label="Passos de criação">
        {[...plan.steps, ...plan.verification].map((step) => (
          <li key={step.id}>
            {step.title} ({step.directory}):{' '}
            <span className={styles.mono}>{step.command ?? `grava ${step.file ?? ''}`}</span>
          </li>
        ))}
      </ol>
      <TextField
        label="Pedido de criação para o agente"
        multiline
        minRows={4}
        fullWidth
        value={scaffoldAgentPrompt}
        onFocus={(event) => event.target.select()}
        slotProps={{ input: { readOnly: true } }}
      />
      <div className={styles.actions}>
        <Button type="button" variant="outlined" color="inherit" onClick={() => void copy(scaffoldAgentPrompt)}>
          {copyState === 'copied' ? 'Copiado' : 'Copiar pedido'}
        </Button>
      </div>
      <h4 className={styles.listTitle}>
        Conferência dos arquivos{' '}
        <StatusChip tone={scaffold.done ? 'success' : 'neutral'}>
          {scaffold.done ? 'Esqueleto confere' : `${passed} de ${scaffold.checks.length}`}
        </StatusChip>
      </h4>
      <ul className={styles.checks} aria-label="Conferência da criação">
        {scaffold.checks.map((check) => (
          <li key={check.id}>
            {check.passed ? 'Passou' : 'Falta'}: {check.label}
            {check.detail ? ` (${check.detail})` : ''}
          </li>
        ))}
      </ul>
    </div>
  );
}
