export function BrandHeader({ subtitle }: { subtitle?: string }) {
  return (
    <header className="brand">
      <div className="brand-mark" aria-hidden="true">
        <svg className="brand-logo" viewBox="0 0 100 100" role="img" aria-label="QMSofts">
          <defs>
            <linearGradient id="qm-bg" x1="0" y1="0" x2="1" y2="1">
              <stop offset="0" stopColor="#16213A" />
              <stop offset="1" stopColor="#0A0F1C" />
            </linearGradient>
            <linearGradient id="qm-ring" x1="0" y1="0" x2="1" y2="1">
              <stop offset="0" stopColor="#7BFFE6" />
              <stop offset="0.6" stopColor="#2FE6C0" />
              <stop offset="1" stopColor="#8E7BFF" />
            </linearGradient>
          </defs>
          <rect width="100" height="100" rx="26" fill="url(#qm-bg)" />
          <rect x="1.5" y="1.5" width="97" height="97" rx="24.5" fill="none"
            stroke="#2FE6C0" strokeOpacity="0.22" />
          <circle cx="50" cy="48" r="22" fill="none" stroke="url(#qm-ring)" strokeWidth="6" />
          <circle cx="66" cy="64" r="6.5" fill="#8E7BFF" />
          <path d="M20 50 H38 l4 -12 6 24 5 -16 4 8 H80" fill="none"
            stroke="#7BFFE6" strokeWidth="3" strokeLinecap="round" strokeLinejoin="round" />
        </svg>
      </div>

      <div>
        <span className="brand-tag">
          <span className="spark" aria-hidden="true" />
          Validated &amp; audit-ready
        </span>
      </div>

      <h1 className="brand-name">
        QMSofts<span className="dot">.</span>
      </h1>
      {subtitle && <p className="brand-sub">{subtitle}</p>}

      <svg className="pulse" viewBox="0 0 280 26" aria-hidden="true" preserveAspectRatio="none">
        <path className="pulse-trace"
          d="M0 13 H86 l8 -9 10 18 8 -22 7 26 9 -13 H170 l6 -6 7 12 H280" />
      </svg>
    </header>
  );
}