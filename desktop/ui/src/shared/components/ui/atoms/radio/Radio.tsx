import type { FormControlLabelProps, RadioProps as MuiRadioProps, SxProps, Theme } from '@mui/material';
import type { ReactNode } from 'react';

import { StyledFormControlLabel, StyledRadio } from './Radio.styled';

export type RadioProps = Omit<MuiRadioProps, 'name'> & {
  /** O que a pessoa lê ao lado do botão; também é o nome acessível da opção. Pode ser mais que texto. */
  label: ReactNode;
  /** Agrupa as opções: só uma de cada `name` fica marcada. */
  name: string;
  /** Estilo do rótulo que envolve o botão, como o `formControlSX` do `Checkbox` do Synthra. */
  formControlSX?: SxProps<Theme>;
  /** Atributos do rótulo que envolve o botão (classe, `data-*`): para a linha inteira ser a área clicável. */
  labelProps?: Omit<FormControlLabelProps, 'control' | 'label'>;
};

/**
 * Botão de opção do Spock Studio. O Synthra não tem um: este átomo segue o desenho do `Checkbox` dele (rótulo do MUI com o
 * controle ao lado) e fica no app, na camada de átomos. O `input` continua nativo por baixo, com nome, estado e teclado do
 * navegador.
 */
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
