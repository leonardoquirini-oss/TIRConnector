# SQL Server REST API Connector - TIRConnector

## Obiettivo del Progetto

Creare un connector REST API per esporre un database SQL Server con le seguenti caratteristiche:
- Autenticazione tramite API Key
- Compatibile con AI agents (documentazione OpenAPI/Swagger)
- Containerizzato con Docker
- Performante e scalabile

## Stack Tecnologico

- **Framework**: ASP.NET Core 8.0 (Web API)
- **ORM**: Entity Framework Core 8.0
- **Database**: Microsoft SQL Server
- **Autenticazione**: API Key custom (header `X-API-Key`)
- **Documentazione**: Swashbuckle (Swagger/OpenAPI 3.0)
- **Containerizzazione**: Docker con multi-stage build
- **Build Tool**: .NET CLI / MSBuild
- **.NET Version**: 8.0 LTS

## Requisiti Funzionali

### 1. Struttura del Progetto

```
TIRConnector/
├── TIRConnector.API/
│   ├── Controllers/
│   │   ├── HealthController.cs
│   │   ├── QueryController.cs
│   │   └── TablesController.cs
│   ├── Middleware/
│   │   └── ApiKeyAuthenticationMiddleware.cs
│   ├── Services/
│   │   ├── IQueryService.cs
│   │   ├── QueryService.cs
│   │   ├── ITableService.cs
│   │   └── TableService.cs
│   ├── Models/
│   │   ├── DTOs/
│   │   │   ├── QueryRequest.cs
│   │   │   ├── QueryResponse.cs
│   │   │   ├── ErrorResponse.cs
│   │   │   └── PagedResult.cs
│   │   └── Entities/
│   │       └── (modelli EF Core generati)
│   ├── Data/
│   │   └── ApplicationDbContext.cs
│   ├── Configuration/
│   │   ├── ApiKeySettings.cs
│   │   ├── QuerySettings.cs
│   │   └── SwaggerConfiguration.cs
│   ├── Filters/
│   │   └── GlobalExceptionFilter.cs
│   ├── Program.cs
│   ├── appsettings.json
│   ├── appsettings.Docker.json
│   └── TIRConnector.API.csproj
├── TIRConnector.Tests/
│   ├── Controllers/
│   ├── Services/
│   └── TIRConnector.Tests.csproj
├── Dockerfile
├── docker-compose.yml
├── .dockerignore
└── README.md
```

### 2. Funzionalità API

#### Endpoint Principali

1. **GET /api/health**
   - Health check endpoint
   - Non richiede autenticazione
   - Response: 
     ```json
     {
       "status": "Healthy",
       "database": "Connected",
       "timestamp": "2025-11-18T10:30:00Z"
     }
     ```

2. **POST /api/query/execute**
   - Esegue query SQL parametriche
   - Richiede API Key
   - Request body:
     ```json
     {
       "sql": "SELECT * FROM Users WHERE Status = @Status",
       "parameters": {
         "Status": "active"
       }
     }
     ```
   - Response:
     ```json
     {
       "success": true,
       "data": [...],
       "rowCount": 10,
       "executionTimeMs": 45
     }
     ```

3. **GET /api/tables**
   - Lista tutte le tabelle del database
   - Richiede API Key
   - Response: 
     ```json
     {
       "tables": ["Users", "Products", "Orders"]
     }
     ```

4. **GET /api/tables/{tableName}**
   - Recupera tutti i record di una tabella (con paginazione)
   - Query params: `page=1&pageSize=50&sortBy=Id&sortOrder=asc`
   - Richiede API Key
   - Response:
     ```json
     {
       "data": [...],
       "page": 1,
       "pageSize": 50,
       "totalCount": 1000,
       "totalPages": 20
     }
     ```

5. **GET /api/tables/{tableName}/{id}**
   - Recupera un singolo record per ID
   - Richiede API Key

6. **POST /api/tables/{tableName}**
   - Crea un nuovo record
   - Richiede API Key
   - Request body: JSON object con i campi della tabella

7. **PUT /api/tables/{tableName}/{id}**
   - Aggiorna un record esistente
   - Richiede API Key
   - Request body: JSON object con i campi da aggiornare

8. **DELETE /api/tables/{tableName}/{id}**
   - Elimina un record
   - Richiede API Key

