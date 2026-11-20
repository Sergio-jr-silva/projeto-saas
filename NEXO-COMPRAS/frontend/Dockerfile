# ==========================================
# NEXO COMPRAS — FRONTEND DOCKERFILE (MULTI-STAGE)
# ==========================================

# Estágio 1: Build da aplicação com Node.js
FROM node:22-alpine AS builder
WORKDIR /app

# Copia dependências primeiro para cache eficiente de camadas Docker
COPY package*.json ./
RUN npm ci || npm install

# Copia o código-fonte e compila para produção
COPY . .
RUN npm run build

# Estágio 2: Servidor Web Nginx Alpine leve para servir os arquivos estáticos
FROM nginx:1.27-alpine AS runner

# Remove a página padrão do Nginx
RUN rm -rf /usr/share/nginx/html/*

# Copia os artefatos compilados da SPA
COPY --from=builder /app/dist /usr/share/nginx/html

# Copia configuração otimizada do Nginx (SPA routing, Gzip, Security Headers, Caching)
COPY nginx.conf /etc/nginx/conf.d/default.conf

# Script de injeção da URL da API em tempo de execução
COPY docker-entrypoint.sh /docker-entrypoint.sh
RUN chmod +x /docker-entrypoint.sh

# Porta padrão de escuta
EXPOSE 80

# Health check funcional
HEALTHCHECK --interval=30s --timeout=5s --start-period=5s --retries=3 \
  CMD wget -qO- http://localhost:${PORT:-80}/health || exit 1

ENTRYPOINT ["/docker-entrypoint.sh"]
