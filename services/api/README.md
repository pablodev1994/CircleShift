# CircleShift API

ASP.NET Core (.NET 9) modular monolith with EF Core on PostgreSQL (pgvector) and Redis.

| Project | Responsibility |
|---|---|
| `CircleShift.Domain` | Entities and business rules (members, canonical skills); no framework dependencies |
| `CircleShift.Application` | Use-case ports such as `IClock` |
| `CircleShift.Infrastructure` | EF Core `CircleShiftDbContext`, table mappings, migrations, system clock |
| `CircleShift.Api` | HTTP host: controllers, health checks, OpenAPI (Development only) |
| `CircleShift.Tests` | xUnit domain tests and in-memory API tests |

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

The API tests point PostgreSQL and Redis at a closed port, so they run without Docker and confirm that readiness fails closed.

## Adding a migration

```bash
dotnet ef migrations add <Name> --project src/CircleShift.Infrastructure --startup-project src/CircleShift.Api --output-dir Persistence/Migrations
```

Tables and columns use snake_case names set explicitly in `Persistence/Configurations`.
