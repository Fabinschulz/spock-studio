import type { ConfigurationPlan } from '@/shared/schema';

export interface PlannedStep {
  id: string;
  title: string;
}

/**
 * Etapas que a aplicação executa, na ordem (ApplyExecution.cs): pré-condições, backup, um comando por instalação
 * aprovada, escrita, registro e validação. Os títulos são os que o motor publica; o motor deveria declarar esta lista
 * ao iniciar (evento proposto `run.plan`, ver docs/redesign/contratos-propostos.md). Até lá, a lista é derivada do
 * plano e das aprovações, e a UI só trata como "aguardando" o que o motor de fato sempre faz.
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
