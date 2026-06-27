import { useState, useEffect, useCallback } from "react";
import { useAuth } from "../lib/AuthContext";
import * as api from "../lib/identityApi";
import { ROLES, APPS } from "../lib/constants";
import type { UserProfile } from "../types";
import { BrandHeader } from "./BrandHeader";

export function UserAdmin({ onBack }: { onBack: () => void }) {
  const { accessToken } = useAuth();
  const [users, setUsers] = useState<UserProfile[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [showCreate, setShowCreate] = useState(false);

  const load = useCallback(async () => {
    if (!accessToken) return;
    setLoading(true);
    setError(null);
    try {
      setUsers(await api.listUsers(accessToken));
    } catch (e) {
      setError(e instanceof Error ? e.message : "Failed to load users.");
    } finally {
      setLoading(false);
    }
  }, [accessToken]);

  useEffect(() => {
    load();
  }, [load]);

  return (
    <div className="frame">
      <div className="page">
        <BrandHeader subtitle="User Administration" />

        <div className="launcher-head">
          <button className="signout" onClick={onBack}>
            &larr; Back
          </button>
          <button className="btn-small" onClick={() => setShowCreate((v) => !v)}>
            {showCreate ? "Close" : "+ New User"}
          </button>
        </div>

        {showCreate && (
          <CreateUserForm
            token={accessToken!}
            onCreated={() => {
              setShowCreate(false);
              load();
            }}
          />
        )}

        {error && <div className="error-note">{error}</div>}

        {loading ? (
          <p className="empty-note">Loading users&hellip;</p>
        ) : users.length === 0 ? (
          <p className="empty-note">No users yet.</p>
        ) : (
          <div className="user-list">
            {users.map((u) => (
              <UserRow key={u.id} user={u} token={accessToken!} onSaved={load} />
            ))}
          </div>
        )}
      </div>
    </div>
  );
}

function CreateUserForm({
  token,
  onCreated,
}: {
  token: string;
  onCreated: () => void;
}) {
  const [email, setEmail] = useState("");
  const [name, setName] = useState("");
  const [password, setPassword] = useState("");
  const [roles, setRoles] = useState<string[]>(["User"]);
  const [apps, setApps] = useState<string[]>([]);
  const [busy, setBusy] = useState(false);
  const [err, setErr] = useState<string | null>(null);

  const toggle = (list: string[], v: string, set: (x: string[]) => void) =>
    set(list.includes(v) ? list.filter((i) => i !== v) : [...list, v]);

  const submit = async () => {
    setErr(null);
    if (!email || !name || password.length < 12) {
      setErr("Email, name, and a password of at least 12 characters are required.");
      return;
    }
    setBusy(true);
    try {
      await api.createUser(token, { email, name, password, roles, apps });
      onCreated();
    } catch (e) {
      setErr(e instanceof Error ? e.message : "Failed to create user.");
    } finally {
      setBusy(false);
    }
  };

  return (
    <div className="admin-card">
      <div className="field">
        <label>Email</label>
        <input value={email} onChange={(e) => setEmail(e.target.value)} type="email" />
      </div>
      <div className="field">
        <label>Name</label>
        <input value={name} onChange={(e) => setName(e.target.value)} />
      </div>
      <div className="field">
        <label>Password (min 12 chars)</label>
        <input
          value={password}
          onChange={(e) => setPassword(e.target.value)}
          type="password"
        />
      </div>

      <div className="field">
        <label>Roles</label>
        <div className="chip-row">
          {ROLES.map((r) => (
            <button
              key={r}
              type="button"
              className={`chip ${roles.includes(r) ? "chip-on" : ""}`}
              onClick={() => toggle(roles, r, setRoles)}
            >
              {r}
            </button>
          ))}
        </div>
      </div>

      <div className="field">
        <label>App Access</label>
        <div className="chip-row">
          {APPS.map((a) => (
            <button
              key={a.key}
              type="button"
              className={`chip ${apps.includes(a.key) ? "chip-on" : ""}`}
              onClick={() => toggle(apps, a.key, setApps)}
            >
              {a.label}
            </button>
          ))}
        </div>
      </div>

      {err && <div className="error-note">{err}</div>}
      <button className="btn-primary" onClick={submit} disabled={busy}>
        {busy ? "Creating&hellip;" : "Create user"}
      </button>
    </div>
  );
}

function UserRow({
  user,
  token,
  onSaved,
}: {
  user: UserProfile;
  token: string;
  onSaved: () => void;
}) {
  const [editing, setEditing] = useState(false);
  const [roles, setRoles] = useState<string[]>(user.roles);
  const [apps, setApps] = useState<string[]>(user.apps);
  const [busy, setBusy] = useState(false);

  const toggle = (list: string[], v: string, set: (x: string[]) => void) =>
    set(list.includes(v) ? list.filter((i) => i !== v) : [...list, v]);

  const save = async () => {
    setBusy(true);
    try {
      await api.updateUser(token, user.id, {
        name: user.name,
        roles,
        apps,
        isActive: true,
      });
      setEditing(false);
      onSaved();
    } catch {
      // surfaced by parent reload
    } finally {
      setBusy(false);
    }
  };

  return (
    <div className="user-row">
      <div className="user-row-head">
        <div>
          <div className="user-name">{user.name}</div>
          <div className="user-email">{user.email}</div>
        </div>
        <button className="btn-small" onClick={() => setEditing((v) => !v)}>
          {editing ? "Cancel" : "Edit"}
        </button>
      </div>

      {!editing ? (
        <div className="user-tags">
          {user.roles.map((r) => (
            <span key={r} className="tag tag-role">
              {r}
            </span>
          ))}
          {user.apps.map((a) => (
            <span key={a} className="tag tag-app">
              {a}
            </span>
          ))}
        </div>
      ) : (
        <div className="user-edit">
          <div className="chip-row">
            {ROLES.map((r) => (
              <button
                key={r}
                className={`chip ${roles.includes(r) ? "chip-on" : ""}`}
                onClick={() => toggle(roles, r, setRoles)}
              >
                {r}
              </button>
            ))}
          </div>
          <div className="chip-row">
            {APPS.map((a) => (
              <button
                key={a.key}
                className={`chip ${apps.includes(a.key) ? "chip-on" : ""}`}
                onClick={() => toggle(apps, a.key, setApps)}
              >
                {a.label}
              </button>
            ))}
          </div>
          <button className="btn-primary" onClick={save} disabled={busy}>
            {busy ? "Saving&hellip;" : "Save changes"}
          </button>
        </div>
      )}
    </div>
  );
}