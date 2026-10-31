export const EXPECTED_APPLY_STEPS = [
  { id: 'precheck', title: 'Verificar pré-condições' },
  { id: 'backup', title: 'Guardar cópia do que será alterado' },
  { id: 'write', title: 'Aplicar as alterações aprovadas' },
  { id: 'lock', title: 'Registrar os arquivos gerenciados' },
  { id: 'validate', title: 'Validar a configuração aplicada' }
] as const;
