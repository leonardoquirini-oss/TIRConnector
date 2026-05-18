# TIRConnector - Project Guide

## Overview

TIRConnector is a **read-only** REST API connector that exposes data from a TIR SQL Server database. It is part of the BERLink Platform ecosystem. The API is built with ASP.NET Core 8.0 and includes an admin UI built with Svelte.

**Key constraint**: the connector must NEVER modify the target SQL Server database. Only SELECT queries are allowed, enforced by `SqlQueryValidator`.

## Architecture

```
TIRConnector.API/          ASP.NET Core 8.0 Web API
  Configuration/           Settings classes (bound from appsettings.json)
  Controllers/             REST API controllers (Health, Tables, Metadata, Query, Cache)
  Data/                    EF Core DbContexts (ApplicationDbContext=SQL Server, PostgresDbContext=PostgreSQL)
  Filters/                 Global exception filter
  Middleware/              API Key authentication middleware
  Models/
    DTOs/                  Request/response data transfer objects
    Entities/              EF Core entities (QueryTemplate, QueryTag)
  Services/                Business logic services
  Validation/              SQL query validation (SqlQueryValidator)
admin-ui/                  Svelte admin panel (built into wwwroot/admin)
database/                  SQL scripts and ad-hoc queries
```

### Databases

- **SQL Server** (`DefaultConnection`): Target TIR database - READ ONLY access. Used for query execution, table/view listing, metadata, and container cache.
- **PostgreSQL** (`PostgresConnection`): Internal storage for query templates and tags. Full CRUD.

### Key Services

| Service | Scope | Purpose |
|---------|-------|---------|
| `QueryService` | Scoped | Execute ad-hoc SQL queries with validation |
| `QueryTemplateService` | Scoped | CRUD and execute query templates (stored in PostgreSQL) |
| `TableService` | Scoped | List tables and views from SQL Server |
| `MetadataService` | Scoped | Get column schema for tables/views |
| `ContainerCacheService` | Scoped | Sync container data to Valkey cache |
| `ValkeyService` | Singleton | Redis/Valkey connection management |
| `ContainerCacheSyncJob` | HostedService | Background cron job for container cache sync |

## Build & Deploy

### Prerequisites

- Docker and Docker Compose
- [Task](https://taskfile.dev/) (task runner)

### Commands

```bash
task tir            # Stop + rebuild Docker container
task build-tir      # Build Docker image only
task stop-tir       # Stop the container
task up             # docker-compose up -d (all services)
task restart        # down + up
task logs-tir       # Follow container logs
```

The Docker image is a multi-stage build:
1. **svelte-build**: Builds the admin UI (Node 20 Alpine)
2. **build**: Compiles .NET (SDK 8.0)
3. **publish**: Creates publish artifacts
4. **final**: Runtime image (ASP.NET 8.0 Alpine) with admin UI in `/app/wwwroot/admin`

Container runs on port **8080** internally, mapped to **9090** externally.

## Authentication

All API endpoints require an `X-API-Key` header, except:
- `/api/health/live` (public liveness probe per platform [HEALTH_CONTRACT](../../BERLink/prompt/HEALTH_CONTRACT.md))
- `/health` (built-in ASP.NET health checks)
- `/swagger/*`
- `/admin/*` (static admin UI files)

`/api/health/ready` and the deprecated `/api/health` alias require the API key. API keys are configured via `ApiKeySettings.Keys` (comma-separated).

## Health Checks

`HealthController` implements the BERLink platform health contract:

| Endpoint | Auth | Behaviour |
|---|---|---|
| `GET /api/health/live` | Public | Always 200. Body `{status, service, version, timestamp}`. Used by Docker `HEALTHCHECK`. |
| `GET /api/health/ready` | API Key | Pings SQL Server and PostgreSQL. 200 if all UP, 503 otherwise. Body includes `checks` map. |
| `GET /api/health` | API Key | Deprecated alias of `/ready`. |

Service identifier is `tir-connector`; version is read from the assembly (`<Version>` in `TIRConnector.API.csproj`). To add a new dependency check, ping it inside `HealthController.Ready` and add the result to the `checks` dictionary.

## SQL Query Validation

**Critical**: `SqlQueryValidator` (in `Validation/SqlQueryValidator.cs`) enforces read-only access:

1. Strips string literals, comments, and quoted identifiers (prevents false positives and obfuscation)
2. Verifies query starts with `SELECT`
3. Blocks semicolons (prevents stacked query injection)
4. Checks for dangerous keywords using word-boundary regex (`\b`), which correctly allows keywords in identifiers (e.g., `delete_date`, `updated_at`, `created_by`)

**Blocked keywords**: INSERT, UPDATE, DELETE, MERGE, CREATE, ALTER, DROP, TRUNCATE, RENAME, GRANT, REVOKE, DENY, EXEC, EXECUTE, DECLARE, INTO, OPENROWSET, OPENDATASOURCE, OPENQUERY, DBCC, BACKUP, RESTORE, SHUTDOWN, KILL, RECONFIGURE, WAITFOR, BULK, XP_CMDSHELL, SP_EXECUTESQL, WRITETEXT, UPDATETEXT

Both `QueryService` and `QueryTemplateService` use this validator.

## Configuration

Settings are in `appsettings.json` (dev) and overridden via environment variables in `docker-compose.yml` (production):

| Section | Key settings |
|---------|-------------|
| `ConnectionStrings` | `DefaultConnection` (SQL Server), `PostgresConnection` (PostgreSQL) |
| `ApiKeySettings` | `Keys` (comma-separated API keys) |
| `QuerySettings` | `TimeoutSeconds=30`, `MaxRows=1000`, `AllowedCommands=["SELECT"]`, `EnableQueryValidation=true` |
| `Valkey` | `ConnectionString`, `Password`, `Database` |
| `ContainerCache` | `EnableScheduler`, `CronExpression` |
| `Cors` | `AllowedOrigins`, `AllowedMethods`, `AllowedHeaders` |

## Conventions

- **Language**: C# code and comments in English, user-facing messages and descriptions in Italian
- **Naming**: PascalCase for C# classes/methods, snake_case for PostgreSQL table/column names
- **EF Core**: PostgreSQL sequences for ID generation (`s_query_templates`, `s_query_tags`)
- **Parameters**: Query parameters use `:param` format (PostgreSQL-style), converted to `@param` (SQL Server) at runtime
- **Error handling**: `GlobalExceptionFilter` maps exceptions to HTTP status codes (ArgumentException/InvalidOperationException -> 400, KeyNotFoundException -> 404, UnauthorizedAccessException -> 401)
- **Logging**: Serilog with console + daily rolling file (`logs/tirconnector-{date}.txt`)

## API Reference

See [API.md](API.md) for complete endpoint documentation.
