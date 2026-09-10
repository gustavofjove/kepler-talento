FROM mcr.microsoft.com/dotnet/sdk:10.0.102-alpine3.23 AS build
WORKDIR /src
COPY global.json ./
COPY backend/Directory.Build.props backend/Directory.Packages.props backend/KeplerTalento.slnx ./backend/
COPY backend/Domain/Domain.csproj backend/Domain/
COPY backend/Application/Application.csproj backend/Application/
COPY backend/Infrastructure/Infrastructure.csproj backend/Infrastructure/
COPY backend/Web/Web.csproj backend/Web/
COPY backend/Tests/UnitTests/UnitTests.csproj backend/Tests/UnitTests/
COPY backend/Tests/IntegrationTests/IntegrationTests.csproj backend/Tests/IntegrationTests/
RUN dotnet restore backend/Web/Web.csproj
COPY backend/ backend/
RUN dotnet publish backend/Web/Web.csproj --no-restore -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0.2-alpine3.23 AS runtime
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_EnableDiagnostics=0
COPY --from=build /app/publish ./
USER root
RUN apk add --no-cache krb5-libs su-exec \
    && mkdir -p /var/lib/kepler-talento/documents \
    && chown -R app:app /var/lib/kepler-talento /app
COPY backend/docker-entrypoint.sh /usr/local/bin/kepler-entrypoint
RUN chmod 0755 /usr/local/bin/kepler-entrypoint
EXPOSE 8080
ENTRYPOINT ["/usr/local/bin/kepler-entrypoint", "dotnet", "Web.dll"]
