FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["shared/AutoCo.Shared.csproj", "shared/"]
COPY ["web/AutoCo.Web.csproj", "web/"]
RUN dotnet restore "web/AutoCo.Web.csproj"
COPY shared/ shared/
COPY web/ web/
WORKDIR /src/web
RUN dotnet publish -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "AutoCo.Web.dll"]
