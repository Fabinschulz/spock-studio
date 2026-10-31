import type { ReactNode } from 'react';

import type { Tone } from '@/prototype/types/tag';

export interface TagProps {
  tone?: Tone;
  children: ReactNode;
}
