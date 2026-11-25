import type { Tone } from '@/shared/components';
import type { ModelConnection, ProviderConnection } from '@/shared/schema';

const STATE: Record<string, { label: string; tone: Tone }> = {
  none: { label: 'Sem chave', tone: 'neutral' },
  untested: { label: 'Ainda não conferida', tone: 'neutral' },
  connected: { label: 'Conectado', tone: 'success' },
  error: { label: 'Com erro', tone: 'danger' }
};

export function describeProviderState(status: string): { label: string; tone: Tone } {
  return STATE[status] ?? { label: status, tone: 'neutral' };
}

/** Onde a chave está, sem rodeio: no cofre do sistema ou em arquivo, que é o que ainda acontece onde não há cofre. */
export function protectionText(protection: string | null): string | null {
  if (protection === 'vault') return 'Guardada com a proteção do sistema operacional.';
  if (protection === 'file')
    return 'Guardada em arquivo neste computador, sem a proteção do sistema: não há cofre disponível aqui. A tela Conexões › Serviços diz o que falta.';
  return null;
}

export interface PurposeOption {
  value: string;
  label: string;
}

/** Os modelos de todo provedor com chave. O do provedor padrão que vale sem escolha leva "(padrão)" e é o que "volta ao padrão". */
export function purposeModelOptions(connection: ModelConnection): PurposeOption[] {
  return connection.providers
    .filter((provider) => provider.configured)
    .flatMap((provider) =>
      provider.models.map((model) => ({
        value: model.id,
        label: `${model.name} (${provider.name})${isDefaultModel(provider, model.id) ? ' (padrão)' : ''}`
      }))
    );
}

export function defaultModelOf(connection: ModelConnection): string | null {
  return connection.providers.find((provider) => provider.isDefault)?.model ?? null;
}

function isDefaultModel(provider: ProviderConnection, model: string): boolean {
  return provider.isDefault && provider.model === model;
}
