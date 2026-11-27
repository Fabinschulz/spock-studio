import { errorMessage } from '@/shared/api';
import type { BootRecord } from '@/shared/schema';
import { useCallback, useRef, useState } from 'react';

import type { BootApi, BootRequestInput } from '../service/boot-api';

export type BootState =
  | { status: 'idle' }
  | { status: 'running'; label: string }
  | { status: 'ready'; record: BootRecord; text: string }
  | { status: 'failed'; message: string };

/**
 * Uma execução do Boot de cada vez. `request` e `project` devolvem o registro, ou nulo quando o motor não respondeu: quem
 * chama decide o que fazer (a Home volta ao caminho antigo, direto para a tela). Um pedido que chega com outro em andamento
 * é descartado (também nulo): `isRunning` diz quando é esse o caso, porque a resposta do primeiro é a que vale.
 */
export function useBoot(api: BootApi | null) {
  const [state, setState] = useState<BootState>({ status: 'idle' });
  // Um duplo Enter chega antes de o estado mudar: o estado sozinho não basta para barrar o segundo pedido.
  const inFlight = useRef(false);

  const run = useCallback(
    async (label: string, text: string, call: (api: BootApi) => Promise<BootRecord>) => {
      if (!api || inFlight.current) return null;
      inFlight.current = true;
      setState({ status: 'running', label });
      try {
        const record = await call(api);
        setState({ status: 'ready', record, text });
        return record;
      } catch (cause) {
        setState({ status: 'failed', message: errorMessage(cause, 'O Spock não conseguiu preparar este pedido.') });
        return null;
      } finally {
        inFlight.current = false;
      }
    },
    [api]
  );

  const request = useCallback(
    (input: BootRequestInput) => run('Entendendo o pedido…', input.text ?? '', (boot) => boot.start(input)),
    [run]
  );

  const project = useCallback(
    (projectId: string) => run('Preparando o projeto…', '', (boot) => boot.project(projectId)),
    [run]
  );

  const dismiss = useCallback(() => setState({ status: 'idle' }), []);

  const isRunning = useCallback(() => inFlight.current, []);

  return { state, request, project, dismiss, isRunning, available: api !== null };
}
