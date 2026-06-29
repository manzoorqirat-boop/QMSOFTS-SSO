import { useState } from "react";
import { useAuth } from "../lib/AuthContext";
import * as api from "../lib/identityApi";
import { BrandHeader } from "./BrandHeader";

function AuditFootnote() {
  return (
    <div className="secure-foot">
      <svg width="16" height="16" viewBox="0 0 24 24" fill="none" aria-hidden="true">
        <rect x="4" y="10" width="16" height="11" rx="2" stroke="currentColor" strokeWidth="2" />
        <path d="M8 10V7a4 4 0 0 1 8 0v3" stroke="currentColor" strokeWidth="2" />
      </svg>
      All access attempts are logged to the immutable audit trail.
    </div>
  );
}

export function LoginScreen() {
  const { signIn, error } = useAuth();
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [showPw, setShowPw] = useState(false);
  const [busy, setBusy] = useState(false);
  const [sessionPrompt, setSessionPrompt] = useState<{ ip: string | null } | null>(null);
  const [localError, setLocalError] = useState<string | null>(null);

  const canSubmit = email.trim().length > 0 && password.length > 0 && !busy;

  async function attempt(decision?: "replace" | "logoutAll") {
    if (!email.trim() || !password) return;
    setBusy(true);
    setLocalError(null);
    try {
      const result = await signIn(email.trim(), password, decision);
      if (result.loggedOut) {
        setSessionPrompt(null);
        setLocalError("Previous sessions were logged out. Please sign in again.");
        setPassword("");
      }
      // success → Gate re-renders to launcher / forced-change
    } catch (e) {
      if (api.isSessionDecisionError(e)) {
        setSessionPrompt({ ip: e.data.activeSessionIp ?? null });
      }
      // other errors surface via context `error`
    } finally {
      setBusy(false);
    }
  }

  if (sessionPrompt) {
    return (
      <div className="frame">
        <div className="page">
          <BrandHeader subtitle="Quality &amp; Compliance Suite" />
          <div className="login">
            <p className="forced-note">
              You are already signed in
              {sessionPrompt.ip ? ` from ${sessionPrompt.ip}` : ""}. What would you
              like to do?
            </p>
            <button className="btn-primary" disabled={busy}
              onClick={() => attempt("replace")}>
              {busy ? "Working…" : "Sign out the other session & continue"}
            </button>
            <button className="signout full" disabled={busy}
              onClick={() => attempt("logoutAll")}>
              Log out everywhere (don't sign in)
            </button>
            <button className="signout full" disabled={busy}
              onClick={() => { setSessionPrompt(null); setPassword(""); }}>
              Cancel
            </button>
          </div>
        </div>
      </div>
    );
  }

  return (
    <div className="frame">
      <div className="page">
        <BrandHeader subtitle="Quality &amp; Compliance Suite" />
        <div className="login">
          <div className="login-intro">
            <p className="login-eyebrow">Secure access</p>
            <h2 className="login-title">Sign in to your account</h2>
            <p className="login-desc">
              Authenticate to reach every application you&rsquo;re entitled to —
              one identity across the compliance suite.
            </p>
          </div>

          <div className="field">
            <label htmlFor="email">Email</label>
            <input id="email" type="email" autoComplete="username" value={email}
              placeholder="you@company.com"
              onChange={(e) => setEmail(e.target.value)}
              onKeyDown={(e) => e.key === "Enter" && attempt()} />
          </div>

          <div className="field">
            <label htmlFor="password">Password</label>
            <div className="field-pw">
              <input id="password" type={showPw ? "text" : "password"}
                autoComplete="current-password" value={password}
                placeholder="Enter your password"
                onChange={(e) => setPassword(e.target.value)}
                onKeyDown={(e) => e.key === "Enter" && attempt()} />
              <button type="button" className="pw-toggle"
                onClick={() => setShowPw((s) => !s)}
                aria-label={showPw ? "Hide password" : "Show password"}>
                {showPw ? "Hide" : "Show"}
              </button>
            </div>
          </div>

          <button className="btn-primary" onClick={() => attempt()} disabled={!canSubmit}>
            {busy ? "Signing in…" : "Sign in"}
            {!busy && (
              <svg width="18" height="18" viewBox="0 0 24 24" fill="none" aria-hidden="true">
                <path d="M5 12h14M13 6l6 6-6 6" stroke="currentColor"
                  strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" />
              </svg>
            )}
          </button>

          {(error || localError) && <div className="error-note">{localError ?? error}</div>}

          <AuditFootnote />
        </div>
      </div>
    </div>
  );
}