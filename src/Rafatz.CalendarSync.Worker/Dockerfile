FROM mcr.microsoft.com/dotnet/runtime:10.0 AS base
USER $APP_UID
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["Rafatz.CalendarSync.Worker/Rafatz.CalendarSync.Worker.csproj", "Rafatz.CalendarSync.Worker/"]
RUN dotnet restore "Rafatz.CalendarSync.Worker/Rafatz.CalendarSync.Worker.csproj"
COPY . .
WORKDIR "/src/Rafatz.CalendarSync.Worker"
RUN dotnet build "./Rafatz.CalendarSync.Worker.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./Rafatz.CalendarSync.Worker.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Rafatz.CalendarSync.Worker.dll"]
