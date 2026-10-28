/**
 * Ilustração da marca: painéis em camadas com a marca ao centro, como nos modelos. Só ornamento: não carrega informação
 * e fica escondida do leitor de tela. As cores vêm dos tokens, então acompanha o tema.
 */
export function HeroArt({ className }: { className?: string }) {
  return (
    <svg className={className} viewBox="0 0 380 210" fill="none" aria-hidden="true" focusable="false">
      <defs>
        <linearGradient id="hero-panel" x1="0" y1="0" x2="1" y2="1">
          <stop offset="0" stopColor="var(--color-primary)" stopOpacity="0.32" />
          <stop offset="1" stopColor="var(--color-primary)" stopOpacity="0.04" />
        </linearGradient>
        <linearGradient id="hero-tile" x1="0" y1="0" x2="1" y2="1">
          <stop offset="0" stopColor="var(--color-primary)" stopOpacity="0.95" />
          <stop offset="1" stopColor="var(--color-primary-hover)" stopOpacity="0.9" />
        </linearGradient>
        <radialGradient id="hero-floor" cx="0.5" cy="0.5" r="0.5">
          <stop offset="0" stopColor="var(--color-primary)" stopOpacity="0.45" />
          <stop offset="1" stopColor="var(--color-primary)" stopOpacity="0" />
        </radialGradient>
      </defs>

      <ellipse cx="190" cy="184" rx="170" ry="20" fill="url(#hero-floor)" />

      {/* Painel de código, à esquerda */}
      <g transform="translate(34 40) skewY(-14)">
        <rect
          width="96"
          height="112"
          rx="10"
          fill="url(#hero-panel)"
          stroke="var(--color-primary)"
          strokeOpacity="0.5"
        />
        <g stroke="var(--color-primary)" strokeOpacity="0.8" strokeWidth="3" strokeLinecap="round">
          <path d="M16 24h40M16 40h56M16 56h32M16 72h48" />
        </g>
        <path
          d="m70 90-8-6 8-6M82 78l8 6-8 6"
          stroke="var(--color-primary)"
          strokeWidth="3"
          strokeLinecap="round"
          strokeLinejoin="round"
        />
      </g>

      {/* Painel de rede, à direita */}
      <g transform="translate(252 34) skewY(14)">
        <rect
          width="96"
          height="112"
          rx="10"
          fill="url(#hero-panel)"
          stroke="var(--color-primary)"
          strokeOpacity="0.5"
        />
        <g stroke="var(--color-primary)" strokeOpacity="0.9" strokeWidth="2.5">
          <path d="M48 56 24 30M48 56l26-24M48 56 24 84M48 56l28 28" />
        </g>
        <g fill="var(--color-background)" stroke="var(--color-primary)" strokeWidth="3">
          <circle cx="48" cy="56" r="10" />
          <circle cx="24" cy="30" r="6" />
          <circle cx="74" cy="32" r="6" />
          <circle cx="24" cy="84" r="6" />
          <circle cx="76" cy="84" r="6" />
        </g>
      </g>

      {/* Placa central com a marca */}
      <g transform="translate(190 88)">
        <rect
          x="-52"
          y="-62"
          width="104"
          height="116"
          rx="18"
          fill="var(--color-background)"
          stroke="var(--color-primary)"
          strokeOpacity="0.55"
          strokeWidth="2"
        />
        <rect x="-44" y="-54" width="88" height="100" rx="14" fill="url(#hero-tile)" />
        <g transform="translate(-26 -36) scale(1.6)">
          <path
            d="M2 24V12.5a4.6 4.6 0 0 1 9.2 0V24M11.2 12.5a4.6 4.6 0 0 1 9.2 0V24"
            stroke="var(--color-on-primary)"
            strokeWidth="2.6"
            strokeLinecap="round"
            strokeLinejoin="round"
          />
          <circle cx="23.6" cy="3.6" r="2.2" fill="var(--color-on-primary)" />
        </g>
      </g>
    </svg>
  );
}
