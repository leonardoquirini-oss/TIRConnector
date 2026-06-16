# TIRConnector API Reference

ASP.NET Core 8.0 REST API connector for TIR SQL Server database.

**Base URL**: `http://host:9090`
**Swagger UI**: `http://host:9090/swagger`
**Admin UI**: `http://host:9090/admin`

---

## Authentication

All API endpoints require an `X-API-Key` header, except:
- `GET /api/health/live` (public liveness probe)
- `GET /health` (built-in ASP.NET health checks)
- `/swagger/*`
- `/admin/*` (static files)

`GET /api/health/ready` and `GET /api/health` (deprecated alias) require the API key.

```
X-API-Key: your-api-key
```

**401 Unauthorized response**:
```json
{
  "error": "Unauthorized",
  "message": "API Key is missing"
}
```

---

## Error Format

All errors follow this structure:

```json
{
  "error": "ExceptionType",
  "message": "Human-readable error message",
  "details": "Optional additional details",
  "timestamp": "2026-01-15T10:30:00Z"
}
```

| HTTP Status | When |
|---|---|
| 400 | Invalid input, query validation error, forbidden SQL keyword |
| 401 | Missing or invalid API key |
| 404 | Resource not found |
| 500 | Internal server error |
| 503 | Database unavailable |

---

## Endpoints

### Health

The service implements the BERLink platform [HEALTH_CONTRACT](../../BERLink/prompt/HEALTH_CONTRACT.md): two endpoints under `/api/health/`.

| URL | Auth | Purpose |
|---|---|---|
| `GET /api/health/live` | Public | Liveness probe. No dependency checks. Always returns 200 if process is alive. |
| `GET /api/health/ready` | `X-API-Key` | Readiness probe. Verifies SQL Server and PostgreSQL. Returns 503 if any dependency is DOWN. |
| `GET /api/health` | `X-API-Key` | **Deprecated** alias of `/api/health/ready`. Kept for backward compatibility. |

Use `/live` for Docker `HEALTHCHECK` and K8s `livenessProbe`; use `/ready` for fleet monitors and K8s `readinessProbe`.

#### `GET /api/health/live`

**200 OK** (always):
```json
{
  "status": "UP",
  "service": "tir-connector",
  "version": "1.0.0",
  "timestamp": "2026-05-15T10:30:00Z"
}
```

#### `GET /api/health/ready`

Headers: `X-API-Key: <key>`

**200 OK** — all dependencies UP:
```json
{
  "status": "UP",
  "service": "tir-connector",
  "version": "1.0.0",
  "timestamp": "2026-05-15T10:30:00Z",
  "checks": {
    "database": "UP",
    "postgres": "UP"
  }
}
```

**503 Service Unavailable** — at least one dependency DOWN:
```json
{
  "status": "DOWN",
  "service": "tir-connector",
  "version": "1.0.0",
  "timestamp": "2026-05-15T10:30:00Z",
  "checks": {
    "database": "UP",
    "postgres": "DOWN"
  }
}
```

Rules:
- `status` is `UP` only if all `checks` are `UP`; otherwise `DOWN`.
- `service` is the stable kebab-case service identifier (`tir-connector`).
- `version` is the application version (from assembly metadata).
- `checks` keys: `database` (SQL Server / TIR), `postgres` (template store).

---

### Tables

#### `GET /api/tables`

List all tables in the SQL Server database.

**200 OK**:
```json
[
  {
    "name": "Articoli",
    "schema": "dbo",
    "type": "TABLE"
  }
]
```

#### `GET /api/tables/views`

List all views in the SQL Server database.

**200 OK**:
```json
[
  {
    "name": "V_Clienti",
    "schema": "dbo",
    "type": "VIEW"
  }
]
```

---

### Metadata

#### `GET /api/metadata/table/{tableName}`

Get column schema for a table.

