import { Button } from '@mariliz/ui-kit';

import { Glyph } from '@/shared/components/ui';

import { Notice } from '@/prototype/components/notice';
import { useCopyPrompt } from '@/prototype/hooks/use-copy-prompt';

import type { ProposalNoticeProps } from './types';

export function ProposalNotice({ demoPlanning }: ProposalNoticeProps) {
  const { copied, copy } = useCopyPrompt();

  return (
    <Notice icon={<Glyph name="spark" size={16} />}>
      <p>
        <strong>Plano pelas regras-base</strong>
      </p>
      <p>
        O Harness Engineer ainda não gerou uma proposta. Ele roda no seu ambiente de IA: peça a proposta lá e planeje de
        novo.
      </p>
      {demoPlanning ? (
        <p>Contrato proposto (demonstração): o painel ao lado mostra como o planejamento assistido apareceria.</p>
      ) : null}
      <p>
        <Button variant="outlined" color="inherit" onClick={() => void copy()}>
          {copied ? 'Pedido copiado' : 'Copiar pedido para o Harness Engineer'}
        </Button>
      </p>
    </Notice>
  );
}
