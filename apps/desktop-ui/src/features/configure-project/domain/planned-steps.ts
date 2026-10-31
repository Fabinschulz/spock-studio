import type { ConfigurationPlan } from '@/shared/schema';
import type { PlannedStep } from '../types/run';

/**
 * Ordem das etapas de ApplyExecution.cs. O motor ainda não declara a lista ao iniciar (evento proposto `run.plan`),
 * então ela é derivada do plano e das aprovações.
 */
export function plannedApplySteps(plan: ConfigurationPlan, approvedCommandIds: readonly string[]): PlannedStep[] {
  const commands = plan.commands
    .filter((c) => approvedCommandIds.includes(c.id))
    .map((c) => ({ id: `command:${c.id}`, title: c.description }));

  return [
    { id: 'precheck', title: 'Verificar pré-condições' },
    { id: 'backup', title: 'Guardar cópia do que será alterado' },
    ...commands,
    { id: 'write', title: 'Aplicar as alterações aprovadas' },
    { id: 'lock', title: 'Registrar os arquivos gerenciados' },
    { id: 'validate', title: 'Validar a configuração aplicada' }
  ];
}
