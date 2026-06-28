// Mirrors QmsRoles and QmsApps on the backend (global roles + suite apps).

export const ROLES = [
  "Admin",
  "QualityManager",
  "Auditor",
  "Reviewer",
  "User",
] as const;

export const APPS = [
  { key: "parakh", label: "Parakh" },
  { key: "eres", label: "ERES Manager" },
] as const;

export const USER_STATUSES = ["Active", "Inactive", "Disabled"] as const;

// Per-app role choices. Identity stores the assignment; each app defines what
// the role can do via its own frontend privilege config. Edit freely.
export const APP_ROLES: Record<string, string[]> = {
  parakh: ["Admin", "QualityManager", "Auditor", "Reviewer", "User"],
  eres: ["Author", "Reviewer", "Approver", "Site Admin", "IT Admin", "Administrator"],
};

// Security settings metadata for the policy page (key → label, help, input).
export const SETTING_FIELDS: {
  key: string;
  label: string;
  help: string;
  type: "number" | "yesno";
}[] = [
  { key: "minPasswordLength", label: "Minimum password length", help: "Characters (min 8).", type: "number" },
  { key: "forceComplexity", label: "Require complexity", help: "Uppercase, number, special character.", type: "yesno" },
  { key: "passwordHistory", label: "Password history", help: "Cannot reuse last N passwords.", type: "number" },
  { key: "passwordExpiry", label: "Password expiry (days)", help: "0 disables expiry.", type: "number" },
  { key: "maxFailedAttempts", label: "Max failed attempts", help: "Before lockout.", type: "number" },
  { key: "lockoutDuration", label: "Lockout duration (min)", help: "How long an account stays locked.", type: "number" },
  { key: "sessionTimeout", label: "Session timeout (min)", help: "Token lifetime.", type: "number" },
];

// Maps backend AuthAuditEventType enum (int) → human label.
export const AUDIT_EVENT_LABELS: Record<number, string> = {
  1: "Login succeeded",
  2: "Login failed",
  3: "Logout",
  4: "Token refreshed",
  5: "Password changed",
  6: "Account locked",
  7: "Account unlocked",
  8: "Role granted",
  9: "Role revoked",
  10: "App access granted",
  11: "App access revoked",
  12: "User created",
  13: "User deactivated",
  14: "App entry denied",
  15: "Signature authentication",
  16: "Login blocked",
  17: "Session expired",
  18: "Session superseded",
  19: "Session replaced",
  20: "Logged out all sessions",
  21: "Concurrent login blocked",
  22: "Force-logout applied",
  23: "Account auto-unlocked",
  24: "Password expired",
  25: "Password change failed",
  26: "User reactivated",
  27: "Setting changed",
};
