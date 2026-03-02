# ── Build stage ──────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project files and restore dependencies
COPY backend/DAL/DAL.csproj backend/DAL/
COPY backend/BLL/BLL.csproj backend/BLL/
COPY backend/JGMS/JGMS.csproj backend/JGMS/
RUN dotnet restore backend/JGMS/JGMS.csproj

# Copy everything and publish
COPY backend/ backend/
RUN dotnet publish backend/JGMS/JGMS.csproj -c Release -o /app/publish

# ── Runtime stage ────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .

# Copy SQL migration files so MigrationRunner can apply them on startup
COPY database/migrations/ ./migrations/

# Port config — cloud platforms (Render) set PORT env var at runtime
EXPOSE 8080
ENV ASPNETCORE_ENVIRONMENT=Production

# Use shell form so $PORT is expanded at runtime; default to 8080
CMD ASPNETCORE_URLS=http://+:${PORT:-8080} dotnet JGMS.dll



