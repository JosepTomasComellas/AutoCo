FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["shared/AutoCo.Shared.csproj", "shared/"]
COPY ["api/AutoCo.Api.csproj", "api/"]
RUN dotnet restore "api/AutoCo.Api.csproj"
COPY shared/ shared/
COPY api/ api/
WORKDIR /src/api
RUN dotnet publish -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
COPY web/wwwroot/images/logo2.png ./resources/logo2.png
ENTRYPOINT ["dotnet", "AutoCo.Api.dll"]
