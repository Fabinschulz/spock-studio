export function NodesGlyph({ size = 20 }: { size?: number }) {
  return (
    <svg width={size} height={size} viewBox="0 0 24 24" fill="none" aria-hidden="true" focusable="false">
      <g stroke="currentColor" strokeWidth="1.6" strokeLinecap="round" strokeLinejoin="round">
        <circle cx="6" cy="6" r="2.2" />
        <circle cx="18" cy="8" r="2.2" />
        <circle cx="8" cy="18" r="2.2" />
        <circle cx="18" cy="18" r="2.2" />
        <path d="M7.8 7.2 16.2 7.8M7 8 7.7 15.8M9.9 17.6 15.8 18M17.6 10.2 18 15.8M8 8 16.4 16.6" />
      </g>
    </svg>
  );
}

export function TerminalGlyph({ size = 20 }: { size?: number }) {
  return (
    <svg width={size} height={size} viewBox="0 0 24 24" fill="none" aria-hidden="true" focusable="false">
      <g stroke="currentColor" strokeWidth="1.7" strokeLinecap="round" strokeLinejoin="round">
        <rect x="3" y="4.5" width="18" height="15" rx="2.5" />
        <path d="m7.5 10 3 2.5-3 2.5M12.5 15h4" />
      </g>
    </svg>
  );
}

const toolProps = {
  width: 18,
  height: 18,
  viewBox: '0 0 24 24',
  fill: 'none',
  'aria-hidden': true,
  focusable: false
} as const;
const stroke = { stroke: 'currentColor', strokeWidth: 1.7, strokeLinecap: 'round', strokeLinejoin: 'round' } as const;

export function NodeGlyph() {
  return (
    <svg {...toolProps}>
      <g {...stroke}>
        <path d="M12 2.5 20 7v10l-8 4.5L4 17V7l8-4.5Z" />
        <path d="M9 15.5v-7l6 7v-7" />
      </g>
    </svg>
  );
}

export function PythonGlyph() {
  return (
    <svg {...toolProps}>
      <g {...stroke}>
        <path d="M8 8V6.5A2.5 2.5 0 0 1 10.5 4h3A2.5 2.5 0 0 1 16 6.5V11a1.5 1.5 0 0 1-1.5 1.5h-5A1.5 1.5 0 0 0 8 14v3.5A2.5 2.5 0 0 0 10.5 20h3a2.5 2.5 0 0 0 2.5-2.5V16M8 8H5.5A1.5 1.5 0 0 0 4 9.5v4A1.5 1.5 0 0 0 5.5 15H8M16 16h2.5a1.5 1.5 0 0 0 1.5-1.5v-4A1.5 1.5 0 0 0 18.5 9H16" />
      </g>
    </svg>
  );
}

export function GitGlyph() {
  return (
    <svg {...toolProps}>
      <g {...stroke}>
        <circle cx="7" cy="6" r="2" />
        <circle cx="7" cy="18" r="2" />
        <circle cx="17" cy="9" r="2" />
        <path d="M7 8v8M17 11c0 3-3 3.5-8.5 5.2" />
      </g>
    </svg>
  );
}

export function RefreshGlyph({ size = 20 }: { size?: number }) {
  return (
    <svg width={size} height={size} viewBox="0 0 20 20" fill="none" aria-hidden="true" focusable="false">
      <path
        d="M16.5 10a6.5 6.5 0 0 1-11.1 4.6M3.5 10a6.5 6.5 0 0 1 11.1-4.6M15 2.8v3.2h-3.2M5 17.2V14h3.2"
        stroke="currentColor"
        strokeWidth="1.6"
        strokeLinecap="round"
        strokeLinejoin="round"
      />
    </svg>
  );
}
