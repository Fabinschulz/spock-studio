import type { CloudConnection, CloudLink } from '@/shared/schema';

/**
 * As conexões vinculadas ao projeto. `null` quando uma das leituras falhou: "não deu para ler" não pode virar "nada
 * vinculado", senão a tela manda vincular uma conexão que já está vinculada.
 */
export function linkedConnections(
  links: CloudLink[] | null,
  connections: CloudConnection[] | null
): CloudConnection[] | null {
  if (links === null || connections === null) return null;
  return links
    .map((link) => connections.find((connection) => connection.id === link.connectionId))
    .filter((connection) => connection !== undefined);
}

export const LINKED_UNREADABLE =
  'Não foi possível ler as conexões cloud vinculadas ao projeto. Use "Tentar de novo" no topo da página ou reabra a tela.';
