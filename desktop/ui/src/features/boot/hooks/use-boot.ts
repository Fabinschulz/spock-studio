import { errorMessage } from '@/shared/api';
import type { BootRecord } from '@/shared/schema';
import { useCallback, useRef, useState } from 'react';

import type { BootApi, BootRequestInput } from '../service/boot-api';

export type BootState =
  | { status: 'idle' }
  | { status: 'running'; label: string }
  | { status: 'ready'; record: BootRecord; text: string }
  | { status: 'failed'; message: string };

const REQUEST_FAILED = 'O Spock não conseguiu preparar este pedido.';

interface RunOptions {
  label: string;
  fallback?: string;
  /** O texto que a Home leva adiante se a pessoa continuar na tela sugerida; o registro de um Boot antigo já o traz. */
  text?: string | ((record: BootRecord) => string);
}

/**
 * Uma execução do Boot de cada vez. `request`, `project`, `resume` e `open` devolvem o registro, ou nulo quando o motor não
 * respondeu: quem chama decide o que fazer (a Home volta ao caminho antigo, direto para a tela). Um pedido que chega com
 * outro em andamento é descartado (também nulo): `isRunning` diz quando é esse o caso, porque a resposta do primeiro é a
 * que vale. O resultado de uma retomada e o de um pedido novo caem no mesmo estado, e por isso no mesmo cartão.
 */
export function useBoot(api: BootApi | null) {
  const [state, setState] = useState<BootState>({ status: 'idle' });
  // Um duplo Enter chega antes de o estado mudar: o estado sozinho não basta para barrar o segundo pedido.
  const inFlight = useRef(false);

  const run = useCallback(
    async (
      { label, fallback = REQUEST_FAILED, text = '' }: RunOptions,
      call: (api: BootApi) => Promise<BootRecord>
    ) => {
      if (!api || inFlight.current) return null;
      inFlight.current = true;
      setState({ status: 'running', label });
      try {
        const record = await call(api);
        setState({ status: 'ready', record, text: typeof text === 'function' ? text(record) : text });
        return record;
      } catch (cause) {
        setState({ status: 'failed', message: errorMessage(cause, fallback) });
        return null;
      } finally {
        inFlight.current = false;
      }
    },
    [api]
  );

  const request = useCallback(
    (input: BootRequestInput) =>
      run({ label: 'Entendendo o pedido…', text: input.text ?? '' }, (boot) => boot.start(input)),
    [run]
  );

  const project = useCallback(
    (projectId: string) => run({ label: 'Preparando o projeto…' }, (boot) => boot.project(projectId)),
    [run]
  );

  const resume = useCallback(
    (projectId: string, bootId: string) =>
      run(
        {
          label: 'Retomando o pedido…',
          fallback: 'O Spock não conseguiu retomar este pedido.',
          text: (record) => record.request ?? ''
        },
        (boot) => boot.resume(projectId, bootId)
      ),
    [run]
  );

  const open = useCallback(
    (projectId: string, bootId: string) =>
      run(
        {
          label: 'Abrindo o pedido…',
          fallback: 'O Spock não conseguiu abrir este pedido.',
          text: (record) => record.request ?? ''
        },
        (boot) => boot.detail(projectId, bootId)
      ),
    [run]
  );

  const dismiss = useCallback(() => setState({ status: 'idle' }), []);

  const isRunning = useCallback(() => inFlight.current, []);

  return { state, request, project, resume, open, dismiss, isRunning, available: api !== null };
}
