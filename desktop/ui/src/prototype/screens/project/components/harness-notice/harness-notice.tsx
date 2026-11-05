import { Button } from '@mariliz/ui-kit';
import { useNavigate } from 'react-router';

import { Glyph } from '@/shared/components/ui';

import { Notice } from '@/prototype/components/notice';

export function HarnessNotice() {
  const navigate = useNavigate();

  return (
    <Notice
      icon={<Glyph name="spark" size={16} />}
      action={
        <Button variant="contained" onClick={() => navigate('/configurar')}>
          Configurar projeto
        </Button>
      }
    >
      <p>
        <strong>O Harness ainda não está configurado neste projeto.</strong>
      </p>
      <p>A estrutura de especificação não está instalada e não há configuração declarada (mariliz.yaml).</p>
    </Notice>
  );
}
