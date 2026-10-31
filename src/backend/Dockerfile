# Build context is the repository root (shared Directory.*.props live there).
# The SDK image's feature band may differ from global.json, so global.json is not copied.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /repo
COPY Directory.Build.props Directory.Packages.props ./
COPY src/backend/ src/backend/
RUN dotnet publish src/backend/DevSync.Api/DevSync.Api.csproj -c Release -o /app/publish

# Runtime: non-root user provided by the aspnet image.
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "DevSync.Api.dll"]
