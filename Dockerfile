# Multi-stage build for TIRConnector ASP.NET Core 8.0

# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy csproj and restore dependencies
COPY TIRConnector.API/TIRConnector.API.csproj TIRConnector.API/
RUN dotnet restore "TIRConnector.API/TIRConnector.API.csproj"

# Copy everything else and build
COPY TIRConnector.API/ TIRConnector.API/
WORKDIR "/src/TIRConnector.API"
RUN dotnet build "TIRConnector.API.csproj" -c Release -o /app/build

# Stage 2: Publish
FROM build AS publish
RUN dotnet publish "TIRConnector.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Stage 3: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# Create logs directory
RUN mkdir -p /app/logs

# Copy published application
COPY --from=publish /app/publish .

# Set environment variables for ASP.NET Core
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

# Expose port
EXPOSE 8080

# Health check
HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
  CMD curl -f http://localhost:8080/api/health || exit 1

ENTRYPOINT ["dotnet", "TIRConnector.API.dll"]
