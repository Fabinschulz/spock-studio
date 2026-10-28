import { app } from '@/shared/config/app';

import styles from './hero.module.scss';

export function Hero() {
  return (
    <header className={styles.hero}>
      <div className={styles.layout}>
        <div className={styles.main}>
          <p className={styles.brand}>
            <strong className={styles.name}>{app.name}</strong>
            <span className={styles.tagline}>{app.tagline}</span>
          </p>

          <div className={styles.intro}>
            <h1 className={styles.title}>
              Como podemos <span className={styles.accent}>ajudar?</span>
            </h1>
            <p className={styles.text}>
              Escolha uma atividade para começar. Vamos configurar o ambiente conforme as necessidades do seu projeto.
            </p>
          </div>
        </div>

        {/* Ornamento da marca: o texto é real, os chevrons são só desenho (escondidos do leitor de tela). */}
        <div className={styles.pitch}>
          <p className={styles.pitchText}>
            Mais que ferramentas,
            <br />
            uma engenharia <span className={styles.accent}>inteligente.</span>
          </p>
          <svg className={styles.chevrons} viewBox="0 0 96 64" fill="none" aria-hidden="true" focusable="false">
            <path d="M8 6 38 32 8 58" />
            <path d="M34 6 64 32 34 58" />
            <path d="M60 6 90 32 60 58" />
          </svg>
        </div>
      </div>
    </header>
  );
}
