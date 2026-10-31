import type { StepDefinition, StepSlug } from '@/prototype/types/journey';

export const STEPS: readonly StepDefinition[] = [
  { slug: 'projeto', id: 'project', label: 'Projeto' },
  { slug: 'modelo', id: 'model', label: 'Modelo e ambiente' },
  { slug: 'plano', id: 'plan', label: 'Plano' },
  { slug: 'revisao', id: 'review', label: 'Revisão' },
  { slug: 'aplicacao', id: 'run', label: 'Aplicação' },
  { slug: 'resultado', id: 'result', label: 'Resultado' }
];

export const NEXT_HINT: Record<StepSlug, string> = {
  projeto: 'A seguir: escolher o ambiente de IA.',
  modelo: 'A seguir: o motor monta o plano. Nada é alterado ainda.',
  plano: 'A seguir: você revisa e aprova cada alteração.',
  revisao: 'A seguir: o motor aplica só o que você aprovou.',
  aplicacao: 'A seguir: o resultado e a validação.',
  resultado: 'Fim da jornada.'
};
