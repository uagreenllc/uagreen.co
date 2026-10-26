# План: переїзд uagreen.co з Weblium на власний VPS + бекофіс

## Контекст

Сайт uagreen.co живе на конструкторі Weblium (без експорту коду). Потрібно перенести його на власний VPS (`173.242.59.241`, той самий, де Gospodar/TTApp/OberigAI) і дати команді бекофіс для редагування товарів, цін, блогу й вакансій.

Що є на сайті зараз (знято з sitemap + HTML):
- **Мови**: uk (основна) + en (`/en/...`). EN має лише головну, about, contacts, оплату, умови, вакансії, магазин — блогу англійською немає.
- **Магазин** (`/shop`): 5 категорій (Вітроелектростанції, Генератори, Лопаті, Послуги, Щогли), 8 товарів, у товару: код, ціна/стара ціна, опції-дропдауни (напр. «Щогла», «Зона встановлення»), опис, тех. характеристики, FAQ, прикріплені файли, до ~20 фото, «In stock». Кошик → оплата LiqPay / безготівка за рахунком / кредит 0% у банках-партнерах. Доставка Нова пошта / Укрпошта.
- **Блог**: 9 статей `/blog/{slug}` (дата, заголовок, автор, обкладинка, текст).
- **Вакансії**: `/vacancies` + 7 сторінок на кореневих slug-ах (`/junior-assembly-fitter` тощо). Поля: назва, відділ (Менеджмент/Слюсарі/Інженери/Програмування), зайнятість, місто, досвід, мова, вимоги / бажані навички / умови; форма «Відгукнутися».
- **Сторінки**: головна (hero, категорії, топ товарів, послуги, проєкти, відгуки, новини), about-us, contacts (+ форма), oplata, umovi-vikoristannya-saytu, 404.
- **Аналітика**: GA4 `G-9MEWBJF4B0`, Google Ads `AW-16667422706`, FB Pixel `1534621291606786`. Соцмережі: Instagram, LinkedIn, TikTok, YouTube. Телефони +380668696072, +380957173772.

Рішення:
- Магазин: **повний кошик + LiqPay** (не просто каталог).
- Бекофіс: **власний .NET API + Flutter-адмінка** (як Gospodar/TTApp), **кілька користувачів з ролями**.
- Дизайн: **оновити** (зберегти структуру й контент, зробити сучасніший).
- Публічний сайт: **Nuxt 3 (Vue, SSR)** — віддає готовий HTML, Google індексує як звичайний сайт.

## Архітектура

```
uagreen.co  ──► OberigAI nginx (спільний, 443, certbot)
                  └─ uagreen.co.conf → host.docker.internal:8083
                         └─ uagreen_site_nginx (стек uagreen_site)
                              ├─ /            → web  (Nuxt SSR, node)
                              ├─ /api/        → api  (.NET 9, :8080)
                              ├─ /media/      → minio (фото товарів, обкладинки, файли)
                              └─ admin.uagreen.co → статика Flutter-адмінки
postgres (uagreen_site) ← api ← admin (JWT від Gospodar, ролі локально)
```

Один репозиторій `D:/Work/uagreen.co` → `github.com/uagreenllc/uagreen.co`:

```
backend/src/UaGreen.Site.Api/   .NET 9 Web API (структура як TTApp: Features/*, Infrastructure/*, Program.cs)
backend/tests/UaGreen.Site.Tests/
frontend/                        Nuxt 3 (публічний сайт)
admin/                           Flutter web (бекофіс)
tools/scrape_weblium.py          одноразовий скрейпер контенту
migration/                       JSON + медіа, вивантажені з Weblium (в .gitignore медіа)
infra/docker/docker-compose.prod.yml, .env.prod.example
infra/nginx/nginx.conf, Dockerfile, vhost.conf.template
infra/scripts/deploy.sh
```

### Що переюзуємо з TTApp (копіюємо файли, міняємо namespace)
- `D:/Work/TTApp/infra/scripts/deploy.sh` — деплой-флоу (build Flutter → rsync → backup pg → compose build/up → reload oberig-nginx).
- `D:/Work/TTApp/infra/docker/docker-compose.prod.yml` — postgres + minio + minio-init + api + nginx; додано сервіс `web` (Nuxt), порт nginx `8083:80`, `name: uagreen_site`.
- `D:/Work/OberigAI/backend/nginx/erp.uagreen.co.conf` — шаблон vhost для спільного nginx → `infra/nginx/vhost.conf.template`.
- `TTApp.Api/Program.cs:59-102` — JWT-валідація зі спільним ключем Gospodar + policies; `Infrastructure/Auth/GospodarClient.cs` — проксі логіну; `JitUserMiddleware.cs`; `Infrastructure/Storage` (S3/MinIO, presigned upload); `AddAppRateLimiting`.
- Flutter-адмінка: каркас, API-клієнт, auth-флоу й `--dart-define=API_HOST` з `TTApp_Front`.

