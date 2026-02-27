# TIRConnector API Reference

ASP.NET Core 8.0 REST API connector for TIR SQL Server database.

**Base URL**: `http://host:9090`
**Swagger UI**: `http://host:9090/swagger`
**Admin UI**: `http://host:9090/admin`

---

## Authentication

All API endpoints require an `X-API-Key` header, except:
- `GET /api/health` and `GET /health`
- `/swagger/*`
- `/admin/*` (static files)

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

#### `GET /api/health`

Health check. No authentication required.

**200 OK**:
```json
{
  "status": "Healthy",
  "timestamp": "2026-01-15T10:30:00Z",
  "database": "Connected"
}
```

**503 Service Unavailable**:
```json
{
  "status": "Unhealthy",
  "timestamp": "2026-01-15T10:30:00Z",
  "database": "Disconnected"
}
```

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

Execute a SQL query with pagination.

**Request body**: Same as `/execute`.

**Query parameters**:
- `page` (int, default: 1) - Page number (1-based)
- `pageSize` (int, default: 20) - Records per page

**200 OK**:
```json
{
  "data": [
    { "Id": 1, "Codice": "ART001" },
    { "Id": 2, "Codice": "ART002" }
  ],
  "page": 1,
  "pageSize": 20,
  "totalCount": 150,
  "totalPages": 8
}
```

> The query must contain an `ORDER BY` clause for pagination to work correctly (uses SQL Server `OFFSET/FETCH`).

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
  }
}
```

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `templateName` | string | yes | Template name (must be active and non-deprecated) |
| `parameters` | object | no | Key-value pairs for template parameters |

**200 OK**: Same response format as `POST /api/query/execute`.
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
