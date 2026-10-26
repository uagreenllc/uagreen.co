#!/bin/bash
# Deploy the uagreen.co site stack to the VPS (alongside OberigAI + Gospodar + TTApp).
# Prerequisites:
#   - Flutter SDK on PATH (or FLUTTER=... override)
#   - SSH: ssh root@173.242.59.241 works without password
#   - DNS: $DOMAIN, www.$DOMAIN and $ADMIN_DOMAIN → 173.242.59.241 (www only for prod)
#   - /opt/uagreen_site/infra/docker/.env configured on the VPS (see .env.prod.example)
#
# Usage:
#   First deploy of a domain: bash infra/scripts/deploy.sh --ssl
#   Re-deploy:                bash infra/scripts/deploy.sh
#   One-time Weblium import:  bash infra/scripts/deploy.sh --import
#   Staging:                  DOMAIN=new.uagreen.co ADMIN_DOMAIN=admin-new.uagreen.co WWW_DOMAIN= bash infra/scripts/deploy.sh --ssl

set -e

VPS_IP="173.242.59.241"
VPS_USER="root"
REMOTE_DIR="/opt/uagreen_site"
DOMAIN="${DOMAIN:-uagreen.co}"
ADMIN_DOMAIN="${ADMIN_DOMAIN:-admin.uagreen.co}"
WWW_DOMAIN="${WWW_DOMAIN-www.${DOMAIN}}"   # set to empty to skip the www cert name
OBERIG_DIR="/opt/oberig"                   # shared TLS nginx + certbot live here
FLUTTER="${FLUTTER:-flutter}"

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
ADMIN_DIR="$PROJECT_ROOT/admin"

GET_SSL=false
DO_IMPORT=false
[[ "${1:-}" == "--ssl" ]] && GET_SSL=true
[[ "${1:-}" == "--import" ]] && DO_IMPORT=true

echo "==> [1/6] Building Flutter admin..."
cd "$ADMIN_DIR"
# --pwa-strategy=none: no offline service worker (it pins browsers to the old
# bundle after a deploy). Cache-busting is done by nginx no-cache headers.
"$FLUTTER" build web --release --pwa-strategy=none --dart-define=API_HOST=https://${ADMIN_DOMAIN}
echo "    Done."

echo "==> [2/6] Syncing files to VPS..."
cd "$PROJECT_ROOT"
rsync -az --progress \
  --exclude='.git' \
  --exclude='.claude' \
  --exclude='admin/.dart_tool' \
  --exclude='backend/**/bin' \
  --exclude='backend/**/obj' \
  --exclude='frontend/node_modules' \
  --exclude='frontend/.nuxt' \
  --exclude='frontend/.output' \
  --exclude='infra/docker/.env' \
  ./ "${VPS_USER}@${VPS_IP}:${REMOTE_DIR}/"
echo "    Done."

if $GET_SSL; then
  echo "==> [3/6] Obtaining SSL certificate + installing vhost for ${DOMAIN}..."
  CERT_DOMAINS="-d ${DOMAIN} -d ${ADMIN_DOMAIN}"
  [[ -n "${WWW_DOMAIN}" ]] && CERT_DOMAINS="${CERT_DOMAINS} -d ${WWW_DOMAIN}"
  # The shared nginx's catch-all :80 server already answers ACME challenges, so
  # the cert can be issued BEFORE our vhost (which references it) is mounted.
  ssh "${VPS_USER}@${VPS_IP}" "
    set -e
    cd ${OBERIG_DIR}
    docker compose --env-file .env -f docker-compose.prod.yml run --rm certbot certonly \
      --webroot -w /var/www/certbot \
      --email admin@uagreen.co \
      --agree-tos --no-eff-email \
      ${CERT_DOMAINS} \
      --cert-name ${DOMAIN}
    echo '    SSL cert obtained.'
  "
  sed -e "s/__DOMAIN__/${DOMAIN}/g" -e "s/__ADMIN_DOMAIN__/${ADMIN_DOMAIN}/g" \
    "$PROJECT_ROOT/infra/nginx/vhost.conf.template" > "/tmp/${DOMAIN}.conf"
  scp "/tmp/${DOMAIN}.conf" "${VPS_USER}@${VPS_IP}:${OBERIG_DIR}/nginx/${DOMAIN}.conf"
  # The shared nginx bind-mounts every vhost explicitly (OberigAI
  # docker-compose.prod.yml, nginx.volumes) — add ours next to erp.uagreen.co's.
  ssh "${VPS_USER}@${VPS_IP}" "
    set -e
    cd ${OBERIG_DIR}
    if ! grep -q 'nginx/${DOMAIN}.conf' docker-compose.prod.yml; then
      sed -i '/erp.uagreen.co.conf:ro/a\\      - ./nginx/${DOMAIN}.conf:/etc/nginx/conf.d/${DOMAIN}.conf:ro' docker-compose.prod.yml
      echo '    vhost mount added to ${OBERIG_DIR}/docker-compose.prod.yml'
    fi
    docker compose --env-file .env -f docker-compose.prod.yml up -d nginx
    docker exec oberig-nginx nginx -t
    echo '    vhost installed.'
  "
