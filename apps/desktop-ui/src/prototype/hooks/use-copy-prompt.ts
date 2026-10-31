import { useState } from 'react';

// Protótipo: reutiliza o texto real do pedido; em produção entra pela API pública da feature.
// eslint-disable-next-line no-restricted-imports
import { harnessEngineerPrompt } from '@/features/configure-project/domain/harness-engineer-prompt';

export function useCopyPrompt() {
  const [copied, setCopied] = useState(false);

  const copy = async () => {
    try {
      await navigator.clipboard.writeText(harnessEngineerPrompt);
      setCopied(true);
    } catch {
      setCopied(false);
    }
  };

  return { copied, copy };
}
