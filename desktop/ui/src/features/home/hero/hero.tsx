import styles from './hero.module.scss';

export function Hero() {
  return (
    <header className={styles.hero}>
      <div className={styles.main}>
        <h1 className={styles.title}>
          Como podemos <span className={styles.accent}>ajudar?</span>
        </h1>
        <p className={styles.text}>
          Escolha uma atividade para começar. Vamos configurar o ambiente conforme as necessidades do seu projeto.
        </p>
      </div>
    </header>
  );
}
