import { formatEngineDate } from '@/shared/utils';
import { errorMessage, type EngineClient } from '@/shared/api';
import { pathTo } from '@/shared/routing';
import type { Compatibility, ProfileResult, TechnologyProfile } from '@/shared/schema';
import { Button } from '@mariliz/ui-kit';
import { useCallback, useState } from 'react';
import { Link } from 'react-router';

import { CONFIDENCE_LABEL, changeText, toolKindLabel } from '../domain/profile-labels';
import { PageSection, ErrorState, Disclosure, ResourceView } from '@/shared/components';
import { useAsyncResource } from '@/shared/hooks';
import styles from '../project-page.module.scss';
import { readProfile, refreshProfile } from '../service/project-api';

const HISTORY_SHOWN = 10;

/** Na primeira visita o motor cria o perfil (a análise só lê o projeto). */
export function ProfileSection({ client, projectId }: { client: EngineClient; projectId: string }) {
  const read = useAsyncResource(
    useCallback(async () => {
      const result = await readProfile(client, projectId);
      return result.profile ? result : refreshProfile(client, projectId);
    }, [client, projectId])
  );
  // O "Atualizar perfil" já traz o perfil novo: ele vale sobre a leitura, sem pedir de novo ao motor.
  const [refreshed, setRefreshed] = useState<ProfileResult | null>(null);
  const [busy, setBusy] = useState(false);
  const [refreshError, setRefreshError] = useState<string | null>(null);
  const profile = refreshed && read.status === 'ready' ? { ...read, value: refreshed } : read;

  const refresh = async () => {
    setBusy(true);
    setRefreshError(null);
    try {
      setRefreshed(await refreshProfile(client, projectId));
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
        profile.status === 'ready' ? (
          <Button type="button" variant="outlined" size="small" disabled={busy} onClick={() => void refresh()}>
            {busy ? 'Atualizando…' : 'Atualizar perfil'}
          </Button>
        ) : null
      }
    >
      {refreshError ? <ErrorState onRetry={() => void refresh()}>{refreshError}</ErrorState> : null}
      <ResourceView
        resource={profile}
        loading="Montando o perfil do projeto…"
        fallbackError="Não foi possível montar o perfil."
      >
        {(result) =>
          result.profile ? (
            <ProfileContent profile={result.profile} compatibility={result.compatibility ?? null} />
          ) : null
        }
      </ResourceView>
    </PageSection>
  );
}

const ATTENTION = new Set(['incompatible', 'end-of-life', 'maintenance', 'preview', 'unknown']);

/** A situação orienta os agentes a trabalhar na versão do projeto; atualizar é uma modernização à parte. */
function VersionsList({ compatibility }: { compatibility: Compatibility }) {
  return (
    <>
      <h3 className={styles.subsection}>Versões e suporte</h3>
      <p className={styles.meta}>
        Os agentes trabalham nas versões do projeto. Catálogo de versões de{' '}
        {formatEngineDate(compatibility.catalogGeneratedAt)}
        {compatibility.catalogStale ? ', possivelmente desatualizado' : ''}.
      </p>
      <ul className={styles.rows} aria-label="Versões e suporte">
        {compatibility.modules.map((module) => (
          <li key={`${module.root}:${module.technologyId}:${module.origin}`} className={styles.row}>
            <span>
              <span className={styles.rowTitle}>
                {module.name} {module.version ?? '(sem versão)'}
              </span>
              <span className={styles.meta}>
                {' '}
                · em {module.root} · {module.reason}
                {module.confirmed ? '' : ' Versão lida de uma faixa declarada.'}
              </span>
            </span>
            <span className={`${styles.end} ${ATTENTION.has(module.status) ? styles.warning : ''}`}>
              {module.statusLabel}
            </span>
          </li>
        ))}
      </ul>
    </>
  );
}

function ProfileContent({
  profile,
  compatibility
}: {
  profile: TechnologyProfile;
  compatibility: Compatibility | null;
}) {
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

      {compatibility && compatibility.modules.length > 0 ? <VersionsList compatibility={compatibility} /> : null}

      {profile.tooling.length > 0 ? (
        <ul className={styles.rows} aria-label="Ferramentas">
          {/* Dois módulos do projeto podem declarar a mesma ferramenta: a posição desempata a chave. */}
          {profile.tooling.map((tool, index) => (
            <li key={`${tool.kind}:${tool.name}:${index}`} className={styles.row}>
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

      <Disclosure className={styles.history} summary={<>Histórico de mudanças ({profile.history.length})</>}>
        <ul>
          {profile.history.slice(0, HISTORY_SHOWN).map((change, index) => (
            <li key={`${change.at}:${index}`}>
              <span className={styles.meta}>{formatEngineDate(change.at)}:</span> {changeText(change)}
            </li>
          ))}
        </ul>
      </Disclosure>
    </>
  );
}
