# Multi-stage build for TIRConnector ASP.NET Core 8.0 + Svelte Admin UI

# Stage 1: Build Svelte Admin UI
FROM node:20-alpine AS svelte-build
WORKDIR /app
COPY admin-ui/package.json ./
RUN npm install
COPY admin-ui/ ./
RUN npm run build

# Stage 2: Build .NET
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy csproj and restore dependencies
COPY TIRConnector.API/TIRConnector.API.csproj TIRConnector.API/
RUN dotnet restore "TIRConnector.API/TIRConnector.API.csproj"

# Copy everything else and build
COPY TIRConnector.API/ TIRConnector.API/
WORKDIR "/src/TIRConnector.API"
RUN dotnet build "TIRConnector.API.csproj" -c Release -o /app/build

# Stage 3: Publish .NET
FROM build AS publish
RUN dotnet publish "TIRConnector.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Stage 4: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# Create directories
RUN mkdir -p /app/logs /app/wwwroot/admin

# Copy published .NET application
COPY --from=publish /app/publish .

# Copy Svelte build output to wwwroot/admin
COPY --from=svelte-build /app/dist ./wwwroot/admin

# Set environment variables for ASP.NET Core
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

# Expose port
EXPOSE 8080

# Health check
HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
  CMD curl -f http://localhost:8080/api/health || exit 1

ENTRYPOINT ["dotnet", "TIRConnector.API.dll"]