9. **API Specifiche**
   - Verranno create API specifiche per determinate interrogazioni su tabelle e viste (da implementare successivamente)
   

#### Metadata Operations

##### Get All Views
```bash
GET /api/metadata/views
Headers: X-API-Key: your-api-key
```

**Response:**
```json
["vw_ActiveUsers", "vw_OrderSummary", "vw_ProductCatalog"]
```

##### Get All Database Objects
```bash
GET /api/metadata/objects
Headers: X-API-Key: your-api-key
```

**Response:**
```json
[
  {
    "name": "Users",
    "type": "TABLE",
    "schema": "dbo",
    "remarks": "User information table"
  },
  {
    "name": "vw_ActiveUsers",
    "type": "VIEW",
    "schema": "dbo",
    "remarks": "Active users view"
  }
]
```

##### Get Table Schema
```bash
GET /api/metadata/tables/Users/schema
Headers: X-API-Key: your-api-key
```

**Response:**
```json
{
  "objectName": "Users",
  "objectType": "TABLE",
  "schema": "dbo",
  "catalog": "TestDB",
  "columns": [
    {
      "columnName": "id",
      "dataType": "INT",
      "columnSize": 10,
      "decimalDigits": 0,
      "nullable": false,
      "defaultValue": null,
      "primaryKey": true,
      "autoIncrement": true,
      "ordinalPosition": 1,
      "remarks": "Unique identifier"
    },
    {
      "columnName": "username",
      "dataType": "VARCHAR",
      "columnSize": 100,
      "decimalDigits": 0,
      "nullable": false,
      "defaultValue": null,
      "primaryKey": false,
      "autoIncrement": false,
      "ordinalPosition": 2,
      "remarks": null
    },
    {
      "columnName": "email",
      "dataType": "VARCHAR",
      "columnSize": 255,
      "decimalDigits": 0,
      "nullable": false,
      "defaultValue": null,
      "primaryKey": false,
      "autoIncrement": false,
      "ordinalPosition": 3,
      "remarks": null
    }
  ],
  "primaryKeys": ["id"],
  "foreignKeys": [
    {
      "constraintName": "FK_Orders_Users",
      "columnName": "user_id",
      "referencedTable": "Users",
      "referencedColumn": "id",
      "updateRule": "CASCADE",
      "deleteRule": "RESTRICT"
    }
  ],
  "indexes": [
    {
      "indexName": "IX_Users_Email",
      "unique": true,
      "columns": ["email"],
      "indexType": "BTREE"
    }
  ],
  "remarks": "User information table",
  "rowCount": 1250
}
```

##### Get View Schema
```bash
GET /api/metadata/views/vw_ActiveUsers/schema
Headers: X-API-Key: your-api-key
```

**Response:**
```json
{
  "objectName": "vw_ActiveUsers",
  "objectType": "VIEW",
  "schema": "dbo",
  "catalog": "TestDB",
  "columns": [
    {
      "columnName": "id",
      "dataType": "INT",
      "columnSize": 10,
      "nullable": false,
      "primaryKey": false,
      "autoIncrement": false,
      "ordinalPosition": 1
    },
    {
      "columnName": "username",
      "dataType": "VARCHAR",
      "columnSize": 100,
      "nullable": false,
      "primaryKey": false,
      "autoIncrement": false,
      "ordinalPosition": 2
    }
  ],
  "primaryKeys": [],
  "foreignKeys": [],
  "indexes": [],
  "viewDefinition": "CREATE VIEW vw_ActiveUsers AS SELECT id, username, email FROM Users WHERE status = 'active'",
  "remarks": "View of active users"
}
```   

### 3. Autenticazione API Key

#### Implementazione

