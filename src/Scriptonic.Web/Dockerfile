# Scriptonic.Web — Umbraco 17 company site + customer portal.
# Build from the repo root:
#   docker build -f src/Scriptonic.Web/Dockerfile -t scriptonic-web:dev .

# ---- Tailwind CSS build ----
FROM node:24-alpine AS assets
WORKDIR /src
COPY src/Scriptonic.Web/package.json src/Scriptonic.Web/package-lock.json ./
RUN npm ci --no-audit --no-fund
COPY src/Scriptonic.Web/Assets ./Assets
COPY src/Scriptonic.Web/Views ./Views
RUN npx @tailwindcss/cli -i Assets/site.css -o /out/site.css --minify

# ---- .NET build ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY src/Scriptonic.Web/Scriptonic.Web.csproj src/Scriptonic.Web/Directory.Packages.props src/Scriptonic.Web/
RUN dotnet restore src/Scriptonic.Web/Scriptonic.Web.csproj
COPY src/ src/
RUN dotnet publish src/Scriptonic.Web/Scriptonic.Web.csproj -c Release -o /app/publish /p:UseAppHost=false

# ---- Runtime ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
COPY --from=assets /out/site.css ./wwwroot/css/site.css

ARG APP_VERSION=dev
ENV Site__AppVersion=${APP_VERSION} \
    ASPNETCORE_URLS=http://+:8080

# SQLite databases + logs, uploaded media
VOLUME ["/app/umbraco/Data", "/app/umbraco/Logs", "/app/wwwroot/media"]
EXPOSE 8080

ENTRYPOINT ["dotnet", "Scriptonic.Web.dll"]
