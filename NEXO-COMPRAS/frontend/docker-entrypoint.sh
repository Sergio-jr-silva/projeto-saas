#!/bin/sh
set -e

# Injeção em tempo de execução da URL da API (sem recompilar Vite)
TARGET_API_URL="${VITE_API_URL:-${API_URL:-}}"

echo "[NEXO-FRONTEND] Inicializando container..."
echo "[NEXO-FRONTEND] API_URL configurada: '${TARGET_API_URL}'"

cat <<EOF > /usr/share/nginx/html/config.js
// NEXO COMPRAS — CONFIGURAÇÃO DINÂMICA GERADA EM RUNTIME PELO DOCKER ENTRYPOINT
window.__NEXO_CONFIG__ = {
  API_URL: "${TARGET_API_URL}"
};
EOF

# Suporte à variável $PORT do Railway (caso o Railway injete $PORT para o Nginx)
if [ -n "$PORT" ] && [ "$PORT" != "80" ]; then
    echo "[NEXO-FRONTEND] Ajustando porta de escuta do Nginx para ${PORT}..."
    sed -i "s/listen 80;/listen ${PORT};/g" /etc/nginx/conf.d/default.conf
fi

echo "[NEXO-FRONTEND] Iniciando Nginx..."
exec nginx -g "daemon off;"
