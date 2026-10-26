# uagreen.co

Public site + back office for UA GREEN, replacing the Weblium build.
Plan and architecture: [docs/PLAN.md](docs/PLAN.md).

```
backend/    .NET 9 API (UaGreen.Site.Api) — catalog, blog, vacancies, orders, LiqPay, admin API
frontend/   Nuxt 3 SSR public site
admin/      Flutter web back office (login via Gospodar / erp.uagreen.co)
tools/      scrape_weblium.py — one-off export of the old site
migration/  Weblium export (JSON in git, media/ ignored) read by the API's ImportRunner
infra/      docker compose, nginx, deploy script
```

## Local dev

```bash
cp infra/docker/.env.prod.example infra/docker/.env   # fill in dev values
docker compose -f infra/docker/docker-compose.prod.yml -f infra/docker/docker-compose.dev.yml --env-file infra/docker/.env up -d postgres minio minio-init
```

Postgres → `localhost:5432`, MinIO → `localhost:9000` (console `:9001`), bucket `media`, public-read.

## Contracts between the parts

- API listens on `:8080`; config keys are the `Xxx__Yyy` env vars in
  `infra/docker/docker-compose.prod.yml` (`ConnectionStrings__Postgres`, `Jwt__*`,
  `Gospodar__BaseUrl`, `Storage__*`, `Smtp__*`, `LiqPay__*`, `Import__Dir`, …).
- Nuxt reads `NUXT_API_BASE` (server-side, `http://api:8080`), `NUXT_PUBLIC_API_BASE`
  (`/api`, browser) and `NUXT_PUBLIC_SITE_URL`; listens on `NITRO_PORT=3000`;
  ships its own `frontend/Dockerfile`.
- Flutter admin is built with `flutter build web --release --pwa-strategy=none
  --dart-define=API_HOST=https://admin.uagreen.co` and served as static files on
  `admin.uagreen.co` (`/api/` proxied on the same host).
- Media: MinIO bucket `media`, public URL `PUBLIC_BASE_URL/media/<key>`.
- Weblium export JSON contract: see `tools/scrape_weblium.py` / `docs/PLAN.md`.

## Deploy

```bash
bash infra/scripts/deploy.sh          # re-deploy
bash infra/scripts/deploy.sh --ssl    # first deploy of a domain (vhost + certbot)
DOMAIN=new.uagreen.co ADMIN_DOMAIN=admin-new.uagreen.co bash infra/scripts/deploy.sh --ssl   # staging
```
