import type { CheckboxProps, FormControlLabelProps } from '@mui/material';
import { Checkbox, FormControlLabel } from '@mui/material';
import { styled } from '@mui/material/styles';
import type { ReactNode } from 'react';

// A mesma linha do `Radio` do ui-kit: rótulo flexível, para caber ícone, nome e etiquetas.
const StyledFormControlLabel = styled(FormControlLabel)(({ theme }) => ({
  marginLeft: 0,
  marginRight: 0,
  gap: theme.spacing(1),
  alignItems: 'center',
  '& .MuiFormControlLabel-label': {
    ...theme.typography.body2,
    display: 'flex',
    alignItems: 'center',
    gap: theme.spacing(1.5),
    flex: 1,
    minWidth: 0,
    color: theme.palette.text.primary,
    '&.Mui-disabled': { color: theme.palette.text.disabled }
  }
}));

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
