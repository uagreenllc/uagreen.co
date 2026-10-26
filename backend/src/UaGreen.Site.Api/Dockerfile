FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY src/UaGreen.Site.Api/UaGreen.Site.Api.csproj src/UaGreen.Site.Api/
RUN dotnet restore src/UaGreen.Site.Api/UaGreen.Site.Api.csproj
COPY src/ src/
RUN dotnet publish src/UaGreen.Site.Api/UaGreen.Site.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
# aspnet:9.0 ships a non-root `app` user; reuse it.
RUN (id -u app >/dev/null 2>&1 || useradd -m -u 1000 app) && chown -R app /app
USER app
COPY --from=build --chown=app:app /app .
EXPOSE 8080
ENTRYPOINT ["dotnet", "UaGreen.Site.Api.dll"]
