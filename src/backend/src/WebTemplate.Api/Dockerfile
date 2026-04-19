# ---- Restore Stage ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS restore
WORKDIR /src
COPY src/WebTemplate.Api/WebTemplate.Api.csproj src/WebTemplate.Api/
RUN dotnet restore src/WebTemplate.Api/WebTemplate.Api.csproj

# ---- Build Stage ----
FROM restore AS build
COPY src/ src/
RUN dotnet build src/WebTemplate.Api/WebTemplate.Api.csproj -c Release --no-restore

# ---- Publish Stage ----
FROM build AS publish
RUN dotnet publish src/WebTemplate.Api/WebTemplate.Api.csproj -c Release --no-build -o /app/publish

# ---- Runtime Stage ----
# Base image ships a non-root `app` user (UID 1654). Don't recreate it:
# UID 1000 is already taken by `ubuntu` on this base, and the built-in user
# is MS's recommended non-root default.
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

COPY --from=publish --chown=app:app /app/publish .

USER app

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

# Docker HEALTHCHECK intentionally omitted: the aspnet runtime image does not
# ship curl/wget, and pulling one in just for a healthcheck bloats the image.
# k8s livenessProbe/readinessProbe (see infra/k8s/backend/deployment.yml) hit
# /api/health and /api/health/ready directly via HTTP and are the source of truth.
# Route prefix must stay in sync with HealthController's [Route("api/health")].

ENTRYPOINT ["dotnet", "WebTemplate.Api.dll"]
