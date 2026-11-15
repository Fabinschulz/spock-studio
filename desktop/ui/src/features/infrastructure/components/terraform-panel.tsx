import { formatEngineDate } from '@/features/integrations';
import { ConfirmAction, StatusChip } from '@/shared/components';
import type { TerraformOffer, TerraformStatus } from '@/shared/schema';
import { Button } from '@mariliz/ui-kit';
import { useState } from 'react';

import styles from '../infrastructure-page.module.scss';
import { INSTALL_EXPLANATION, TERRAFORM_STATE, sourceLabel } from '../domain/labels';

interface TerraformPanelProps {
  status: TerraformStatus;
  offer: TerraformOffer | null;
  busy: boolean;
  onRequestOffer: () => void;
  onDiscardOffer: () => void;
  onInstall: (offer: TerraformOffer) => void;
  onDecline: () => void;
  onAdopt: (version: string) => void;
  onRemove: (version: string) => void;
}

/** A dependência Terraform: o que há na máquina e as decisões que só a pessoa toma (instalar, usar a externa, remover). */
export function TerraformPanel({
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
  const state = TERRAFORM_STATE[status.state];
  const external = status.installations.find((installation) => installation.source === 'external');

  return (
    <div className={styles.panel}>
      <div className={styles.rowHead}>
        <span className={styles.rowTitle}>Terraform</span>
        <StatusChip tone={state.tone}>{state.label}</StatusChip>
      </div>
      <p>{status.message}</p>
      <p className={styles.hint}>
        Mínimo do Spock: {status.minimumVersion}
        {status.platform ? ` · Esta máquina: ${status.platform.os}_${status.platform.arch}` : ''}
      </p>

      {offer ? (
        <OfferReview offer={offer} busy={busy} onInstall={() => onInstall(offer)} onCancel={onDiscardOffer} />
      ) : status.state === 'missing' ? (
        <section className={styles.consent} aria-label="Instalar o Terraform">
          {INSTALL_EXPLANATION.map((paragraph) => (
            <p key={paragraph}>{paragraph}</p>
          ))}
          <div className={styles.actions}>
            <Button type="button" disabled={busy} onClick={onRequestOffer}>
              Autorizar instalação
            </Button>
            <Button type="button" variant="outlined" disabled={busy} onClick={onDecline}>
              Não instalar agora
            </Button>
          </div>
        </section>
      ) : status.state === 'declined' ? (
        <div className={styles.actions}>
          <Button type="button" variant="outlined" disabled={busy} onClick={onRequestOffer}>
            Instalar o Terraform
          </Button>
        </div>
      ) : status.state === 'externalAvailable' && external ? (
        <div className={styles.actions}>
          <Button type="button" disabled={busy} onClick={() => onAdopt(external.version)}>
            Usar o Terraform {external.version} desta máquina
          </Button>
        </div>
      ) : status.state === 'ready' || status.state === 'incompatible' ? (
        <div className={styles.actions}>
          <Button type="button" variant="outlined" disabled={busy} onClick={onRequestOffer}>
            {status.state === 'ready' ? 'Procurar versão mais nova' : 'Ver versão compatível para instalar'}
          </Button>
        </div>
      ) : null}

      {status.installations.length > 0 ? (
        <ul className={styles.rows} aria-label="Instalações do Terraform">
          {status.installations.map((installation) => (
            <li key={`${installation.source}-${installation.path}`} className={styles.row}>
              <div className={styles.rowHead}>
                <span className={styles.rowTitle}>Terraform {installation.version}</span>
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
                removing === installation.version ? (
                  <ConfirmAction
                    label={`Remover o Terraform ${installation.version}`}
                    confirmLabel="Remover versão"
                    busy={busy}
                    onConfirm={() => {
                      setRemoving(null);
                      onRemove(installation.version);
                    }}
                    onCancel={() => setRemoving(null)}
                  >
                    Só esta versão sai da pasta do Spock. Projetos fixados nela impedem a remoção.
                  </ConfirmAction>
                ) : (
                  <div className={styles.actions}>
                    <Button
                      type="button"
                      variant="text"
                      disabled={busy}
                      onClick={() => setRemoving(installation.version)}
                    >
                      Remover
                    </Button>
                  </div>
                )
              ) : null}
            </li>
          ))}
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
  offer: TerraformOffer;
  busy: boolean;
  onInstall: () => void;
  onCancel: () => void;
}) {
  const alreadyInstalled = offer.impacts.some((impact) => impact.includes('já está instalado'));
  return (
    <section className={styles.consent} aria-label="Proposta de instalação">
      <p className={styles.rowTitle}>
        {offer.isUpdate
          ? `Atualização: Terraform ${offer.currentVersion} → ${offer.version}`
          : `Terraform ${offer.version}`}
      </p>
      <dl className={styles.facts}>
        <dt>Plataforma</dt>
        <dd>
          {offer.platform.os}_{offer.platform.arch}
        </dd>
        <dt>Origem</dt>
        <dd>{offer.archiveUrl}</dd>
        <dt>Verificação</dt>
        <dd>Checksum SHA-256 da lista assinada pela HashiCorp (chave {offer.signingKey.match(/.{4}/g)?.join(' ')})</dd>
        <dt>Publicada em</dt>
        <dd>{formatEngineDate(offer.publishedAt)}</dd>
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
            Instalar Terraform {offer.version}
          </Button>
        )}
        <Button type="button" variant="outlined" disabled={busy} onClick={onCancel}>
          {alreadyInstalled ? 'Fechar' : 'Cancelar'}
        </Button>
      </div>
    </section>
  );
}
