# syntax=docker/dockerfile:1
# Zwei Images aus einem Dockerfile (ADR 0006):
#   docker build --target game  .   → Spiel (Standard, letztes Ziel)
#   docker build --target admin .   → Admin-Interface
# Basis-Images per Digest gepinnt; Dependabot aktualisiert Tag und Digest.
# Multi-Arch (amd64, arm64) als Cross-Build: Build-Stages laufen auf der Build-Plattform,
# nur die Laufzeit-Images sind zielspezifisch und enthalten kein RUN – keine Emulation nötig.

# 1) Client bauen: Spiel-Client, CSS für Startseite und Admin, passkeys.js (landen in den wwwroot-Ordnern)
FROM --platform=$BUILDPLATFORM node:24-slim@sha256:55fb5f5a527ea2c5c4a95f963e48ef90aa38c7b346054fdc8ee9957a2b55d205 AS client
WORKDIR /src
COPY buf.yaml buf.gen.yaml ./
COPY contracts/ contracts/
COPY client/package.json client/package-lock.json client/.npmrc client/
RUN cd client && npm ci
COPY client/ client/
# Tailwind scannt die Razor-Seiten von Spiel und Admin.
COPY server/BrowserGame.Server/Pages/ server/BrowserGame.Server/Pages/
COPY server/BrowserGame.Admin/Pages/ server/BrowserGame.Admin/Pages/
RUN cd client && npm run build

# 2) Gemeinsamer Restore für Spiel und Admin
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0@sha256:747cbade5bfd918a175ac32f8f1d931edec2c7d0d5397a3de72eaef1ac1f66d2 AS restore
ARG TARGETARCH
ENV DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1
WORKDIR /src
COPY contracts/ contracts/
COPY server/global.json server/Directory.Build.props server/
COPY server/BrowserGame.Data/BrowserGame.Data.csproj server/BrowserGame.Data/
COPY server/BrowserGame.Server/BrowserGame.Server.csproj server/BrowserGame.Server/
COPY server/BrowserGame.Admin/BrowserGame.Admin.csproj server/BrowserGame.Admin/
RUN dotnet restore server/BrowserGame.Server/BrowserGame.Server.csproj --arch $TARGETARCH \
 && dotnet restore server/BrowserGame.Admin/BrowserGame.Admin.csproj --arch $TARGETARCH
COPY server/BrowserGame.Data/ server/BrowserGame.Data/

# 3a) Spiel publishen
FROM --platform=$BUILDPLATFORM restore AS publish-game
ARG TARGETARCH
COPY server/BrowserGame.Server/ server/BrowserGame.Server/
COPY --from=client /src/server/BrowserGame.Server/wwwroot/ server/BrowserGame.Server/wwwroot/
RUN dotnet publish server/BrowserGame.Server/BrowserGame.Server.csproj --configuration Release --arch $TARGETARCH --no-restore --output /app

# 3b) Admin publishen
FROM --platform=$BUILDPLATFORM restore AS publish-admin
ARG TARGETARCH
COPY server/BrowserGame.Admin/ server/BrowserGame.Admin/
COPY --from=client /src/server/BrowserGame.Admin/wwwroot/ server/BrowserGame.Admin/wwwroot/
RUN dotnet publish server/BrowserGame.Admin/BrowserGame.Admin.csproj --configuration Release --arch $TARGETARCH --no-restore --output /app

# 4a) Laufzeit Admin: chiseled (keine Shell, kein Paketmanager), non-root-User "app"
FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled@sha256:5165aeeb688df15e869a56a7584bb94bf16a14e0b3ba77b49fcdbe350f6b8020 AS admin
WORKDIR /app
COPY --from=publish-admin /app ./
USER $APP_UID
EXPOSE 8080
HEALTHCHECK --interval=30s --timeout=5s --start-period=10s --retries=3 \
  CMD ["dotnet", "BrowserGame.Admin.dll", "--healthcheck"]
ENTRYPOINT ["dotnet", "BrowserGame.Admin.dll"]

# 4b) Laufzeit Spiel (Standardziel)
FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled@sha256:5165aeeb688df15e869a56a7584bb94bf16a14e0b3ba77b49fcdbe350f6b8020 AS game
WORKDIR /app
COPY --from=publish-game /app ./
USER $APP_UID
EXPOSE 8080
HEALTHCHECK --interval=30s --timeout=5s --start-period=10s --retries=3 \
  CMD ["dotnet", "BrowserGame.Server.dll", "--healthcheck"]
ENTRYPOINT ["dotnet", "BrowserGame.Server.dll"]
