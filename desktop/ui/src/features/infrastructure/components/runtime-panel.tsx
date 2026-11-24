import { formatEngineDate } from '@/shared/utils';
import { ConfirmAction, StatusChip } from '@/shared/components';
import type { IacEngine, IacRuntimeOffer, IacRuntimeStatus } from '@/shared/schema';
import { Button } from '@mariliz/ui-kit';
import { useState } from 'react';

import styles from '../infrastructure-page.module.scss';
import { ENGINE_NAME, INSTALL_EXPLANATION, RUNTIME_STATE, installationName, sourceLabel } from '../domain/labels';

interface TerraformPanelProps {
  status: IacRuntimeStatus;
  offer: IacRuntimeOffer | null;
  busy: boolean;
  onRequestOffer: (engine?: IacEngine) => void;
  onDiscardOffer: () => void;
  onInstall: (offer: IacRuntimeOffer) => void;
  onDecline: () => void;
  onAdopt: (engine: IacEngine, version: string) => void;
  onRemove: (engine: IacEngine, version: string) => void;
}

/** A ferramenta de IaC: o que há na máquina e as decisões que só a pessoa toma (instalar, usar a externa, remover). */
export function RuntimePanel({
  status,
  offer,
  busy,
  onRequestOffer,
  onDiscardOffer,
  onInstall,
  onDecline,
  onAdopt,
  onRemove
}: TerraformPanelProps) {
  const [removing, setRemoving] = useState<string | null>(null);
  const state = RUNTIME_STATE[status.state];
  const waiting = status.installations.find(
    (installation) => installation.source === 'external' && installation.compatible && !installation.adopted
  );
  const minimums = status.engines.map((engine) => `${engine.name} ${engine.minimumVersion}`).join(' · ');
  // O motor recusa a instalação que a edição não permite; aqui só não se oferece o que seria recusado.
  const canInstall = (engine: IacEngine) =>
    status.engines.find((info) => info.engine === engine)?.managedInstall ?? false;
  const notes = status.engines.filter((info) => info.note);
  const selectedEngine = status.selected?.engine ?? 'openTofu';

  return (
    <div className={styles.panel}>
      <div className={styles.rowHead}>
        <span className={styles.rowTitle}>Ferramenta de infraestrutura como código</span>
        <StatusChip tone={state.tone}>{state.label}</StatusChip>
      </div>
      <p>{status.message}</p>
      <p className={styles.hint}>
        Mínimo do Spock: {minimums}
        {status.platform ? ` · Esta máquina: ${status.platform.os}_${status.platform.arch}` : ''}
      </p>
      {notes.map((info) => (
        <p key={info.engine} className={styles.hint}>
          {info.note}
        </p>
      ))}

      {offer ? (
        <OfferReview offer={offer} busy={busy} onInstall={() => onInstall(offer)} onCancel={onDiscardOffer} />
      ) : status.state === 'missing' ? (
        <section className={styles.consent} aria-label="Instalar o OpenTofu">
          {INSTALL_EXPLANATION.map((paragraph) => (
            <p key={paragraph}>{paragraph}</p>
          ))}
          <div className={styles.actions}>
            <Button type="button" disabled={busy} onClick={() => onRequestOffer()}>
              Autorizar instalação
            </Button>
            <Button type="button" variant="outlined" disabled={busy} onClick={onDecline}>
              Não instalar agora
            </Button>
            {canInstall('terraform') ? (
              <Button type="button" variant="text" disabled={busy} onClick={() => onRequestOffer('terraform')}>
                Prefiro o Terraform da HashiCorp
              </Button>
            ) : null}
          </div>
        </section>
      ) : status.state === 'declined' ? (
        <div className={styles.actions}>
          <Button type="button" variant="outlined" disabled={busy} onClick={() => onRequestOffer()}>
            Instalar o OpenTofu
          </Button>
        </div>
      ) : status.state === 'externalAvailable' && waiting ? (
        <div className={styles.actions}>
          <Button type="button" disabled={busy} onClick={() => onAdopt(waiting.engine, waiting.version)}>
            Usar o {installationName(waiting)} desta máquina
          </Button>
        </div>
      ) : (status.state === 'ready' || status.state === 'incompatible') && canInstall(selectedEngine) ? (
        <div className={styles.actions}>
          <Button
            type="button"
            variant="outlined"
            disabled={busy}
            onClick={() => onRequestOffer(status.selected?.engine)}
          >
            {status.state === 'ready'
              ? `Procurar versão mais nova do ${ENGINE_NAME[selectedEngine]}`
              : 'Ver versão compatível para instalar'}
          </Button>
        </div>
      ) : null}

      {status.installations.length > 0 ? (
        <ul className={styles.rows} aria-label="Instalações encontradas">
          {status.installations.map((installation) => {
            const key = `${installation.engine}-${installation.source}-${installation.version}`;
            return (
              <li key={key} className={styles.row}>
                <div className={styles.rowHead}>
                  <span className={styles.rowTitle}>{installationName(installation)}</span>
                  <StatusChip tone={installation.problem ? 'warning' : installation.verified ? 'success' : 'danger'}>
                    {installation.problem ? 'Atenção' : installation.verified ? 'Verificado' : 'Não verificado'}
                  </StatusChip>
                </div>
                <p className={styles.meta}>
                  {sourceLabel(installation)} · {installation.path}
                  {installation.installedAt ? ` · desde ${formatEngineDate(installation.installedAt)}` : ''}
                </p>
                {installation.problem ? <p className={styles.hint}>{installation.problem}</p> : null}
                {installation.source === 'managed' ? (
                  removing === key ? (
                    <ConfirmAction
                      label={`Remover o ${installationName(installation)}`}
                      confirmLabel="Remover versão"
                      busy={busy}
                      onConfirm={() => {
                        setRemoving(null);
                        onRemove(installation.engine, installation.version);
                      }}
                      onCancel={() => setRemoving(null)}
                    >
                      Só esta versão sai da pasta do Spock. Projetos fixados nela impedem a remoção.
                    </ConfirmAction>
                  ) : (
                    <div className={styles.actions}>
                      <Button type="button" variant="text" disabled={busy} onClick={() => setRemoving(key)}>
                        Remover
                      </Button>
                    </div>
                  )
                ) : null}
              </li>
            );
          })}
        </ul>
      ) : null}
    </div>
  );
}

