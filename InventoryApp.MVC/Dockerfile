# Stage 1 - Build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy only the project files
COPY InventoryApp.Core/InventoryApp.Core.csproj InventoryApp.Core/
COPY InventoryApp.MVC/InventoryApp.MVC.csproj InventoryApp.MVC/

# Restore
RUN dotnet restore InventoryApp.MVC/InventoryApp.MVC.csproj

# Copy the rest of the source code (including appsettings.json)
COPY InventoryApp.Core/ ./InventoryApp.Core/
COPY InventoryApp.MVC/ ./InventoryApp.MVC/

# Publish
RUN dotnet publish InventoryApp.MVC/InventoryApp.MVC.csproj -c Release -o /app/publish

# Stage 2 - Runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/publish .

EXPOSE 80
ENTRYPOINT ["dotnet", "InventoryApp.MVC.dll"]
