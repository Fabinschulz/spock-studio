/**
 * O texto que a pessoa escreveu na entrada de pedidos viaja no estado da navegação até a tela que vai tratá-lo: não
 * entra na URL nem fica guardado, e a tela de destino continua sendo quem decide o que fazer com ele.
 */
export interface RequestHandoff {
  requestText: string;
}

export function handoffState(text: string): RequestHandoff {
  return { requestText: text.trim() };
}

/** `null` quando a tela foi aberta sem pedido (pelo menu, por um link) ou o estado não é o esperado. */
export function readHandoff(state: unknown): string | null {
  if (typeof state !== 'object' || state === null || !('requestText' in state)) return null;
  const { requestText } = state;
  return typeof requestText === 'string' && requestText.trim() !== '' ? requestText.trim() : null;
}
