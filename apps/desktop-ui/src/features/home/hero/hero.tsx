import { Logo } from '@/shared/components/ui';
import { app } from '@/shared/config/app';

import styles from './hero.module.scss';

export function Hero() {
  return (
    <header className={styles.hero}>
      <div className={styles.brand}>
        <Logo />
        <div>
          <p className={styles.name}>{app.name}</p>
          <p className={styles.tagline}>{app.tagline}</p>
        </div>
      </div>

      <div className={styles.intro}>
        <h1 className={styles.title}>Como podemos ajudar?</h1>
        <p className={styles.text}>
          Escolha uma atividade para começar. Vamos configurar o ambiente conforme as necessidades do seu projeto.
        </p>
      </div>
    </header>
  );
}
