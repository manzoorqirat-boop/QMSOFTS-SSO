export function BrandHeader({ subtitle }: { subtitle?: string }) {
  return (
    <header className="brand">
      <div className="brand-mark" aria-hidden="true">
        <svg className="brand-logo" viewBox="0 0 100 100" role="img" aria-label="QMSofts">
          <path d="M50 26 L68 33 V49 C68 61 60 69 50 74 C40 69 32 61 32 49 V33 Z"
            fill="none" stroke="#ffffff" strokeWidth="4.5" strokeLinejoin="round" />
          <path d="M42 50 l5.5 6.5 12 -14" fill="none" stroke="#ffffff"
            strokeWidth="5" strokeLinecap="round" strokeLinejoin="round" />
        </svg>
      </div>

      <h1 className="brand-name">
        QMSofts<span className="dot">.</span>
      </h1>
      {subtitle && <p className="brand-sub">{subtitle}</p>}
    </header>
  );
}