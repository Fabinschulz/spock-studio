import { componentRequestState, type ComponentRequestItem } from '@/features/configure-project';
import { errorMessage, type EngineClient } from '@/shared/api';
import { StatusChip, PageSection, LoadingState, ErrorState } from '@/shared/components';
import { pathTo } from '@/shared/routing';
import type { HarnessStatus } from '@/shared/schema';
import { Button } from '@mariliz/ui-kit';
import { useCallback, useEffect, useState } from 'react';
import { Link, useNavigate } from 'react-router';

import { HARNESS_STATE } from '../domain/harness-state';
import styles from '../project-page.module.scss';
import { readHarness } from '../service/project-api';

const STATE = HARNESS_STATE;

const KIND_LABEL: Record<string, string> = { agent: 'agente', skill: 'skill', rule: 'regra', hook: 'hook' };

type State = { status: 'loading' } | { status: 'error'; message: string } | { status: 'ready'; harness: HarnessStatus };

/**
 * Só leitura: mudar componentes é pela jornada Configurar, que planeja, mostra a diferença, pede aprovação e permite
 * reverter.
 */
export function HarnessSection({ client, projectId }: { client: EngineClient; projectId: string }) {
  const [state, setState] = useState<State>({ status: 'loading' });
  const [tick, setTick] = useState(0);
  const reload = useCallback(() => setTick((current) => current + 1), []);

  useEffect(() => {
    let active = true;
    readHarness(client, projectId).then(
      (harness) => {
        if (active) setState({ status: 'ready', harness });
      },
      (error: unknown) => {
        if (active) setState({ status: 'error', message: errorMessage(error, 'Não foi possível conferir o Harness.') });
      }
    );
    return () => {
      active = false;
    };
  }, [client, projectId, tick]);

  return (
    <PageSection
      title="Harness aplicado"
      action={
        state.status !== 'loading' ? (
          <Button type="button" variant="outlined" size="small" onClick={reload}>
            Conferir de novo
          </Button>
        ) : null
      }
    >
      {state.status === 'loading' ? <LoadingState>Conferindo o Harness…</LoadingState> : null}
      {state.status === 'error' ? <ErrorState onRetry={reload}>{state.message}</ErrorState> : null}
      {state.status === 'ready' ? <HarnessContent harness={state.harness} /> : null}
    </PageSection>
  );
}

function HarnessContent({ harness }: { harness: HarnessStatus }) {
  const navigate = useNavigate();
  // O pedido só abre o Configurar: lá vira plano, com a diferença da declaração e dos arquivos, e espera aprovação.
  const request = (kind: 'exclude' | 'include', { id, label }: ComponentRequestItem) =>
    void navigate(pathTo('configure'), { state: componentRequestState({ [kind]: [{ id, label }] }) });

  if (!harness.applied) {
    return (
      <p className={styles.meta}>
        O Harness ainda não foi aplicado neste projeto. Para aplicar, use{' '}
        <Link to={pathTo('configure')}>Configurar</Link>.
      </p>
    );
  }
  if (!harness.lockReadable) {
    return (
      <p className={styles.warning} role="alert">
        O lock do Harness existe, mas não pôde ser lido. O próximo plano em{' '}
        <Link to={pathTo('configure')}>Configurar</Link> o recria.
      </p>
    );
  }

  const total = harness.components.length;
  return (
    <>
      <p className={styles.meta}>
        {total} {total === 1 ? 'componente' : 'componentes'}: {harness.intact}{' '}
        {harness.intact === 1 ? 'íntegro' : 'íntegros'}, {harness.modified}{' '}
        {harness.modified === 1 ? 'modificado' : 'modificados'} à mão, {harness.missing}{' '}
        {harness.missing === 1 ? 'ausente' : 'ausentes'}
        {harness.kept > 0 ? `, ${harness.kept} ${harness.kept === 1 ? 'mantido' : 'mantidos'} na sua versão` : ''}.
        {harness.aiEnvironment ? ` Ambiente de IA: ${harness.aiEnvironment}.` : ''}
        {harness.specificationVersion ? ` Estrutura de especificação ${harness.specificationVersion}.` : ''}
      </p>

      <ul className={styles.rows} aria-label="Componentes do Harness">
        {harness.components.map((component) => (
          <li key={component.id} className={styles.harnessRow}>
            <div className={styles.row}>
              <span>
                <span className={styles.rowTitle}>{component.label}</span>
                <span className={styles.meta}>
                  {' '}
                  · {KIND_LABEL[component.kind] ?? component.kind} · versão {component.version}
                </span>
              </span>
              <span className={styles.rowActions}>
                <StatusChip tone={STATE[component.state].tone}>{STATE[component.state].label}</StatusChip>
                {component.referenceChanges.length > 0 ? (
                  <StatusChip tone="warning">Referência desatualizada</StatusChip>
                ) : null}
                {component.manageable ? (
                  <Button
                    type="button"
                    variant="text"
                    size="small"
                    aria-label={`Dispensar ${component.label}`}
                    onClick={() => request('exclude', component)}
                  >
                    Dispensar
                  </Button>
                ) : null}
              </span>
            </div>
            {component.state !== 'intact' ? (
              <ul className={styles.fileList}>
                {component.files
                  .filter((file) => file.state !== 'intact')
                  .map((file) => (
                    <li key={file.path}>
                      <span className={styles.mono}>{file.path}</span>{' '}
                      <span className={styles.meta}>({STATE[file.state].label.toLowerCase()})</span>
                    </li>
                  ))}
              </ul>
            ) : null}
            {component.referenceChanges.length > 0 ? (
              <p className={styles.meta}>
                A pasta Organização mudou desde que esta regra foi gerada ({component.referenceChanges.join('; ')}).
                Configure o projeto de novo para atualizá-la.
              </p>
            ) : null}
          </li>
        ))}
      </ul>

      {harness.excluded.length > 0 ? (
        <>
          <h3 className={styles.subsection}>Dispensados na declaração</h3>
          <ul className={styles.rows} aria-label="Componentes dispensados">
            {harness.excluded.map((item) => (
              <li key={item.id} className={styles.row}>
                <span>
                  <span className={styles.rowTitle}>{item.label}</span>
                  <span className={styles.meta}>
                    {' '}
                    · <span className={styles.mono}>{item.id}</span>
                  </span>
                </span>
                {item.manageable ? (
                  <Button
                    type="button"
                    variant="text"
                    size="small"
                    aria-label={`Voltar a aplicar ${item.label}`}
                    onClick={() => request('include', item)}
                  >
                    Voltar a aplicar
                  </Button>
                ) : null}
              </li>
            ))}
          </ul>
        </>
      ) : null}
      <p className={styles.meta}>
        Dispensar ou voltar a aplicar abre o plano em Configurar: ele grava o pedido na declaração do projeto e mostra o
        que sai ou volta. Nada muda sem a sua aprovação, e a aplicação pode ser revertida.
      </p>
      {harness.modified + harness.missing > 0 ? (
        <p className={styles.meta}>
          No próximo plano em <Link to={pathTo('configure')}>Configurar</Link>, o arquivo editado à mão aparece como
          conflito, com a diferença, para você decidir; o ausente é recriado. Nada é aplicado sem aprovação.
        </p>
      ) : null}
    </>
  );
}
