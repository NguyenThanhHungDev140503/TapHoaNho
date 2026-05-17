# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Communication

- User prefers Vietnamese for explanations and discussions

## Commands

### Frontend (`frontend/`)
```bash
yarn dev          # Dev server (port 5173)
yarn build        # tsc -b && vite build
yarn lint         # ESLint
```

### Backend (`RetailStoreManagement/`)
```bash
dotnet run --project src/WebApi                          # WebApi (port 5175)
dotnet run --project src/IdentityServer                  # IdentityServer (port 5001)
dotnet watch run --launch-profile http                   # Hot reload (from src/WebApi/)
dotnet build                                             # Build solution
dotnet test                                              # Run all tests
dotnet ef migrations add <Name> --project src/Infrastructure --startup-project src/WebApi
dotnet ef database update --project src/Infrastructure --startup-project src/WebApi
```

### Dev environment (devenv/Nix)
```bash
devenv up         # Start frontend + backend (both processes)
setup             # Install all dependencies
build-all         # Build frontend + backend in parallel
db-check          # Check Neon PostgreSQL connectivity
```

## Architecture

### Two-Service Backend

The backend is split into two ASP.NET Core projects targeting .NET 10:

- **`src/IdentityServer`** (port 5001): Duende IdentityServer 7. Issues OAuth 2.1 tokens. Uses in-memory config (`Config.cs`). Shares `ApplicationDbContext` from Infrastructure for user data via `CustomProfileService`.
- **`src/WebApi`** (port 5175): Resource API. Validates DPoP-bound tokens issued by IdentityServer using `Duende.AspNetCore.Authentication.JwtBearer.DPoP`. Does not issue any tokens itself.

The four library projects follow Clean Architecture with dependency direction: `WebApi/IdentityServer → Application → Domain` and `WebApi → Infrastructure → Domain/Application`.

### Backend Layer Responsibilities

| Project | Responsibility |
|---------|---------------|
| `Domain` | Entities, value objects, `BaseEntity<TKey>` with soft-delete |
| `Application` | MediatR commands/queries, FluentValidation, AutoMapper profiles |
| `Infrastructure` | EF Core + Npgsql, generic `Repository<T>`, `UnitOfWork`, BCrypt |
| `WebApi` | Controllers, DI wiring, DPoP/JWT middleware, Swagger |
| `IdentityServer` | OAuth 2.1 auth server, PKCE, DPoP token binding |

### Authentication Flow (DPoP)

```
Browser → IdentityServer /connect/authorize (Auth Code + PKCE)
       ← auth_code

Browser → IdentityServer /connect/token (+ DPoP proof, code_verifier)
       ← DPoP-bound access_token (cnf.jkt claim), refresh_token

Browser → WebApi /api/... (Authorization: DPoP <token>, DPoP: <proof>)
       ← data
```

The frontend uses `oidc-client-ts` with `IndexedDbDPoPStore`. The library generates a P-256 ECDSA non-extractable keypair on first login and persists it in IndexedDB (`database:"oidc"`, `store:"dpop"`). The same keypair is used by the axios interceptor (`src/lib/oidc/dpop.ts`) to sign per-request DPoP proofs so `cnf.jkt` binding holds end-to-end.

Two IdentityServer clients exist:
- `react-dpop`: Production SPA client. `RequireDPoP = true`. DPoP-bound tokens only.
- `swagger-ui`: Dev-only testing client. `RequireDPoP = false`. Registered only when `isDevelopment()` is true.

In Development, `AllowBearerTokens = true` on the WebApi side so Swagger UI can test with plain Bearer. In Production both gates close: `swagger-ui` client doesn't exist AND `AllowBearerTokens = false`.

### Frontend Architecture

Feature-Sliced Design (FSD). Each feature under `src/features/<name>/` has `pages/`, `components/`, `hooks/`, and sometimes `store/`.

**Generic CRUD pattern** — most admin pages are driven by a `GenericPageConfig` object passed to `<GenericPage>`. Config declares columns, form fields, API service, and feature flags. No per-entity boilerplate needed.

**Hook layer** — universal hooks wrap TanStack Query:
- `useApiList`, `useApiPaginated`, `useApiDetail` → queries
- `useApiCreate`, `useApiUpdate`, `useApiDelete` → mutations
- `usePaginationWithRouter` → syncs page/search/sort to URL via TanStack Router

**Service layer** — `BaseApiService<TData, TCreate, TUpdate>` holds one Axios instance per entity endpoint. It automatically converts camelCase params to PascalCase before sending and unwraps the `ApiResponse<T>` wrapper on the way back.

**Routing** — TanStack Router with code-based route definitions. `routeTree.gen.ts` is auto-generated; do not edit it manually. Routes live in `src/app/routes/`.

**Auth state** — Zustand store in `src/features/auth/store/authStore.ts`. OIDC lifecycle (signinRedirect, callback, silentRenew, signout) via `src/lib/oidc/userManager.ts`. Axios interceptor attaches `Authorization: DPoP <token>` and `DPoP: <proof>` on every request, and retries once on `401 use_dpop_nonce` to pick up a server-issued nonce.

## Non-Obvious Conventions

- **Controllers** inherit `BaseApiController(IMediator)` and dispatch MediatR commands/queries. Direct service injection is avoided in the API layer.
- **Authorization** — fallback policy `"RetailApi"` requires the `retail-api` scope on every endpoint. Role checks use `[Authorize(Roles="Admin")]` on top of that. The `scope` claim is a space-separated string (RFC 9068), not an array — the policy splits it manually.
- **Distributed replay cache** — DPoP jti replay detection uses `AddDistributedMemoryCache()`. For multi-instance production this must be swapped to Redis.
- **EF naming** — `EFCore.NamingConventions` maps PascalCase C# properties to `snake_case` PostgreSQL columns automatically.
- **Database** — hosted on Neon (serverless PostgreSQL). Connection string in `appsettings.json` uses `SSL Mode=Require`. Actual credentials come from `.env.secrets` (loaded by devenv dotenv, never committed).
- **Environment** — `ASPNETCORE_ENVIRONMENT` in `.env.secrets` controls Development/Production. Loaded by devenv, determines `isDevelopment` in `Config.Clients()`.
- **React Compiler** — `babel-plugin-react-compiler` is enabled in the Vite config. This auto-memoizes components; manual `useMemo`/`useCallback` should only be added when the compiler cannot handle a case.
- **Yarn Berry** (v4) with `packageManager` field in `package.json`. Use `yarn` not `npm`.
- **Pre-commit hook** — devenv registers a git pre-commit hook that runs `yarn lint`. Lint must pass before commits are allowed.
