// Mirrors QMSofts.Shared.Contracts on the backend.

export interface UserProfile {
  id: string;
  email: string;
  name: string;
  roles: string[];
  apps: string[];
}

export interface TokenResponse {
  accessToken: string;
  refreshToken: string;
  expiresAt: string; // ISO
  user: UserProfile;
}

export interface AppDefinition {
  key: string; // matches qms_app entitlement, e.g. "parakh"
  name: string;
  tagline: string;
  url: string; // deployed app URL the tile launches
  glyph: string; // short calligraphic mark shown on the tile
}
