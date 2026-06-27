// Mirrors QmsRoles and QmsApps on the backend. Used to populate the
// user-admin form's role and app-access choices.

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
