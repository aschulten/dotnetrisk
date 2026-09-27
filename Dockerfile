FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY DotnetRisk.sln ./
COPY DotnetRisk.Api/DotnetRisk.Api.csproj DotnetRisk.Api/
COPY DotnetRisk.Api.Tests/DotnetRisk.Api.Tests.csproj DotnetRisk.Api.Tests/
RUN dotnet restore DotnetRisk.Api/DotnetRisk.Api.csproj

COPY DotnetRisk.Api/ DotnetRisk.Api/
RUN dotnet publish DotnetRisk.Api/DotnetRisk.Api.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_EnableDiagnostics=0
EXPOSE 8080
COPY --from=build /app/publish .
USER $APP_UID
ENTRYPOINT ["dotnet", "DotnetRisk.Api.dll"]
