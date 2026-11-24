import { InlineNotice, PageSection } from '@/shared/components';
import { useCopyToClipboard } from '@/shared/hooks';
import type { ProjectDesign } from '@/shared/schema';
import { Button, TextField } from '@mariliz/ui-kit';

import styles from '../idea-page.module.scss';

const IDEA_PLACEHOLDER = '<descreva aqui a ideia: o problema, quem usa e o que precisa acontecer>';

/** Pedido pronto para colar no agente que tem o MCP do Spock; o formato e os critérios vêm da ferramenta. */
export function designAgentPrompt(idea: string): string {
  return [
    'Use a ferramenta spock_project_design_prepare do Spock para transformar a ideia abaixo em especificação, arquitetura e backlog.',
    'Grave onde a ferramenta indicar e confira com spock_project_design_check até passar.',
    'Não crie código nem grave os documentos: eu aprovo cada etapa no Spock.',
    '',
    'A ideia:',
    idea.trim() || IDEA_PLACEHOLDER
  ].join('\n');
}

interface SessionPanelProps {
  idea: string;
  design: ProjectDesign | null;
  busy: boolean;
  onRead: () => void;
}

/**
 * W4-3 no modo Sessão: sem chave de modelo, o agente que a pessoa já usa escreve as três etapas num arquivo do projeto. O
 * Spock confere com as mesmas verificações do modo Chave, e a aprovação e a gravação seguem nesta tela.
 */
export function SessionPanel({ idea, design, busy, onRead }: SessionPanelProps) {
  const { state: copyState, copy } = useCopyToClipboard();
  const prompt = designAgentPrompt(idea);
  const failed = design?.checks.filter((check) => !check.passed) ?? [];

  return (
    <PageSection title="Sem chave: pelo agente da sua sessão">
      <p className={styles.note}>
        Cole este pedido no agente que tem o MCP do Spock (Claude Code, Cursor, Codex). Ele escreve a especificação, a
        arquitetura, com cada escolha de stack pelos critérios do catálogo da Organização, e o backlog. Depois, leia
        aqui o que ele gravou: o Spock confere cada etapa e você aprova antes de gravar.
      </p>
      <TextField
        label="Pedido para o agente"
        multiline
        minRows={4}
        fullWidth
        value={prompt}
        onFocus={(event) => event.target.select()}
        slotProps={{ input: { readOnly: true } }}
      />
      <div className={styles.actions}>
        <Button type="button" variant="outlined" color="inherit" onClick={() => void copy(prompt)}>
          {copyState === 'copied' ? 'Copiado' : 'Copiar pedido'}
        </Button>
        <Button type="button" variant="contained" disabled={busy} onClick={onRead}>
          Ler o que o agente gravou
        </Button>
      </div>
      {design && !design.found ? (
        <InlineNotice tone="info" title="Nada gravado ainda">
          O agente ainda não gravou <span className={styles.mono}>{design.path}</span>.
        </InlineNotice>
      ) : null}
      {design?.found && failed.length > 0 ? (
        <InlineNotice tone="warning" title="O arquivo do agente não serve ainda">
          <ul className={styles.checks} aria-label="Verificações do arquivo">
            {failed.map((check) => (
              <li key={check.id}>{check.label}</li>
            ))}
          </ul>
          Peça ao agente para corrigir com spock_project_design_check e leia de novo.
        </InlineNotice>
      ) : null}
    </PageSection>
  );
}
