import { useState } from "react";
import { useAuth } from "../lib/AuthContext";
import * as api from "../lib/identityApi";
import { BrandHeader } from "./BrandHeader";

export function LoginScreen() {
  const { signIn, error } = useAuth();
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
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
          <BrandHeader subtitle="Active Session Found" />
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
          <div className="field">
            <label htmlFor="email">Email</label>
            <input id="email" type="email" autoComplete="username" value={email}
              onChange={(e) => setEmail(e.target.value)}
              onKeyDown={(e) => e.key === "Enter" && attempt()} />
          </div>
          <div className="field">
            <label htmlFor="password">Password</label>
            <input id="password" type="password" autoComplete="current-password" value={password}
              onChange={(e) => setPassword(e.target.value)}
              onKeyDown={(e) => e.key === "Enter" && attempt()} />
          </div>
          <button className="btn-primary" onClick={() => attempt()} disabled={!canSubmit}>
            {busy ? "Signing in…" : "Sign in"}
          </button>
          {(error || localError) && <div className="error-note">{localError ?? error}</div>}
        </div>
      </div>
    </div>
  );
}