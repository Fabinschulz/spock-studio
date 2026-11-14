import { z } from 'zod';

/** O caminho do executável não vem do motor. `version` é nulo quando a ferramenta está instalada mas não disse a versão. */
export const aiToolSchema = z.object({
  id: z.string(),
  name: z.string(),
  binary: z.string(),
  installed: z.boolean(),
  version: z.string().nullable(),
  harnessSupport: z.boolean()
});

export const aiToolListSchema = z.object({ tools: z.array(aiToolSchema) });

export type AiTool = z.infer<typeof aiToolSchema>;
export type AiToolList = z.infer<typeof aiToolListSchema>;
