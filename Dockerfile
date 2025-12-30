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

# Stage 4: Runtime (Alpine has different SSL config, more compatible with older SQL Server)
FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS final
WORKDIR /app

# Install ICU libs for globalization support and bash for entrypoint
RUN apk add --no-cache icu-libs icu-data-full curl bash openssl

# Configure OpenSSL for older SQL Server compatibility
RUN sed -i 's/providers = provider_sect/providers = provider_sect\n\n[openssl_init]\nssl_conf = ssl_sect\n\n[ssl_sect]\nsystem_default = system_default_sect\n\n[system_default_sect]\nMinProtocol = TLSv1\nCipherString = DEFAULT:@SECLEVEL=0/' /etc/ssl/openssl.cnf || true

# Disable globalization invariant mode to enable full culture support
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false
ENV OPENSSL_CONF=/etc/ssl/openssl.cnf

# Create directories
RUN mkdir -p /app/logs /app/wwwroot/admin

# Copy published .NET application
COPY --from=publish /app/publish .

# Copy Svelte build output to wwwroot/admin
COPY --from=svelte-build /app/dist ./wwwroot/admin

# Copy entrypoint script and fix line endings (Windows -> Unix)
COPY entrypoint.sh /app/entrypoint.sh
RUN sed -i 's/\r$//' /app/entrypoint.sh && chmod +x /app/entrypoint.sh

# Set environment variables for ASP.NET Core
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

# Expose port
EXPOSE 8080

# Health check
HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
  CMD curl -f http://localhost:8080/api/health || exit 1

ENTRYPOINT ["/app/entrypoint.sh"]
