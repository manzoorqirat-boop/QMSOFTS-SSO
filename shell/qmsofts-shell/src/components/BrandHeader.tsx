export function BrandHeader({ subtitle }: { subtitle?: string }) {
  return (
    <header className="brand">
      <div className="brand-mark" aria-hidden="true">
        <span className="brand-q">Q</span>
      </div>
      <h1 className="brand-name">QMSofts</h1>
      {subtitle && <p className="brand-sub">{subtitle}</p>}
      <div className="verse-dots" aria-hidden="true">
        <span />
        <span />
        <span />
      </div>
    </header>
  );
}
