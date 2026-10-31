import type { RunResult } from '@/shared/schema';

export function summarizeResult(result: RunResult): string {
  const files = result.files;
  if (!files) return 'sem arquivos alterados';

  const parts = (
    [
      [files.created.length, 'criado'],
      [files.updated.length, 'atualizado'],
      [files.removed.length, 'removido']
    ] as const
  )
    .filter(([count]) => count > 0)
    .map(([count, word]) => `${count} arquivo${count > 1 ? 's' : ''} ${word}${count > 1 ? 's' : ''}`);

  return parts.length > 0 ? parts.join(', ') : 'nenhum arquivo alterado';
}
