import { Button } from '@synthra.io/ui-kit';
import { useState } from 'react';

// Protótipo: reutiliza o texto real do pedido. Em produção entra pela API pública da feature.
// eslint-disable-next-line no-restricted-imports
import { harnessEngineerPrompt } from '@/features/configure-project/domain/harness-engineer-prompt';
import type { ConfigurationPlan } from '@/shared/schema';

import { Glyph } from '../../ui/glyph';
import { Section, StatusGlyph, Tag } from '../../ui/primitives';
import screen from '../screen.module.scss';

import { StepHeader } from './step-project';

const kindText: Record<ConfigurationPlan['decisions'][number]['kind'], string> = {
  specification: 'Estrutura de especificação',
  agent: 'Agente',
  skill: 'Skill',
  hook: 'Hook',
  rule: 'Regra',
  tool: 'Ferramenta ou MCP',
  eval: 'Eval',
  config: 'Configuração'
};

const decisionText: Record<string, string> = {
  enable: 'habilitar',
  keep: 'manter',
  skip: 'dispensar',
  blocked: 'bloqueado'
};
const decisionTone = { enable: 'success', keep: 'accent', skip: 'neutral', blocked: 'danger' } as const;

export function PlanStep({ plan, demoPlanning }: { plan: ConfigurationPlan; demoPlanning: boolean }) {
  const [copied, setCopied] = useState(false);
  const noProposal = plan.limitations.some((l) => l.code === 'HARNESS_ENGINEER_NOT_INVOKED');

  const copy = async () => {
    try {
      await navigator.clipboard.writeText(harnessEngineerPrompt);
      setCopied(true);
    } catch {
      setCopied(false);
    }
  };

  return (
    <>
      <StepHeader
        title="Composição do Harness"
        lead="Cada componente tem uma decisão, a origem dela e a justificativa. O plano é uma proposta: nada é aplicado antes da revisão."
      />

      {noProposal ? (
        <div className={screen.notice}>
          <Glyph name="spark" size={16} />
          <div>
            <p>
              <strong>Plano pelas regras-base</strong>
            </p>
            <p>
              O Harness Engineer ainda não gerou uma proposta. Ele roda no seu ambiente de IA: peça a proposta lá e
              planeje de novo.
            </p>
            {demoPlanning ? (
              <p>Contrato proposto (demonstração): o painel ao lado mostra como o planejamento assistido apareceria.</p>
            ) : null}
            <p>
              <Button variant="outlined" color="inherit" onClick={() => void copy()}>
                {copied ? 'Pedido copiado' : 'Copiar pedido para o Harness Engineer'}
              </Button>
            </p>
          </div>
        </div>
      ) : null}

      <Section title={`Decisões por componente (${plan.decisions.length})`} id="decisoes">
        <ul role="list" className={screen.rows}>
          {plan.decisions.map((d) => {
            const name = d.componentId.split('.').slice(2).join('.') || d.componentId;
            const skipped = d.decision === 'skip';
            return (
              <li key={d.componentId} className={screen.row}>
                <span className={screen.rowIcon}>
                  <StatusGlyph
                    state={skipped ? 'skipped' : d.decision === 'blocked' ? 'failed' : 'done'}
                    label={decisionText[d.decision] ?? d.decision}
                  />
                </span>
                <span>
                  <span className={screen.rowTitle}>{name}</span>
                  <span className={screen.rowMeta}> · {kindText[d.kind]}</span>
                  <span className={screen.rowMeta} style={{ display: 'block' }}>
                    {d.justification}
                  </span>
                </span>
                <Tag tone={decisionTone[d.decision as keyof typeof decisionTone] ?? 'neutral'}>
                  {decisionText[d.decision] ?? d.decision}
                </Tag>
              </li>
            );
          })}
        </ul>
      </Section>

      <Section title="Riscos" id="riscos" aside={`${plan.risks.length}`}>
        <ul role="list" className={screen.stack}>
          {plan.risks.map((risk) => (
            <li key={risk} className={screen.rowMeta}>
              {risk}
            </li>
          ))}
        </ul>
      </Section>
    </>
  );
}
