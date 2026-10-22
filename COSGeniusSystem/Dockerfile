# Base image for running the application
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080

# Build image with .NET SDK
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["COSGeniusSystem/COSGeniusSystem.csproj", "COSGeniusSystem/"]
RUN dotnet restore "COSGeniusSystem/COSGeniusSystem.csproj"
COPY . .
WORKDIR "/src/COSGeniusSystem"
RUN dotnet build "COSGeniusSystem.csproj" -c $BUILD_CONFIGURATION -o /app/build

# Publish application
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "COSGeniusSystem.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# Final stage/image
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENV ASPNETCORE_HTTP_PORTS=8080
ENV ASPNETCORE_ENVIRONMENT=Development
ENTRYPOINT ["dotnet", "COSGeniusSystem.dll"]
