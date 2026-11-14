import { z } from 'zod';

const entrySchema = z.object({
  atMs: z.number(),
  source: z.enum(['motor', 'host']),
  message: z.string()
});

export type DiagnosticLogEntry = z.infer<typeof entrySchema>;

export async function readDiagnosticLog(): Promise<DiagnosticLogEntry[]> {
  const { invoke } = await import('@tauri-apps/api/core');
  return z.array(entrySchema).parse(await invoke('diagnostic_log'));
}
