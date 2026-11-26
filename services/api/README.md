# CircleShift API

ASP.NET Core (.NET 9) modular monolith with EF Core on PostgreSQL (pgvector) and Redis.

| Project | Responsibility |
|---|---|
| `CircleShift.Domain` | Entities and business rules (members and profiles, role grants, accounts, canonical skills); no framework dependencies |
| `CircleShift.Application` | Use-case ports such as `IClock` |
| `CircleShift.Infrastructure` | EF Core `CircleShiftDbContext`, table mappings, migrations, account/role services, system clock, seed data |
| `CircleShift.Api` | HTTP host: controllers, cookie sessions, permission policies, error envelope, health checks, OpenAPI (Development only) |
| `CircleShift.Tests` | xUnit domain, seed-data and in-memory API tests |

## Run locally

From the repository root:

```bash
cp .env.example .env              # pick your own POSTGRES_PASSWORD
docker compose up -d --wait       # PostgreSQL 17 + pgvector, Redis 7.4

cd services/api
dotnet tool restore
set -a && . ../../.env && set +a  # exports ConnectionStrings__Postgres and CIRCLESHIFT_POSTGRES
dotnet ef database update --project src/CircleShift.Infrastructure --startup-project src/CircleShift.Api
dotnet run --project src/CircleShift.Api   # http://localhost:5080
```

## Seed data

```bash
dotnet run --project src/CircleShift.Api -- seed                  # canonical skill catalog
dotnet run --project src/CircleShift.Api -- seed --demo-members   # plus fictional demo members and persona roles (Development only)
dotnet run --project src/CircleShift.Api -- bootstrap-admin <email> # first admin; refused once one exists
```

Set `CIRCLESHIFT_DEMO_PASSWORD` (12+ characters) when seeding to also create
demo sign-in accounts.

`Seeding/DemoDataSeeder` is idempotent and uses stable version 5 ids derived
from each slug or handle. See [demo profiles](../../docs/product/demo-profiles.md).

## Accounts, sessions and permissions

| Route | Who | Notes |
|---|---|---|
| `GET /api/v1/auth/csrf` | anyone | Sets the `XSRF-TOKEN` cookie; send it back in `X-XSRF-TOKEN` on every POST/PUT/PATCH/DELETE |
| `POST /api/v1/auth/register` | anyone | Email, password (12–128), display name, IANA time zone, `confirmedAdult: true`; signs in |
| `POST /api/v1/auth/sign-in` / `sign-out` | anyone | One `auth.invalid_credentials` answer for every failure; 5 failures lock the account for 15 minutes |
| `GET /api/v1/auth/me` | member | Verified roles and the permissions they give |
| `GET`/`POST /api/v1/members/me/role-grants` | member | List or request a role; a request gives nothing until an admin verifies it |
| `GET`/`POST /api/v1/admin/members/{id}/role-grants` | `roles.manage` | List, or grant (verified at once) |
| `POST /api/v1/admin/role-grants/{id}/verify` / `revoke` | `roles.manage` | Nobody verifies their own role; the last admin cannot be revoked |

- Sessions are `httpOnly`, `SameSite=Lax` cookies. Outside Development they are
  also `Secure` with the `__Host-` prefix; `Auth:SecureCookies=false` in
  `appsettings.Development.json` lets them work over `http://localhost:5080`.
- Every endpoint requires a signed-in, adult-confirmed member unless it is
  marked `[AllowAnonymous]` (fallback policy). Permission policies are named
  after the permissions in `Authorization/Permissions.cs`, which mirrors the
  [permission summary](../../docs/product/personas.md#permission-summary).
- Roles are reloaded from `role_grants` on every request, so verifying or
  revoking a role applies to existing sessions immediately. Role claims never
  come from the client.
- Register and sign-in are rate limited per client IP
  (`RateLimits:AuthPermitPerMinute`, default 10).
- Errors use `{ code, message, requestId, details? }`.

## Profiles, directory and blocks

| Route | Who | Notes |
|---|---|---|
| `GET`/`PUT /api/v1/members/me/profile` | member (`profile.edit` to save) | Display name, IANA time zone, bio (≤500), up to 8 BCP 47 languages, coarse area, visibility `hidden`/`members`/`public` |
| `GET /api/v1/members?q=&language=&limit=&offset=` | anyone | Directory by display name; `limit` 1–50 (default 20); the caller is not listed |
| `GET /api/v1/members/{id}` | anyone | 404 `profile.not_found` when the profile is missing, hidden, members-only for a visitor, or blocked either way |
| `GET /api/v1/members/me/blocks` | member | Members you blocked, newest first |
| `PUT`/`DELETE /api/v1/members/me/blocks/{id}` | member | Block or unblock; both are idempotent (204). Blocking yourself is 422 `block.self` |

- New members are `hidden` until they choose otherwise. Visitors see `public`
  profiles; members also see `members` profiles.
- Visibility and blocks are applied inside the database query
  (`ProfileService.VisibleTo`), so hidden or blocked profiles never leave the
  database for that caller. A block hides both members from each other and
  only the blocker can lift it.
- Profiles are only edited through `members/me`; there is no route that takes
  another member's id for writing. Search text matches literally (`%` and `_`
  are not wildcards).

## Health routes

| Route | Checks | Status codes |
|---|---|---|
| `GET /health/live` | none — the process is serving requests | 200 |
| `GET /health/ready` | `database` (PostgreSQL) and `redis` | 200, or 503 when a dependency is down |

Responses list each check's status only; connection details and exception messages stay in the logs.

## Tests and checks

```bash
dotnet test CircleShift.sln
dotnet format CircleShift.sln --verify-no-changes
dotnet ef migrations has-pending-model-changes --project src/CircleShift.Infrastructure --startup-project src/CircleShift.Api
```

The health tests point PostgreSQL and Redis at a closed port, so they run without Docker and confirm that readiness fails closed. Account and permission tests use the EF Core in-memory provider.

## Adding a migration

```bash
dotnet ef migrations add <Name> --project src/CircleShift.Infrastructure --startup-project src/CircleShift.Api --output-dir Persistence/Migrations
```

Tables and columns use snake_case names set explicitly in `Persistence/Configurations`.
