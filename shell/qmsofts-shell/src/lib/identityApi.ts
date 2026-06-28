import type { TokenResponse } from "../types";

// Base URL of the QMSofts.Identity service. Set VITE_IDENTITY_URL in env.
const IDENTITY_URL = (import.meta.env.VITE_IDENTITY_URL ?? "").replace(/\/$/, "");

if (!IDENTITY_URL && import.meta.env.PROD) {
  console.error(
    "VITE_IDENTITY_URL is not set. Login and token refresh will fail. " +
      "Point it at the QMSofts.Identity service base URL."
  );
}

async function parseError(res: Response): Promise<string> {
  try {
    const body = await res.json();
    return body?.error ?? `Request failed (${res.status}).`;
  } catch {
    return `Request failed (${res.status}).`;
  }
}

function authHeaders(token: string) {
  return { "Content-Type": "application/json", Authorization: `Bearer ${token}` };
}

// ── Auth ──────────────────────────────────────────────────────────────────

export interface LoginResult extends TokenResponse {
  mustChangePassword?: boolean;
  reason?: string;
}

// Concurrent-session signal (HTTP 409) the caller may act on.
export class SessionDecisionRequired extends Error {
  activeSessionIp: string | null;
  constructor(activeSessionIp: string | null) {
    super("You are already logged in on another session.");
    // Restore the prototype chain — without this, `instanceof` fails when this
    // class (extending the built-in Error) is transpiled down by Vite/TS.
    Object.setPrototypeOf(this, SessionDecisionRequired.prototype);
    this.name = "SessionDecisionRequired";
    this.activeSessionIp = activeSessionIp;
  }
}

export async function login(
  email: string,
  password: string,
  sessionDecision?: "replace" | "logoutAll"
): Promise<LoginResult | { loggedOut: true }> {
  const res = await fetch(`${IDENTITY_URL}/api/auth/login`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ email, password, sessionDecision }),
  });

  // Read the body exactly once (a Response body can only be consumed one time).
  let body: any = null;
  try {
    body = await res.json();
  } catch {
    body = null;
  }

  // Concurrent session — backend returns 409 with requiresSessionDecision.
  if (res.status === 409 && body?.requiresSessionDecision) {
    throw new SessionDecisionRequired(body.activeSessionIp ?? null);
  }

  if (!res.ok) {
    throw new Error(body?.error ?? `Request failed (${res.status}).`);
  }

  if (body?.loggedOut) return { loggedOut: true };
  return body as LoginResult;
}

export async function refresh(refreshToken: string): Promise<TokenResponse> {
  const res = await fetch(`${IDENTITY_URL}/api/auth/refresh`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ refreshToken }),
  });
  if (!res.ok) throw new Error(await parseError(res));
  return res.json();
}

export async function logout(token: string): Promise<void> {
  await fetch(`${IDENTITY_URL}/api/auth/logout`, {
    method: "POST",
    headers: authHeaders(token),
  }).catch(() => {});
}

export async function changePassword(
  token: string,
  currentPassword: string,
  newPassword: string
): Promise<void> {
  const res = await fetch(`${IDENTITY_URL}/api/auth/change-password`, {
    method: "PUT",
    headers: authHeaders(token),
    body: JSON.stringify({ currentPassword, newPassword }),
  });
  if (!res.ok) throw new Error(await parseError(res));
}

// ── Users (Admin) ───────────────────────────────────────────────────────────

export interface AppGrant {
  appKey: string;
  role: string | null;
}

export interface UserDetail {
  id: string;
  email: string;
  name: string;
  employeeId: string;
  roles: string[];
  apps: AppGrant[];
  status: string; // Active | Inactive | Disabled
  mustChangePassword: boolean;
  isLocked: boolean;
  lockedUntil: string | null;
  hasActiveSession: boolean;
  passwordLastChanged: string;
  createdAt: string;
}

export interface CreateUserPayload {
  email: string;
  name: string;
  password: string;
  employeeId?: string;
  roles: string[];
  apps: AppGrant[];
}

