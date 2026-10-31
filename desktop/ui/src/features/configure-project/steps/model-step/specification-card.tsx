import { Checklist, StatusChip } from '@/shared/components/ui';
import type { Discovery } from '@/shared/schema';
import { cn } from '@/shared/utils';
import { SpecificationGlyph } from '../../components/implementation-model-badge';
import styles from '../../components/panels.module.scss';
import { specificationStateLabel, specificationStateTone } from '../../domain/labels';
import { SpecificationDependencies } from './specification-dependencies';
import { SpecificationFooter } from './specification-footer';

const SPECIFICATION_FEATURES = [
  'Especificações estruturadas',
  'Planejamento e execução guiada',
  'Integração com agentes e ferramentas',
  'Modelo de documentação e validação'
].map((text) => ({ text }));

type Specification = Discovery['specification'];

function versionBadge({ version, versionTested }: Specification) {
  if (!version) return { text: 'Não instalado', tone: 'neutral' } as const;
  return versionTested
    ? ({ text: 'Compatível', tone: 'success' } as const)
    : ({ text: 'Não validada', tone: 'warning' } as const);
}

interface SpecificationCardProps {
  specification: Specification;
  tools: Discovery['tools'];
  updateSpecification: boolean;
  allowReinstall: boolean;
  onUpdateSpecification: (value: boolean) => void;
  onReinstall: (value: boolean) => void;
}

export function SpecificationCard({
  specification,
  tools,
  updateSpecification,
  allowReinstall,
  onUpdateSpecification,
  onReinstall
}: SpecificationCardProps) {
  const badge = versionBadge(specification);

  return (
    <section className={cn(styles.areaSpecification, styles.specificationCard)} aria-labelledby="specification-title">
      <div className={styles.specificationGrid}>
        <span className={styles.specificationIcon} aria-hidden="true">
          <SpecificationGlyph size={28} />
        </span>
        <div className={styles.stack}>
          <header className={styles.specificationHeader}>
            <h3 id="specification-title" className={styles.specificationTitle}>
              Estrutura de especificação
            </h3>
            {specification.state === 'not_installed' ? null : (
              <StatusChip tone={specificationStateTone[specification.state]}>
                {specificationStateLabel[specification.state]}
              </StatusChip>
            )}
          </header>
          <span className={styles.specificationDescription}>
            Organiza as especificações do projeto em um ciclo guiado (especificar, desenhar, planejar, executar e
            validar), com foco em qualidade, rastreabilidade e entrega contínua.
          </span>
          <div className={styles.featureGroup}>
            <h4 className={styles.featuresTitle}>Principais recursos</h4>
            <Checklist items={SPECIFICATION_FEATURES} />
          </div>
        </div>

        <div className={styles.stack}>
          <h4 className={styles.fieldLabel}>{specification.version ? 'Versão instalada' : 'Instalação'}</h4>
          <div className={styles.row}>
            {specification.version ? <span className={styles.version}>v{specification.version}</span> : null}
            <StatusChip tone={badge.tone}>{badge.text}</StatusChip>
          </div>
          {specification.version ? (
            <span className={styles.muted}>
              {specification.versionTested
                ? 'Versão validada para esta fase.'
                : 'Versão ainda não validada por esta fase.'}
            </span>
          ) : null}
          <h4 className={styles.fieldLabel}>Dependências</h4>
          <SpecificationDependencies tools={tools} />
        </div>

        <SpecificationFooter
          state={specification.state}
          updateSpecification={updateSpecification}
          allowReinstall={allowReinstall}
          onUpdateSpecification={onUpdateSpecification}
          onReinstall={onReinstall}
        />
      </div>
    </section>
  );
}
