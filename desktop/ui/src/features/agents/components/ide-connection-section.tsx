import { InlineNotice, PageSection, Radio, ResourceView } from '@/shared/components';
import { useAsyncResource, useCopyToClipboard } from '@/shared/hooks';
import type { IdeConnection } from '@/shared/schema';
import { Button } from '@mariliz/ui-kit';
import { useCallback, useState } from 'react';

import { ideSetups } from '../domain/ide-setup';
import type { AgentApi } from '../service/agent-api';
import styles from './ide-connection-section.module.scss';

export function IdeConnectionSection({ api }: { api: AgentApi }) {
  const connection = useAsyncResource(useCallback(() => api.ideConnection(), [api]));
  return (
    <PageSection
      title="O Spock dentro da sua ferramenta"
      description="Ligue o Spock uma vez e a sua ferramenta de IA passa a consultá-lo enquanto trabalha, sem você colar contexto. Ela só lê, e só os projetos abertos aqui."
    >
      <ResourceView
        resource={connection}
        loading="Preparando o passo a passo…"
        fallbackError="Não foi possível montar o passo a passo."
      >
        {(value) =>
          value.command ? (
            <Setup connection={value} command={value.command} />
          ) : (
            <InlineNotice tone="warning" title="Passo a passo indisponível">
              O motor não conseguiu descobrir onde está instalado. Reinstale o Spock e volte a esta tela.
            </InlineNotice>
          )
        }
      </ResourceView>
    </PageSection>
  );
}

function Setup({ connection, command }: { connection: IdeConnection; command: string }) {
  const setups = ideSetups(command, connection.args);
  const [chosen, setChosen] = useState(setups[0]!.id);
  const setup = setups.find((item) => item.id === chosen) ?? setups[0]!;
  const { state, copy } = useCopyToClipboard({ resetAfterMs: 3000 });

  return (
    <div className={styles.setup}>
      <fieldset className={styles.choice}>
        <legend>Sua ferramenta</legend>
        {setups.map((item) => (
          <Radio
            key={item.id}
            name="ide"
            value={item.id}
            checked={item.id === chosen}
            onChange={() => setChosen(item.id)}
            label={item.name}
          />
        ))}
      </fieldset>

      <p className={styles.how}>{setup.how}</p>
      <pre className={styles.snippet} aria-label={`Configuração para ${setup.name}`}>
        {setup.snippet}
      </pre>
      <div className={styles.actions}>
        <Button variant="outlined" color="inherit" onClick={() => void copy(setup.snippet)}>
          {state === 'copied' ? 'Copiado' : 'Copiar'}
        </Button>
        <span role="status" className={styles.how}>
          {state === 'failed' ? 'Não foi possível copiar. Selecione o texto acima e copie com Ctrl+C.' : ''}
        </span>
      </div>
      <p className={styles.how}>
        Depois, reinicie {setup.name}. Ela passa a mostrar o servidor “spock” com estas ferramentas:
      </p>
      <ul className={styles.tools}>
        {connection.tools.map((tool) => (
          <li key={tool.name}>
            <strong>{tool.title}</strong>: {tool.description}
          </li>
        ))}
      </ul>
    </div>
  );
}
