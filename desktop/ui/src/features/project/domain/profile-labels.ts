import type { ProfileChange } from '@/shared/schema';

export const CONFIDENCE_LABEL = { high: 'confiança alta', medium: 'confiança média', low: 'confiança baixa' } as const;

const TOOL_KIND_LABEL: Record<string, string> = {
  build: 'Build',
  test: 'Teste',
  lint: 'Lint',
  typecheck: 'Tipos',
  ci: 'Integração contínua',
  'package-manager': 'Pacotes'
};

export function toolKindLabel(kind: string): string {
  return TOOL_KIND_LABEL[kind] ?? kind;
}

/** Uma mudança do histórico como frase: "Next.js entrou (^16.0.1)", ".NET mudou de net8.0 para net10.0". */
export function changeText(change: ProfileChange): string {
  switch (change.kind) {
    case 'created':
      return `Perfil criado com ${change.to ?? 'nenhuma tecnologia'}.`;
    case 'added':
      return `${change.subject} entrou${change.to ? ` (${change.to})` : ''}.`;
    case 'removed':
      return `${change.subject} saiu${change.from ? ` (${change.from})` : ''}.`;
    case 'changed':
      return `${change.subject} mudou de ${change.from ?? '?'} para ${change.to ?? '?'}.`;
  }
}
