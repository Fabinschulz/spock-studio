import type { Tone } from '@/shared/components';
import type { CloudConnection, CloudConnectionStatus, CloudProvider } from '@/shared/schema';

/** O estado da conexão (autenticação), separado do catálogo (provedor disponível) e da verificação (o que cada leitura pode). */
export const CLOUD_STATUS: Record<CloudConnectionStatus, { label: string; tone: Tone }> = {
  connected: { label: 'Conectada', tone: 'success' },
  notAuthenticated: { label: 'Aguardando autenticação', tone: 'warning' },
  expired: { label: 'Credenciais expiradas', tone: 'warning' },
  invalidIdentity: { label: 'Identidade inválida', tone: 'danger' },
  permissionDenied: { label: 'Permissão insuficiente', tone: 'danger' },
  invalidRegion: { label: 'Região inválida', tone: 'danger' },
  unavailable: { label: 'Indisponível', tone: 'neutral' }
};

export const PERMISSION_STATE: Record<CloudConnection['permissions'][number]['state'], { label: string; tone: Tone }> =
  {
    unchecked: { label: 'Não verificada', tone: 'neutral' },
    allowed: { label: 'Permitida', tone: 'success' },
    denied: { label: 'Negada', tone: 'danger' },
    error: { label: 'Sem resposta', tone: 'warning' }
  };

export function capabilityTitle(provider: CloudProvider | undefined, id: string): string {
  return provider?.capabilities.find((capability) => capability.id === id)?.title ?? id;
}

export function identityText(connection: CloudConnection): string {
  const identity = connection.identity;
  if (!identity) return 'Identidade ainda não confirmada.';
  const account = identity.accountName ? `${identity.accountName} (${identity.account})` : identity.account;
  return [`Conta: ${account}`, identity.tenant ? `Tenant: ${identity.tenant}` : null, `Quem: ${identity.principal}`]
    .filter((part): part is string => part !== null)
    .join(' · ');
}

export function permissionSummary(connection: CloudConnection): string {
  const allowed = connection.permissions.filter((permission) => permission.state === 'allowed').length;
  const checked = connection.permissions.filter((permission) => permission.state !== 'unchecked').length;
  if (checked === 0) return 'Permissões ainda não verificadas.';
  return `${allowed} de ${connection.permissions.length} leituras permitidas para esta identidade.`;
}
