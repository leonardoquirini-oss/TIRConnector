# TIRConnector - ASP.NET Core 8.0 REST API

A production-ready REST API connector for TIR SQL Server database built with ASP.NET Core 8.0 and Entity Framework Core.

## Features

- ✅ **ASP.NET Core 8.0** - Modern, high-performance web framework
- ✅ **Entity Framework Core 8.0** - SQL Server database access
- ✅ **API Key Authentication** - Secure X-API-Key header validation
- ✅ **Query Execution** - Execute SQL SELECT queries with validation
- ✅ **Pagination Support** - Execute paginated queries
- ✅ **Database Metadata** - Retrieve tables, views, and schema information
- ✅ **Swagger/OpenAPI** - Interactive API documentation
- ✅ **Health Checks** - Monitor application and database health
- ✅ **Structured Logging** - Serilog with console and file outputs
- ✅ **Docker Support** - Multi-stage Dockerfile and Docker Compose
- ✅ **CORS Configuration** - Cross-origin resource sharing support

## Project Structure

```
TIRConnector/
├── TIRConnector.API/
│   ├── Configuration/           # Application settings models
│   │   ├── ApiKeySettings.cs
│   │   └── QuerySettings.cs
│   ├── Controllers/             # REST API endpoints
│   │   ├── HealthController.cs
│   │   ├── QueryController.cs
│   │   ├── TablesController.cs
│   │   └── MetadataController.cs
│   ├── Data/                    # EF Core DbContext
│   │   └── ApplicationDbContext.cs
│   ├── Filters/                 # Global exception handling
│   │   └── GlobalExceptionFilter.cs
│   ├── Middleware/              # Custom middleware
│   │   └── ApiKeyAuthenticationMiddleware.cs
│   ├── Models/DTOs/             # Data transfer objects
│   │   ├── QueryRequest.cs
│   │   ├── QueryResponse.cs
│   │   ├── ErrorResponse.cs
│   │   ├── PagedResult.cs
│   │   ├── TableInfo.cs
│   │   ├── ColumnMetadata.cs
│   │   └── TableMetadataResponse.cs
│   ├── Services/                # Business logic services
│   │   ├── IQueryService.cs
│   │   ├── QueryService.cs
│   │   ├── ITableService.cs
│   │   ├── TableService.cs
│   │   ├── IMetadataService.cs
│   │   └── MetadataService.cs
│   ├── Program.cs               # Application entry point
│   ├── appsettings.json         # Application configuration
│   └── appsettings.Docker.json  # Docker environment configuration
├── Dockerfile                   # Multi-stage Docker build
├── docker-compose.yml           # Docker Compose configuration
├── .dockerignore                # Docker ignore file
└── README.md                    # This file
```

## Quick Start with Docker (Recommended)

### Prerequisites
- Docker and Docker Compose installed
- Access to TIR SQL Server database

### Run with Docker Compose

1. **Update environment variables** in `docker-compose.yml`:
```yaml
environment:
  - DB_HOST=192.168.0.10
  - DB_PORT=1433
  - DB_NAME=TirSQL
  - DB_USER=YourUsername
  - DB_PASSWORD=YourPassword
  - API_KEYS=your-secret-key-1,your-secret-key-2
```

2. **Build and run**:
```bash
docker-compose up -d
```

3. **Check logs**:
```bash
docker-compose logs -f tirconnector
```

4. **Access the API**:
- Swagger UI: http://localhost:8080/swagger
- Health Check: http://localhost:8080/api/health

### Stop the Application

```bash
docker-compose down
```

## API Endpoints

### Health Check
```http
GET /api/health
```
No authentication required. Returns application and database health status.

### Query Execution

**Execute SQL Query**
```http
POST /api/query/execute
X-API-Key: your-secret-key

{
  "query": "SELECT * FROM YourTable WHERE Column = @param1",
  "parameters": {
    "param1": "value"
  }
}
```

**Execute Paginated Query**
```http
POST /api/query/execute/paged?page=1&pageSize=20
X-API-Key: your-secret-key

{
  "query": "SELECT * FROM YourTable ORDER BY Id"
}
```

### Table Metadata

**Get All Tables**
```http
GET /api/tables
X-API-Key: your-secret-key
```

**Get All Views**
```http
GET /api/tables/views
X-API-Key: your-secret-key
```

**Get Table Schema**
```http
GET /api/metadata/table/YourTableName?schema=dbo
X-API-Key: your-secret-key
```

**Get View Schema**
```http
GET /api/metadata/view/YourViewName?schema=dbo
X-API-Key: your-secret-key
```

## Authentication

All endpoints (except `/api/health` and `/swagger`) require API Key authentication via the `X-API-Key` header.

Configure API keys in `docker-compose.yml`:
```yaml
- API_KEYS=key1,key2,key3
```

## Configuration

