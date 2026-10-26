import { ArrowRightIcon } from '@synthra.io/ui-kit';
import { Box, Card, CardActionArea, Typography } from '@mui/material';

export type ProjectPickerProps = {
  selected: { displayName: string; displayPath: string } | null;
  onPick: () => void;
  busy?: boolean;
  /** Presente quando a seleção não está disponível neste ambiente (ex.: navegador sem o host desktop). */
  unavailableReason?: string;
};

/** O ícone de pasta ainda não existe na Synthra; este SVG é provisório (ver docs/experience-blueprint.md, seção 11.2). */
function FolderGlyph() {
  return (
    <svg width="24" height="24" viewBox="0 0 24 24" fill="none" aria-hidden="true" focusable="false">
      <path d="M3 6.5A1.5 1.5 0 0 1 4.5 5h4.2l2 2H19.5A1.5 1.5 0 0 1 21 8.5v9a1.5 1.5 0 0 1-1.5 1.5h-15A1.5 1.5 0 0 1 3 17.5v-11Z" stroke="currentColor" strokeWidth="1.6" strokeLinejoin="round" />
    </svg>
  );
}

export function ProjectPicker({ selected, onPick, busy, unavailableReason }: ProjectPickerProps) {
  const unavailable = unavailableReason !== undefined;
  return (
    <Box component="section" aria-labelledby="projeto-rotulo">
      <Typography id="projeto-rotulo" variant="overline" component="h2" sx={{ color: 'text.secondary' }}>
        Projeto
      </Typography>
      <Card variant="outlined" sx={{ mt: 0.5 }}>
        <CardActionArea
          onClick={unavailable || busy ? undefined : onPick}
          aria-disabled={unavailable || busy || undefined}
          aria-describedby="projeto-detalhe"
          sx={{ p: 2, display: 'flex', alignItems: 'center', gap: 2, textAlign: 'left' }}
        >
          <Box aria-hidden="true" sx={{ color: 'primary.main', display: 'flex' }}>
            <FolderGlyph />
          </Box>
          <Box sx={{ flex: 1, minWidth: 0 }}>
            <Typography variant="subtitle2" component="span" sx={{ display: 'block', fontWeight: 600 }}>
              {selected ? selected.displayName : 'Selecionar pasta do projeto'}
            </Typography>
            <Typography id="projeto-detalhe" variant="body2" component="span" noWrap sx={{ display: 'block', color: 'text.secondary' }}>
              {selected ? selected.displayPath : (unavailableReason ?? 'Os arquivos permanecem no computador.')}
            </Typography>
          </Box>
          {selected ? (
            <Typography variant="body2" component="span" sx={{ color: 'primary.main', fontWeight: 600 }}>
              Trocar
            </Typography>
          ) : (
            <Box aria-hidden="true" sx={{ display: 'flex', color: 'text.secondary' }}>
              <ArrowRightIcon />
            </Box>
          )}
        </CardActionArea>
      </Card>
      {selected ? (
        <Typography variant="caption" sx={{ display: 'block', color: 'text.secondary', mt: 0.75 }}>
          Os arquivos permanecem no computador.
        </Typography>
      ) : null}
    </Box>
  );
}

