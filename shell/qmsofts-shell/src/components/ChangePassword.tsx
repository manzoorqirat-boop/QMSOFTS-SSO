import { useState } from "react";
import { useAuth } from "../lib/AuthContext";
import * as api from "../lib/identityApi";
import { BrandHeader } from "./BrandHeader";

export function ChangePassword({
  forced,
  onDone,
  onCancel,
}: {
  forced?: boolean;
  onDone: () => void;
  onCancel?: () => void;
}) {
  const { accessToken } = useAuth();
  const [current, setCurrent] = useState("");
  const [next, setNext] = useState("");
  const [confirm, setConfirm] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const submit = async () => {
    setError(null);
    if (next !== confirm) {
      setError("New passwords do not match.");
      return;
    }
    if (!accessToken) return;
    setBusy(true);
    try {
      await api.changePassword(accessToken, current, next);
      onDone();
    } catch (e) {
      setError(e instanceof Error ? e.message : "Failed to change password.");
    } finally {
      setBusy(false);
    }
  };

  return (
    <div className="frame">
      <div className="page">
        <BrandHeader subtitle={forced ? "Password Change Required" : "Change Password"} />
        <div className="login">
          {forced && (
            <p className="forced-note">
              Your password must be changed before you can continue.
            </p>
          )}
          <div className="field">
            <label>Current password</label>
            <input type="password" value={current}
              onChange={(e) => setCurrent(e.target.value)} autoComplete="current-password" />
          </div>
          <div className="field">
            <label>New password</label>
            <input type="password" value={next}
              onChange={(e) => setNext(e.target.value)} autoComplete="new-password" />
          </div>
          <div className="field">
            <label>Confirm new password</label>
            <input type="password" value={confirm}
              onChange={(e) => setConfirm(e.target.value)}
              onKeyDown={(e) => e.key === "Enter" && submit()}
              autoComplete="new-password" />
          </div>
          <button className="btn-primary" onClick={submit} disabled={busy}>
            {busy ? "Saving…" : "Change password"}
          </button>
          {!forced && onCancel && (
            <button className="signout full" onClick={onCancel}>Cancel</button>
          )}
          {error && <div className="error-note">{error}</div>}
        </div>
      </div>
    </div>
  );
}