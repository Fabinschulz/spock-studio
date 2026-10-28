import styles from './hero.module.scss';

export function Hero() {
  return (
    <header className={styles.hero}>
      <div className={styles.layout}>
        <div className={styles.main}>
          <p className={styles.welcome}>Bem-vindo(a)!</p>
          <h1 className={styles.title}>
            Como podemos <span className={styles.accent}>ajudar?</span>
          </h1>
          <p className={styles.text}>
            Escolha uma atividade para começar. Vamos configurar o ambiente conforme as necessidades do seu projeto.
          </p>
        </div>

        {/* Ornamento da marca: o texto é real, os chevrons são só desenho (escondidos do leitor de tela). */}
        <div className={styles.pitch}>
          <p className={styles.pitchText}>
            Mais que ferramentas,
            <br />
            uma engenharia <span className={styles.accent}>inteligente.</span>
          </p>
          <svg className={styles.chevrons} viewBox="0 0 120 96" fill="none" aria-hidden="true" focusable="false">
            <path d="M10 8 52 48 10 88" />
            <path d="M42 8 84 48 42 88" />
            <path d="M74 8 116 48 74 88" />
          </svg>
        </div>
      </div>
    </header>
  );
}
