# Stage 1 - Build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy only the API and Core project files
COPY InventoryApp.Core/InventoryApp.Core.csproj InventoryApp.Core/
COPY Inventory.Api/Inventory.Api.csproj Inventory.Api/

# Restore API project (implicitly restores Core project if referenced)
RUN dotnet restore Inventory.Api/Inventory.Api.csproj

# Copy full source code needed to build
COPY InventoryApp.Core/ ./InventoryApp.Core/
COPY Inventory.Api/ ./Inventory.Api/

# Publish API only
RUN dotnet publish Inventory.Api/Inventory.Api.csproj -c Release -o /app/publish

# Stage 2 - Runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/publish .

EXPOSE 80
ENTRYPOINT ["dotnet", "Inventory.Api.dll"]
