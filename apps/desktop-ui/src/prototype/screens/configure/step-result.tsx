import { Button } from '@synthra.io/ui-kit';
import { useState } from 'react';

// Protótipo: reutiliza o texto real do pedido. Em produção entra pela API pública da feature.
// eslint-disable-next-line no-restricted-imports
import { harnessEngineerPrompt } from '@/features/configure-project/domain/harness-engineer-prompt';
import type { EvalResult, RunResult } from '@/shared/schema';

import { Glyph } from '../../ui/glyph';
import { Section, StatusGlyph, Tag, type GlyphState } from '../../ui/primitives';
import screen from '../screen.module.scss';

import { StepHeader } from './step-project';

const statusText: Record<EvalResult['status'], string> = {
  passed: 'aprovado',
  failed: 'reprovado',
  blocked: 'bloqueado',
  not_run: 'não executado'
};
const statusGlyph: Record<EvalResult['status'], GlyphState> = {
  passed: 'done',
  failed: 'failed',
  blocked: 'warning',
  not_run: 'skipped'
};

/** "11 arquivos criados, 2 atualizados": só entra o que o resultado do motor realmente traz. */
function summaryOf(result: RunResult): string {
  const files = result.files;
  if (!files) return 'sem arquivos alterados';
  const parts = (
    [
      [files.created.length, 'criado'],
      [files.updated.length, 'atualizado'],
      [files.removed.length, 'removido']
    ] as const
  )
    .filter(([count]) => count > 0)
    .map(([count, word]) => `${count} arquivo${count > 1 ? 's' : ''} ${word}${count > 1 ? 's' : ''}`);
  return parts.length > 0 ? parts.join(', ') : 'nenhum arquivo alterado';
}

export function ResultStep({ result }: { result: RunResult }) {
  const [copied, setCopied] = useState(false);
  const evals = result.validation?.results ?? [];
  const passed = evals.filter((e) => e.status === 'passed').length;
  const notRun = evals.filter((e) => e.status === 'not_run');
  const created = result.files?.created ?? [];
  const noProposal = result.limitations?.some((l) => l.code === 'HARNESS_ENGINEER_NOT_INVOKED');

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
        title="Configuração aplicada"
        lead="O Harness do projeto foi escrito e validado. Você pode desfazer tudo pelo backup guardado antes da aplicação."
      />

      <div className={screen.notice}>
        <StatusGlyph state="done" label="concluído" />
        <div>
          <p>
            <strong>Concluído</strong> · {summaryOf(result)}
          </p>
          <p>
            {passed} de {evals.length} verificações aprovadas
            {notRun.length > 0
              ? `; ${notRun.length} não executada${notRun.length > 1 ? 's' : ''} nesta validação.`
              : '.'}
          </p>
        </div>
      </div>

      <Section title="Validação" id="validacao" aside={`${passed}/${evals.length} aprovadas`}>
        <ul role="list" className={screen.rows}>
          {evals.map((e) => (
            <li key={e.id}>
              <details className={screen.details}>
                <summary className={screen.evalSummary}>
                  <StatusGlyph state={statusGlyph[e.status]} label={statusText[e.status]} />
                  <span className={screen.rowTitle}>{e.name}</span>
                  {e.kind === 'model' ? <Tag>juiz por modelo</Tag> : null}
                  {e.required ? null : <Tag>opcional</Tag>}
                  <span className={`${screen.rowEnd} ${screen.pushRight}`}>{statusText[e.status]}</span>
                </summary>
                <div className={screen.evalBody}>
                  <p className={screen.rowMeta}>{e.property}</p>
                  <p>{e.criteria}</p>
                  {e.evidence.length > 0 ? (
                    <ul role="list" className={screen.stack}>
                      {e.evidence.map((line) => (
                        <li key={line} className={screen.mono}>
                          {line}
                        </li>
                      ))}
                    </ul>
                  ) : null}
                  {e.reason ? <p className={screen.rowMeta}>{e.reason}</p> : null}
                </div>
              </details>
            </li>
          ))}
        </ul>
      </Section>

      <Section title="Arquivos criados" id="arquivos" aside={`${created.length}`}>
        <details className={screen.details}>
          <summary>Mostrar a lista</summary>
          <ul role="list" className={screen.stack}>
            {created.map((path) => (
              <li key={path} className={screen.mono}>
                {path}
              </li>
            ))}
          </ul>
        </details>
      </Section>

      {noProposal ? (
        <div className={`${screen.notice} ${screen.noticeWarn}`}>
          <Glyph name="spark" size={16} />
          <div>
            <p>
              <strong>Próximo passo: peça a proposta ao Harness Engineer</strong>
            </p>
            <p>
              A configuração aplicada usa as regras-base. Peça ao agente, no seu ambiente de IA, uma proposta específica
              para este projeto.
            </p>
            <p>
              <Button variant="outlined" color="inherit" onClick={() => void copy()}>
                {copied ? 'Pedido copiado' : 'Copiar pedido'}
              </Button>
            </p>
          </div>
        </div>
      ) : null}
    </>
  );
}
