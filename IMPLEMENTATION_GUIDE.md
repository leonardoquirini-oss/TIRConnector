# TIRConnector ASP.NET Core 8.0 - Implementation Guide

## Quick Setup

```bash
# 1. Make setup script executable
chmod +x setup-project.sh

# 2. Run setup
./setup-project.sh

# 3. Build
dotnet build

# 4. Run
dotnet run --project TIRConnector.API

# 5. Test
curl http://localhost:5000/api/health
open http://localhost:5000/swagger
```

## Complete File List

Follow PROJECT_MS.md specifications. All code must be created manually or use the setup script.

### Core Files Created:
✅ TIRConnector.API/TIRConnector.API.csproj
✅ TIRConnector.API/appsettings.json
✅ setup-project.sh

### Files to Create:

1. **TIRConnector.API/Program.cs** - Main entry point with DI, Swagger, CORS, Health Checks
2. **TIRConnector.API/appsettings.Docker.json** - Docker-specific config
3. **TIRConnector.API/Data/ApplicationDbContext.cs** - EF Core DbContext
4. **TIRConnector.API/Middleware/ApiKeyAuthenticationMiddleware.cs** - API Key auth
5. **TIRConnector.API/Configuration/ApiKeySettings.cs** - API key config model
6. **TIRConnector.API/Configuration/QuerySettings.cs** - Query settings model
7. **TIRConnector.API/Models/DTOs/** - All DTOs (QueryRequest, QueryResponse, etc.)
8. **TIRConnector.API/Services/** - QueryService, TableService, MetadataService
9. **TIRConnector.API/Controllers/** - HealthController, QueryController, TablesController, MetadataController
10. **TIRConnector.API/Filters/GlobalExceptionFilter.cs** - Exception handling
11. **Dockerfile** - Multi-stage build with TLS 1.0 support
12. **docker-compose.yml** - Docker Compose configuration
13. **.dockerignore** - Docker ignore file
14. **README.md** - Complete documentation

## Key Implementation Notes

### TLS 1.0 Support for Legacy SQL Server

Add to Dockerfile (same approach as Java project):
```dockerfile
# Modify java.security equivalent in .NET
# Or use connection string with TrustServerCertificate=True;Encrypt=False
```

### Connection String for Docker

```json
"Server=192.168.0.10;Database=TirSQL;User Id=UtenteSolaLettura;Password=test;TrustServerCertificate=True;Encrypt=False;MultipleActiveResultSets=true"
```

### API Key Middleware

Validates X-API-Key header, excludes /api/health and /swagger endpoints.

### Entity Framework Scaffold

```bash
dotnet ef dbcontext scaffold "Server=192.168.0.10;Database=TirSQL;..." Microsoft.EntityFrameworkCore.SqlServer --output-dir Models/Entities --context-dir Data --force
```

## Testing

```bash
# Run tests
dotnet test

# With coverage
dotnet test /p:CollectCoverage=true
```

## Docker

```bash
# Build
docker build -t tirconnector:latest .

# Run
docker-compose up -d

# Logs
docker-compose logs -f
```

## Next Steps

1. Create all files listed above following PROJECT_MS.md
2. Use the existing Java/Spring Boot implementation as reference for business logic
3. Adapt metadata endpoints from Java QueryService to C# MetadataService
4. Test with actual SQL Server database
5. Configure TLS 1.0 support if needed

## Reference

See PROJECT_MS.md for complete specifications.