```csharp
// ApiKeyAuthenticationMiddleware.cs
public class ApiKeyAuthenticationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IConfiguration _configuration;

    public ApiKeyAuthenticationMiddleware(RequestDelegate next, IConfiguration configuration)
    {
        _next = next;
        _configuration = configuration;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Salta autenticazione per /api/health e /swagger
        if (context.Request.Path.StartsWithSegments("/api/health") ||
            context.Request.Path.StartsWithSegments("/swagger"))
        {
            await _next(context);
            return;
        }

        if (!context.Request.Headers.TryGetValue("X-API-Key", out var extractedApiKey))
        {
            context.Response.StatusCode = 401;
            await context.Response.WriteAsJsonAsync(new ErrorResponse
            {
                Timestamp = DateTime.UtcNow,
                Status = 401,
                Error = "Unauthorized",
                Message = "API Key is missing",
                Path = context.Request.Path
            });
            return;
        }

        var apiKeys = _configuration["ApiKeySettings:Keys"]?.Split(',') ?? Array.Empty<string>();
        
        if (!apiKeys.Contains(extractedApiKey))
        {
            context.Response.StatusCode = 401;
            await context.Response.WriteAsJsonAsync(new ErrorResponse
            {
                Timestamp = DateTime.UtcNow,
                Status = 401,
                Error = "Unauthorized",
                Message = "Invalid API Key",
                Path = context.Request.Path
            });
            return;
        }

        await _next(context);
    }
}
```

- Header HTTP: `X-API-Key: <your-api-key>`
- Le API keys sono configurabili via environment variables o appsettings
- Formato: `ApiKeySettings__Keys=key1,key2,key3` (supporto multi-key)
- Risposta 401 se API key mancante o invalida

### 4. Configurazione

#### appsettings.json

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "Microsoft.EntityFrameworkCore": "Information"
    }
  },
  "AllowedHosts": "*",
  "ConnectionStrings": {
    "DefaultConnection": "Server=${DB_HOST};Database=${DB_NAME};User Id=${DB_USER};Password=${DB_PASSWORD};TrustServerCertificate=True;MultipleActiveResultSets=true"
  },
  "ApiKeySettings": {
    "Keys": "default-key-change-me"
  },
  "QuerySettings": {
    "TimeoutSeconds": 30,
    "MaxRows": 1000,
    "AllowedCommands": ["SELECT"],
    "EnableQueryValidation": true
  },
  "Cors": {
    "AllowedOrigins": ["http://localhost:3000"],
    "AllowedMethods": ["GET", "POST", "PUT", "DELETE"],
    "AllowedHeaders": ["*"]
  }
}
```

#### appsettings.Docker.json

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=sqlserver,1433;Database=TestDB;User Id=sa;Password=YourStrong@Password;TrustServerCertificate=True;MultipleActiveResultSets=true;Connection Timeout=30"
  },
  "ApiKeySettings": {
    "Keys": "test-key-123,production-key-456"
  }
}
```

### 5. Program.cs - Configurazione Completa

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using TIRConnector.API.Data;
using TIRConnector.API.Middleware;
using TIRConnector.API.Services;
using TIRConnector.API.Filters;

var builder = WebApplication.CreateBuilder(args);

// Configurazione DbContext
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Servizi
builder.Services.AddScoped<IQueryService, QueryService>();
builder.Services.AddScoped<ITableService, TableService>();

// Controllers e Filters
builder.Services.AddControllers(options =>
{
    options.Filters.Add<GlobalExceptionFilter>();
});

// Swagger/OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "TIRConnector API",
        Version = "v1",
        Description = "REST API Connector per SQL Server con autenticazione API Key",
        Contact = new OpenApiContact
        {
            Name = "Support Team",
            Email = "support@example.com"
        }
    });

    // Configurazione API Key Security
    c.AddSecurityDefinition("ApiKey", new OpenApiSecurityScheme
    {
        Description = "API Key necessaria per accedere agli endpoint. Header: X-API-Key",
        Type = SecuritySchemeType.ApiKey,
        Name = "X-API-Key",
        In = ParameterLocation.Header,
        Scheme = "ApiKeyScheme"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "ApiKey"
                }
            },
            Array.Empty<string>()
        }
    });
});

// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Health Checks
builder.Services.AddHealthChecks()
    .AddSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")!);

// Response Compression
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
});

var app = builder.Build();

// Middleware Pipeline
app.UseResponseCompression();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "TIRConnector API v1");
        c.RoutePrefix = "swagger";
    });
}

app.UseCors("AllowAll");

// Custom API Key Authentication Middleware
app.UseMiddleware<ApiKeyAuthenticationMiddleware>();

app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/api/health");

app.Run();
```

### 6. Gestione Errori

#### GlobalExceptionFilter.cs

```csharp
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Data.SqlClient;
using TIRConnector.API.Models.DTOs;

