import styles from './hero.module.scss';

export function Hero() {
  return (
    <header className={styles.hero}>
      <div className={styles.main}>
        <h1 className={styles.title}>
          Olá! O que você deseja <span className={styles.accent}>realizar hoje?</span>
        </h1>
        <p className={styles.text}>
          Descreva o que precisa ou escolha uma atividade. Nada muda no seu projeto sem a sua aprovação.
        </p>
      </div>
    </header>
  );
}
