import { useCopyToClipboard, useReloadable } from '@/shared/hooks';
import type { IacBlueprint } from '@/shared/schema';
import { Button, TextField } from '@mariliz/ui-kit';
import { useCallback } from 'react';

import styles from '../infrastructure-page.module.scss';
import type { IacApi } from '../service/iac-api';

/** Pedido pronto para colar no agente que tem o MCP do Spock; o roteiro completo vem da ferramenta. */
export const sessionAgentPrompt = [
  'Use a ferramenta iac_architecture_prepare do Spock para propor a infraestrutura deste projeto.',
  'Pergunte o que o sistema precisa se eu não tiver dito, confira o desenho com iac_blueprint_check e grave a proposta onde a ferramenta indicar.',
  'Não escreva arquivos .tf e não rode plano nem apply: eu reviso e gero pelo Spock.'
].join('\n');

interface SessionProposalProps {
  api: IacApi;
  projectId: string;
  busy: boolean;
  onUse: (blueprint: IacBlueprint) => void;
}

/**
 * Modo Sessão: sem chave de IA no Spock, o agente que a pessoa já usa propõe o desenho e grava no projeto. O motor só
 * confere com as checagens da tela; usar o desenho preenche o formulário, e gerar continua sendo a revisão do diff.
 */
export function SessionProposal({ api, projectId, busy, onUse }: SessionProposalProps) {
  const load = useCallback(() => api.architectProposal(projectId), [api, projectId]);
  const proposal = useReloadable(load);
  const { state: copyState, copy } = useCopyToClipboard();
  const current = proposal.value;
  const failed = current?.checks.filter((check) => !check.passed) ?? [];

  return (
    <section className={styles.consent} aria-label="Desenho do agente da sessão">
      <p className={styles.rowTitle}>Propor com o seu agente (sem chave de IA no Spock)</p>
      <p className={styles.hint}>
        No Claude Code ou em outro agente com o MCP do Spock, cole o pedido abaixo. O agente monta o desenho, confere
        com o Spock e grava a proposta no projeto; ela aparece aqui para você completar e revisar.
      </p>
      <TextField
        label="Pedido para o agente"
        multiline
        minRows={3}
        fullWidth
        value={sessionAgentPrompt}
        onFocus={(event) => event.target.select()}
        slotProps={{ input: { readOnly: true } }}
      />
      <div className={styles.actions}>
        <Button type="button" variant="outlined" disabled={busy} onClick={() => void copy(sessionAgentPrompt)}>
          {copyState === 'copied' ? 'Copiado' : 'Copiar pedido'}
        </Button>
        <Button type="button" variant="text" disabled={busy} onClick={() => proposal.reload()}>
          Ler a proposta de novo
        </Button>
      </div>
      {proposal.error ? (
        <p className={styles.error} role="alert">
          Não foi possível ler a proposta do agente.
        </p>
      ) : null}
      {current ? (
        <div aria-label="Proposta do agente">
          <p className={styles.hint}>
            Proposta em {current.path}:{' '}
            {current.blueprint === null
              ? 'o arquivo não tem o desenho no formato esperado. Peça ao agente para gravar de novo.'
              : failed.length === 0
                ? 'passa em todas as verificações.'
                : `${failed.length} verificação(ões) falhando; complete ou ajuste no formulário.`}
          </p>
          {current.notes?.services.length ? (
            <ul className={styles.impacts}>
              {current.notes.services.map((service) => (
                <li key={service.name}>
                  <strong>{service.name}</strong>: {service.reason}
                </li>
              ))}
            </ul>
          ) : null}
          {current.notes?.risks.length ? (
            <p className={styles.hint}>Riscos: {current.notes.risks.join(' · ')}</p>
          ) : null}
          {failed.length > 0 && current.blueprint !== null ? (
            <ul className={styles.impacts} aria-label="Verificações da proposta">
              {failed.map((check) => (
                <li key={check.id}>
                  {check.description}
                  {check.detail ? ` (${check.detail})` : ''}
                </li>
              ))}
            </ul>
          ) : null}
          {current.blueprint !== null ? (
            <div className={styles.actions}>
              <Button type="button" disabled={busy} onClick={() => onUse(current.blueprint!)}>
                Usar este desenho
              </Button>
            </div>
          ) : null}
        </div>
      ) : null}
    </section>
  );
}
