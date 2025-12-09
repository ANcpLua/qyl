FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["qyl.sdk.aspnetcore/qyl.sdk.aspnetcore.csproj", "qyl.sdk.aspnetcore/"]
RUN dotnet restore "qyl.sdk.aspnetcore/qyl.sdk.aspnetcore.csproj"
COPY . .
WORKDIR "/src/qyl.sdk.aspnetcore"
RUN dotnet build "./qyl.sdk.aspnetcore.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./qyl.sdk.aspnetcore.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "qyl.sdk.aspnetcore.dll"]
