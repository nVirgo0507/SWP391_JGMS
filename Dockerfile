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

# Port config — Render web services typically route through PORT (default 10000)
EXPOSE 10000
ENV ASPNETCORE_ENVIRONMENT=Production

# Expand PORT at runtime; default to 10000 when PORT is not injected
CMD ["sh", "-c", "ASPNETCORE_URLS=http://0.0.0.0:${PORT:-10000} exec dotnet JGMS.dll"]



