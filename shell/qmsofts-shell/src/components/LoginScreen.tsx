import { useState } from "react";
import { useAuth } from "../lib/AuthContext";
import { BrandHeader } from "./BrandHeader";

export function LoginScreen() {
  const { signIn, error } = useAuth();
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [busy, setBusy] = useState(false);

  const canSubmit = email.trim().length > 0 && password.length > 0 && !busy;

  async function submit() {
    if (!canSubmit) return;
    setBusy(true);
    try {
      await signIn(email.trim(), password);
    } catch {
      // error surfaced via context
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="frame">
      <div className="page">
        <BrandHeader subtitle="Quality &amp; Compliance Suite" />
        <div className="login">
          <div className="field">
            <label htmlFor="email">Email</label>
            <input
              id="email"
              type="email"
              autoComplete="username"
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              onKeyDown={(e) => e.key === "Enter" && submit()}
            />
          </div>
          <div className="field">
            <label htmlFor="password">Password</label>
            <input
              id="password"
              type="password"
              autoComplete="current-password"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              onKeyDown={(e) => e.key === "Enter" && submit()}
            />
          </div>
          <button
            className="btn-primary"
            onClick={submit}
            disabled={!canSubmit}
          >
            {busy ? "Signing in…" : "Sign in"}
          </button>
          {error && <div className="error-note">{error}</div>}
        </div>
      </div>
    </div>
  );
}
