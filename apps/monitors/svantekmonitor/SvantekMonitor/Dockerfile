# syntax=docker/dockerfile:1.10
FROM mcr.microsoft.com/dotnet/sdk:10.0.302 AS build
WORKDIR /src
COPY . .
RUN dotnet publish apps/monitors/svantekmonitor/SvantekMonitor/SvantekMonitor.csproj -c Release -o /app/publish /p:UseAppHost=false

# Pinned like the SDK above: aspnet:10.0 floats to the newest 10.0 patch, so a rebuild could
# ship a runtime the repository never pinned. 10.0.10 is the image aspnet:10.0 resolves to today.
FROM mcr.microsoft.com/dotnet/aspnet:10.0.10 AS final
WORKDIR /app
RUN apt-get update \
    && apt-get install -y --no-install-recommends libgssapi-krb5-2 \
    && rm -rf /var/lib/apt/lists/*

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
COPY --from=build /app/publish .
USER $APP_UID
ENTRYPOINT ["dotnet", "SvantekMonitor.dll"]
