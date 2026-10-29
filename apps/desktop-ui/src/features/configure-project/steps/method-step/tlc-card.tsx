import { Checklist, StatusChip } from '@/shared/components/ui';
import type { Discovery } from '@/shared/schema';
import { cn } from '@/shared/utils';

import styles from '../../components/panels.module.scss';
import { TlcGlyph } from '../../components/method-badge';
import { tlcStateLabel, tlcStateTone } from '../../domain/labels';
import { TlcDependencies } from './tlc-dependencies';
import { TlcFooter } from './tlc-footer';

const TLC_FEATURES = [
  'Especificações estruturadas',
  'Planejamento e execução guiada',
  'Integração com agentes e ferramentas',
  'Modelo de documentação e validação'
].map((text) => ({ text }));

type Tlc = Discovery['tlc'];

function versionBadge({ version, versionTested }: Tlc) {
  if (!version) return { text: 'Não instalado', tone: 'neutral' } as const;
  return versionTested
    ? ({ text: 'Compatível', tone: 'success' } as const)
    : ({ text: 'Não validada', tone: 'warning' } as const);
}

interface TlcCardProps {
  tlc: Tlc;
  tools: Discovery['tools'];
  updateTlc: boolean;
  allowReinstall: boolean;
  onUpdateTlc: (value: boolean) => void;
  onReinstall: (value: boolean) => void;
}

export function TlcCard({ tlc, tools, updateTlc, allowReinstall, onUpdateTlc, onReinstall }: TlcCardProps) {
  const badge = versionBadge(tlc);

  return (
    <section className={cn(styles.areaTlc, styles.tlcCard)} aria-labelledby="tlc-titulo">
      <div className={styles.tlcGrid}>
        <span className={styles.tlcIcon} aria-hidden="true">
          <TlcGlyph size={28} />
        </span>
        <div className={styles.stack}>
          <header className={styles.tlcHeader}>
            <h3 id="tlc-titulo" className={styles.tlcTitle}>
              TLC (Tech Leads Club)
            </h3>
            <StatusChip tone="info">Padrão</StatusChip>
            {tlc.state === 'not_installed' ? null : (
              <StatusChip tone={tlcStateTone[tlc.state]}>{tlcStateLabel[tlc.state]}</StatusChip>
            )}
          </header>
          <span className={styles.tlcDescription}>
            Método de especificação e desenvolvimento orientado a boas práticas, com foco em qualidade, rastreabilidade
            e entrega contínua.
          </span>
          <div className={styles.featureGroup}>
            <h4 className={styles.featuresTitle}>Principais recursos</h4>
            <Checklist items={TLC_FEATURES} />
          </div>
        </div>

        <div className={styles.stack}>
          <h4 className={styles.fieldLabel}>{tlc.version ? 'Versão instalada' : 'Instalação'}</h4>
          <div className={styles.row}>
            {tlc.version ? <span className={styles.version}>v{tlc.version}</span> : null}
            <StatusChip tone={badge.tone}>{badge.text}</StatusChip>
          </div>
          {tlc.version ? (
            <span className={styles.muted}>
              {tlc.versionTested ? 'Versão validada para esta fase.' : 'Versão ainda não validada por esta fase.'}
            </span>
          ) : null}
          <h4 className={styles.fieldLabel}>Dependências</h4>
          <TlcDependencies tools={tools} />
        </div>

        <TlcFooter
          state={tlc.state}
          updateTlc={updateTlc}
          allowReinstall={allowReinstall}
          onUpdateTlc={onUpdateTlc}
          onReinstall={onReinstall}
        />
      </div>
    </section>
  );
}
