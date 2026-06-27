import type { TokenResponse, UserProfile } from "../types";

// Base URL of the QMSofts.Identity service. Set VITE_IDENTITY_URL in env.
const IDENTITY_URL = (import.meta.env.VITE_IDENTITY_URL ?? "").replace(/\/$/, "");

if (!IDENTITY_URL && import.meta.env.PROD) {
  // Surface the misconfig loudly rather than failing silently with relative URLs.
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

export async function login(
  email: string,
  password: string
): Promise<TokenResponse> {
  const res = await fetch(`${IDENTITY_URL}/api/auth/login`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ email, password }),
  });
  if (!res.ok) throw new Error(await parseError(res));
  return res.json();
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

// --- User administration (Admin-only on the backend) ----------------------
// All calls send the access token as a Bearer credential.

export interface CreateUserPayload {
  email: string;
  name: string;
  password: string;
  roles: string[];
  apps: string[];
}

export interface UpdateUserPayload {
  name: string;
  roles: string[];
  apps: string[];
  isActive: boolean;
}

function authHeaders(token: string) {
  return {
    "Content-Type": "application/json",
    Authorization: `Bearer ${token}`,
  };
}

export async function listUsers(token: string): Promise<UserProfile[]> {
  const res = await fetch(`${IDENTITY_URL}/api/users`, {
    headers: authHeaders(token),
  });
  if (!res.ok) throw new Error(await parseError(res));
  return res.json();
}

export async function createUser(
  token: string,
  payload: CreateUserPayload
): Promise<UserProfile> {
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
): Promise<UserProfile> {
  const res = await fetch(`${IDENTITY_URL}/api/users/${id}`, {
    method: "PUT",
    headers: authHeaders(token),
    body: JSON.stringify(payload),
  });
  if (!res.ok) throw new Error(await parseError(res));
  return res.json();
}
