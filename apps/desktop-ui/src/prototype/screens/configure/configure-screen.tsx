import { Button } from '@synthra.io/ui-kit';
import { useEffect, useMemo, useState } from 'react';
import { useNavigate, useParams, useSearchParams } from 'react-router';

import type { ConflictResolution } from '@/shared/schema';

import {
  applyScriptCurrent,
  applyScriptProposed,
  EXPECTED_APPLY_STEPS,
  planningScriptProposed
} from '../../data/demo-scripts';
import { applyResult, demoProject, plan } from '../../data/fixtures';
import { describeEvent } from '../../domain/describe-event';
import { buildOperation, type Actor } from '../../domain/operation-model';
import { useReplay } from '../../hooks/use-replay';
import { ActivitySection, ParticipantsSection, SummarySection, type Participant } from '../../shell/context-sections';
import { Stage } from '../../shell/stage';

import { JourneyBar } from './journey-bar';
import { nextHint, STEPS, stepIndex, stepPath, type StepSlug } from './journey';
import type { Contract } from './replay-bar';
import { ModelStep } from './step-model';
import { OperationStep } from './step-operation';
import { PlanStep } from './step-plan';
import { ProjectStep, type Analysis } from './step-project';
import { ResultStep } from './step-result';
import { ReviewStep } from './step-review';

const engineIdentity = { id: 'engine', name: 'Motor Mariliz' } as const;
const envNames: Record<string, string> = { 'claude-code': 'Claude Code' };

const defaultApproved = plan.changes.filter((c) => c.action === 'create' || c.action === 'update').map((c) => c.id);
const defaultResolutions = Object.fromEntries(
  plan.changes.filter((c) => c.action === 'conflict').map((c) => [c.id, 'keep' as const])
);

