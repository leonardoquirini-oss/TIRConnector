using Microsoft.EntityFrameworkCore;
using Serilog;
using TIRConnector.API.Configuration;
using TIRConnector.API.Data;
using TIRConnector.API.Filters;
using TIRConnector.API.Middleware;
using TIRConnector.API.Services;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File("logs/tirconnector-.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Host.UseSerilog();

// Add services to the container
builder.Services.AddControllers(options =>
{
    options.Filters.Add<GlobalExceptionFilter>();
});

// Configure options
builder.Services.Configure<ApiKeySettings>(builder.Configuration.GetSection("ApiKeySettings"));
builder.Services.Configure<QuerySettings>(builder.Configuration.GetSection("QuerySettings"));
builder.Services.Configure<ValkeySettings>(builder.Configuration.GetSection("Valkey"));
builder.Services.Configure<ContainerCacheSettings>(builder.Configuration.GetSection("ContainerCache"));

// Add DbContext for SQL Server (TIR database)
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Add DbContext for PostgreSQL (Query Templates)
builder.Services.AddDbContext<PostgresDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("PostgresConnection")));

// Add services
builder.Services.AddScoped<IQueryService, QueryService>();
builder.Services.AddScoped<ITableService, TableService>();
builder.Services.AddScoped<IMetadataService, MetadataService>();
builder.Services.AddScoped<IQueryTemplateService, QueryTemplateService>();

// Add Valkey services
builder.Services.AddSingleton<IValkeyService, ValkeyService>();
builder.Services.AddScoped<IContainerCacheService, ContainerCacheService>();

// Add background job for container cache sync
builder.Services.AddHostedService<ContainerCacheSyncJob>();

// Add Health Checks
builder.Services.AddHealthChecks()
    .AddDbContextCheck<ApplicationDbContext>();

// Add CORS
var corsOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? new[] { "*" };
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(corsOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "TIRConnector API",
        Version = "v1",
        Description = "ASP.NET Core 8.0 REST API connector for TIR SQL Server database"
    });

    // Add API Key authentication to Swagger
    options.AddSecurityDefinition("ApiKey", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Name = "X-API-Key",
        Description = "API Key authentication using X-API-Key header"
    });

    options.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "ApiKey"
                }
            },
            Array.Empty<string>()
        }
    });

    // Include XML comments if available
    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath);
    }
});

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    // Enable Swagger in production too for this API
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "TIRConnector API v1");
        options.RoutePrefix = "swagger";
    });
}

app.UseCors();

// Serve static files from wwwroot (admin UI)
app.UseDefaultFiles();
app.UseStaticFiles();

// Custom middleware for API Key authentication
app.UseMiddleware<ApiKeyAuthenticationMiddleware>();

app.UseAuthorization();

app.MapControllers();

// Map health checks
app.MapHealthChecks("/health");

try
{
    Log.Information("Starting TIRConnector API");

    // Log delle API Key configurate
    var apiKeySettings = builder.Configuration.GetSection("ApiKeySettings").Get<ApiKeySettings>();
    if (apiKeySettings != null)
    {
        var keys = apiKeySettings.GetKeys().ToList();
        Log.Information("Configured API Keys ({Count}): {Keys}", keys.Count, string.Join(", ", keys));
    }
    else
    {
        Log.Warning("No API Keys configured!");
    }

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
