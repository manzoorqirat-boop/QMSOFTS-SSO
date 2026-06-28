import { useState, useEffect, useCallback } from "react";
import { useAuth } from "../lib/AuthContext";
import * as api from "../lib/identityApi";
import type { UserDetail, AppGrant } from "../lib/identityApi";
import { ROLES, APPS, USER_STATUSES, APP_ROLES } from "../lib/constants";
import { BrandHeader } from "./BrandHeader";

export function UserAdmin({ onBack }: { onBack: () => void }) {
  const { accessToken } = useAuth();
  const [users, setUsers] = useState<UserDetail[]>([]);
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
          <button className="signout" onClick={onBack}>&larr; Back</button>
          <button className="btn-small" onClick={() => setShowCreate((v) => !v)}>
            {showCreate ? "Close" : "+ New User"}
          </button>
        </div>

        {showCreate && accessToken && (
          <CreateUserForm
            token={accessToken}
            onCreated={() => { setShowCreate(false); load(); }}
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

// Chips for global roles + per-app access with role pickers.
function AppAccessEditor({
  apps,
  setApps,
}: {
  apps: AppGrant[];
  setApps: (a: AppGrant[]) => void;
}) {
  const has = (key: string) => apps.find((a) => a.appKey === key);
  const toggleApp = (key: string) => {
    if (has(key)) setApps(apps.filter((a) => a.appKey !== key));
    else setApps([...apps, { appKey: key, role: (APP_ROLES[key]?.[0] ?? null) }]);
  };
  const setRole = (key: string, role: string) =>
    setApps(apps.map((a) => (a.appKey === key ? { ...a, role } : a)));

  return (
    <div className="app-access">
      {APPS.map((a) => {
        const grant = has(a.key);
        return (
          <div key={a.key} className="app-access-row">
            <button
              type="button"
              className={`chip ${grant ? "chip-on" : ""}`}
              onClick={() => toggleApp(a.key)}
            >
              {a.label}
            </button>
            {grant && APP_ROLES[a.key] && (
              <select
                className="role-select"
                value={grant.role ?? ""}
                onChange={(e) => setRole(a.key, e.target.value)}
              >
                {APP_ROLES[a.key].map((r) => (
                  <option key={r} value={r}>{r}</option>
                ))}
              </select>
            )}
          </div>
        );
      })}
    </div>
  );
}

function CreateUserForm({ token, onCreated }: { token: string; onCreated: () => void }) {
  const [email, setEmail] = useState("");
  const [name, setName] = useState("");
  const [employeeId, setEmployeeId] = useState("");
  const [password, setPassword] = useState("");
  const [roles, setRoles] = useState<string[]>(["User"]);
  const [apps, setApps] = useState<AppGrant[]>([]);
  const [busy, setBusy] = useState(false);
  const [err, setErr] = useState<string | null>(null);

  const toggleRole = (r: string) =>
    setRoles(roles.includes(r) ? roles.filter((i) => i !== r) : [...roles, r]);

  const submit = async () => {
    setErr(null);
    if (!email || !name || password.length < 8) {
      setErr("Email, name, and a password of at least 8 characters are required.");
      return;
    }
    setBusy(true);
    try {
      await api.createUser(token, { email, name, employeeId, password, roles, apps });
      onCreated();
    } catch (e) {
      setErr(e instanceof Error ? e.message : "Failed to create user.");
    } finally {
      setBusy(false);
    }
  };

  return (
    <div className="admin-card">
      <div className="field"><label>Email</label>
        <input value={email} onChange={(e) => setEmail(e.target.value)} type="email" /></div>
      <div className="field"><label>Name</label>
        <input value={name} onChange={(e) => setName(e.target.value)} /></div>
      <div className="field"><label>Employee ID (optional)</label>
        <input value={employeeId} onChange={(e) => setEmployeeId(e.target.value)} /></div>
      <div className="field"><label>Temporary password</label>
        <input value={password} onChange={(e) => setPassword(e.target.value)} type="password" />
        <p className="field-hint">User must change it at first login.</p></div>

      <div className="field"><label>Global roles</label>
        <div className="chip-row">
          {ROLES.map((r) => (
            <button key={r} type="button"
              className={`chip ${roles.includes(r) ? "chip-on" : ""}`}
              onClick={() => toggleRole(r)}>{r}</button>
          ))}
        </div>
      </div>

      <div className="field"><label>App access &amp; role</label>
        <AppAccessEditor apps={apps} setApps={setApps} />
      </div>

      {err && <div className="error-note">{err}</div>}
      <button className="btn-primary" onClick={submit} disabled={busy}>
        {busy ? "Creating…" : "Create user"}
      </button>
    </div>
  );
}

function UserRow({ user, token, onSaved }: { user: UserDetail; token: string; onSaved: () => void }) {
  const [editing, setEditing] = useState(false);
  const [name, setName] = useState(user.name);
  const [employeeId, setEmployeeId] = useState(user.employeeId);
  const [roles, setRoles] = useState<string[]>(user.roles);
  const [apps, setApps] = useState<AppGrant[]>(user.apps);
  const [status, setStatus] = useState(user.status);
  const [busy, setBusy] = useState(false);
  const [err, setErr] = useState<string | null>(null);

  const toggleRole = (r: string) =>
    setRoles(roles.includes(r) ? roles.filter((i) => i !== r) : [...roles, r]);

  const act = async (fn: () => Promise<unknown>) => {
    setBusy(true); setErr(null);
    try { await fn(); onSaved(); }
    catch (e) { setErr(e instanceof Error ? e.message : "Action failed."); }
    finally { setBusy(false); }
  };

  const save = () => act(() =>
    api.updateUser(token, user.id, { name, employeeId, roles, apps, status }));

  return (
    <div className="user-row">
      <div className="user-row-head">
        <div>
          <div className="user-name">
            {user.name}
            {user.status !== "Active" && <span className="status-pill off">{user.status}</span>}
            {user.isLocked && <span className="status-pill lock">Locked</span>}
            {user.hasActiveSession && <span className="status-pill on">Online</span>}
          </div>
          <div className="user-email">{user.email}{user.employeeId ? ` · ${user.employeeId}` : ""}</div>
        </div>
        <button className="btn-small" onClick={() => setEditing((v) => !v)}>
          {editing ? "Cancel" : "Edit"}
        </button>
      </div>

      {!editing ? (
        <>
          <div className="user-tags">
            {user.roles.map((r) => <span key={r} className="tag tag-role">{r}</span>)}
            {user.apps.map((a) => (
              <span key={a.appKey} className="tag tag-app">
                {a.appKey}{a.role ? `: ${a.role}` : ""}
              </span>
            ))}
          </div>
          <div className="row-actions">
            {user.isLocked && (
              <button className="btn-tiny" disabled={busy}
                onClick={() => act(() => api.unlockUser(token, user.id))}>Unlock</button>
            )}
            {user.hasActiveSession && (
              <button className="btn-tiny" disabled={busy}
                onClick={() => act(() => api.forceLogoutUser(token, user.id))}>Force logout</button>
            )}
            {user.status === "Active" ? (
              <button className="btn-tiny danger" disabled={busy}
                onClick={() => act(() => api.changeUserStatus(token, user.id, "Inactive"))}>Deactivate</button>
            ) : (
              <button className="btn-tiny" disabled={busy}
                onClick={() => act(() => api.changeUserStatus(token, user.id, "Active"))}>Reactivate</button>
            )}
          </div>
        </>
      ) : (
        <div className="user-edit">
          <div className="field"><label>Name</label>
            <input value={name} onChange={(e) => setName(e.target.value)} /></div>
          <div className="field"><label>Employee ID</label>
            <input value={employeeId} onChange={(e) => setEmployeeId(e.target.value)} /></div>
          <div className="field"><label>Status</label>
            <select className="role-select" value={status} onChange={(e) => setStatus(e.target.value)}>
              {USER_STATUSES.map((s) => <option key={s} value={s}>{s}</option>)}
            </select></div>
          <div className="field"><label>Global roles</label>
            <div className="chip-row">
              {ROLES.map((r) => (
                <button key={r} type="button"
                  className={`chip ${roles.includes(r) ? "chip-on" : ""}`}
                  onClick={() => toggleRole(r)}>{r}</button>
              ))}
            </div></div>
          <div className="field"><label>App access &amp; role</label>
            <AppAccessEditor apps={apps} setApps={setApps} /></div>
          {err && <div className="error-note">{err}</div>}
          <button className="btn-primary" onClick={save} disabled={busy}>
            {busy ? "Saving…" : "Save changes"}
          </button>
        </div>
      )}
      {err && !editing && <div className="error-note">{err}</div>}
    </div>
  );
}