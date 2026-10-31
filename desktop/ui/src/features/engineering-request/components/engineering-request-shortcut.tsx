import { InlineNotice } from '@/shared/components/ui';
import { pathTo } from '@/shared/routing';
import { Button } from '@synthra.io/ui-kit';
import { useNavigate } from 'react-router';

import styles from './engineering-request-shortcut.module.scss';

/** Atalho na jornada Configurar projeto — não substitui o fluxo de harness. */
export function EngineeringRequestShortcut() {
  const navigate = useNavigate();

  return (
    <InlineNotice tone="info" className={styles.notice}>
      <p className={styles.text}>
        Precisa analisar objetivo, evidências ou impacto sem alterar o Harness?{' '}
        <Button type="button" variant="text" size="small" onClick={() => navigate(pathTo('engineeringRequest'))}>
          Abrir pedido de engenharia
        </Button>
      </p>
    </InlineNotice>
  );
}
