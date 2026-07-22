# syntax=docker/dockerfile:1.10
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN --mount=type=secret,id=nuget_credentials,env=NuGetPackageSourceCredentials_rvt \
    dotnet publish omnidotsmonitor/OmnidotsMonitor/OmnidotsMonitor.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
COPY --from=build /app/publish .
USER $APP_UID
ENTRYPOINT ["dotnet", "OmnidotsMonitor.dll"]
