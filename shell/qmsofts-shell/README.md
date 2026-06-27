# QMSofts Shell

The front door to the QMSofts suite: one login, then a tile launcher showing
only the apps a user is entitled to. Built with Vite + React + TypeScript,
served as static files by nginx. Carries the QMSofts manuscript/Qirat brand.

## How it fits

```
  Shell  --login--> QMSofts.Identity (returns JWT + refresh token)
    |
    +-- tiles (Parakh, ERES...) render from the user's qms_app entitlements
            |
            +-- click --> launches the app, passing the session in the URL #fragment
```

The Shell never talks to Parakh/ERES directly. It authenticates against
Identity, then hands the session to the target app, which exchanges it for its
own access token. Tiles for apps the user can't access simply don't render.

## Local dev

```
cp .env.example .env      # point VITE_IDENTITY_URL at a running Identity
npm install
npm run dev
```

## Environment (build-time)

Vite inlines VITE_* at build time, so these are BUILD variables, not runtime:

| Variable | Purpose |
|---|---|
| VITE_IDENTITY_URL | Base URL of QMSofts.Identity (login/refresh). |
| VITE_PARAKH_URL | Where the Parakh tile launches. |
| VITE_ERES_URL | Where the ERES tile launches. |

## Railway

- Service Root Directory: shell/qmsofts-shell
- Uses the included Dockerfile (Vite build then nginx).
- Set the VITE_* variables on the service as build variables (the Dockerfile
  declares matching ARGs and forwards them).
- nginx binds to Railway's injected PORT via nginx.conf.template (only ${PORT}
  is substituted; nginx's own $uri is preserved via NGINX_ENVSUBST_FILTER).
- After deploy, add the Shell's origin to the Identity service's
  QmsAuth__AllowedOrigins so browser login calls pass CORS.

## Adding a new app tile later

Add one entry to src/lib/appRegistry.ts (key, name, tagline, env URL, glyph)
and grant users the matching qms_app entitlement in Identity. No other change.

## Security notes

- Only the refresh token is persisted (localStorage); the access token stays in
  memory and is refreshed silently ~60s before expiry.
- Handoff uses the URL fragment (#qms_handoff=...), which browsers never send to
  servers, keeping it out of access logs and Referer headers. The receiving app
  reads it, exchanges via /api/auth/refresh, then clears the fragment.
