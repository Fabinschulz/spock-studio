import applyJson from '@contracts/fixtures/golden/apply-result.json';
import discoveryJson from '@contracts/fixtures/golden/discovery.json';
import planJson from '@contracts/fixtures/golden/plan.json';

import { discoverySchema, planSchema, runResultSchema } from '@/shared/schema';

// Respostas golden do motor, validadas pelos mesmos esquemas Zod do aplicativo.
export const discovery = discoverySchema.parse(discoveryJson);
export const plan = planSchema.parse(planJson);
export const applyResult = runResultSchema.parse(applyJson);
