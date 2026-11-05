import { Button, Modal } from '@mariliz/ui-kit';

import styles from '../../components/panels.module.scss';

interface RevertConfirmModalProps {
  open: boolean;
  onCancel: () => void;
  onConfirm: () => void;
}

export function RevertConfirmModal({ open, onCancel, onConfirm }: RevertConfirmModalProps) {
  return (
    <Modal
      open={open}
      onClose={onCancel}
      title="Reverter as alterações?"
      description="Isto restaura o estado anterior dos arquivos que a aplicação alterou. Arquivos que você editou depois da aplicação não são sobrescritos: ficam como estão e aparecem como pendência."
      closeLabel="Fechar"
    >
      <div className={styles.actions}>
        <Button variant="outlined" color="inherit" onClick={onCancel}>
          Não reverter
        </Button>
        <Button variant="contained" onClick={onConfirm}>
          Reverter agora
        </Button>
      </div>
    </Modal>
  );
}
