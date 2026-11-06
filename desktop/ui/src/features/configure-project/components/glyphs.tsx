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
