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
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=publish /app/publish .

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

ENTRYPOINT ["dotnet", "WebTemplate.Api.dll"]
