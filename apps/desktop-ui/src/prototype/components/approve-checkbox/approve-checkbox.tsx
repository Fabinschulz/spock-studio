import { Glyph } from '@/shared/components/ui';

import screen from '@/prototype/styles/screen.module.scss';

import type { ApproveCheckboxProps } from './types';

// O <input> nativo segue sendo o controle (teclado e leitor de tela); o desenho é só visual.
export function ApproveCheckbox({ checked, label, onChange }: ApproveCheckboxProps) {
  return (
    <label className={screen.approve} data-checked={checked}>
      <span className={screen.check} aria-hidden="true">
        <Glyph name="check" size={12} />
      </span>
      <input className="mz-vh" type="checkbox" checked={checked} onChange={onChange} aria-label={label} />
    </label>
  );
}
