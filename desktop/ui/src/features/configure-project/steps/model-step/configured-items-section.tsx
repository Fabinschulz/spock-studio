import { Term } from '@/shared/components/ui';

import styles from './model.module.scss';

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
        <li>
          Três <Term id="agent">agentes</Term>: o engenheiro do Harness, um revisor de código e um verificador.
        </li>
        <li>
          Uma <Term id="skill" /> que ensina os agentes a mostrar as evidências do que afirmam.
        </li>
        <li>
          Um <Term id="hook" /> de segurança que pede permissão antes de gravar arquivos ou rodar comandos.
        </li>
        <li>Regras gerais, das tecnologias e dos comandos do projeto.</li>
        <li>
          <Term id="eval">Evals</Term>, os testes que conferem a configuração depois de aplicada.
        </li>
        <li>
          Nenhuma conexão <Term id="mcp" /> é instalada nesta jornada.
        </li>
      </ul>
    </section>
  );
}
