# syntax=docker/dockerfile:1.10
FROM mcr.microsoft.com/dotnet/sdk:10.0.302 AS build
WORKDIR /src
COPY . .
RUN dotnet publish apps/monitors/myatmmonitor/MyAtmMonitor/MyAtmMonitor.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
RUN apt-get update \
    && apt-get install -y --no-install-recommends libgssapi-krb5-2 \
    && rm -rf /var/lib/apt/lists/*

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
COPY --from=build /app/publish .
USER $APP_UID
ENTRYPOINT ["dotnet", "MyAtmMonitor.dll"]