### Авторизація бекофісу
- Аутентифікація — через Gospodar як IdP (як TTApp: проксі `/api/auth/login|refresh`, той самий `Jwt__SigningKey/Issuer/Audience`).
- Авторизація — локальна таблиця `site_user_roles(gospodar_user_id, role)`; роль Gospodar `Admin` = автоматично Admin тут. Ролі: `Admin` (усе + призначення ролей), `Sales` (товари, категорії, замовлення), `Editor` (блог, контент сторінок, налаштування), `HR` (вакансії, відгуки).
- `ponytail:` власні паролі не робимо — Gospodar уже IdP; якщо треба логін для людей поза ERP, додати ASP.NET Identity пізніше.

### Модель даних (Postgres, EF Core migrations)
Двомовність — парні колонки `*_uk` / `*_en` (EN nullable → фолбек на UK у Nuxt).

| Таблиця | Поля (скорочено) |
|---|---|
| `categories` | slug, name_uk/en, sort, image |
| `products` | slug, sku, category_id, name, short_desc, description_html, specs_html, faq_html (всі uk/en), price, old_price, in_stock, is_featured, sort, seo_title/desc, published |
| `product_images` | product_id, key (MinIO), alt, sort |
| `product_files` | product_id, key, title |
| `product_options` / `product_option_values` | option name uk/en; value uk/en, price_delta |
| `blog_posts` | slug, title, excerpt, body_html (uk/en), cover_key, author, published_at, seo, published |
| `vacancies` | slug, title, department, employment_type, city, experience, language, requirements_html, nice_to_have_html, conditions_html (uk/en), is_active, published_at |
| `applications` | vacancy_id?, name, phone, email, message, cv_key, created_at |
| `orders` + `order_items` | number, status (new/awaiting_payment/paid/invoice_sent/credit/shipped/cancelled), customer name/phone/email, delivery (np/ukrposhta, city, warehouse), payment (liqpay/invoice/credit), subtotal, total, liqpay_order_id, paid_at; items: product snapshot (name, options, qty, unit_price) |
| `contact_requests` | name, phone, email, message, created_at |
| `content` | key (напр. `page.about`, `home.hero`), lang, title, body_html |
| `settings` | key, value — телефони, адреса, соцмережі, GA/Ads/FB id (LiqPay keys — лише в env) |
| `site_user_roles` | gospodar_user_id, role |
| `redirects` | from_path, to_path — старі URL Weblium |

### API
Публічне (anonymous, кеш 60 с): `GET /api/catalog/categories`, `/api/catalog/products?category=`, `/api/catalog/products/{slug}`, `/api/blog`, `/api/blog/{slug}`, `/api/vacancies`, `/api/vacancies/{slug}`, `/api/content/{key}`, `/api/settings/public`.
Публічне з rate-limit + honeypot-полем: `POST /api/orders` (повертає order + для liqpay — `data`/`signature`), `POST /api/payments/liqpay/callback`, `POST /api/applications`, `POST /api/contact`. Кожна заявка/замовлення → email на адресу з settings.
Адмін (`/api/admin/*`, JWT + policy per role): CRUD для всіх таблиць вище, `POST /api/admin/media/presign` (MinIO), `GET/PUT /api/admin/users/{id}/roles`, список замовлень зі зміною статусу.

### LiqPay
Checkout API v3: `data = base64(json{version:3, public_key, action:"pay", amount, currency:"UAH", description, order_id, result_url, server_url, language})`, `signature = base64(sha1(private_key + data + private_key))`. Фронт робить form-POST на `https://www.liqpay.ua/api/3/checkout`. `server_url` → наш callback: перевірити підпис, `status in (success, sandbox)` → `orders.status = paid`. Ключі — `LiqPay__PublicKey/PrivateKey` в env, sandbox-ключі для тесту.
`ponytail:` кредит 0% і «безготівка» = замовлення зі статусом credit/invoice_sent, менеджер веде вручну; доставка — «за тарифом перевізника при отриманні», без API Нової пошти.

