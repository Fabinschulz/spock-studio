import { Radio } from '@/shared/components';
import type { IacProviderOptions } from '@/shared/schema';
import { Checkbox, TextField } from '@mariliz/ui-kit';

import styles from '../infrastructure-page.module.scss';
import type { BlueprintForm } from '../domain/blueprint';
import type { TextFieldSpec } from '../domain/provider-fields';
import type { GenerateFlow } from '../hooks/use-generate-flow';

/** Um campo de texto do desenho: lê e escreve a chave dele no formulário. */
export function FormText({ flow, field }: { flow: GenerateFlow; field: TextFieldSpec }) {
  return (
    <TextField
      label={field.label}
      value={flow.state.form[field.key]}
      disabled={flow.busy}
      helperText={field.hint}
      onChange={(event) =>
        flow.dispatch({ type: 'edit', change: { [field.key]: event.target.value } as Partial<BlueprintForm> })
      }
      slotProps={{ htmlInput: { spellCheck: false } }}
    />
  );
}

type FlagKey = { [K in keyof BlueprintForm]: BlueprintForm[K] extends boolean ? K : never }[keyof BlueprintForm];

export function FormFlag({ flow, field, label }: { flow: GenerateFlow; field: FlagKey; label: string }) {
  return (
    <Checkbox
      label={label}
      checked={flow.state.form[field]}
      disabled={flow.busy}
      onChange={(_, checked) => flow.dispatch({ type: 'edit', change: { [field]: checked } })}
    />
  );
}

/** Uma escolha entre opções fixas, com o valor guardado como texto no formulário. */
export function FormChoice({
  flow,
  field,
  legend,
  options
}: {
  flow: GenerateFlow;
  field: 'size' | 'engine';
  legend: string;
  options: { value: string; label: string }[];
}) {
  return (
    <fieldset className={styles.choices}>
      <legend>{legend}</legend>
      {options.map((option) => (
        <Radio
          key={option.value}
          name={`iac-generate-${field}`}
          value={option.value}
          checked={flow.state.form[field] === option.value}
          disabled={flow.busy}
          onChange={() => flow.dispatch({ type: 'edit', change: { [field]: option.value } })}
          label={option.label}
        />
      ))}
    </fieldset>
  );
}

/** "Entre 20 e 1000." ou "32, 64 ou 128.", do que o motor anuncia para o armazenamento. */
export function storageHint({ minGb, maxGb, allowedGb }: IacProviderOptions['database']['storage']): string {
  if (allowedGb.length === 0) return `Entre ${minGb} e ${maxGb}.`;
  const last = allowedGb[allowedGb.length - 1];
  return allowedGb.length === 1 ? `${last}.` : `${allowedGb.slice(0, -1).join(', ')} ou ${last}.`;
}
