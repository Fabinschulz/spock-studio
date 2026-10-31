/**
 * As seis etapas da jornada, na mesma ordem e com as mesmas dependências da aplicação real
 * (project → model → plan → review → run → result). Definidas aqui porque entre features só vale a API pública.
 */
export const STEPS = [
  { slug: 'projeto', id: 'project', label: 'Projeto' },
  { slug: 'modelo', id: 'model', label: 'Modelo e ambiente' },
  { slug: 'plano', id: 'plan', label: 'Plano' },
  { slug: 'revisao', id: 'review', label: 'Revisão' },
  { slug: 'aplicacao', id: 'run', label: 'Aplicação' },
  { slug: 'resultado', id: 'result', label: 'Resultado' }
] as const;

export type StepSlug = (typeof STEPS)[number]['slug'];

export const stepIndex = (slug: string | undefined): number => {
  const i = STEPS.findIndex((s) => s.slug === slug);
  return i < 0 ? 0 : i;
};

export const stepPath = (slug: StepSlug, search = '') => `/configurar/${slug}${search}`;

/** O que acontece a seguir, dito em uma frase, para a barra de etapas. */
export const nextHint: Record<StepSlug, string> = {
  projeto: 'A seguir: escolher o ambiente de IA.',
  modelo: 'A seguir: o motor monta o plano. Nada é alterado ainda.',
  plano: 'A seguir: você revisa e aprova cada alteração.',
  revisao: 'A seguir: o motor aplica só o que você aprovou.',
  aplicacao: 'A seguir: o resultado e a validação.',
  resultado: 'Fim da jornada.'
};
