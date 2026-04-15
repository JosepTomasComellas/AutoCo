FROM nginx:alpine

# Instal·lar openssl per generar certificat autosignat si cal
RUN apk add --no-cache openssl

# Copiar configuració i script d'entrada
COPY nginx.conf    /etc/nginx/nginx.conf
COPY entrypoint.sh /entrypoint.sh
RUN chmod +x /entrypoint.sh

EXPOSE 80 443

ENTRYPOINT ["/entrypoint.sh"]
