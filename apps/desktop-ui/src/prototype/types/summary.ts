export type SummaryFact = [label: string, value: string];

export interface SummaryInput {
  env: string | null;
  stepIndex: number;
  changeCount: number;
  approvedCount: number;
}
