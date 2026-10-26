export type SelectedProject = {
  projectId: string;
  displayName: string;
  /** Caminho já abreviado e sanitizado pelo host; a UI nunca recebe o caminho completo. */
  displayPath: string;
};

export type ProjectPickerPort = {
  /** `null` quando o usuário cancela o diálogo. Lança `ProjectPickError` nos demais casos. */
  pick(): Promise<SelectedProject | null>;
};

export class ProjectPickError extends Error {
  constructor(
    readonly code: string,
    message: string
  ) {
    super(message);
    this.name = 'ProjectPickError';
  }
}
