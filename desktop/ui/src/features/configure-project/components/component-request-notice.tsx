import { Button } from '@mariliz/ui-kit';

import type { ComponentRequest } from '../domain/component-request';
import styles from '../configure-project-page.module.scss';

const names = (items: ComponentRequest['exclude']) => items.map((item) => item.label).join(', ');

/** Mostra o pedido vindo da tela Projeto enquanto ele ainda não virou plano aplicado, com a opção de desfazê-lo. */
export function ComponentRequestNotice({ request, onClear }: { request: ComponentRequest; onClear: () => void }) {
  return (
    <aside className={styles.requestNotice} aria-label="Pedido da tela Projeto">
      <p>
        {request.exclude.length > 0 ? <>Dispensar: {names(request.exclude)}. </> : null}
        {request.include.length > 0 ? <>Voltar a aplicar: {names(request.include)}. </> : null}O plano grava o pedido em
        mariliz.yaml (components.exclude) e mostra o que sai ou volta; nada muda sem a sua aprovação.
      </p>
      <Button type="button" variant="outlined" size="small" onClick={onClear}>
        Desfazer pedido
      </Button>
    </aside>
  );
}
