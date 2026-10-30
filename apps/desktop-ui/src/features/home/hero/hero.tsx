import banner from '@/assets/hero-banner.webp';
import { HeroArt } from './hero-art';
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

      <div className={styles.banner}>
        <img className={styles.bannerImage} src={banner} alt="" aria-hidden="true" draggable={false} />
        <HeroArt className={styles.bannerArt} />
        <p className={styles.pitch}>
          Mais que ferramentas,
          <br />
          <span className={styles.accent}>uma engenharia inteligente.</span>
        </p>
      </div>
    </header>
  );
}
