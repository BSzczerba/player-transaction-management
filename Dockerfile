# ─── Build stage ──────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project files first for layer caching (restore only reruns when .csproj changes)
COPY Domain/Domain.csproj                                           Domain/
COPY Application/Application.csproj                                 Application/
COPY Infrastructure/Infrastructure.csproj                           Infrastructure/
COPY player-transaction-management/player-transaction-management.csproj  player-transaction-management/

RUN dotnet restore player-transaction-management/player-transaction-management.csproj

# Copy everything and publish
COPY . .
RUN dotnet publish player-transaction-management/player-transaction-management.csproj \
    -c Release -o /app/publish --no-restore

# ─── Runtime stage ────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Create non-root user for security
RUN adduser --disabled-password --gecos "" appuser && chown -R appuser /app
USER appuser

EXPOSE 8080

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "player-transaction-management.dll"]
