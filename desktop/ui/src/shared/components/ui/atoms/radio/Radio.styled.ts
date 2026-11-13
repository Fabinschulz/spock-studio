import { FormControlLabel, Radio } from '@mui/material';
import { styled } from '@mui/material/styles';

/**
 * Mesmo desenho do `Checkbox` do Synthra (rótulo do MUI com o controle ao lado), com as cores do tema do app. O rótulo é
 * flexível porque pode trazer mais que texto (ícone, nome e etiquetas na escolha do ambiente de IA).
 */
export const StyledFormControlLabel = styled(FormControlLabel)(({ theme }) => ({
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

export const StyledRadio = styled(Radio)(({ theme }) => ({
  padding: theme.spacing(0.5),
  color: theme.palette.text.secondary,
  '&.Mui-checked': { color: theme.palette.primary.main },
  '&.Mui-disabled': { color: theme.palette.text.disabled }
}));
