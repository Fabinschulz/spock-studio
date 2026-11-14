import { Tooltip as MuiTooltip, type TooltipProps } from '@mui/material';
import type { ReactElement } from 'react';

/**
 * A dica descreve sem renomear: o controle mantém o próprio nome acessível (com avisos como "2 em andamento") e a dica
 * entra só como descrição. Desativada, devolve o controle intacto.
 */
export function Tooltip({
  title,
  placement = 'top',
  disabled = false,
  children
}: {
  title: string;
  placement?: TooltipProps['placement'];
  disabled?: boolean;
  children: ReactElement;
}) {
  if (disabled) return children;

  return (
    <MuiTooltip title={title} placement={placement} describeChild disableInteractive>
      {children}
    </MuiTooltip>
  );
}
