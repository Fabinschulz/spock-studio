import { formatEngineDate } from '@/features/integrations';
import { errorMessage, type EngineClient } from '@/shared/api';
import { pathTo } from '@/shared/routing';
import type { TechnologyProfile } from '@/shared/schema';
import { Button } from '@mariliz/ui-kit';
import { useEffect, useState } from 'react';
import { Link } from 'react-router';

import { CONFIDENCE_LABEL, changeText, toolKindLabel } from '../domain/profile-labels';
import { PageSection, LoadingState, ErrorState } from '@/shared/components';
import styles from '../project-page.module.scss';
import { readProfile, refreshProfile } from '../service/project-api';

const HISTORY_SHOWN = 10;

type State =
  { status: 'loading' } | { status: 'error'; message: string } | { status: 'ready'; profile: TechnologyProfile };

/**
 * O perfil tecnológico salvo pelo motor. Na primeira visita o motor cria o perfil (a análise só lê o projeto); depois,
 * "Atualizar perfil" roda a análise de novo e o histórico mostra o que mudou.
 */
export function ProfileSection({ client, projectId }: { client: EngineClient; projectId: string }) {
  const [state, setState] = useState<State>({ status: 'loading' });
  const [busy, setBusy] = useState(false);
  const [refreshError, setRefreshError] = useState<string | null>(null);

  useEffect(() => {
    let active = true;
    readProfile(client, projectId)
      .then((profile) => profile ?? refreshProfile(client, projectId))
      .then(
        (profile) => {
          if (active) setState({ status: 'ready', profile });
        },
        (error: unknown) => {
          if (active) setState({ status: 'error', message: errorMessage(error, 'Não foi possível montar o perfil.') });
        }
      );
    return () => {
      active = false;
    };
  }, [client, projectId]);

  const refresh = async () => {
    setBusy(true);
    setRefreshError(null);
    try {
      setState({ status: 'ready', profile: await refreshProfile(client, projectId) });
    } catch (error) {
      setRefreshError(errorMessage(error, 'Não foi possível atualizar o perfil.'));
    } finally {
      setBusy(false);
    }
  };

  return (
    <PageSection
      title="Perfil tecnológico"
      action={
        state.status === 'ready' ? (
          <Button type="button" variant="outlined" size="small" disabled={busy} onClick={() => void refresh()}>
            {busy ? 'Atualizando…' : 'Atualizar perfil'}
          </Button>
        ) : null
      }
    >
      {state.status === 'loading' ? <LoadingState>Montando o perfil do projeto…</LoadingState> : null}
      {state.status === 'error' ? <ErrorState>{state.message}</ErrorState> : null}
      {refreshError ? <ErrorState>{refreshError}</ErrorState> : null}
      {state.status === 'ready' ? <ProfileContent profile={state.profile} /> : null}
    </PageSection>
  );
}

function ProfileContent({ profile }: { profile: TechnologyProfile }) {
  return (
    <>
      <p className={styles.meta}>
        Analisado {formatEngineDate(profile.analyzedAt)}. A análise lê os arquivos de configuração do projeto e não
        altera nenhum.
      </p>

      {profile.technologies.length === 0 ? <p>A análise não identificou nenhuma tecnologia.</p> : null}
      <ul className={styles.rows} aria-label="Tecnologias">
        {profile.technologies.map((technology) => (
          <li key={technology.id} className={styles.row}>
            <span>
              <span className={styles.rowTitle}>{technology.name}</span>
              <span className={styles.meta}>
                {' '}
                · {technology.category} ·{' '}
                {technology.versions.length > 0 ? technology.versions.join(', ') : 'versão não declarada'} · em{' '}
                {technology.roots.join(', ')}
              </span>
            </span>
            <span className={styles.end}>{CONFIDENCE_LABEL[technology.confidence]}</span>
          </li>
        ))}
      </ul>

      {profile.tooling.length > 0 ? (
        <ul className={styles.rows} aria-label="Ferramentas">
          {profile.tooling.map((tool) => (
            <li key={`${tool.kind}:${tool.name}`} className={styles.row}>
              <span>
                <span className={styles.rowTitle}>{toolKindLabel(tool.kind)}</span>{' '}
                {tool.command ? (
                  <span className={styles.mono}>{tool.command}</span>
                ) : (
                  <span className={styles.meta}>{tool.name}</span>
                )}
              </span>
            </li>
          ))}
        </ul>
      ) : null}

      <h3 className={styles.subsection}>Conexões</h3>
      {profile.connections.length === 0 ? (
        <p className={styles.meta}>
          Nenhuma ferramenta externa vinculada. Vincule repositório e gestão em{' '}
          <Link to={pathTo('integrations')}>Integrações</Link>.
        </p>
      ) : (
        <ul className={styles.plainList}>
          {profile.connections.map((connection) => (
            <li key={`${connection.role}:${connection.resource}`}>
              <span className={styles.rowTitle}>{connection.role}:</span> {connection.resource}
            </li>
          ))}
        </ul>
      )}

      <h3 className={styles.subsection}>Como o Harness atende este perfil</h3>
      <ul className={styles.plainList}>
        {profile.harness.map((guidance) => (
          <li key={guidance.technology}>
            <span className={styles.rowTitle}>{guidance.technology}:</span>{' '}
            <span className={guidance.covered ? undefined : styles.warning}>{guidance.detail}</span>
          </li>
        ))}
      </ul>

      <details className={styles.history}>
        <summary>Histórico de mudanças ({profile.history.length})</summary>
        <ul>
          {profile.history.slice(0, HISTORY_SHOWN).map((change, index) => (
            <li key={`${change.at}:${index}`}>
              <span className={styles.meta}>{formatEngineDate(change.at)}:</span> {changeText(change)}
            </li>
          ))}
        </ul>
      </details>
    </>
  );
}
