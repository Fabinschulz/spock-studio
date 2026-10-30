import { Checklist, SectionCard, StatusChip } from '@/shared/components/ui';
import type { Discovery } from '@/shared/schema';
import { cn } from '@/shared/utils';

import { InfoGlyph } from '../../components/glyphs';
import styles from '../../components/panels.module.scss';
import { aiEnvironmentNames } from '../../domain/ai-environments';

const CONFIGURED_ITEMS = [
  { text: 'Agentes: Harness Engineer, revisor de código e verificador' },
  { text: 'Skill: contrato de evidência' },
  { text: 'Hook: política de segurança antes de escritas e comandos' },
  { text: 'Regras gerais, da stack e dos comandos do repositório' },
  { text: 'Evals: verificações da configuração aplicada' },
  { text: 'Tools e MCPs: nenhum é instalado nesta jornada', state: 'neutral' as const }
];

interface ConfigurationSummaryCardProps {
  specificationVersion: Discovery['specification']['version'];
  aiEnvironment: string | null;
}

export function ConfigurationSummaryCard({ specificationVersion, aiEnvironment }: ConfigurationSummaryCardProps) {
  return (
    <SectionCard className={cn(styles.areaSummary, styles.fillCard)} title="Resumo da configuração">
      <dl className={styles.stack}>
        <div>
          <dt className={styles.sectionTitle}>Modelo de implementação</dt>
          <dd className={cn(styles.row, styles.spread)}>
            <span>Mariliz</span>
            {specificationVersion ? <StatusChip tone="info">v{specificationVersion}</StatusChip> : null}
          </dd>
        </div>
        <div>
          <dt className={styles.sectionTitle}>Ambiente de IA</dt>
          <dd>{aiEnvironment ? (aiEnvironmentNames[aiEnvironment] ?? aiEnvironment) : 'Ainda não escolhido'}</dd>
        </div>
        <div>
          <dt className={styles.sectionTitle}>O que será configurado</dt>
          <dd>
            <Checklist items={CONFIGURED_ITEMS} />
          </dd>
        </div>
      </dl>
      <p className={styles.cardNote}>
        <InfoGlyph />
        <span>Nada é gravado antes da revisão: a configuração só é aplicada depois da sua aprovação.</span>
      </p>
    </SectionCard>
  );
}
