export const demoProject = {
  name: 'empreganet-api',
  path: '…\\MyApps\\empreganet-api'
} as const;

export const recentProjects = [
  { id: 'r1', name: 'empreganet-api', path: '…\\MyApps\\empreganet-api', when: 'há 1 h' },
  { id: 'r2', name: 'mariliz-site', path: '…\\MyApps\\mariliz-site', when: 'anteontem' }
] as const;

export const demoRuns = [
  { id: 'run-0007', kind: 'Aplicação', outcome: 'Concluído', when: 'ontem, 16:42', files: 11 },
  { id: 'run-0006', kind: 'Validação', outcome: 'Concluído com limitações', when: 'ontem, 16:50', files: 0 }
] as const;
