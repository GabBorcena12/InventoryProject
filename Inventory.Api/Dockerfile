# Stage 1 - Build
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy project files
COPY InventoryApp.Core/InventoryApp.Core.csproj InventoryApp.Core/
COPY Inventory.Api/Inventory.Api.csproj Inventory.Api/

# Restore API project (implicitly restores Core project if referenced)
RUN dotnet restore Inventory.Api/Inventory.Api.csproj

# Copy full source code
COPY InventoryApp.Core/ ./InventoryApp.Core/
COPY Inventory.Api/ ./Inventory.Api/

# Publish API only
RUN dotnet publish Inventory.Api/Inventory.Api.csproj -c Release -o /app/publish

# Stage 2 - Runtime
FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app

# Copy published output
COPY --from=build /app/publish .

# Hard-code the port to 5000
ENV ASPNETCORE_URLS=http://0.0.0.0:5000
EXPOSE 5000


# Run the API
ENTRYPOINT ["dotnet", "Inventory.Api.dll"]
