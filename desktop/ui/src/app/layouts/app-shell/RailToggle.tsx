import { Tooltip } from '@/shared/components';
import { ArrowLeftIcon, ArrowRightIcon, Button } from '@mariliz/ui-kit';
import styles from './AppShell.module.scss';

export function RailToggle({ collapsed, onToggle }: { collapsed: boolean; onToggle: () => void }) {
  const label = collapsed ? 'Expandir menu' : 'Recolher menu';

  return (
    <Tooltip title={label} placement="right" disabled={!collapsed}>
      <Button variant="text" color="inherit" className={styles.item} onClick={onToggle}>
        <span className={styles.itemIcon} aria-hidden="true">
          {collapsed ? <ArrowRightIcon fontSize="small" /> : <ArrowLeftIcon fontSize="small" />}
        </span>
        <span className={styles.itemLabel}>{label}</span>
      </Button>
    </Tooltip>
  );
}
