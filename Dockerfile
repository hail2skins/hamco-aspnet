# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy solution and project files first (for better caching)
COPY Hamco.slnx ./
COPY src/Hamco.Api/Hamco.Api.csproj src/Hamco.Api/
COPY src/Hamco.Core/Hamco.Core.csproj src/Hamco.Core/
COPY src/Hamco.Data/Hamco.Data.csproj src/Hamco.Data/
COPY src/Hamco.Services/Hamco.Services.csproj src/Hamco.Services/

# Copy all source files
COPY . .

# Restore dependencies for entire solution
RUN dotnet restore

# Build and publish
RUN dotnet publish src/Hamco.Api/Hamco.Api.csproj -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Copy published files
COPY --from=build /app/publish .

# Expose port (Railway sets PORT env var)
EXPOSE 8080

# Run the app
ENTRYPOINT ["dotnet", "Hamco.Api.dll"]
