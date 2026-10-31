import type { WorkbenchContext } from '@/prototype/types/shell';
import { useOutletContext } from 'react-router';

export const useWorkbench = () => useOutletContext<WorkbenchContext>();
