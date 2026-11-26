import { EmptyState, PageSection } from '@/shared/components';
import { pathTo } from '@/shared/routing';
import { Button } from '@mariliz/ui-kit';
import { useNavigate } from 'react-router';

/** Sem ferramenta não há o que acompanhar: a tela diz o que aparece depois de vincular, e a ação é uma só. */
export function NoSourcesState() {
  const navigate = useNavigate();

  return (
    <PageSection title="Nenhuma ferramenta vinculada">
      <EmptyState>
        Sem uma ferramenta de tarefas ou de código vinculada, não há andamento para acompanhar. Depois de vincular e
        sincronizar, esta tela mostra as atividades, a previsão de entrega, o fluxo, os marcos e os riscos detectados
        por regra.
      </EmptyState>
      <div>
        <Button type="button" variant="contained" onClick={() => navigate(pathTo('integrations'))}>
          Vincular uma ferramenta
        </Button>
      </div>
    </PageSection>
  );
}
