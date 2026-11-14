import {
  ideaArchitectureView,
  ideaBacklogView,
  ideaSpecificationView,
  type IdeaArtifact,
  type IdeaStage
} from '@/shared/schema';
import type { ReactNode } from 'react';

import styles from '../idea-page.module.scss';

export function ArtifactView({ stage, artifact }: { stage: IdeaStage; artifact: IdeaArtifact }) {
  if (stage === 'specification') return <Specification artifact={artifact} />;
  if (stage === 'architecture') return <Architecture artifact={artifact} />;
  return <Backlog artifact={artifact} />;
}

function Specification({ artifact }: { artifact: IdeaArtifact }) {
  const spec = ideaSpecificationView.parse(artifact);
  return (
    <div className={styles.artifact}>
      <h3 className={styles.heading}>{spec.title}</h3>
      <p className={styles.text}>{spec.problem}</p>
      <List title="Usuários" items={spec.users} />
      <List title="Objetivos" items={spec.goals} />
      <List
        title="Requisitos"
        items={spec.requirements.map((item) => (
          <>
            <strong>{item.id}</strong> ({item.priority}): {item.text}
          </>
        ))}
      />
      <List
        title="Requisitos não funcionais"
        items={spec.nonFunctional.map((item) => (
          <>
            <strong>{item.id}</strong>: {item.text}
          </>
        ))}
      />
      <List title="Fora do escopo" items={spec.outOfScope} />
      <List title="Perguntas em aberto" items={spec.openQuestions} />
    </div>
  );
}

function Architecture({ artifact }: { artifact: IdeaArtifact }) {
  const architecture = ideaArchitectureView.parse(artifact);
  return (
    <div className={styles.artifact}>
      <p className={styles.text}>
        <strong>{architecture.style}.</strong> {architecture.summary}
      </p>
      <List
        title="Componentes"
        items={architecture.components.map((item) => (
          <>
            <strong>
              {item.id} {item.name}
            </strong>
            : {item.responsibility} Atende a {item.requirements.join(', ')}.
          </>
        ))}
      />
      <List
        title="Stack"
        items={architecture.stack.map((item) => (
          <>
            <strong>{item.area}</strong>: {item.choice}. {item.reason}
          </>
        ))}
      />
      <List title="Riscos" items={architecture.risks.map((item) => `${item.text} Mitigação: ${item.mitigation}`)} />
      <List
        title="Decisões"
        items={architecture.decisions.map((item) => (
          <>
            <strong>{item.title}</strong>: {item.choice}. {item.reason}
          </>
        ))}
      />
    </div>
  );
}

function Backlog({ artifact }: { artifact: IdeaArtifact }) {
  const backlog = ideaBacklogView.parse(artifact);
  return (
    <div className={styles.artifact}>
      {backlog.epics.map((epic) => (
        <List
          key={epic.id}
          title={`${epic.id} ${epic.title}`}
          items={epic.stories.map((story) => (
            <>
              <strong>{story.id}</strong> {story.title} ({story.estimate}, {story.requirements.join(', ')})
              <ul className={styles.criteria}>
                {story.acceptance.map((criterion) => (
                  <li key={criterion}>{criterion}</li>
                ))}
              </ul>
            </>
          ))}
        />
      ))}
    </div>
  );
}

function List({ title, items }: { title: string; items: ReactNode[] }) {
  if (items.length === 0) return null;
  return (
    <>
      <h4 className={styles.listTitle}>{title}</h4>
      <ul className={styles.list}>
        {items.map((item, index) => (
          // Os itens vêm do modelo e podem repetir texto; a posição é estável enquanto a etapa não muda.
          <li key={index}>{item}</li>
        ))}
      </ul>
    </>
  );
}