export function ConfigureScreen() {
  const { step } = useParams();
  const [search, setSearch] = useSearchParams();
  const navigate = useNavigate();

  const index = stepIndex(step);
  const slug = STEPS[index]?.slug ?? 'projeto';
  const contract: Contract = search.get('contrato') === 'proposto' ? 'proposed' : 'current';
  const fixedAt = search.get('t') === null ? null : Number(search.get('t'));
  const keepSearch = search.toString() ? `?${search.toString()}` : '';

  // Abrir direto numa etapa (link, captura de tela) parte com as etapas anteriores já resolvidas.
  // `?estado=` fixa um estado intermediário para inspeção: analisando | analisado | gerando-plano.
  const forced = search.get('estado');
  const [analysis, setAnalysis] = useState<Analysis>(
    forced === 'analisando' ? 'loading' : index > 0 || forced === 'analisado' ? 'ready' : 'idle'
  );
  const [env, setEnv] = useState<string | null>(index > 1 || forced === 'gerando-plano' ? 'claude-code' : null);
  const [planning, setPlanning] = useState(forced === 'gerando-plano');
  const [approved, setApproved] = useState<string[]>(defaultApproved);
  const [commands, setCommands] = useState<string[]>([]);
  const [resolutions, setResolutions] = useState<Record<string, ConflictResolution>>(defaultResolutions);

  const script = contract === 'proposed' ? applyScriptProposed : applyScriptCurrent;
  const applyReplay = useReplay(script, { startAt: fixedAt ?? 0 });
  const planReplay = useReplay(planningScriptProposed, { startAt: fixedAt ?? 0 });
  const { restart: restartApply } = applyReplay;
  const { restart: restartPlan } = planReplay;

  useEffect(() => {
    if (fixedAt === null && slug === 'aplicacao') restartApply();
  }, [slug, contract, fixedAt, restartApply]);

  useEffect(() => {
    if (fixedAt === null && slug === 'plano' && contract === 'proposed') restartPlan();
  }, [slug, contract, fixedAt, restartPlan]);

  const go = (to: StepSlug) => navigate(stepPath(to, keepSearch));
  const setContract = (value: Contract) => {
    const next = new URLSearchParams(search);
    if (value === 'proposed') next.set('contrato', 'proposto');
    else next.delete('contrato');
    setSearch(next, { replace: true });
  };

  const toggle = (list: string[], id: string) => (list.includes(id) ? list.filter((x) => x !== id) : [...list, id]);
  const approvedCount =
    approved.length + commands.length + Object.values(resolutions).filter((r) => r === 'overwrite').length;

  // Os dois "tempos" de demonstração só existem neste protótipo; o modelo de visão é o mesmo da aplicação real.
  const op = useMemo(
    () => buildOperation(applyReplay.events, contract === 'proposed' ? [] : EXPECTED_APPLY_STEPS, engineIdentity),
    [applyReplay.events, contract]
  );
  const planOp = useMemo(() => buildOperation(planReplay.events, [], engineIdentity), [planReplay.events]);
  const feed = useMemo(
    () =>
      (slug === 'plano' ? planReplay.events : applyReplay.events)
        .map((e, i) => describeEvent(e, op, i))
        .filter((item) => item !== null),
    [slug, planReplay.events, applyReplay.events, op]
  );

  const proposalMissing = plan.limitations.some((l) => l.code === 'HARNESS_ENGINEER_NOT_INVOKED');
  const judge = applyResult.validation?.results.find((r) => r.id === 'EV-JUDGE-001');

  const asParticipant = (actor: Actor): Participant => ({ ...actor, demo: actor.source === 'proposed' });
  const idleEngine: Participant = {
    ...engineIdentity,
    state: 'not_started',
    activity: null,
    note: 'Executa localmente.'
  };

  let participants: Participant[];
  if (slug === 'aplicacao' || slug === 'resultado') {
    const actors =
      slug === 'resultado' ? [{ ...op.actors[0]!, state: 'done' as const, activity: 'Execução concluída' }] : op.actors;
    participants = actors.map(asParticipant);
    if (slug === 'resultado' && judge) {
      participants.push({
        id: 'judge',
        name: judge.name,
        state: judge.status === 'passed' ? 'done' : 'not_started',
        activity: judge.status === 'not_run' ? 'Não executado nesta validação' : null
      });
    }
  } else if (slug === 'plano' && contract === 'proposed') {
    participants = planOp.actors.filter((a) => a.source === 'proposed').map(asParticipant);
  } else {
    participants = [idleEngine];
    if (index >= 2) {
      participants.push({
        id: 'harness-engineer',
        name: 'Harness Engineer',
        state: 'waiting',
        activity: proposalMissing ? 'Sem proposta: o plano usa as regras-base.' : 'Proposta recebida.',
        note: 'Roda no seu ambiente de IA, fora do Mariliz. O motor só lê .mariliz/proposal.json.'
      });
    }
  }

  const live =
    (slug === 'aplicacao' && !op.finished && applyReplay.playing) ||
    (slug === 'plano' && contract === 'proposed' && planReplay.playing);
  const summary: [string, string][] = [
    ['Projeto', demoProject.name],
    ['Modelo', 'Mariliz'],
    ['Ambiente de IA', env ? (envNames[env] ?? env) : 'a escolher'],
    ['Plano', index >= 2 ? `${plan.changes.length} alterações` : 'ainda não gerado'],
    ['Aprovados', index >= 3 ? `${approvedCount}` : '—']
  ];

  const analyze = () => {
    // Protótipo: um atraso curto representa a chamada única do motor (project.discover), que não emite progresso.
    setAnalysis('loading');
    window.setTimeout(() => setAnalysis('ready'), 900);
  };
  const generatePlan = () => {
    setPlanning(true);
    window.setTimeout(() => {
      setPlanning(false);
      go('plano');
    }, 1100);
  };

  let body;
  let actions;
  switch (slug) {
    case 'projeto':
      body = <ProjectStep analysis={analysis} onAnalyze={analyze} />;
      actions = (
        <Button variant="contained" disabled={analysis !== 'ready'} onClick={() => go('modelo')}>
          Continuar
        </Button>
      );
      break;
    case 'modelo':
      body = <ModelStep env={env} onEnv={setEnv} planning={planning} />;
      actions = (
        <>
          <Button variant="outlined" color="inherit" disabled={planning} onClick={() => go('projeto')}>
            Voltar
          </Button>
          <Button variant="contained" disabled={!env || planning} onClick={generatePlan}>
            {planning ? 'Gerando plano…' : 'Gerar plano'}
          </Button>
        </>
      );
      break;
    case 'plano':
      body = <PlanStep plan={plan} demoPlanning={contract === 'proposed'} />;
      actions = (
        <>
          <Button variant="outlined" color="inherit" onClick={() => go('modelo')}>
            Voltar
          </Button>
          <Button variant="contained" onClick={() => go('revisao')}>
            Revisar alterações
          </Button>
        </>
      );
      break;
    case 'revisao':
      body = (
        <ReviewStep
          plan={plan}
          approved={approved}
          commands={commands}
          resolutions={resolutions}
          onToggle={(id) => setApproved((list) => toggle(list, id))}
          onToggleCommand={(id) => setCommands((list) => toggle(list, id))}
          onResolve={(id, value) => setResolutions((r) => ({ ...r, [id]: value }))}
        />
      );
      actions = (
        <>
          <Button variant="outlined" color="inherit" onClick={() => go('plano')}>
            Voltar
          </Button>
          <Button variant="contained" disabled={approvedCount === 0} onClick={() => go('aplicacao')}>
            Aplicar configuração ({approvedCount} aprovados)
          </Button>
        </>
      );
      break;
    case 'aplicacao':
      body = <OperationStep view={op} replay={applyReplay} contract={contract} onContract={setContract} />;
      actions = (
        <>
          <Button variant="outlined" color="inherit" disabled={op.finished} onClick={() => go('revisao')}>
            Cancelar e desfazer
          </Button>
          <Button variant="contained" disabled={!op.finished} onClick={() => go('resultado')}>
            Ver resultado
          </Button>
        </>
      );
      break;
    default:
      body = <ResultStep result={applyResult} />;
      actions = (
        <>
          <Button variant="outlined" color="inherit" onClick={() => undefined}>
            Reverter alterações
          </Button>
          <Button variant="outlined" color="inherit" onClick={() => go('aplicacao')}>
            Rever execução
          </Button>
          <Button variant="contained" onClick={() => navigate('/projeto')}>
            Concluir
          </Button>
        </>
      );
  }

  return (
    <Stage
      bar={<JourneyBar current={index} onGo={go} />}
      panelTitle="Contexto"
      live={live}
      panel={
        <>
          <SummarySection facts={summary} />
          <ParticipantsSection participants={participants} empty="Nenhum participante ainda." />
          {slug === 'aplicacao' || (slug === 'plano' && contract === 'proposed') ? (
            <ActivitySection items={feed} live={live} />
          ) : null}
        </>
      }
      actions={actions}
      actionsNote={nextHint[slug]}
    >
      {body}
    </Stage>
  );
}
