import { useState, useEffect, useCallback } from "react";
import { useAuth } from "../lib/AuthContext";
import * as api from "../lib/identityApi";
import { SETTING_FIELDS } from "../lib/constants";
import { BrandHeader } from "./BrandHeader";

export function SecuritySettings({ onBack }: { onBack: () => void }) {
  const { accessToken } = useAuth();
  const [values, setValues] = useState<Record<string, string>>({});
  const [loading, setLoading] = useState(true);
  const [savingKey, setSavingKey] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [savedKey, setSavedKey] = useState<string | null>(null);

  const load = useCallback(async () => {
    if (!accessToken) return;
    setLoading(true);
    try {
      setValues(await api.getSettings(accessToken));
    } catch (e) {
      setError(e instanceof Error ? e.message : "Failed to load settings.");
    } finally {
      setLoading(false);
    }
  }, [accessToken]);

  useEffect(() => { load(); }, [load]);

  const save = async (key: string, value: string) => {
    if (!accessToken) return;
    setSavingKey(key);
    setError(null);
    try {
      const updated = await api.updateSetting(accessToken, key, value);
      setValues(updated);
      setSavedKey(key);
      setTimeout(() => setSavedKey(null), 1500);
    } catch (e) {
      setError(e instanceof Error ? e.message : "Failed to save.");
    } finally {
      setSavingKey(null);
    }
  };

  return (
    <div className="frame">
      <div className="page">
        <BrandHeader subtitle="Security Policy" />
        <div className="launcher-head">
          <button className="signout" onClick={onBack}>&larr; Back</button>
        </div>

        {error && <div className="error-note">{error}</div>}

        {loading ? (
          <p className="empty-note">Loading settings&hellip;</p>
        ) : (
          <div className="settings-list">
            {SETTING_FIELDS.map((f) => (
              <div key={f.key} className="setting-row">
                <div className="setting-label">
                  <div className="setting-title">{f.label}</div>
                  <div className="setting-help">{f.help}</div>
                </div>
                <div className="setting-input">
                  {f.type === "yesno" ? (
                    <select
                      className="role-select"
                      value={values[f.key] ?? "Yes"}
                      onChange={(e) => save(f.key, e.target.value)}
                      disabled={savingKey === f.key}
                    >
                      <option value="Yes">Yes</option>
                      <option value="No">No</option>
                    </select>
                  ) : (
                    <input
                      type="number"
                      value={values[f.key] ?? ""}
                      onChange={(e) =>
                        setValues({ ...values, [f.key]: e.target.value })}
                      onBlur={(e) => save(f.key, e.target.value)}
                      disabled={savingKey === f.key}
                    />
                  )}
                  {savedKey === f.key && <span className="saved-tick">Saved</span>}
                </div>
              </div>
            ))}
          </div>
        )}
      </div>
    </div>
  );
}