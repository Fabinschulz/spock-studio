import type { Tone } from '@/shared/components';
import type { HarnessStatus } from '@/shared/schema';

type ComponentState = HarnessStatus['components'][number]['state'];

/** Como a interface nomeia o estado dos arquivos de um componente, na tela de estado e no catálogo. */
export const HARNESS_STATE: Record<ComponentState, { label: string; tone: Tone }> = {
  intact: { label: 'Íntegro', tone: 'success' },
  kept: { label: 'Sua versão, mantida', tone: 'neutral' },
  modified: { label: 'Modificado à mão', tone: 'warning' },
  outOfScope: { label: 'Fora do escopo gerenciado', tone: 'danger' },
  missing: { label: 'Arquivo ausente', tone: 'danger' }
};
