import { useEffect, useMemo } from 'react';

import { EXPECTED_APPLY_STEPS } from '@/prototype/constants/apply-steps';
import { ENGINE_IDENTITY } from '@/prototype/constants/harness';
import { applyScriptCurrent } from '@/prototype/data/scripts/apply-current';
import { applyScriptProposed } from '@/prototype/data/scripts/apply-proposed';
import { planningScriptProposed } from '@/prototype/data/scripts/planning-proposed';
import { describeEvent } from '@/prototype/domain/describe-event';
import { buildOperation } from '@/prototype/domain/operation/build-operation';
import type { Contract, StepSlug } from '@/prototype/types/journey';

import { useReplay } from './use-replay';

interface Options {
  slug: StepSlug;
  contract: Contract;
  fixedAt: number | null;
}

export function useConfigureReplays({ slug, contract, fixedAt }: Options) {
  const proposed = contract === 'proposed';
  const startAt = fixedAt ?? 0;

  const applyReplay = useReplay(proposed ? applyScriptProposed : applyScriptCurrent, { startAt });
  const planReplay = useReplay(planningScriptProposed, { startAt });
  const { restart: restartApply } = applyReplay;
  const { restart: restartPlan } = planReplay;

  useEffect(() => {
    if (fixedAt === null && slug === 'aplicacao') restartApply();
  }, [slug, contract, fixedAt, restartApply]);

  useEffect(() => {
    if (fixedAt === null && slug === 'plano' && proposed) restartPlan();
  }, [slug, proposed, fixedAt, restartPlan]);

  const operation = useMemo(
    () => buildOperation(applyReplay.events, proposed ? [] : EXPECTED_APPLY_STEPS, ENGINE_IDENTITY),
    [applyReplay.events, proposed]
  );
  const planning = useMemo(() => buildOperation(planReplay.events, [], ENGINE_IDENTITY), [planReplay.events]);

  const feed = useMemo(
    () =>
      (slug === 'plano' ? planReplay.events : applyReplay.events)
        .map((event, index) => describeEvent(event, operation, index))
        .filter((item) => item !== null),
    [slug, planReplay.events, applyReplay.events, operation]
  );

  const live =
    (slug === 'aplicacao' && !operation.finished && applyReplay.playing) ||
    (slug === 'plano' && proposed && planReplay.playing);

  return { applyReplay, operation, planning, feed, live };
}
