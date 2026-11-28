import { Checkbox, TextField } from '@mariliz/ui-kit';

import styles from '../quality-page.module.scss';
import type { GatesInfo, ReviewRequest } from '../service/quality-api';

/**
 * O que vai além da revisão simples, sempre por escolha da pessoa: o modelo recebe o diff (sem as linhas com cara de segredo), e as
 * verificações rodam comandos do próprio projeto neste computador, sem sandbox.
 */
export function ReviewOptionsForm({
  info,
  request,
  onChange,
  disabled
}: {
  info: GatesInfo | null;
  request: ReviewRequest;
  onChange: (next: ReviewRequest) => void;
  disabled: boolean;
}) {
  const modelAvailable = info?.modelAvailable ?? false;
  const gates = info?.gates ?? [];
  const toggleGate = (id: string, checked: boolean) => {
    const next = checked ? [...request.gates, id] : request.gates.filter((item) => item !== id);
    onChange({ ...request, gates: next, gatesApproved: next.length === 0 ? false : request.gatesApproved });
  };

  return (
    <fieldset className={styles.options} disabled={disabled}>
      <legend>Opções da revisão</legend>
      <Checkbox
        label="Incluir a revisão por modelo de IA"
        description={
          modelAvailable
            ? 'Envia o trecho alterado, sem as linhas com cara de segredo, ao provedor de IA configurado. O que o modelo aponta é hipótese e nunca bloqueia a entrega.'
            : 'Configure um provedor de IA em Configurações para usar esta opção.'
        }
        checked={request.useModel}
        disabled={disabled || !modelAvailable}
        onChange={(_, checked) => onChange({ ...request, useModel: checked })}
      />
      <TextField
        label="Critérios de aceite (um por linha)"
        multiline
        minRows={2}
        size="small"
        fullWidth
        value={request.requirements}
        disabled={disabled || !modelAvailable}
        onChange={(event) => onChange({ ...request, requirements: event.target.value })}
        helperText="Opcional. Cada critério só conta como atendido se o modelo apontar a linha do diff que o atende."
      />
      {gates.length > 0 ? (
        <div className={styles.gates} role="group" aria-label="Verificações do projeto">
          <p className={styles.note}>
            Verificações que o próprio projeto define. Elas rodam código do projeto neste computador, sem sandbox.
          </p>
          {gates.map((gate) => (
            <Checkbox
              key={gate.id}
              label={gate.command}
              description={`${gate.origin}${gate.directory === '.' ? '' : `, em ${gate.directory}`}`}
              checked={request.gates.includes(gate.id)}
              disabled={disabled}
              onChange={(_, checked) => toggleGate(gate.id, checked)}
            />
          ))}
          {request.gates.length > 0 ? (
            <Checkbox
              label="Autorizo rodar os comandos marcados neste computador"
              checked={request.gatesApproved}
              disabled={disabled}
              onChange={(_, checked) => onChange({ ...request, gatesApproved: checked })}
            />
          ) : null}
        </div>
      ) : null}
    </fieldset>
  );
}