namespace TIRConnector.API.Filters
{
    public class GlobalExceptionFilter : IExceptionFilter
    {
        private readonly ILogger<GlobalExceptionFilter> _logger;

        public GlobalExceptionFilter(ILogger<GlobalExceptionFilter> logger)
        {
            _logger = logger;
        }

        public void OnException(ExceptionContext context)
        {
            var statusCode = context.Exception switch
            {
                SqlException => 500,
                ArgumentException => 400,
                InvalidOperationException => 400,
                UnauthorizedAccessException => 401,
                _ => 500
            };

            _logger.LogError(context.Exception, "An error occurred: {Message}", context.Exception.Message);

            var errorResponse = new ErrorResponse
            {
                Timestamp = DateTime.UtcNow,
                Status = statusCode,
                Error = statusCode == 500 ? "Internal Server Error" : "Bad Request",
                Message = context.Exception.Message,
                Path = context.HttpContext.Request.Path
            };

            context.Result = new ObjectResult(errorResponse)
            {
                StatusCode = statusCode
            };

            context.ExceptionHandled = true;
        }
    }
}
```

Response formato:
```json
{
  "timestamp": "2025-11-18T10:30:00Z",
  "status": 400,
  "error": "Bad Request",
  "message": "Invalid SQL syntax",
  "path": "/api/query/execute"
}
```

### 7. Docker Setup

#### Dockerfile (multi-stage ottimizzato)

```dockerfile
# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy csproj and restore dependencies
COPY ["TIRConnector.API/TIRConnector.API.csproj", "TIRConnector.API/"]
RUN dotnet restore "TIRConnector.API/TIRConnector.API.csproj"

# Copy everything else and build
COPY . .
WORKDIR "/src/TIRConnector.API"
RUN dotnet build "TIRConnector.API.csproj" -c Release -o /app/build

# Stage 2: Publish
FROM build AS publish
RUN dotnet publish "TIRConnector.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Stage 3: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

COPY --from=publish /app/publish .

# Health check
HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
  CMD curl --fail http://localhost:8080/api/health || exit 1

ENTRYPOINT ["dotnet", "TIRConnector.API.dll"]
```

#### .dockerignore

```
**/.dockerignore
**/.git
**/.gitignore
**/.vs
**/.vscode
**/bin
**/obj
**/*.user
**/node_modules
```

#### docker-compose.yml

```yaml
version: '3.8'

services:
  # SQL Server commentato - usa SQL Server esistente
  # sqlserver:
  #   image: mcr.microsoft.com/mssql/server:2022-latest
  #   environment:
  #     ACCEPT_EULA: Y
  #     SA_PASSWORD: YourStrong@Password
  #     MSSQL_PID: Developer
  #   ports:
  #     - "1433:1433"
  #   volumes:
  #     - sqlserver-data:/var/opt/mssql
  #   healthcheck:
  #     test: /opt/mssql-tools/bin/sqlcmd -S localhost -U sa -P "YourStrong@Password" -Q "SELECT 1"
  #     interval: 10s
  #     timeout: 5s
  #     retries: 5

  api:
    build:
      context: .
      dockerfile: Dockerfile
    ports:
      - "8080:8080"
    environment:
      - ASPNETCORE_ENVIRONMENT=Docker
      - ASPNETCORE_URLS=http://+:8080
      - DB_HOST=host.docker.internal
      - DB_PORT=1433
      - DB_NAME=TestDB
      - DB_USER=sa
      - DB_PASSWORD=YourStrong@Password
      - ApiKeySettings__Keys=test-key-123,production-key-456
    extra_hosts:
      - "host.docker.internal:host-gateway"
    # depends_on:
    #   sqlserver:
    #     condition: service_healthy

# volumes:
#   sqlserver-data:
```

### 8. Entity Framework Core - DbContext

#### ApplicationDbContext.cs

```csharp
using Microsoft.EntityFrameworkCore;

namespace TIRConnector.API.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        // Esempio di DbSet - aggiungi i tuoi modelli
        // public DbSet<User> Users { get; set; }
        // public DbSet<Product> Products { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configurazioni Fluent API
            // modelBuilder.Entity<User>()
            //     .HasKey(u => u.Id);
        }
    }
}
```

#### Comandi EF Core

```bash
# Installare EF Core Tools
dotnet tool install --global dotnet-ef