function OfferReview({
  offer,
  busy,
  onInstall,
  onCancel
}: {
  offer: IacRuntimeOffer;
  busy: boolean;
  onInstall: () => void;
  onCancel: () => void;
}) {
  const alreadyInstalled = offer.alreadyInstalled;
  return (
    <section className={styles.consent} aria-label="Proposta de instalação">
      <p className={styles.rowTitle}>
        {offer.isUpdate
          ? `Atualização: ${offer.engineName} ${offer.currentVersion} → ${offer.version}`
          : `${offer.engineName} ${offer.version}`}
      </p>
      <dl className={styles.facts}>
        <dt>Licença</dt>
        <dd>{offer.license}</dd>
        <dt>Plataforma</dt>
        <dd>
          {offer.platform.os}_{offer.platform.arch}
        </dd>
        <dt>Origem</dt>
        <dd>{offer.archiveUrl}</dd>
        <dt>Verificação</dt>
        <dd>
          Checksum SHA-256 da lista assinada por quem publica o {offer.engineName} (chave{' '}
          {offer.signingKey.match(/.{4}/g)?.join(' ')})
        </dd>
        {offer.publishedAt ? (
          <>
            <dt>Publicada em</dt>
            <dd>{formatEngineDate(offer.publishedAt)}</dd>
          </>
        ) : null}
      </dl>
      <ul className={styles.impacts}>
        {offer.impacts.map((impact) => (
          <li key={impact}>{impact}</li>
        ))}
      </ul>
      {offer.changelogUrl ? <p className={styles.hint}>Changelog: {offer.changelogUrl}</p> : null}
      <div className={styles.actions}>
        {alreadyInstalled ? null : (
          <Button type="button" disabled={busy} onClick={onInstall}>
            Instalar {offer.engineName} {offer.version}
          </Button>
        )}
        <Button type="button" variant="outlined" disabled={busy} onClick={onCancel}>
          {alreadyInstalled ? 'Fechar' : 'Cancelar'}
        </Button>
      </div>
    </section>
  );
}
