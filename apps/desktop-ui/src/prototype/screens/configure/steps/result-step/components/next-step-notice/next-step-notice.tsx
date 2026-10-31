import { Button } from '@synthra.io/ui-kit';

import { Glyph } from '@/shared/components/ui';

import { Notice } from '@/prototype/components/notice';
import { useCopyPrompt } from '@/prototype/hooks/use-copy-prompt';

export function NextStepNotice() {
  const { copied, copy } = useCopyPrompt();

  return (
    <Notice icon={<Glyph name="spark" size={16} />} warn>
      <p>
        <strong>Próximo passo: peça a proposta ao Harness Engineer</strong>
      </p>
      <p>
        A configuração aplicada usa as regras-base. Peça ao agente, no seu ambiente de IA, uma proposta específica para
        este projeto.
      </p>
      <p>
        <Button variant="outlined" color="inherit" onClick={() => void copy()}>
          {copied ? 'Pedido copiado' : 'Copiar pedido'}
        </Button>
      </p>
    </Notice>
  );
}
