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

# .NET 8+ 官方镜像已内置非 root 用户 'app'（UID 1654），无需手动创建
# Copy published output
COPY --from=build --chown=app:app /app/publish .

# Copy prompt templates to /prompts
# 代码通过 AppContext.BaseDirectory(/app) 向上 5 层计算路径，结果为 /prompts
COPY --from=build /repo/prompts /prompts
RUN chown -R app:app /prompts

# Log directory (writable by app user)
RUN mkdir -p logs && chown app:app logs

USER app

EXPOSE 8080
ENTRYPOINT ["dotnet", "MuseSpace.Api.dll"]