### Environment Variables (Docker)

| Variable | Description | Default |
|----------|-------------|---------|
| `DB_HOST` | SQL Server hostname or IP | `192.168.0.10` |
| `DB_PORT` | SQL Server port | `1433` |
| `DB_NAME` | Database name | `TirSQL` |
| `DB_USER` | Database username | `UtenteSolaLettura` |
| `DB_PASSWORD` | Database password | `test` |
| `API_KEYS` | Comma-separated API keys | `default-key-change-me` |
| `CORS_ORIGINS` | Comma-separated allowed origins | `http://localhost:3000` |

### Query Settings

Configured in `appsettings.json`:
```json
{
  "QuerySettings": {
    "TimeoutSeconds": 30,
    "MaxRows": 1000,
    "AllowedCommands": ["SELECT"],
    "EnableQueryValidation": true
  }
}
```

## Security Features

- **API Key Authentication**: Validates X-API-Key header for all protected endpoints
- **Query Validation**: Only allows SELECT queries by default
- **SQL Injection Protection**: Uses parameterized queries
- **Max Rows Limit**: Prevents excessive data retrieval
- **Query Timeout**: Prevents long-running queries
- **CORS Policy**: Configurable cross-origin access

## Development

### Local Development (without Docker)

1. **Prerequisites**:
   - .NET 8.0 SDK
   - SQL Server access

2. **Update connection string** in `appsettings.json`:
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=YOUR_SERVER;Database=TirSQL;User Id=USERNAME;Password=PASSWORD;TrustServerCertificate=True;Encrypt=False"
  }
}
```

3. **Run the application**:
```bash
dotnet restore
dotnet build
dotnet run --project TIRConnector.API
```

4. **Access**:
- API: http://localhost:5000
- Swagger: http://localhost:5000/swagger

### Build Docker Image Manually

```bash
# Build
docker build -t tirconnector:latest .

# Run
docker run -d \
  -p 8080:8080 \
  -e DB_HOST=192.168.0.10 \
  -e DB_NAME=TirSQL \
  -e DB_USER=YourUsername \
  -e DB_PASSWORD=YourPassword \
  -e API_KEYS=your-secret-key \
  --name tirconnector \
  tirconnector:latest

# View logs
docker logs -f tirconnector
```

## Testing

Example using curl:

```bash
# Health Check
curl http://localhost:8080/api/health

# Execute Query
curl -X POST http://localhost:8080/api/query/execute \
  -H "X-API-Key: your-secret-key" \
  -H "Content-Type: application/json" \
  -d '{
    "query": "SELECT TOP 10 * FROM YourTable"
  }'

# Get All Tables
curl http://localhost:8080/api/tables \
  -H "X-API-Key: your-secret-key"

# Get Table Schema
curl "http://localhost:8080/api/metadata/table/YourTable?schema=dbo" \
  -H "X-API-Key: your-secret-key"
```

## Logging

Logs are written to:
- **Console**: All log levels
- **File**: `logs/tirconnector-YYYYMMDD.txt` (daily rotation)

Log files are persisted in the `./logs` directory via Docker volume mount.

## Troubleshooting

### Database Connection Issues

1. **Verify database connectivity**:
```bash
docker exec tirconnector-api curl http://localhost:8080/api/health
```

2. **Check connection string** in docker-compose.yml
3. **Verify firewall rules** allow connection to SQL Server
4. **Check SQL Server authentication** mode (SQL Server and Windows Authentication)

### TLS 1.0 Support (Legacy SQL Server)

If connecting to legacy SQL Server requiring TLS 1.0, modify the connection string:
```
TrustServerCertificate=True;Encrypt=False
```

This is already configured in the default settings.

### API Key Issues

- Ensure `X-API-Key` header is included in requests
- Verify the key matches one of the configured API keys
- Check that `/api/health` and `/swagger` work without authentication

## Production Deployment

### Recommendations

1. **Use strong API keys**: Generate secure random keys
2. **Configure HTTPS**: Use a reverse proxy (nginx, Traefik) for TLS
3. **Restrict CORS**: Specify exact allowed origins
4. **Monitor logs**: Set up log aggregation (ELK, Splunk)
5. **Database credentials**: Use environment variables or secrets management
6. **Resource limits**: Configure Docker memory/CPU limits
7. **Backup logs**: Implement log rotation and archival

### Docker Compose Production Example

```yaml
services:
  tirconnector:
    image: tirconnector:latest
    restart: always
    deploy:
      resources:
        limits:
          cpus: '2'
          memory: 1G
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - DB_HOST=${DB_HOST}
      - DB_USER=${DB_USER}
      - DB_PASSWORD=${DB_PASSWORD}
      - API_KEYS=${API_KEYS}
```

## License

This project is part of the BERLink Platform.

## Support

For issues or questions, please contact the development team.
