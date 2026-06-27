# QMSofts Platform

The shared backbone of the QMSofts suite. One login, one user directory, one
auth-audit trail; each product (Parakh, ERES, future modules) stays an
independently deployable app that trusts tokens this platform issues.

This repository contains three of the four foundation pieces:

| Folder | What it is | Deploys as |
|---|---|---|
| `src/QMSofts.Shared` | Shared contract (claim types, JWT validation, audit + DTO models). Referenced by the backend; copied into apps at integration. | (library, not deployed) |
| `src/QMSofts.Identity` | The auth service: login/refresh, user admin, JWKS, append-only auth audit, RSA-signed JWTs. | Railway service #1 (.NET 8 / Postgres) |
| `shell/qmsofts-shell` | The front door: login + tile launcher, branded. Tiles render per the user's app entitlements and hand the session off to each app. | Railway service #2 (static / nginx) |

> Parakh and ERES live in their own repos and are **not** changed yet. Their
> integration (auth swap, shadow-user sync, per-app permission map) happens later
> against their real code.

## Repository layout

```
qmsofts-platform/
├── QMSofts.Platform.sln
├── docs/README-foundation.md          ← backend deploy guide + env vars
├── src/
│   ├── QMSofts.Shared/                 ← shared contract library
│   └── QMSofts.Identity/               ← auth service (has its own Dockerfile)
└── shell/qmsofts-shell/                ← shell SPA (has its own Dockerfile)
```

## Push to GitHub

This is a single monorepo. From a clone (or via github.dev upload), commit the
whole tree as-is. The two `.gitignore` files (root + shell) keep build output,
`node_modules`, `.env`, and any `*.pem` signing keys out of the repo.

```
git init
git add .
git commit -m "QMSofts platform foundation: Shared + Identity + Shell"
git branch -M main
git remote add origin <your-repo-url>
git push -u origin main
```

## Deploy on Railway (two services, one repo)

Create a Postgres database, then two services from this same repo:

**Service 1 — Identity (.NET 8)**
- Root Directory: repo root
- Dockerfile path: `src/QMSofts.Identity/Dockerfile`
- Env vars: see `docs/README-foundation.md` (DATABASE_URL, QmsAuth__Authority,
  QmsAuth__SigningKeyPem, QmsAuth__AllowedOrigins, bootstrap admin, …)

**Service 2 — Shell (static / nginx)**
- Root Directory: `shell/qmsofts-shell`
- Uses that folder's Dockerfile
- Build vars: `VITE_IDENTITY_URL`, `VITE_PARAKH_URL`, `VITE_ERES_URL`
- After deploy: add the Shell's origin to Identity's `QmsAuth__AllowedOrigins`.

## Suggested bring-up order

1. Deploy Postgres + Identity. Hit `/health` and `/.well-known/jwks.json`.
2. `POST /api/auth/login` with the bootstrap admin → confirm a token comes back.
3. Deploy the Shell. Log in, confirm the tiles render.
4. (Later) Wire Parakh, then ERES, one at a time.

## A note on the two stacks

The Shell was built and verified in this environment (TypeScript compile +
production build pass). The .NET services are structured to be compiled by
Railway's Docker build — that build is the compile check, matching the normal
github.dev → Railway workflow. The EF migration is included so the database
schema is created on first boot without needing the `dotnet ef` CLI.
