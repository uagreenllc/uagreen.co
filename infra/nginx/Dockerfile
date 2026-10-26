FROM nginx:alpine
COPY infra/nginx/nginx.conf /etc/nginx/nginx.conf
# Flutter admin, built by infra/scripts/deploy.sh (`flutter build web` in admin/).
COPY admin/build/web /usr/share/nginx/html/admin
