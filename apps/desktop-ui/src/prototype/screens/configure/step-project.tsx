import { Button } from '@synthra.io/ui-kit';

import { demoProject, discovery } from '../../data/fixtures';
import { Glyph } from '../../ui/glyph';
import { Section, StatusGlyph, Tag } from '../../ui/primitives';
import screen from '../screen.module.scss';

export type Analysis = 'idle' | 'loading' | 'ready';

const confidenceText = { high: 'confiança alta', medium: 'confiança média', low: 'confiança baixa' } as const;
const confidenceTone = { high: 'success', medium: 'accent', low: 'warning' } as const;

export function StepHeader({ title, lead }: { title: string; lead: string }) {
  return (
    <header className={screen.head}>
      <h1 className={screen.title}>{title}</h1>
      <p className={screen.lead}>{lead}</p>
    </header>
  );
}

export function ProjectStep({ analysis, onAnalyze }: { analysis: Analysis; onAnalyze: () => void }) {
  const stack = discovery.stacks[0];
  const existing = Object.values(discovery.inventory).reduce((sum, list) => sum + list.length, 0);

  return (
    <>
      <StepHeader
        title="Analisar o projeto"
        lead="O Mariliz lê a estrutura do projeto para entender stack, comandos e o que já existe. Nada é alterado."
      />

      <div className={screen.composer}>
        <Glyph name="folder" size={18} />
        <div className={screen.composerText}>
          <p className={screen.composerName}>{demoProject.name}</p>
          <p className={screen.composerPath}>{demoProject.path}</p>
        </div>
        <Button
          variant={analysis === 'idle' ? 'contained' : 'outlined'}
          color={analysis === 'idle' ? 'primary' : 'inherit'}
          disabled={analysis === 'loading'}
          onClick={onAnalyze}
        >
          {analysis === 'ready' ? 'Analisar de novo' : analysis === 'loading' ? 'Analisando…' : 'Analisar projeto'}
        </Button>
      </div>

      {analysis === 'idle' ? <p className={screen.note}>A análise é somente de leitura.</p> : null}

      {analysis === 'loading' ? (
        // O motor não emite progresso na descoberta (é uma chamada única): estado honesto, sem etapas nem percentual inventados.
        <div className={screen.working} role="status">
          <div className={screen.workingLine}>
            <StatusGlyph state="running" />
            <span>Lendo o projeto…</span>
          </div>
          <div className={screen.indeterminate} aria-hidden="true" />
          <p className={screen.note}>
            O motor não informa o andamento desta etapa; assim que terminar, o resultado aparece aqui.
          </p>
        </div>
      ) : null}

      {analysis === 'ready' ? (
        <>
          <Section title="Stack" id="stack">
            <ul role="list" className={screen.rows}>
              {stack ? (
                <li className={screen.row}>
                  <span className={screen.rowIcon}>
                    <Glyph name="cpu" size={18} />
                  </span>
                  <span>
                    <span className={screen.rowTitle}>{stack.name}</span>
                    <span className={screen.rowMeta}>
                      {' '}
                      {stack.version} · em {stack.root}
                    </span>
                  </span>
                  <Tag tone={confidenceTone[stack.confidence]}>{confidenceText[stack.confidence]}</Tag>
                </li>
              ) : null}
            </ul>
            {stack ? (
              <details className={screen.details}>
                <summary>Evidências ({stack.evidence.length})</summary>
                <ul role="list" className={screen.stack}>
                  {stack.evidence.map((e) => (
                    <li key={e.path} className={screen.mono}>
                      {e.path}
                    </li>
                  ))}
                </ul>
              </details>
            ) : null}
          </Section>

          <Section title="Comandos detectados" id="comandos" aside="nada é executado por ter sido detectado">
            <ul role="list" className={screen.rows}>
              {discovery.tooling.map((tool) => (
                <li key={tool.kind} className={screen.row}>
                  <span className={screen.rowIcon}>
                    <Glyph name="clock" size={18} />
                  </span>
                  <span>
                    <span className={screen.rowTitle}>{tool.kind === 'build' ? 'Build' : 'Teste'}</span>{' '}
                    <span className={screen.mono}>{tool.command}</span>
                  </span>
                  <span />
                </li>
              ))}
            </ul>
          </Section>

          <Section title="Estado atual" id="estado">
            <ul role="list" className={screen.rows}>
              <li className={screen.row}>
                <span className={screen.rowIcon}>
                  <StatusGlyph state="skipped" label="não instalada" />
                </span>
                <span className={screen.rowTitle}>Estrutura de especificação</span>
                <Tag>não instalada</Tag>
              </li>
              <li className={screen.row}>
                <span className={screen.rowIcon}>
                  <Glyph name="layers" size={18} />
                </span>
                <span>
                  <span className={screen.rowTitle}>Harness existente</span>
                  <span className={screen.rowMeta}> {existing} item(ns): 1 agente, 1 regra</span>
                </span>
                <span />
              </li>
              {discovery.warnings.map((w) => (
                <li key={w.code} className={screen.row}>
                  <span className={screen.rowIcon}>
                    <StatusGlyph state="warning" />
                  </span>
                  <span>
                    <span className={screen.rowTitle}>Não é um repositório git</span>
                    <span className={screen.rowMeta}> — a reversão usará o backup da aplicação.</span>
                  </span>
                  <span />
                </li>
              ))}
            </ul>
          </Section>
        </>
      ) : null}
    </>
  );
}
