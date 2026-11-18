import type { SelectedProject } from '@/features/project-selection';
import { InlineNotice, PageSection, Radio, ResourceView } from '@/shared/components';
import { useAsyncResource, useCopyToClipboard } from '@/shared/hooks';
import type { IdeConnection } from '@/shared/schema';
import { Button } from '@mariliz/ui-kit';
import { useCallback, useState } from 'react';

import { ideSetups, scopedServerName } from '../domain/ide-setup';
import type { AgentApi } from '../service/agent-api';
import styles from './ide-connection-section.module.scss';

export function IdeConnectionSection({ api, project = null }: { api: AgentApi; project?: SelectedProject | null }) {
  const connection = useAsyncResource(useCallback(() => api.ideConnection(), [api]));
  return (
    <PageSection
      title="O Spock dentro da sua ferramenta"
      description="Ligue o Spock uma vez e a sua ferramenta de IA passa a consultá-lo enquanto trabalha, sem você colar contexto. Ela só lê, e só o projeto escolhido aqui."
    >
      <ResourceView
        resource={connection}
        loading="Preparando o passo a passo…"
        fallbackError="Não foi possível montar o passo a passo."
      >
        {(value) => {
          const command = value.launcher ?? value.command;
          if (!command)
            return (
              <InlineNotice tone="warning" title="Passo a passo indisponível">
                O motor não conseguiu descobrir onde está instalado. Reinstale o Spock e volte a esta tela.
              </InlineNotice>
            );
          // A conexão sempre vale para um projeto só: sem --project o servidor não sobe e nenhum projeto vai para a IDE.
          if (!project || !value.projectArgument)
            return (
              <InlineNotice tone="info" title="Abra um projeto para ligar a sua ferramenta">
                Cada conexão lê um projeto só. Abra a pasta do projeto no Spock e volte a esta tela para copiar o passo.
              </InlineNotice>
            );
          return (
            <>
              {!value.launcher && (
                <InlineNotice tone="info" title="Comando spock não encontrado">
                  O passo abaixo usa o caminho do motor nesta máquina. Instale ou atualize o Spock Studio para usar o
                  comando spock, que vale em qualquer máquina e nos arquivos do projeto.
                </InlineNotice>
              )}
              <Setup
                connection={value}
                command={command}
                projectArgument={value.projectArgument}
                project={project}
                launcher={Boolean(value.launcher)}
              />
            </>
          );
        }}
      </ResourceView>
    </PageSection>
  );
}

function Setup({
  connection,
  command,
  projectArgument,
  project,
  launcher
}: {
  connection: IdeConnection;
  command: string;
  projectArgument: string;
  project: SelectedProject;
  launcher: boolean;
}) {
  const serverName = scopedServerName(project);
  const setups = ideSetups(command, [...connection.args, projectArgument, project.projectId], serverName);
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
      {launcher && setup.id === 'claude-code' && (
        <p className={styles.how}>
          Com o Harness do Spock aplicado neste projeto, o .mcp.json dele já liga o servidor “spock” no Claude Code e
          este passo não é necessário.
        </p>
      )}
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
        Depois, reinicie {setup.name}. Ela passa a mostrar o servidor “{serverName}” com estas ferramentas, que só
        enxergam {project.displayName}:
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
