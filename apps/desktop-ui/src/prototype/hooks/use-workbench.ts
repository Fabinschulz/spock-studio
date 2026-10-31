import { useOutletContext } from 'react-router';

import type { WorkbenchContext } from '@/prototype/types/shell';

export const useWorkbench = () => useOutletContext<WorkbenchContext>();
