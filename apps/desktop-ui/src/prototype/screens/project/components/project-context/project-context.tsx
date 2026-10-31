import { ParticipantsSection } from '@/prototype/components/participants-section';
import { SummarySection } from '@/prototype/components/summary-section';
import { demoProject } from '@/prototype/data/demo-project';
import { discovery } from '@/prototype/data/fixtures';
import type { SummaryFact } from '@/prototype/types/summary';
import type { Participant } from '@/prototype/types/participant';

const stack = discovery.stacks[0];

const FACTS: SummaryFact[] = [
  ['Pasta', demoProject.path],
  ['Stack', stack ? `${stack.name} ${stack.version ?? ''}` : '—'],
  ['Git', discovery.git.isRepository ? 'Repositório' : 'Não é repositório'],
  ['Ambiente de IA', 'Claude Code']
];

const IDLE_ENGINE: Participant = {
  id: 'engine',
  name: 'Motor Mariliz',
  state: 'not_started',
  activity: null,
  note: 'Executa localmente. Os agentes só aparecem aqui quando houver eventos reais deles.'
};

export function ProjectContext() {
  return (
    <>
      <SummarySection facts={FACTS} />
      <ParticipantsSection participants={[IDLE_ENGINE]} />
    </>
  );
}
