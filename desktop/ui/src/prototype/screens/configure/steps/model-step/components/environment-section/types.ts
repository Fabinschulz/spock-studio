export interface EnvironmentSectionProps {
  env: string | null;
  onEnv: (id: string) => void;
  disabled: boolean;
}
