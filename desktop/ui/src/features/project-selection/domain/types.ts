export type SelectedProject = {
  projectId: string;
  displayName: string;
  /** Caminho já abreviado e sanitizado pelo host; a UI nunca recebe o caminho completo. */
  displayPath: string;
  /** Quando o host o informa; é o que marca o projeto atual na lista. */
  recentId?: string;
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
  /** Do mais recente ao mais antigo. Ausente quando não há host. */
  recents?(): Promise<RecentProject[]>;
  /** A UI não envia caminho: o host só reabre o que está na sua lista. */
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
