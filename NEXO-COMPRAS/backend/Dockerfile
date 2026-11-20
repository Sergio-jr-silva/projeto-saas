# ==========================================
# NEXO COMPRAS — BACKEND DOCKERFILE (.NET 9 ASP.NET CORE)
# ==========================================

# Estágio 1: Build & Publish com SDK .NET 9
FROM mcr.microsoft.com/dotnet/sdk:9.0-alpine AS build
WORKDIR /src

# Copia o arquivo do projeto e restaura dependências (cache de camadas Docker)
COPY Nexo.Api.csproj ./
RUN dotnet restore Nexo.Api.csproj

# Copia todo o código-fonte e compila em modo Release
COPY . ./
RUN dotnet publish Nexo.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

# Estágio 2: Runtime enxuto e seguro
FROM mcr.microsoft.com/dotnet/aspnet:9.0-alpine AS final
WORKDIR /app

# Instala curl/wget e suporte a internacionalização (icu-libs e tzdata)
RUN apk add --no-cache icu-libs tzdata curl
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false
ENV LC_ALL=pt_BR.UTF-8
ENV LANG=pt_BR.UTF-8

# Cria diretório para uploads/storage e ajusta permissões
RUN mkdir -p /app/storage && chown -R app:app /app

# Copia os binários compilados do estágio de build
COPY --from=build --chown=app:app /app/publish .

# Execução como usuário seguro não-root (UID 1654 predefinido no .NET 8+)
USER app

# Portas padrão (configurável dinamicamente via variável de ambiente PORT)
ENV ASPNETCORE_URLS=http://+:5000
EXPOSE 5000

# Health check funcional
HEALTHCHECK --interval=30s --timeout=5s --start-period=10s --retries=3 \
  CMD curl --fail http://localhost:${PORT:-5000}/health || exit 1

ENTRYPOINT ["dotnet", "Nexo.Api.dll"]
