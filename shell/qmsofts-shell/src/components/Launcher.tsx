import { useAuth } from "../lib/AuthContext";
import { APP_REGISTRY } from "../lib/appRegistry";
import { launchApp } from "../lib/handoff";
import { BrandHeader } from "./BrandHeader";

// Deterministic, decorative "activity" sparkline derived from the app key.
// Purely ambient chrome — gives the console an analytics texture without
// implying real telemetry.
function sparkPaths(seed: string): { line: string; fill: string } {
  let h = 0;
  for (let i = 0; i < seed.length; i++) h = (h * 31 + seed.charCodeAt(i)) >>> 0;
  const n = 12;
  const pts: [number, number][] = [];
  for (let i = 0; i < n; i++) {
    h = (h * 1103515245 + 12345) >>> 0;
    const v = (h % 1000) / 1000; // 0..1
    const x = (i / (n - 1)) * 100;
    const y = 26 - (4 + v * 18); // padded into 0..30 box
    pts.push([x, y]);
  }
  const line = pts.map((p, i) => `${i ? "L" : "M"}${p[0].toFixed(1)} ${p[1].toFixed(1)}`).join(" ");
  const fill = `${line} L100 30 L0 30 Z`;
  return { line, fill };
}

export function Launcher({
  onOpenAdmin,
  onOpenSettings,
  onOpenAudit,
  onChangePassword,
}: {
  onOpenAdmin: () => void;
  onOpenSettings: () => void;
  onOpenAudit: () => void;
  onChangePassword: () => void;
}) {
  const { user, signOut } = useAuth();

  // localStorage holds the refresh token used for app handoff.
  const refreshToken = localStorage.getItem("qms.refresh");

  // Only tiles the user is entitled to (qms_app claim) appear.
  const entitled = APP_REGISTRY.filter((app) => user?.apps.includes(app.key));

  const firstName = user?.name?.split(" ")[0] ?? "";
  const isAdmin = user?.roles.includes("Admin") ?? false;
  const roleCount = user?.roles.length ?? 0;

  return (
    <div className="frame">
      <div className="page">
        <BrandHeader subtitle="Quality &amp; Compliance Suite" />

        <div className="launcher-head">
          <p className="greeting">
            Welcome back, <span className="accent">{firstName}</span>
          </p>
          <div className="head-actions">
            {isAdmin && (
              <>
                <button className="btn-small" onClick={onOpenAdmin}>Users</button>
                <button className="btn-small" onClick={onOpenSettings}>Security</button>
                <button className="btn-small" onClick={onOpenAudit}>Audit</button>
              </>
            )}
            <button className="btn-small" onClick={onChangePassword}>Password</button>
            <button className="signout" onClick={signOut}>Sign out</button>
          </div>
        </div>

        <div className="insight-strip">
          <span className="insight-icon" aria-hidden="true">◇</span>
          <span className="insight-text">
            {entitled.length > 0 ? (
              <>
                <b>You&rsquo;re all set.</b>{" "}
                <span className="muted">
                  {entitled.length} app{entitled.length === 1 ? "" : "s"} provisioned to your
                  account and ready to launch — your session is encrypted end to end.
                </span>
              </>
            ) : (
              <>
                <b>Awaiting access.</b>{" "}
                <span className="muted">No apps are provisioned to your account yet.</span>
              </>
            )}
          </span>
        </div>

        <div className="stat-row">
          <div className="stat">
            <div className="stat-value">{entitled.length}</div>
            <div className="stat-label">Apps ready</div>
          </div>
          <div className="stat">
            <div className="stat-value violet">{roleCount}</div>
            <div className="stat-label">Access roles</div>
          </div>
          <div className="stat">
            <div className="stat-value cyan">Secure</div>
            <div className="stat-label">Session state</div>
          </div>
        </div>

        {entitled.length === 0 ? (
          <div className="empty-note">
            You don&rsquo;t have access to any applications yet. Contact your
            administrator to be granted access.
          </div>
        ) : (
          <div className="tiles">
            {entitled.map((app) => {
              const spark = sparkPaths(app.key);
              return (
                <button
                  key={app.key}
                  className="tile"
                  onClick={() => launchApp(app, refreshToken)}
                  aria-label={`Open ${app.name} — ${app.tagline}`}
                >
                  <div className="tile-top">
                    <span className="tile-glyph" aria-hidden="true">{app.glyph}</span>
                    <span className="tile-status">
                      <span className="led" aria-hidden="true" />
                      Ready
                    </span>
                  </div>
                  <h2 className="tile-name">{app.name}</h2>
                  <p className="tile-tagline">{app.tagline}</p>
                  <svg className="tile-spark" viewBox="0 0 100 30" preserveAspectRatio="none" aria-hidden="true">
                    <path className="fill" d={spark.fill} />
                    <path d={spark.line} />
                  </svg>
                  <span className="tile-launch">Launch &rarr;</span>
                </button>
              );
            })}
          </div>
        )}
      </div>
    </div>
  );
}