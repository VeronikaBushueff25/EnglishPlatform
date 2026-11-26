# Сборка из корня репозитория: docker build -f src/Host/EnglishPlatform.Host/Dockerfile .
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /repo

# Сначала только проекты, чтобы restore кешировался отдельно от исходников.
COPY global.json Directory.Build.props Directory.Packages.props EnglishPlatform.slnx ./
COPY src/ src/
RUN find src -type f ! -name '*.csproj' -delete && dotnet restore src/Host/EnglishPlatform.Host/EnglishPlatform.Host.csproj

COPY src/ src/
RUN dotnet publish src/Host/EnglishPlatform.Host/EnglishPlatform.Host.csproj \
    -c Release -o /app --no-restore -p:OpenApiGenerateDocuments=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
COPY --from=build /app .
USER app
ENTRYPOINT ["dotnet", "EnglishPlatform.Host.dll"]
