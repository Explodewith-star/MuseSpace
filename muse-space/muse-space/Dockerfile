# ── Build Stage ──────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /repo

# Copy solution & project files first (layer cache for restore)
COPY MuseSpace.slnx ./
COPY src/MuseSpace.Api/MuseSpace.Api.csproj               src/MuseSpace.Api/
COPY src/MuseSpace.Application/MuseSpace.Application.csproj src/MuseSpace.Application/
COPY src/MuseSpace.Contracts/MuseSpace.Contracts.csproj   src/MuseSpace.Contracts/
COPY src/MuseSpace.Domain/MuseSpace.Domain.csproj         src/MuseSpace.Domain/
COPY src/MuseSpace.Infrastructure/MuseSpace.Infrastructure.csproj src/MuseSpace.Infrastructure/

RUN dotnet restore src/MuseSpace.Api/MuseSpace.Api.csproj

# Copy everything else and publish
COPY . .
RUN dotnet publish src/MuseSpace.Api/MuseSpace.Api.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

# ── Runtime Stage ─────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Create non-root user
RUN addgroup --system appgroup && adduser --system --ingroup appgroup appuser

# Copy published output
COPY --from=build /app/publish .

# Copy prompt templates
COPY --from=build /repo/prompts ./prompts

# Log directory (writable by appuser)
RUN mkdir -p logs && chown -R appuser:appgroup logs

USER appuser

EXPOSE 8080
ENTRYPOINT ["dotnet", "MuseSpace.Api.dll"]