else
  echo "==> [3/6] Skipping SSL (use --ssl for the first deploy of a domain)."
fi

echo "==> [4/6] Backing up Postgres before deploy..."
ssh "${VPS_USER}@${VPS_IP}" "
  set -e
  BACKUP_DIR='${REMOTE_DIR}/backups'
  mkdir -p \"\$BACKUP_DIR\"
  STAMP=\$(date +%Y%m%d_%H%M%S)
  if docker ps --format '{{.Names}}' | grep -q '^uagreen_site_postgres\$'; then
    docker exec uagreen_site_postgres pg_dumpall -U uagreen_site | gzip > \"\$BACKUP_DIR/uagreen_site_\${STAMP}.sql.gz\"
    echo \"    Backup written: \$BACKUP_DIR/uagreen_site_\${STAMP}.sql.gz\"
    ls -1t \"\$BACKUP_DIR\"/uagreen_site_*.sql.gz 2>/dev/null | tail -n +15 | xargs -r rm -f
  else
    echo '    No existing Postgres container — skipping backup (first deploy).'
  fi
  # Uploaded media + CVs live on plain volumes — tar them next to the DB dump.
  for VOL in media_data private_data; do
    if docker volume ls -q | grep -q \"^uagreen_site_\${VOL}\$\"; then
      docker run --rm -v \"uagreen_site_\${VOL}:/v:ro\" -v \"\$BACKUP_DIR:/b\" alpine tar czf \"/b/\${VOL}_\${STAMP}.tgz\" -C /v .
      ls -1t \"\$BACKUP_DIR\"/\${VOL}_*.tgz 2>/dev/null | tail -n +6 | xargs -r rm -f
    fi
  done
"

echo "==> [5/6] Building and starting uagreen_site containers..."
ssh "${VPS_USER}@${VPS_IP}" "
  set -e
  cd ${REMOTE_DIR}/infra/docker
  if [ ! -f .env ]; then
    echo 'ERROR: .env not found. Copy .env.prod.example to .env and fill in values.'
    exit 1
  fi
  docker compose -f docker-compose.prod.yml build
  docker compose -f docker-compose.prod.yml up -d
  echo ''
  echo 'uagreen_site containers:'
  docker ps --filter 'name=uagreen_site' --format 'table {{.Names}}\t{{.Status}}\t{{.Ports}}'
  docker image prune -a --force --filter 'until=168h' >/dev/null
  docker builder prune --force --filter 'until=72h' >/dev/null
"

if $DO_IMPORT; then
  echo "==> [5b] Running the one-time Weblium import (migration/ → DB + media volume)..."
  ssh "${VPS_USER}@${VPS_IP}" "
    set -e
    cd ${REMOTE_DIR}/infra/docker
    IMPORT_DIR=/import docker compose -f docker-compose.prod.yml up -d --force-recreate api
    echo '    API restarted with Import__Dir=/import — watching logs for 90s...'
    timeout 90 docker logs -f uagreen_site_api 2>&1 | grep -i -m1 'import.*done' || true
    docker compose -f docker-compose.prod.yml up -d --force-recreate api
    echo '    API restarted without import.'
  "
fi

echo "==> [6/6] Reloading shared OberigAI nginx..."
ssh "${VPS_USER}@${VPS_IP}" "
  docker exec oberig-nginx nginx -t && docker exec oberig-nginx nginx -s reload
  echo '    Nginx reloaded.'
"

echo ""
echo "==> Deploy complete!"
echo "    Site:  https://${DOMAIN}"
echo "    Admin: https://${ADMIN_DOMAIN}"
