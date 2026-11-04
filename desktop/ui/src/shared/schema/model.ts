import { z } from 'zod';

export const modelStatusSchema = z.object({
  configured: z.boolean(),
  provider: z.string()
});

export type ModelStatus = z.infer<typeof modelStatusSchema>;
