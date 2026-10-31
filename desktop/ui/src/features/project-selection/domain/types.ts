export type SelectedProject = {
  projectId: string;
  displayName: string;
  /** Caminho já abreviado e sanitizado pelo host; a UI nunca recebe o caminho completo. */
  displayPath: string;
};

export type RecentProject = {
  id: string;
  displayName: string;
  displayPath: string;
  /** Milissegundos desde 1970 da última abertura. */
  lastOpenedMs: number;
  /** Falso quando a pasta não existe mais ou não está acessível. */
  available: boolean;
};

export type ProjectPickerPort = {
  /** `null` quando o usuário cancela o diálogo. Lança `ProjectPickError` nos demais casos. */
  pick(): Promise<SelectedProject | null>;
  /** Projetos já escolhidos pelo diálogo nativo, do mais recente ao mais antigo. Ausente quando não há host. */
  recents?(): Promise<RecentProject[]>;
  /** Reabre um recente pelo id. A UI não envia caminho: o host só reabre o que está na sua lista. */
  openRecent?(id: string): Promise<SelectedProject>;
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
