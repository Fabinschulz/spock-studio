import type { CheckboxProps, FormControlLabelProps } from '@mui/material';
import { Checkbox } from '@mui/material';
import { styled } from '@mui/material/styles';
import type { ReactNode } from 'react';

import { StyledFormControlLabel } from '../radio/Radio.styled';

const StyledCheckbox = styled(Checkbox)(({ theme }) => ({
  padding: theme.spacing(0.5),
  color: theme.palette.text.secondary,
  '&.Mui-checked': { color: theme.palette.primary.main },
  '&.Mui-disabled': { color: theme.palette.text.disabled }
}));

export type CheckboxOptionProps = CheckboxProps & {
  /** Também é o nome acessível da opção. */
  label: ReactNode;
  /** Para a linha inteira ser a área clicável. */
  labelProps?: Omit<FormControlLabelProps, 'control' | 'label'>;
};

/**
 * O par do `Radio` para escolhas múltiplas numa lista: o rótulo aceita conteúdo rico (ícone, nome, etiquetas), o que o
 * `Checkbox` do ui-kit, só com texto, não aceita.
 */
export function CheckboxOption({ label, labelProps, ...rest }: CheckboxOptionProps) {
  return <StyledFormControlLabel {...labelProps} label={label} control={<StyledCheckbox {...rest} />} />;
}