# Creare migration iniziale
dotnet ef migrations add InitialCreate --project TIRConnector.API

# Applicare migration al database
dotnet ef database update --project TIRConnector.API

# Scaffold da database esistente (reverse engineering)
dotnet ef dbcontext scaffold "Server=localhost;Database=TestDB;User Id=sa;Password=YourPassword;TrustServerCertificate=True" Microsoft.EntityFrameworkCore.SqlServer --output-dir Models/Entities --context-dir Data --context ApplicationDbContext --force
```

### 9. Swagger/OpenAPI Configuration

- Swagger UI disponibile su `/swagger`
- OpenAPI JSON su `/swagger/v1/swagger.json`
- Security scheme configurato per `X-API-Key` header
- Tutti gli endpoint documentati con XML comments
- Esempi di request/response per AI agents

#### Abilitare XML Documentation

Nel file `.csproj`:
```xml
<PropertyGroup>
  <GenerateDocumentationFile>true</GenerateDocumentationFile>
  <NoWarn>$(NoWarn);1591</NoWarn>
</PropertyGroup>
```

### 10. Sicurezza

- **SQL Injection Prevention**: EF Core usa parametrizzazione automatica
- **API Key Authentication**: Middleware custom
- **HTTPS Redirect**: Configurabile per production
- **CORS**: Policy configurabile
- **Request Validation**: Model validation automatica
- **Rate Limiting** (opzionale): ASP.NET Core Rate Limiting middleware
- **Logging**: Serilog per structured logging

### 11. Testing

#### TIRConnector.Tests.csproj

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.8.0" />
    <PackageReference Include="xUnit" Version="2.6.2" />
    <PackageReference Include="xUnit.runner.visualstudio" Version="2.5.4" />
    <PackageReference Include="Moq" Version="4.20.70" />
    <PackageReference Include="FluentAssertions" Version="6.12.0" />
    <PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" Version="8.0.0" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\TIRConnector.API\TIRConnector.API.csproj" />
  </ItemGroup>
</Project>
```

```bash
# Eseguire test
dotnet test

# Con coverage
dotnet test /p:CollectCoverage=true /p:CoverageReportFormat=opencover
```

### 12. Logging con Serilog (Opzionale)

```bash
dotnet add package Serilog.AspNetCore
dotnet add package Serilog.Sinks.Console
dotnet add package Serilog.Sinks.File
```

```csharp
// In Program.cs
builder.Host.UseSerilog((context, configuration) =>
    configuration.ReadFrom.Configuration(context.Configuration));
```

## Comandi di Utilizzo

```bash
# Restore dependencies
dotnet restore

# Build del progetto
dotnet build

# Run in development
dotnet run --project TIRConnector.API

# Build Docker image
docker build -t tirconnector:latest .

# Run con Docker Compose
docker-compose up -d

# Verifica stato
curl http://localhost:8080/api/health

# Test con API Key
curl -H "X-API-Key: test-key-123" http://localhost:8080/api/tables

# Accesso Swagger UI
http://localhost:8080/swagger

# Stop containers
docker-compose down

# Logs
docker-compose logs -f api
```

## Note Importanti

1. **Connection String**: Configurare correttamente per ambiente Docker (usa `host.docker.internal` per SQL Server su host)
2. **TrustServerCertificate**: Impostato su `true` per development, valutare certificati validi in production
3. **Secrets**: Usare Azure Key Vault, User Secrets o Docker Secrets in production
4. **Performance**: Connection pooling gestito automaticamente da EF Core
5. **AI Agents**: OpenAPI spec generata automaticamente da Swashbuckle

## Deliverable Attesi

1. ✅ Codice sorgente completo e funzionante
2. ✅ Dockerfile ottimizzato multi-stage
3. ✅ docker-compose.yml configurato
4. ✅ README con istruzioni complete
5. ✅ Swagger/OpenAPI documentation
6. ✅ Unit tests e integration tests
7. ✅ Logging strutturato

## Estensioni Future

- Azure Application Insights per monitoring
- Redis per caching distribuito
- Azure API Management per gateway
- Azure Container Apps per deployment
- GitHub Actions CI/CD pipeline
- Kubernetes manifests (Helm charts)