### Nuxt (frontend/)
- `@nuxtjs/i18n` (uk default без префіксу, en з `/en`), `@nuxtjs/sitemap`, `@nuxt/image` для `/media/`.
- Сторінки: `/`, `/about-us`, `/contacts`, `/oplata`, `/umovi-vikoristannya-saytu`, `/shop`, `/shop/cat/[slug]`, `/shop/[slug]`, `/cart`, `/checkout`, `/order/[number]` (result_url), `/blog`, `/blog/[slug]`, `/vacancies`, `/vacancies/[slug]`, 404.
- Кошик — Pinia + localStorage. Форми: кошик/замовлення, відгук на вакансію (з файлом CV), контакт.
- SEO: ті самі URL, що зараз; `<link hreflang>`, canonical, OG; GA4/Ads/FB pixel з settings; `robots.txt`, `sitemap.xml` з API; 301 для змінених шляхів через таблицю `redirects` у server middleware Nuxt.
- Дизайн: оновлений, у поточній палітрі (`#576e4f` зелений), ті ж секції головної.

### Flutter-адмінка (admin/)
Екрани: логін (Gospodar), Товари, Категорії, Замовлення, Блог (HTML-редактор), Вакансії, Відгуки на вакансії, Контактні запити, Контент сторінок, Налаштування, Користувачі/ролі (тільки Admin). Меню показує лише розділи за роллю.

## Фази

| # | Фаза | Результат | Оцінка |
|---|---|---|---|
| 0 | Репо + інфра-скелет | git + GitHub; `infra/*`; `docker compose up` локально піднімає postgres+minio | 0.5 д |
| 1 | Вивантаження контенту | `tools/scrape_weblium.py` → `migration/*.json` + `migration/media/` + мапа старих URL | 1 д |
| 2 | Backend | Схема + міграції; публічний API; JWT/Gospodar/ролі; MinIO; `ImportRunner`; email-нотифікації | 2–3 д |
| 3 | Nuxt-сайт | Усі сторінки з даними з API, i18n, SEO, редіректи, форми | 4–5 д |
| 4 | Кошик і оплата | Кошик, checkout, `POST /api/orders`, LiqPay (sandbox → prod), email менеджеру | 2–3 д |
| 5 | Flutter-адмінка | Усі екрани, адмін-API, завантаження медіа, ролі | 4–5 д |
| 6 | Деплой на `new.uagreen.co` + QA | `deploy.sh --ssl`; імпорт контенту на VPS; чеклист нижче | 1 д |
| 7 | Переїзд | Знизити TTL DNS → A-запис `uagreen.co`/`www` на VPS → certbot → Weblium лишаємо 2–4 тижні → Search Console | 0.5 д |

Разом ≈ 15–19 робочих днів. Фази 1, 2 незалежні; 3 і 5 паралельно після 2.

## Тести (мінімум)
`backend/tests`: xUnit — підпис LiqPay, розрахунок суми замовлення з опціями, матриця policies, callback з неправильним підписом → 400, імпорт фікстури.

## Верифікація (фаза 6)
1. `curl -A Googlebot https://new.uagreen.co/shop/k1v4` — у HTML є назва, ціна, опис.
2. Кожен URL зі старих sitemap (uk+en) → 200 або 301 на 200 — жодного 404.
3. Lighthouse: SEO ≥ 90, Performance ≥ 80 на мобільному.
4. Повний шлях покупки з LiqPay sandbox → `paid` в адмінці → лист менеджеру.
5. Відгук на вакансію з CV і контактна форма → запис в адмінці + лист.
6. Ролі: під Sales не видно вакансій, під HR не видно товарів; зміна ціни видна на сайті ≤ 60 с.
7. `deploy.sh` повторно проходить; бекап Postgres у `/opt/uagreen_site/backups`.
8. GA4/FB pixel спрацьовують.

## Ризики / припущення
- Gospodar як IdP: якщо ERP лежить — в адмінку не зайти (сайт і покупки працюють).
- Weblium зберігає тексти в SSR-HTML — скрейпер працює без браузера.
- EN-контент частковий — фолбек на UK; переклади додати в адмінці пізніше.
- Оплата частинами (моно/Приват) — підключаємо після запуску, якщо потрібно.
