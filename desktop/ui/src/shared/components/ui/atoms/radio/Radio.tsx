import type { FormControlLabelProps, RadioProps as MuiRadioProps, SxProps, Theme } from '@mui/material';
import type { ReactNode } from 'react';

import { StyledFormControlLabel, StyledRadio } from './Radio.styled';

export type RadioProps = Omit<MuiRadioProps, 'name'> & {
  /** Também é o nome acessível da opção. */
  label: ReactNode;
  name: string;
  formControlSX?: SxProps<Theme>;
  /** Para a linha inteira ser a área clicável. */
  labelProps?: Omit<FormControlLabelProps, 'control' | 'label'>;
};

/** O `input` continua nativo por baixo, com nome, estado e teclado do navegador. */
export function Radio({ label, name, formControlSX, labelProps, ...rest }: RadioProps) {
  return (
    <StyledFormControlLabel
      {...labelProps}
      sx={formControlSX}
      label={label}
      control={<StyledRadio name={name} {...rest} />}
    />
  );
}
