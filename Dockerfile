# syntax=docker/dockerfile:1

FROM node:24-bookworm-slim AS devui-build
WORKDIR /src
COPY src/Riftbound.DevUi/package*.json ./src/Riftbound.DevUi/
RUN cd src/Riftbound.DevUi && npm ci
COPY src/Riftbound.DevUi ./src/Riftbound.DevUi
COPY src/Riftbound.Engine ./src/Riftbound.Engine
COPY src/Riftbound.Api ./src/Riftbound.Api
COPY tests/Riftbound.ConformanceTests ./tests/Riftbound.ConformanceTests
RUN cd src/Riftbound.DevUi && npm run build

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS api-build
WORKDIR /src
COPY global.json Directory.Build.props Riftbound.slnx ./
COPY src/Riftbound.Contracts/*.csproj src/Riftbound.Contracts/
COPY src/Riftbound.CardCatalog/*.csproj src/Riftbound.CardCatalog/
COPY src/Riftbound.Engine/*.csproj src/Riftbound.Engine/
COPY src/Riftbound.Persistence/*.csproj src/Riftbound.Persistence/
COPY src/Riftbound.Api/*.csproj src/Riftbound.Api/
RUN dotnet restore src/Riftbound.Api/Riftbound.Api.csproj
COPY data ./data
COPY src ./src
COPY --from=devui-build /src/src/Riftbound.DevUi/dist ./src/Riftbound.DevUi/dist
RUN dotnet publish src/Riftbound.Api/Riftbound.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
COPY --from=api-build /app/publish ./
HEALTHCHECK --interval=30s --timeout=5s --start-period=20s --retries=3 \
    CMD curl -fsS http://127.0.0.1:8080/health || exit 1
ENTRYPOINT ["dotnet", "Riftbound.Api.dll"]
