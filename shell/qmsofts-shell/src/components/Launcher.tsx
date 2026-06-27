import { useAuth } from "../lib/AuthContext";
import { APP_REGISTRY } from "../lib/appRegistry";
import { launchApp } from "../lib/handoff";
import { BrandHeader } from "./BrandHeader";

export function Launcher({ onOpenAdmin }: { onOpenAdmin: () => void }) {
  const { user, signOut } = useAuth();

  // localStorage holds the refresh token used for app handoff.
  const refreshToken = localStorage.getItem("qms.refresh");

  // Only tiles the user is entitled to (qms_app claim) appear.
  const entitled = APP_REGISTRY.filter((app) => user?.apps.includes(app.key));

  const firstName = user?.name?.split(" ")[0] ?? "";
  const isAdmin = user?.roles.includes("Admin") ?? false;

  return (
    <div className="frame">
      <div className="page">
        <BrandHeader subtitle="Quality &amp; Compliance Suite" />

        <div className="launcher-head">
          <p className="greeting">
            Welcome, <span className="accent">{firstName}</span>
          </p>
          <div className="head-actions">
            {isAdmin && (
              <button className="btn-small" onClick={onOpenAdmin}>
                User Admin
              </button>
            )}
            <button className="signout" onClick={signOut}>
              Sign out
            </button>
          </div>
        </div>

        {entitled.length === 0 ? (
          <div className="empty-note">
            You don&rsquo;t have access to any applications yet. Contact your
            administrator to be granted access.
          </div>
        ) : (
          <div className="tiles">
            {entitled.map((app) => (
              <button
                key={app.key}
                className="tile"
                onClick={() => launchApp(app, refreshToken)}
                aria-label={`Open ${app.name} — ${app.tagline}`}
              >
                <span className="tile-star" aria-hidden="true" />
                <div className="tile-glyph" aria-hidden="true">
                  {app.glyph}
                </div>
                <h2 className="tile-name">{app.name}</h2>
                <p className="tile-tagline">{app.tagline}</p>
              </button>
            ))}
          </div>
        )}
      </div>
    </div>
  );
}