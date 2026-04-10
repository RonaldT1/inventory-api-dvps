FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080

ENV ASPNETCORE_HTTP_PORTS=8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ApiInventario/ApiInventario.csproj ApiInventario/
RUN dotnet restore ApiInventario/ApiInventario.csproj

COPY . .
RUN dotnet publish ApiInventario/ApiInventario.csproj -c Release -o /app/publish --no-restore

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "ApiInventario.dll"]
