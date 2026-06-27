import type { TokenResponse } from "../types";

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
