import type { AppDefinition } from "../types";

// Launches a suite app (Parakh/ERES) from a tile, carrying the session so the
// user doesn't log in again. The refresh token is passed in the URL *fragment*
// (after #), which browsers never send to servers and which stays out of
// access logs and Referer headers. The target app reads it on load, calls
// Identity's /api/auth/refresh to get its own access token, then strips it
// from the URL.
//
// (When we wire Parakh/ERES, each app gets a tiny bootstrap that does exactly
// that exchange. Until then the tile still navigates; the app just won't find
// a session and will show its own login.)
export function launchApp(app: AppDefinition, refreshToken: string | null) {
  if (!app.url || app.url === "#") {
    console.warn(`No URL configured for app "${app.key}".`);
    return;
  }
  const target = new URL(app.url);
  if (refreshToken) {
    target.hash = `qms_handoff=${encodeURIComponent(refreshToken)}`;
  }
  window.location.assign(target.toString());
}
