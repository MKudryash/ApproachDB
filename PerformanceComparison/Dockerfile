FROM mcr.microsoft.com/dotnet/runtime:9.0 AS base
USER $APP_UID
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["PerformanceComparison/PerformanceComparison.csproj", "PerformanceComparison/"]
RUN dotnet restore "PerformanceComparison/PerformanceComparison.csproj"
COPY . .
WORKDIR "/src/PerformanceComparison"
RUN dotnet build "./PerformanceComparison.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./PerformanceComparison.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "PerformanceComparison.dll"]
