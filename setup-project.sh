#!/bin/bash

echo "Setting up TIRConnector .NET Project..."

cd "$(dirname "$0")"

# Create .NET solution
dotnet new sln -n TIRConnector

# Add projects to solution
dotnet sln add TIRConnector.API/TIRConnector.API.csproj

# Create test project
cd TIRConnector.Tests
dotnet new xunit
dotnet add package Moq
dotnet add package FluentAssertions
dotnet add package Microsoft.AspNetCore.Mvc.Testing
cd ..

dotnet sln add TIRConnector.Tests/TIRConnector.Tests.csproj

# Restore packages
dotnet restore

echo "✅ Project setup complete!"
echo "Next steps:"
echo "1. Review configuration files"
echo "2. Run: dotnet build"
echo "3. Run: dotnet run --project TIRConnector.API"
echo "4. Open: http://localhost:8080/swagger"
