FROM mcr.microsoft.com/dotnet/runtime:9.0 AS base
USER $APP_UID
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["DatabaseCommon/DatabaseCommon.csproj", "DatabaseCommon/"]
RUN dotnet restore "DatabaseCommon/DatabaseCommon.csproj"
COPY . .
WORKDIR "/src/DatabaseCommon"
RUN dotnet build "./DatabaseCommon.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./DatabaseCommon.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "DatabaseCommon.dll"]
