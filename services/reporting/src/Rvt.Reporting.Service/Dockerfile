FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Rvt.Reporting.New.slnx ./
COPY Directory.Build.props ./
COPY .editorconfig ./
COPY src ./src
COPY tests ./tests

RUN dotnet restore Rvt.Reporting.New.slnx
RUN dotnet publish src/Rvt.Reporting.Service/Rvt.Reporting.Service.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

COPY --from=build /app/publish ./
USER $APP_UID
ENTRYPOINT ["dotnet", "Rvt.Reporting.Service.dll"]
