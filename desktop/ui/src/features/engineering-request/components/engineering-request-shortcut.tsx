import { Link } from 'react-router';

import { pathTo } from '@/shared/routing';

import styles from './engineering-request-shortcut.module.scss';

/** Uma linha discreta, não um aviso: é uma saída para outro caminho, não algo que a pessoa precise resolver aqui. */
export function EngineeringRequestShortcut() {
  return (
    <p className={styles.text}>
      Só quer analisar objetivo, evidências ou impacto, sem mudar o Harness?{' '}
      <Link to={pathTo('engineeringRequest')}>Abrir pedido de engenharia</Link>
    </p>
  );
}