export interface UpdateUserPayload {
  name: string;
  employeeId?: string;
  roles: string[];
  apps: AppGrant[];
  status: string;
}

export async function listUsers(token: string): Promise<UserDetail[]> {
  const res = await fetch(`${IDENTITY_URL}/api/users`, { headers: authHeaders(token) });
  if (!res.ok) throw new Error(await parseError(res));
  return res.json();
}

export async function createUser(token: string, payload: CreateUserPayload): Promise<UserDetail> {
  const res = await fetch(`${IDENTITY_URL}/api/users`, {
    method: "POST",
    headers: authHeaders(token),
    body: JSON.stringify(payload),
  });
  if (!res.ok) throw new Error(await parseError(res));
  return res.json();
}

export async function updateUser(
  token: string,
  id: string,
  payload: UpdateUserPayload
): Promise<UserDetail> {
  const res = await fetch(`${IDENTITY_URL}/api/users/${id}`, {
    method: "PUT",
    headers: authHeaders(token),
    body: JSON.stringify(payload),
  });
  if (!res.ok) throw new Error(await parseError(res));
  return res.json();
}

export async function changeUserStatus(token: string, id: string, status: string): Promise<UserDetail> {
  const res = await fetch(`${IDENTITY_URL}/api/users/${id}/status`, {
    method: "PUT",
    headers: authHeaders(token),
    body: JSON.stringify({ status }),
  });
  if (!res.ok) throw new Error(await parseError(res));
  return res.json();
}

export async function unlockUser(token: string, id: string): Promise<UserDetail> {
  const res = await fetch(`${IDENTITY_URL}/api/users/${id}/unlock`, {
    method: "PUT",
    headers: authHeaders(token),
  });
  if (!res.ok) throw new Error(await parseError(res));
  return res.json();
}

export async function forceLogoutUser(token: string, id: string): Promise<UserDetail> {
  const res = await fetch(`${IDENTITY_URL}/api/users/${id}/force-logout`, {
    method: "POST",
    headers: authHeaders(token),
  });
  if (!res.ok) throw new Error(await parseError(res));
  return res.json();
}

// ── Settings (security policy) ──────────────────────────────────────────────

export type SecuritySettings = Record<string, string>;

export async function getSettings(token: string): Promise<SecuritySettings> {
  const res = await fetch(`${IDENTITY_URL}/api/settings`, { headers: authHeaders(token) });
  if (!res.ok) throw new Error(await parseError(res));
  return res.json();
}

export async function updateSetting(
  token: string,
  key: string,
  value: string
): Promise<SecuritySettings> {
  const res = await fetch(`${IDENTITY_URL}/api/settings`, {
    method: "PUT",
    headers: authHeaders(token),
    body: JSON.stringify({ key, value }),
  });
  if (!res.ok) throw new Error(await parseError(res));
  return res.json();
}

// ── Audit ───────────────────────────────────────────────────────────────────

export interface AuditEvent {
  id: number;
  eventType: number;
  occurredAt: string;
  userId: string | null;
  identifier: string | null;
  appKey: string | null;
  ipAddress: string | null;
  detail: string | null;
}

export interface ChangeRecord {
  id: number;
  entityType: string;
  entityId: string;
  entityLabel: string;
  action: string;
  before: string | null;
  after: string | null;
  changedFields: string;
  changedBy: string;
  changedByRole: string;
  ipAddress: string | null;
  timestamp: string;
}

export async function getAuditEvents(token: string, limit = 200): Promise<AuditEvent[]> {
  const res = await fetch(`${IDENTITY_URL}/api/audit?limit=${limit}`, { headers: authHeaders(token) });
  if (!res.ok) throw new Error(await parseError(res));
  return res.json();
}

export async function getChangeHistory(token: string, limit = 200): Promise<ChangeRecord[]> {
  const res = await fetch(`${IDENTITY_URL}/api/audit/changes?limit=${limit}`, {
    headers: authHeaders(token),
  });
  if (!res.ok) throw new Error(await parseError(res));
  return res.json();
}
