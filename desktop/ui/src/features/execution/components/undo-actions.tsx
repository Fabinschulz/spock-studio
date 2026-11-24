import { ConfirmGate, ErrorState, InlineNotice, LoadingState, PageSection } from '@/shared/components';
import { pathTo } from '@/shared/routing';
import { Button } from '@mariliz/ui-kit';
import { Link } from 'react-router';

import type { Undo } from '../hooks/use-undo';

export function UndoActions({ undo, onRevert }: { undo: Undo; onRevert: () => void }) {
  switch (undo.status) {
    case 'none':
      return null;
    case 'reverting':
      return <LoadingState>Desfazendo o que a ação gravou…</LoadingState>;
    case 'reverted':
      return (
        <InlineNotice tone="info" title="Desfeito">
          O arquivo que a ação aprovada gravou saiu do projeto.
        </InlineNotice>
      );
    case 'partial':
      return (
        <InlineNotice tone="warning" title="Nem tudo foi desfeito">
          Um arquivo mudou depois da ação e ficou como está, para não perder o que alguém escreveu. Veja o detalhe em{' '}
          <Link to={pathTo('runs')}>Execuções</Link>.
        </InlineNotice>
      );
    case 'failed':
      return <ErrorState>{undo.message}</ErrorState>;
    case 'available':
      return (
        <PageSection title="Desfazer">
          <ConfirmGate
            label="Desfazer o que a ação gravou"
            confirmLabel="Desfazer"
            message="O arquivo que a ação aprovada gravou sai do projeto. Se alguém o mudou depois, ele fica como está."
            onConfirm={onRevert}
          >
            {(ask) => (
              <Button variant="outlined" onClick={ask}>
                Desfazer o que a ação gravou
              </Button>
            )}
          </ConfirmGate>
        </PageSection>
      );
  }
}
