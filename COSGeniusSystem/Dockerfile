# Môi trường chạy ứng dụng
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS base

# Hỗ trợ ngôn ngữ, định dạng ngày tháng và SQL Server
RUN apk add --no-cache icu-libs icu-data-full tzdata
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false

USER $APP_UID
WORKDIR /app
EXPOSE 8080

# Môi trường build
FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
ARG BUILD_CONFIGURATION=Release

WORKDIR /src
COPY ["COSGeniusSystem/COSGeniusSystem.csproj", "COSGeniusSystem/"]
RUN dotnet restore "COSGeniusSystem/COSGeniusSystem.csproj"

COPY . .
WORKDIR "/src/COSGeniusSystem"

RUN dotnet build "COSGeniusSystem.csproj" \
    -c $BUILD_CONFIGURATION -o /app/build

# Publish
FROM build AS publish
ARG BUILD_CONFIGURATION=Release

RUN dotnet publish "COSGeniusSystem.csproj" \
    -c $BUILD_CONFIGURATION \
    -o /app/publish \
    /p:UseAppHost=false

# Image cuối cùng
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

ENV ASPNETCORE_HTTP_PORTS=8080
ENV ASPNETCORE_ENVIRONMENT=Development

ENTRYPOINT ["dotnet", "COSGeniusSystem.dll"]