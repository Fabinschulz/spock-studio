import { Box, Card, CardActionArea, Typography } from '@mui/material';
import type { ReactNode } from 'react';

export type ActionCardProps = {
  title: string;
  description: string;
  icon: ReactNode;
  onClick?: () => void;
  /** Quando indisponível o cartão continua focável (aria-disabled), para o leitor de tela anunciar o motivo. */
  unavailableReason?: string;
};

export function ActionCard({ title, description, icon, onClick, unavailableReason }: ActionCardProps) {
  const unavailable = unavailableReason !== undefined;
  const baseId = title.replace(/\s+/g, '-').toLowerCase();
  const descriptionId = `${baseId}-descricao`;
  const reasonId = `${baseId}-motivo`;

  return (
    <Card variant="outlined" sx={{ height: '100%', opacity: unavailable ? 0.72 : 1 }}>
      <CardActionArea
        onClick={unavailable ? undefined : onClick}
        aria-disabled={unavailable || undefined}
        aria-describedby={unavailable ? `${descriptionId} ${reasonId}` : descriptionId}
        sx={{ height: '100%', p: 2, display: 'flex', alignItems: 'flex-start', gap: 2, textAlign: 'left' }}
      >
        <Box aria-hidden="true" sx={{ color: 'primary.main', display: 'flex', pt: 0.25 }}>
          {icon}
        </Box>
        <Box sx={{ minWidth: 0, flex: 1 }}>
          <Box component="span" sx={{ display: 'flex', alignItems: 'baseline', justifyContent: 'space-between', gap: 1 }}>
            <Typography variant="subtitle1" component="span" sx={{ fontWeight: 600 }}>
              {title}
            </Typography>
            {unavailable ? (
              <Typography id={reasonId} variant="overline" component="span" sx={{ color: 'text.secondary', whiteSpace: 'nowrap' }}>
                {unavailableReason}
              </Typography>
            ) : null}
          </Box>
          <Typography id={descriptionId} variant="body2" component="span" sx={{ display: 'block', color: 'text.secondary', mt: 0.5 }}>
            {description}
          </Typography>
        </Box>
      </CardActionArea>
    </Card>
  );
}
