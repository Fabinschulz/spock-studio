import styles from './model.module.scss';

const CONFIGURED_ITEMS = [
  'Agentes: Harness Engineer, revisor de código e verificador',
  'Skill: contrato de evidência',
  'Hook: política de segurança antes de escritas e comandos',
  'Regras gerais, da stack e dos comandos do repositório',
  'Evals: verificações da configuração aplicada',
  'Tools e MCPs: nenhum é instalado nesta jornada'
];

export function ConfiguredItemsSection() {
  return (
    <section aria-labelledby="configurado-titulo" className={styles.section}>
      <h3 id="configurado-titulo" className={styles.title}>
        O que será configurado
      </h3>
      <p className={styles.note}>
        Nada é gravado antes da revisão: a configuração só é aplicada depois da sua aprovação.
      </p>
      <ul role="list" className={styles.rows}>
        {CONFIGURED_ITEMS.map((item) => (
          <li key={item}>{item}</li>
        ))}
      </ul>
    </section>
  );
}
