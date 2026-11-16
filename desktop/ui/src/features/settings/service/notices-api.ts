import { z } from 'zod';

const noticesSchema = z.object({
  text: z.string(),
  components: z.number(),
  complete: z.boolean()
});

export type ThirdPartyNotices = z.infer<typeof noticesSchema>;

/** Os avisos de terceiros que o instalador leva em `notices/` (D-07, docs/compliance). */
export async function readThirdPartyNotices(): Promise<ThirdPartyNotices> {
  const { invoke } = await import('@tauri-apps/api/core');
  return noticesSchema.parse(await invoke('third_party_notices'));
}