**Path parameters**:
- `tableName` (string, required) - Table name. Supports schema prefix: `bct.TableName`

**Query parameters**:
- `schema` (string, optional) - Schema override (takes priority over prefix)

**200 OK**:
```json
{
  "tableName": "Articoli",
  "schema": "dbo",
  "columns": [
    {
      "columnName": "Id",
      "dataType": "int",
      "maxLength": null,
      "isNullable": false,
      "isPrimaryKey": true,
      "defaultValue": null
    },
    {
      "columnName": "Descrizione",
      "dataType": "varchar",
      "maxLength": 200,
      "isNullable": true,
      "isPrimaryKey": false,
      "defaultValue": null
    }
  ]
}
```

**404 Not Found**: Table does not exist.

#### `GET /api/metadata/view/{viewName}`

Get column schema for a view. Same parameters and response as table metadata.

---

### Query Execution

> All queries are validated to be read-only SELECT statements. See [SQL Query Validation](#sql-query-validation) for details.

#### `POST /api/query/execute`

Execute a SQL query.

**Request body**:
```json
{
  "query": "SELECT * FROM Articoli WHERE delete_date IS NULL AND codice = :codice",
  "parameters": {
    "codice": "ART001"
  }
}
```

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `query` | string | yes | SQL SELECT query. Parameters use `:name` format |
| `parameters` | object | no | Key-value pairs for parameterized queries |

**List parameters (`IN` clause)**: a parameter value can be a JSON array. The placeholder is expanded into multiple SQL parameters (`@name_0, @name_1, ...`), so `IN ( :itemList )` is safe against injection. Use a JSON array for lists and a scalar for single values; the two can be mixed in the same request.

```json
{
  "query": "SELECT * FROM Clienti WHERE codice IN ( :itemList ) AND stato = :stato",
  "parameters": {
    "itemList": ["aa", "bb", "cc"],
    "stato": "ATTIVO"
  }
}
```

Numbers are passed unquoted (`"ids": [1, 2, 3]`). An **empty array** is rejected with `400 Bad Request` (`Il parametro lista 'name' non può essere vuoto`). List parameters work identically in `/execute`, `/execute/paged`, and template execution.

**200 OK**:
```json
{
  "data": [
    {
      "Id": 1,
      "Codice": "ART001",
      "Descrizione": "Articolo di test",
      "delete_date": null
    }
  ],
  "rowCount": 1,
  "executionTimeMs": 45,
  "columns": [
    { "name": "Id", "type": "Int32" },
    { "name": "Codice", "type": "String" },
    { "name": "Descrizione", "type": "String" },
    { "name": "delete_date", "type": "DateTime" }
  ]
}
```

**400 Bad Request**: Query validation error (e.g., forbidden keyword, not a SELECT).

> **Note**: Results are limited to `MaxRows` (default: 1000). Query timeout is `TimeoutSeconds` (default: 30s).

#### `POST /api/query/execute/paged`

Execute a SQL query with server-side pagination (SQL Server `OFFSET/FETCH`).

**Request body**: Same as `/execute`.

**Query parameters**:
- `page` (int, default: 1) - Page number (1-based). Values `< 1` are clamped to 1.
- `pageSize` (int, default: 20) - Records per page. Values `< 1` fall back to 20; values above `MaxRows` are clamped to `MaxRows`.

**Example**:
```bash
curl -X POST "http://localhost:9090/api/query/execute/paged?page=1&pageSize=100" \
  -H "X-API-Key: <your-key>" \
  -H "Content-Type: application/json" \
  -d '{
    "query": "SELECT id, codice, ragione_sociale FROM Clienti WHERE attivo = :stato ORDER BY id",
    "parameters": { "stato": true }
  }'
```

**200 OK**:
```json
{
  "data": [
    { "id": 1, "codice": "ART001", "ragione_sociale": "..." },
    { "id": 2, "codice": "ART002", "ragione_sociale": "..." }
  ],
  "page": 1,
  "pageSize": 100,
  "totalCount": 2046,
  "totalPages": 21
}
```

**ORDER BY handling**:
- If the query contains an outer `ORDER BY`, it is detected (parenthesis-aware, ignoring `ORDER BY` inside subqueries, CTEs, and `OVER(...)`) and re-applied on the paged `SELECT`. It is stripped before wrapping in `COUNT(*)`, which SQL Server would otherwise reject.
- If the query has no outer `ORDER BY`, a neutral `ORDER BY (SELECT NULL)` is injected so `OFFSET/FETCH` remains valid. In that case row order across pages is not guaranteed — add an explicit `ORDER BY` for stable pagination.

**Limitations**:
- SQL Server does not allow `TOP` and `OFFSET` in the same query, so queries using `TOP N` cannot be paginated through this endpoint.

---

### Query Templates

Templates are stored in PostgreSQL and executed against SQL Server.

#### `GET /api/query/templates`

List all query templates.

**Query parameters**:
- `activeOnly` (bool, default: false) - If true, return only active non-deprecated templates

**200 OK**:
```json
[
  {
    "idQueryTemplate": 1,
    "name": "get_articoli_attivi",
    "description": "Recupera articoli attivi",
    "category": "articoli",
    "outputFormat": "json",
    "maxResults": 10000,
    "timeoutSeconds": 30,
    "version": 3,
    "active": true,
    "creationDate": "2026-01-01T00:00:00Z",
    "updateDate": "2026-01-15T10:00:00Z",
    "tagCount": 2
  }
]
```

> The list response does NOT include the `querySql` field. Use `GET /templates/{id}` for the full template.

#### `GET /api/query/templates/{id}`

Get a specific template including the SQL query.

**Path parameters**:
- `id` (int, required) - Template ID

**200 OK**:
```json
{
  "idQueryTemplate": 1,
  "name": "get_articoli_attivi",
  "description": "Recupera articoli attivi",
  "category": "articoli",
  "querySql": "SELECT * FROM Articoli WHERE attivo = 1",
  "outputFormat": "json",
  "maxResults": 10000,
  "timeoutSeconds": 30,
  "version": 3,
  "active": true,
  "creationDate": "2026-01-01T00:00:00Z",
  "updateDate": "2026-01-15T10:00:00Z"
}
```

**404 Not Found**: Template does not exist.

#### `POST /api/query/templates`

Create a new query template.

**Request body**:
```json
{
  "name": "get_articoli_attivi",
  "description": "Recupera articoli attivi",
  "category": "articoli",
  "querySql": "SELECT * FROM Articoli WHERE attivo = 1",
  "outputFormat": "json",
  "maxResults": 10000,
  "timeoutSeconds": 30,
  "active": true
}
```

| Field | Type | Required | Default | Validation |
|-------|------|----------|---------|------------|
| `name` | string | yes | - | Max 200 chars |
| `description` | string | no | null | - |
| `category` | string | no | null | Max 50 chars |
| `querySql` | string | yes | - | Must be a valid SELECT query |
| `outputFormat` | string | no | `"json"` | Must be `"json"` or `"csv"` |
| `maxResults` | int | no | 10000 | Min 1 |
| `timeoutSeconds` | int | no | 30 | Min 1 |
| `active` | bool | no | true | - |

**201 Created**: Returns created template. Location header points to `GET /templates/{id}`.

#### `PUT /api/query/templates/{id}`

Update an existing template. Version is auto-incremented if `querySql` changes.

**Request body**: Same as POST.

**200 OK**: Returns updated template.
**404 Not Found**: Template does not exist.

#### `DELETE /api/query/templates/{id}`

Delete a template.

**204 No Content**: Successfully deleted.
**404 Not Found**: Template does not exist.

#### `POST /api/query/templates/execute`

Execute a template by name.

**Request body**:
```json
{
  "templateName": "get_articoli_per_codice",
  "parameters": {
    "codice": "ART001"
  },
  "outputFormat": "csv"
}
```

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `templateName` | string | yes | Template name (must be active and non-deprecated) |
| `parameters` | object | no | Key-value pairs for template parameters. A value can be a JSON array for `IN ( :list )` clauses — see [List parameters](#post-apiqueryexecute) |
| `outputFormat` | string | no | `json` or `csv`. Overrides the template's default `outputFormat`. If omitted, uses the template's value (default: `json`) |

**Output format behavior**:
- **`json`** (default): Standard JSON response with `MaxRows` limit applied.
- **`csv`**: Streaming CSV download with **no `MaxRows` limit**. Response has `Content-Type: text/csv; charset=utf-8` and `Content-Disposition: attachment` header. CSV follows RFC 4180 (comma separator, `\r\n` line endings, fields with commas/quotes/newlines are quoted).

**200 OK**: JSON response (same format as `POST /api/query/execute`) or CSV file download.
**404 Not Found**: Template not found or not active.

---

### Template Tags (Version Snapshots)

Tags are immutable snapshots of a template at a specific version.

#### `POST /api/query/templates/{id}/tag`

Create a version tag for a template.

**Path parameters**:
- `id` (int, required) - Template ID

**Request body**:
```json
{
  "changeReason": "Aggiunto filtro per data",
  "changeType": "MODIFICATION"
}
```

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `changeReason` | string | yes | Reason for creating this snapshot |
| `changeType` | string | yes | Type of change (e.g., `MODIFICATION`, `BUGFIX`, `NEW`) |

**201 Created**:
```json
{
  "idQueryQueryTag": 5,
  "idQueryTemplate": 1,
  "version": 3,
  "name": "get_articoli_attivi",
  "changeReason": "Aggiunto filtro per data",
  "changeType": "MODIFICATION",
  "creationDate": "2026-01-15T10:30:00Z"
}
```

**404 Not Found**: Template does not exist.

#### `GET /api/query/templates/{id}/tags`

List all tags for a template (ordered by creation date descending).

**200 OK**:
```json
[
  {
    "idQueryQueryTag": 5,
    "idQueryTemplate": 1,
    "version": 3,
    "changeReason": "Aggiunto filtro per data",
    "changeType": "MODIFICATION",
    "creationDate": "2026-01-15T10:30:00Z"
  }
]
```

#### `GET /api/query/tags/{id}`

Get a specific tag with full details (includes SQL query).

**Path parameters**:
- `id` (int, required) - Tag ID

**200 OK**:
```json
{
  "idQueryQueryTag": 5,
  "idQueryTemplate": 1,
  "version": 3,
  "querySql": "SELECT * FROM Articoli WHERE attivo = 1 AND data > :data",
  "params": "[\"data\"]",
  "name": "get_articoli_attivi",
  "description": "Recupera articoli attivi",
  "changeReason": "Aggiunto filtro per data",
  "changeType": "MODIFICATION",
  "creationDate": "2026-01-15T10:30:00Z"
}
```

**404 Not Found**: Tag does not exist.

#### `DELETE /api/query/tags/{id}`

Delete a tag.

**204 No Content**: Successfully deleted.
**404 Not Found**: Tag does not exist.

---

### Cache

#### `POST /api/cache/containers`

Manually trigger container (casse) cache sync to Valkey.

**Request body**: None.

**200 OK**:
```json
{
  "added": 45,
  "removed": 2,
  "total": 150,
  "executionTimeMs": 512
}
```

| Field | Type | Description |
|-------|------|-------------|
| `added` | int | Containers added to cache |
| `removed` | int | Containers removed from cache |
| `total` | int | Total containers now in cache |
| `executionTimeMs` | long | Sync duration in milliseconds |

> The cache is also synced automatically by a background job based on the cron expression in `ContainerCache.CronExpression` (default: every hour in production).

---

## SQL Query Validation

All queries (both ad-hoc and template-based) are validated by `SqlQueryValidator` to enforce read-only access:

1. **Strip noise**: Removes string literals (`'...'`), comments (`-- ...`, `/* ... */`), and quoted identifiers (`[...]`, `"..."`) to prevent false positives and obfuscation attacks
2. **Allowed command check**: Query must start with `SELECT` (configurable via `QuerySettings.AllowedCommands`)
3. **Semicolon block**: No semicolons allowed (prevents stacked query injection)
4. **Dangerous keyword check**: Uses word-boundary regex (`\b`) matching, which:
   - **Allows** keywords as part of identifiers: `delete_date`, `updated_at`, `created_by`, `is_executed`
   - **Blocks** standalone keywords: `DELETE FROM`, `DROP TABLE`, `EXEC sp_name`

### Blocked keywords

| Category | Keywords |
|----------|----------|
| DML | `INSERT`, `UPDATE`, `DELETE`, `MERGE` |
| DDL | `CREATE`, `ALTER`, `DROP`, `TRUNCATE`, `RENAME` |
| DCL | `GRANT`, `REVOKE`, `DENY` |
| Execution | `EXEC`, `EXECUTE`, `DECLARE` |
| Table creation | `INTO` (blocks `SELECT ... INTO`) |
| SQL Server specific | `OPENROWSET`, `OPENDATASOURCE`, `OPENQUERY`, `DBCC`, `BACKUP`, `RESTORE`, `SHUTDOWN`, `KILL`, `RECONFIGURE`, `WAITFOR`, `BULK`, `XP_CMDSHELL`, `SP_EXECUTESQL`, `WRITETEXT`, `UPDATETEXT` |

### Additional protections

- **Parameterized queries**: All user-supplied values are passed as `SqlCommand` parameters (never string-concatenated)
- **Max rows limit**: Results capped at `QuerySettings.MaxRows` (default: 1000)
- **Query timeout**: `QuerySettings.TimeoutSeconds` (default: 30s), overridable per template

---

## Configuration Reference

### `ConnectionStrings`

| Key | Description |
|-----|-------------|
| `DefaultConnection` | SQL Server connection (TIR database, read-only) |
| `PostgresConnection` | PostgreSQL connection (templates/tags storage) |

### `ApiKeySettings`

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `Keys` | string | `""` | Comma-separated list of valid API keys |

### `QuerySettings`

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `TimeoutSeconds` | int | 30 | SQL command timeout |
| `MaxRows` | int | 1000 | Maximum rows returned per query |
| `AllowedCommands` | string[] | `["SELECT"]` | Allowed SQL commands |
| `EnableQueryValidation` | bool | true | Enable/disable query validation |

### `Valkey`

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `ConnectionString` | string | `"localhost:6379"` | Valkey/Redis host:port |
| `Password` | string | `""` | Valkey password |
| `Database` | int | 0 | Valkey database number |

### `ContainerCache`

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `EnableScheduler` | bool | true | Enable background cache sync |
| `CronExpression` | string | `"0 */5 * * * *"` | Cron expression (6-field with seconds) |

### `Cors`

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `AllowedOrigins` | string[] | `["*"]` | Allowed CORS origins |
| `AllowedMethods` | string[] | `["GET","POST","PUT","DELETE"]` | Allowed HTTP methods |
| `AllowedHeaders` | string[] | `["*"]` | Allowed headers |

### Environment variable override

In `docker-compose.yml`, settings are overridden using double-underscore notation:

```yaml
environment:
  - ConnectionStrings__DefaultConnection=Server=host;Database=TirSQL;...
  - ApiKeySettings__Keys=key1,key2
  - QuerySettings__TimeoutSeconds=60
  - Valkey__ConnectionString=redis:6379
  - ContainerCache__CronExpression=0 0 * * * *
```
