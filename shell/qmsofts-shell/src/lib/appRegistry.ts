import type { AppDefinition } from "../types";

// The suite registry. A tile renders only if the signed-in user holds the
// matching `qms_app` entitlement. URLs come from env so each environment
// (preview/prod) points tiles at the right deployed app.
//
// Set in Railway / .env:
//   VITE_PARAKH_URL=https://parakh.qmsofts.com
//   VITE_ERES_URL=https://eres.qmsofts.com
export const APP_REGISTRY: AppDefinition[] = [
  {
    key: "parakh",
    name: "Parakh",
    tagline: "Supplier Quality Management",
    url: import.meta.env.VITE_PARAKH_URL ?? "#",
    glyph: "P",
  },
  {
    key: "eres",
    name: "ERES Manager",
    tagline: "Electronic Records & Signatures",
    url: import.meta.env.VITE_ERES_URL ?? "#",
    glyph: "E",
  },
];
