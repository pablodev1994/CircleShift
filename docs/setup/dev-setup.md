# Development setup

CircleShift is one repository with two apps:

| Path | What | Toolchain |
|---|---|---|
| `apps/web` | Angular front end | Node 22 (`.nvmrc`), pnpm 9 |
| `services/api` | ASP.NET Core API, EF Core migrations, xUnit tests | .NET SDK 9 (`global.json`) |
| `compose.yaml` | PostgreSQL 17 with pgvector, Redis 7.4 | Docker with Compose v2 |

## Prerequisites

- .NET SDK 9.0.100 or a later 9.0 feature band
- Node.js 22 and pnpm 9 (`corepack enable` picks the version from `package.json`)
- Docker Engine or Docker Desktop with Compose v2

## First run

From the repository root:

```bash
cp .env.example .env              # set your own POSTGRES_PASSWORD (and the two connection strings)
docker compose up -d --wait       # PostgreSQL on 127.0.0.1:5432, Redis on 127.0.0.1:6379

cd services/api
dotnet tool restore               # dotnet-ef from .config/dotnet-tools.json
set -a && . ../../.env && set +a  # exports ConnectionStrings__Postgres and CIRCLESHIFT_POSTGRES
dotnet ef database update --project src/CircleShift.Infrastructure --startup-project src/CircleShift.Api
dotnet run --project src/CircleShift.Api   # http://localhost:5080
```

Check it:

```bash
curl -s http://localhost:5080/health/live    # {"status":"Healthy",...}
curl -s http://localhost:5080/health/ready   # 200 when PostgreSQL and Redis are up, 503 otherwise
```

In a second terminal, start the web app:

```bash
pnpm install
pnpm web:start                    # http://localhost:4200
```

## Seed demo data

After migrations, from `services/api` with `.env` exported:

```bash
# Canonical skill catalog only (safe in any environment)
dotnet run --project src/CircleShift.Api -- seed

# Catalog plus the fictional adult demo members and their persona roles (Development only)
ASPNETCORE_ENVIRONMENT=Development dotnet run --project src/CircleShift.Api -- seed --demo-members

# The same, plus demo sign-in accounts (<handle>@demo.circleshift.example)
CIRCLESHIFT_DEMO_PASSWORD='pick-12+-characters' ASPNETCORE_ENVIRONMENT=Development \
  dotnet run --project src/CircleShift.Api -- seed --demo-members
```

The seed is idempotent: a second run reports 0 of everything added. It refuses
to run if a catalog slug already belongs to a different id, or if
`CIRCLESHIFT_DEMO_PASSWORD` is shorter than 12 characters.

To make a member you registered yourself the first admin (refused once a
verified admin exists):

```bash
dotnet run --project src/CircleShift.Api -- bootstrap-admin you@example.org
``` What gets seeded, and the rules demo data follows, are in
[demo profiles](../product/demo-profiles.md).

## Everyday commands

| Task | Command (from) |
|---|---|
| API tests | `dotnet test CircleShift.sln` (`services/api`) |
| API formatting | `dotnet format CircleShift.sln --verify-no-changes` (`services/api`) |
| Model/migration drift | `dotnet ef migrations has-pending-model-changes --project src/CircleShift.Infrastructure --startup-project src/CircleShift.Api` (`services/api`) |
| Web lint | `pnpm web:lint` (root) |
| Web unit tests | `pnpm web:test` (root; CI uses `test:ci` with headless Chrome) |
| Web build | `pnpm web:build` (root) |
| Stop services | `docker compose down` (add `-v` to delete the database volume) |

CI runs the same checks plus a clean-checkout smoke test that applies
migrations and waits for `/health/ready`; see `.github/workflows/ci.yml`.

## Troubleshooting

- **`ConnectionStrings:Postgres is not configured`:** export `.env` in the
  shell that runs `dotnet` (`set -a && . ../../.env && set +a`).
- **`/health/ready` returns 503:** `docker compose ps` should show both
  services healthy; the response lists which check failed, details are in the
  API log.
- **Password authentication failed:** the password in `.env` must match the
  one the database volume was created with. Run `docker compose down -v` to
  start over (this deletes local data).
- **Port already in use:** change `POSTGRES_PORT` / `REDIS_PORT` in `.env` and
  the matching connection strings.

## Conventions

- Tables and columns are snake_case, mapped explicitly in
  `Persistence/Configurations`.
- Business rules live in `CircleShift.Domain` and throw `DomainException`
  with a stable code.
- UI colors, spacing and focus styles come from the
  [design tokens](../design/design-tokens.md).
- Demo and test data are fictional adults only; never commit real names,
  contact details or locations.
