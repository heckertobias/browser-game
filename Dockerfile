# syntax=docker/dockerfile:1
# Basis-Images per Digest gepinnt; Dependabot aktualisiert Tag und Digest.
# Multi-Arch (amd64, arm64) als Cross-Build: Build-Stages laufen auf der Build-Plattform,
# nur das Laufzeit-Image ist zielspezifisch und enthält kein RUN – keine Emulation nötig.

# 1) Client bauen (landet in server/BrowserGame.Server/wwwroot)
FROM --platform=$BUILDPLATFORM node:24-slim@sha256:55fb5f5a527ea2c5c4a95f963e48ef90aa38c7b346054fdc8ee9957a2b55d205 AS client
WORKDIR /src
COPY buf.yaml buf.gen.yaml ./
COPY contracts/ contracts/
COPY client/package.json client/package-lock.json client/.npmrc client/
RUN cd client && npm ci
COPY client/ client/
# Tailwind scannt die Razor-Seiten für das Startseiten-CSS.
COPY server/BrowserGame.Server/Pages/ server/BrowserGame.Server/Pages/
RUN cd client && npm run build

# 2) Server publishen
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0@sha256:747cbade5bfd918a175ac32f8f1d931edec2c7d0d5397a3de72eaef1ac1f66d2 AS server
ARG TARGETARCH
ENV DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1
WORKDIR /src
COPY contracts/ contracts/
COPY server/global.json server/Directory.Build.props server/
COPY server/BrowserGame.Server/BrowserGame.Server.csproj server/BrowserGame.Server/
RUN dotnet restore server/BrowserGame.Server/BrowserGame.Server.csproj --arch $TARGETARCH
COPY server/BrowserGame.Server/ server/BrowserGame.Server/
COPY --from=client /src/server/BrowserGame.Server/wwwroot/ server/BrowserGame.Server/wwwroot/
RUN dotnet publish server/BrowserGame.Server/BrowserGame.Server.csproj --configuration Release --arch $TARGETARCH --no-restore --output /app

# 3) Laufzeit: chiseled (keine Shell, kein Paketmanager), läuft als non-root-User "app"
FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled@sha256:5165aeeb688df15e869a56a7584bb94bf16a14e0b3ba77b49fcdbe350f6b8020
WORKDIR /app
COPY --from=server /app ./
USER $APP_UID
EXPOSE 8080
HEALTHCHECK --interval=30s --timeout=5s --start-period=10s --retries=3 \
  CMD ["dotnet", "BrowserGame.Server.dll", "--healthcheck"]
ENTRYPOINT ["dotnet", "BrowserGame.Server.dll"]
