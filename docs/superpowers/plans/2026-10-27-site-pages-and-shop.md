# Public Pages, Shop and Checkout — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Finish the public Nuxt site: every URL of the old Weblium site answers (200 or 301→200), with the shop (catalog, product with options, cart, checkout with LiqPay / invoice / credit, order status), blog, vacancies with applications, the content pages, the contact form, a 404 page and a complete sitemap.

**Architecture:** Same Nuxt 3 app as the home page (`frontend/`): pages under `pages/`, data through `useApiFetch`, trusted back-office HTML rendered with `.prose`, head tags through `useSiteHead` (moved to `app.vue`, per-page overrides). Cart is a Pinia store persisted in `localStorage`; checkout posts to the .NET API which prices the order server-side and returns the LiqPay form fields. Old URLs are redirected by a Nitro server middleware fed from `GET /api/redirects`.

**Tech Stack:** Nuxt 3.21, @nuxtjs/i18n 9, @pinia/nuxt + pinia 3, @nuxtjs/sitemap 7, vitest for pure logic (cart totals, option pricing, redirect matching).

**Spec:**
- `docs/PLAN.md` — page list, API contract, SEO rules, verification checklist (section "Верифікація").
- `migration/urls.json` — the 69 old URLs that must answer; `migration/redirects.json` — the 20 old→new paths (also in the API's `redirects` table).
- Backend DTOs: `backend/src/UaGreen.Site.Api/Features/{Catalog,Blog,Vacancies,Forms,Orders,Content}/*.cs` (exact field names are copied into `frontend/types/api.ts` in Task 1).
- Design: light theme, brand green `#576e4f`, copper accent, Commissioner; engineering/reliable voice; no template-landing patterns (identical card grids, eyebrows, numbered markers, gradients), no discount banners/timers/popups.

## Global Constraints

- Every public GET takes `?lang=`; EN falls back to UK inside the API. Routes: `uk` without prefix, `en` under `/en` (`strategy: 'prefix_except_default'`). Every page link goes through `localePath()`.
- URLs are exactly the old ones: `/shop`, `/shop/cat/{slug}`, `/shop/{slug}`, `/blog`, `/blog/{slug}`, `/vacancies`, `/vacancies/{slug}`, `/about-us`, `/contacts`, `/oplata`, `/umovi-vikoristannya-saytu`, plus new `/cart`, `/checkout`, `/order/{id}`. Old root-level vacancy slugs and `/en/*-1` paths 301 to the new ones (table from the API).
- Money: `useMoney()` ("295 000 ₴"); prices are decimals from the API, never recomputed from strings. Option pricing: `price + Σ priceDelta` of the chosen values; the server recomputes, the client only displays.
- Forms: honeypot field named `website` rendered off-screen (not `display:none`), never pre-filled; name 2–120 chars, phone 7–30 chars, message ≤ 4000, CV ≤ 10 MB PDF/Word (`application/pdf`, `application/msword`, `application/vnd.openxmlformats-officedocument.wordprocessingml.document`). Validation messages come from i18n, errors from the API (`ProblemDetails.title`) are shown verbatim under the form.
- Not-found product/post/vacancy/order → `throw createError({ statusCode: 404 })` so the error page renders with HTTP 404.
- Every page sets `<title>`, description, hreflang pair, canonical (via `useSiteHead` in `app.vue` + per-page `usePageHead`).
- Degrade: API unreachable at SSR → lists render empty with the page's intro text; detail pages 404 (not 500).
- Mobile (375px): no horizontal scroll; product gallery and checkout form stack.
- Commit after each task with `Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>`; never commit `frontend/node_modules`, `.nuxt`, `.output`.

## Review Focus

1. A product with no options and a product with two option groups must both add to the cart with the right unit price; changing an option after adding must not mutate the line already in the cart. (Task 5 tests.)
2. Checkout with LiqPay must leave the site only after the order exists: a failed `POST /api/orders` shows the API error and keeps the form; `Invoice`/`Credit` go to `/order/{id}` directly. (Task 5 step 7.)
3. Old URL `/en/molodshiy-menedzher-z-prodazhu-1-1-1-1` → 301 → `/en/vacancies/molodshyi-inzhener-konstruktor` → 200; an unknown path → 404 status with the branded page. (Task 1 step 7, Task 6 step 3.)
4. Cart persisted in `localStorage` must not break SSR/hydration: the server renders an empty cart badge, the client fills it after mount without a hydration warning. (Task 5 step 8.)
5. Application form with a 12 MB file or a `.exe` must be rejected client-side before upload, with a message. (Task 3 step 6.)

---

### Task 1: Routing foundation — API types, redirects, 404, per-page head, content pages, contact form

**Files:**
- Create: `frontend/types/api.ts` (extend), `frontend/server/middleware/redirects.ts`, `frontend/error.vue`, `frontend/composables/usePageHead.ts`, `frontend/composables/useForm.ts`, `frontend/components/site/ContentPage.vue`, `frontend/components/site/ContactForm.vue`, `frontend/pages/about-us.vue`, `frontend/pages/contacts.vue`, `frontend/pages/oplata.vue`, `frontend/pages/umovi-vikoristannya-saytu.vue`, `frontend/utils/redirects.ts`, `frontend/utils/redirects.test.ts`
- Modify: `frontend/app.vue` (call `useSiteHead()` here), `frontend/pages/index.vue` (keep its `useSiteHead({ image })` override → becomes `usePageHead`), `frontend/i18n/locales/{uk,en}.json` (namespaces `pages`, `form`, `error`)

**Interfaces:**
- `types/api.ts` adds: `Category { slug; name; image; productCount }`, `Product` (ProductDto: `slug, sku, name, shortDesc, price, oldPrice, inStock, isFeatured, categorySlug, categoryName, descriptionHtml, specsHtml, faqHtml, images: {url, alt}[], files: {url, title}[], options: {id, name, values: {id, value, priceDelta}[]}[], seoTitle, seoDesc, id`), `Post` (PostDto: PostCard + `bodyHtml, seoTitle, seoDesc, updatedAt`), `Vacancy` (VacancyDto), `Redirect { from; to }`, `Order` (OrderDto: `id, number, status, paymentMethod, subtotal, deliveryCost, total, createdAt, items: {productName, optionsText, qty, unitPrice, lineTotal}[], liqPay: {checkoutUrl, data, signature} | null`), `CreateOrder { customer: {name, phone, email}, delivery: {method: 'NovaPoshta'|'Ukrposhta'|'Pickup', city, warehouse}, paymentMethod: 'LiqPay'|'Invoice'|'Credit', comment, items: {productId, qty, optionValueIds}[], lang, website }`.
- `utils/redirects.ts`: `matchRedirect(path: string, table: Redirect[]): string | null` — exact match on the path without trailing slash, case-sensitive; `/en` prefix handled by the table itself (it has both). Pure, tested.
- `server/middleware/redirects.ts`: loads `GET ${apiBase}/api/redirects` at most once per 60 s (module-level cache, swallow errors → empty table), and `sendRedirect(event, to, 301)` on a match. Skips `/api`, `/_nuxt`, `/media`, files with an extension.
- `usePageHead({ title, description, image? })`: thin wrapper over `useSeoMeta` for page-level overrides; `useSiteHead()` in `app.vue` provides the defaults + hreflang once.
- `useForm<T>(submit: (values: T) => Promise<void>)` → `{ pending, error, done, run }`: `run()` sets `pending`, catches API errors and puts `ProblemDetails.title` (or a generic i18n message) into `error`.
- `ContentPage` props `{ contentKey: string }` → fetches `/content/{key}`, renders `<h1>` = title + `.prose` body; 404 if null.
- `ContactForm` posts `{ name, phone, email, message, website }` to `/api/contact`; on success shows `form.sent`.
- `error.vue`: for 404 shows `page.404` content (title + body) and a link home; other statuses show `error.generic`.
- i18n `pages`: `contactsTitle` "Контакти"/"Contacts", `aboutTitle`, `paymentTitle`, `termsTitle`; `form`: `name` "Ім'я"/"Name", `phone` "Телефон"/"Phone", `email` "Email", `message` "Повідомлення"/"Message", `send` "Надіслати"/"Send", `sending` "Надсилаємо…"/"Sending…", `sent` "Дякуємо, ми зв'яжемося з вами."/"Thank you, we'll be in touch.", `required` "Заповніть це поле"/"This field is required", `phoneInvalid` "Введіть номер телефону"/"Enter a phone number", `failed` "Не вдалося надіслати. Спробуйте ще раз або подзвоніть."/"Could not send. Try again or call us."; `error`: `notFoundTitle` fallback "Сторінку не знайдено"/"Page not found", `home` "На головну"/"Home", `generic` "Щось пішло не так"/"Something went wrong".

- [ ] **Step 1: Write the failing test** `utils/redirects.test.ts`

```ts
import { describe, it, expect } from 'vitest'
import { matchRedirect } from './redirects'
const table = [
  { from: '/junior-assembly-fitter', to: '/vacancies/junior-assembly-fitter' },
  { from: '/en/about-us-1', to: '/en/about-us' },
]
describe('matchRedirect', () => {
  it('matches exact paths and tolerates a trailing slash', () => {
    expect(matchRedirect('/junior-assembly-fitter', table)).toBe('/vacancies/junior-assembly-fitter')
    expect(matchRedirect('/junior-assembly-fitter/', table)).toBe('/vacancies/junior-assembly-fitter')
    expect(matchRedirect('/en/about-us-1', table)).toBe('/en/about-us')
  })
  it('ignores everything else', () => {
    expect(matchRedirect('/vacancies/junior-assembly-fitter', table)).toBeNull()
    expect(matchRedirect('/JUNIOR-ASSEMBLY-FITTER', table)).toBeNull()
  })
})
```

- [ ] **Step 2: Run it** — `npx vitest run utils` → FAIL (module missing). Add `utils/**/*.test.ts` to `vitest.config.ts` `include`.
- [ ] **Step 3: Implement `matchRedirect`, the server middleware, `usePageHead`, `useForm`, `ContentPage`, `ContactForm`, `error.vue`, the four pages, the i18n strings.** Content keys: about-us → `page.about`, contacts → `page.contacts` + `<ContactForm>` under it, oplata → `page.oplata`, umovi-vikoristannya-saytu → `page.terms`.
- [ ] **Step 4: Run tests** — `npx vitest run` → all pass.
- [ ] **Step 5: Verify pages** — with API + dev server up: `for p in /about-us /contacts /oplata /umovi-vikoristannya-saytu /en/about-us; do curl -s -o /dev/null -w "$p %{http_code}\n" http://localhost:3000$p; done` → all `200`; `curl -s http://localhost:3000/about-us | grep -c 'Наше Виробництво'` → `1`.
- [ ] **Step 6: Verify the contact form** — browser: fill name/phone/message on `/contacts`, submit → "Дякуємо…"; `curl -s http://localhost:5000/api/admin/...` is not public, so check the API log line "Запит з сайту" via `preview_logs` instead. Submit with an empty name → inline validation, no request.
- [ ] **Step 7: Verify redirects and 404** — `curl -s -o /dev/null -w '%{http_code} %{redirect_url}\n' http://localhost:3000/junior-assembly-fitter` → `301 http://localhost:3000/vacancies/junior-assembly-fitter`; `curl -s -o /dev/null -w '%{http_code}\n' http://localhost:3000/no-such-page` → `404`; `curl -s http://localhost:3000/no-such-page | grep -c 'сонячні панелі'` → `1`.
- [ ] **Step 8: Typecheck, commit** — `npm run typecheck`; `git commit -m "feat(site): redirects, 404, content pages and contact form"`.

---

### Task 2: Blog

**Files:**
- Create: `frontend/pages/blog/index.vue`, `frontend/pages/blog/[slug].vue`, `frontend/components/blog/PostRow.vue`
- Modify: i18n (`blog.title` "Блог"/"Blog", `blog.prev` "Новіші"/"Newer", `blog.next` "Давніші"/"Older", `blog.by` "Автор"/"By", `blog.back` "Усі записи"/"All posts")

**Interfaces:**
- `/blog?page=N` → `useApiFetch<Page<PostCard>>('blog', '/blog', { page, pageSize: 12 })`; rows reuse the home's row layout (`PostRow` extracted from `LatestPosts.vue`, which then uses it); prev/next links only when they exist.
- `/blog/[slug]` → `useApiFetch<Post>('post:'+slug, '/blog/'+slug)`; 404 when null; `usePageHead({ title: seoTitle ?? title, description: seoDesc ?? excerpt, image: cover })`; JSON-LD `BlogPosting` (`headline, datePublished, dateModified, author, image`).

- [ ] **Step 1: Build both pages and the row component.**
- [ ] **Step 2: Verify** — `curl -s http://localhost:3000/blog | grep -o 'href="/blog/[^"]*"' | sort -u | wc -l` → `≥ 9`; `curl -s http://localhost:3000/blog/viva2024 | grep -c 'application/ld+json'` → `1`; `curl -s -o /dev/null -w '%{http_code}' http://localhost:3000/blog/nope` → `404`; `curl -s http://localhost:3000/en/blog | grep -c '>Blog<'` → `≥1` (EN falls back to UK bodies).
- [ ] **Step 3: Browser check desktop + 375px; typecheck; commit** `feat(blog): list and article pages`.

---

### Task 3: Vacancies and applications

**Files:**
- Create: `frontend/pages/vacancies/index.vue`, `frontend/pages/vacancies/[slug].vue`, `frontend/components/vacancies/ApplicationForm.vue`, `frontend/utils/files.ts` (+ `files.test.ts`)
- Modify: i18n (`vacancies.*`: `title` "Вакансії"/"Vacancies", `apply` "Відгукнутися"/"Apply", `requirements` "Вимоги"/"Requirements", `niceToHave` "Буде плюсом"/"Nice to have", `conditions` "Умови"/"What we offer", `cv` "Резюме (PDF або Word, до 10 МБ)"/"CV (PDF or Word, up to 10 MB)", `cvTooBig` "Файл більший за 10 МБ"/"File is over 10 MB", `cvType` "Лише PDF або Word"/"PDF or Word only", `department`, `employment`, `city`, `experience`, `language`, `all` "Усі вакансії"/"All vacancies")

**Interfaces:**
- `utils/files.ts`: `cvProblem(file: File | null): 'tooBig' | 'type' | null` using the Global Constraints limits (size ≤ 10 MB, allowed MIME types; `.doc/.docx/.pdf` extension accepted when the browser reports an empty type).
- `/vacancies` → list from `/vacancies` with `page.vacancies` intro (title + body) above it; each row: title, department · city · employment type.
- `/vacancies/[slug]` → detail: meta line, three `.prose` blocks (only those present), then `<ApplicationForm :slug>`.
- `ApplicationForm` posts `FormData` (`name, phone, email, message, vacancySlug, cv, website`) with `$fetch(apiUrl('/applications'), { method: 'POST', body: formData })`; client-side `cvProblem` check before submit.

- [ ] **Step 1: Write `files.test.ts`** — 12 MB pdf → `'tooBig'`; `.exe` → `'type'`; 1 MB `application/pdf` → `null`; empty-type `cv.docx` → `null`. Run → FAIL.
- [ ] **Step 2: Implement `cvProblem`; run → PASS.**
- [ ] **Step 3: Build pages and the form.**
- [ ] **Step 4: Verify** — `curl -s http://localhost:3000/vacancies | grep -o 'href="/vacancies/[^"]*"' | sort -u | wc -l` → `≥ 7`; `curl -s http://localhost:3000/vacancies/junior-assembly-fitter | grep -c 'Вимоги'` → `1`; `curl -s -o /dev/null -w '%{http_code}' http://localhost:3000/vacancies/nope` → `404`.
- [ ] **Step 5: Browser: submit an application with a small PDF** (create `scratchpad/cv.pdf` with Python's reportlab-free minimal PDF bytes) → success message; API log shows "Відгук на вакансію".
- [ ] **Step 6: Browser: pick a 12 MB file → inline "Файл більший за 10 МБ", no request.**
- [ ] **Step 7: Typecheck; commit** `feat(vacancies): list, detail and application form`.

---

### Task 4: Shop — catalog, category, product

**Files:**
- Create: `frontend/pages/shop/index.vue`, `frontend/pages/shop/cat/[slug].vue`, `frontend/pages/shop/[slug].vue`, `frontend/components/shop/ProductList.vue`, `frontend/components/shop/CategoryNav.vue`, `frontend/components/shop/ProductGallery.vue`, `frontend/components/shop/OptionPicker.vue`, `frontend/utils/pricing.ts` (+ `pricing.test.ts`)
- Modify: i18n (`shop.*`: `title` "Магазин"/"Shop", `all` "Усі товари"/"All products", `inStock` "В наявності"/"In stock", `onOrder` "Під замовлення"/"Made to order", `sku` "Код"/"SKU", `addToCart` "Додати в кошик"/"Add to cart", `added` "Додано в кошик"/"Added to cart", `goToCart` "Перейти в кошик"/"Go to cart", `description` "Опис"/"Description", `specs` "Характеристики"/"Specifications", `faq` "Питання й відповіді"/"FAQ", `files` "Документи"/"Documents", `price` "Ціна"/"Price", `withOptions` "з обраними опціями"/"with selected options")

**Interfaces:**
- `utils/pricing.ts`: `unitPrice(product: Pick<Product,'price'|'options'>, selected: Record<string, string>): number` — `selected` maps optionId → valueId; unknown ids ignored; `optionsText(product, selected, sep = ', ')` → "Щогла: 12м. щогла, Зона встановлення: Зона 1". Pure, tested.
- `ProductList` props `{ products: ProductCard[] }`: rows (photo 4:3 left, name, short desc, price, in-stock line) — not a card grid; `CategoryNav` props `{ categories: Category[]; active?: string }` as a horizontal list of links (`/shop`, `/shop/cat/{slug}` with counts).
- `/shop` → categories + all products; `/shop/cat/[slug]` → same with `?category=`; 404 if the category slug is unknown.
- `/shop/[slug]` → `ProductGallery` (main image + thumbnails, keyboard-accessible buttons), name, sku, price (`unitPrice` recomputed on option change, old price struck), `OptionPicker` (one `<select>` per option group, first value preselected), stock line, add-to-cart button (Task 5 wires it; here it emits `add`), then description / specs / FAQ as stacked sections with `<h2>` (no tabs), files list. `usePageHead({ title: seoTitle ?? name, description: seoDesc ?? shortDesc, image: images[0]?.url })`; JSON-LD `Product` with `offers.price`, `priceCurrency: 'UAH'`, `availability`.

- [ ] **Step 1: Write `pricing.test.ts`** — product price 295000 with options mast(+76000) & zone(+45000): no selection → 295000; both selected → 416000; unknown valueId ignored; `optionsText` returns "Щогла: 12м. щогла, Зона встановлення: Зона 1". Run → FAIL.
- [ ] **Step 2: Implement `pricing.ts`; run → PASS.**
- [ ] **Step 3: Build the components and pages.**
- [ ] **Step 4: Verify** — `curl -s http://localhost:3000/shop | grep -o 'href="/shop/[^"]*"' | sort -u | wc -l` → `≥ 13` (8 products + 5 categories); `curl -s http://localhost:3000/shop/cat/lopati | grep -c 'Лопать'` → `≥ 2`; `curl -s http://localhost:3000/shop/k5v1 | grep -c '295 000 ₴'` → `≥1`; `… | grep -c 'application/ld+json'` → `1`; `curl -s -o /dev/null -w '%{http_code}' http://localhost:3000/shop/cat/nope` → `404`.
- [ ] **Step 5: Browser: on `/shop/k5v1` choose "18м. щогла" + "Зона 2"** → price reads "455 000 ₴"; gallery thumbnails switch the main image; 375px: gallery above the text, no overflow.
- [ ] **Step 6: Typecheck; commit** `feat(shop): catalog, categories and product page`.

---

### Task 5: Cart, checkout, LiqPay hand-off, order status

**Files:**
- Create: `frontend/stores/cart.ts`, `frontend/stores/cart.test.ts`, `frontend/pages/cart.vue`, `frontend/pages/checkout.vue`, `frontend/pages/order/[id].vue`, `frontend/components/shop/CartBadge.vue`, `frontend/components/shop/LiqPayForm.vue`
- Modify: `frontend/components/site/SiteHeader.vue` (badge → `/cart`), `frontend/pages/shop/[slug].vue` (wire add-to-cart), i18n (`cart.*`, `checkout.*`, `order.*`)

**Interfaces:**
- `stores/cart.ts` (Pinia, `defineStore('cart')`): state `items: CartLine[]` where `CartLine { key: string; productId: string; slug: string; name: string; image: string | null; unitPrice: number; qty: number; optionValueIds: string[]; optionsText: string }`; `key = productId + '|' + optionValueIds.slice().sort().join(',')`; actions `add(line: Omit<CartLine,'key'|'qty'>, qty = 1)` (merges by key), `setQty(key, qty)` (qty ≤ 0 removes), `remove(key)`, `clear()`; getters `count` (Σ qty), `subtotal` (Σ unitPrice·qty); persistence: `load()` from `localStorage['uagreen.cart']` in a client-only plugin after mount, `watch` → save. Pure logic in `cartTotals(items)`/`mergeLine(items, line, qty)` exported for tests.
- Header badge: `CartBadge` renders the count only on the client (`<ClientOnly>`), SSR shows the icon alone.
- `/cart`: lines with image, name, optionsText, unit price, qty stepper, line total, remove; subtotal; "Оформити" → `/checkout`; empty state with a link to the shop.
- `/checkout`: form (name, phone, email, delivery method radio [NovaPoshta|Ukrposhta|Pickup] + city/warehouse inputs when not Pickup, payment radio [LiqPay|Invoice|Credit] with one-line explanations from `page.oplata` style copy, comment, honeypot) → `POST /api/orders` with `items` from the store and `lang`. On success: `clear()`; if `liqPay` → render `LiqPayForm` (hidden `<form method="post" action=checkoutUrl>` with `data`/`signature`, auto-submitted on mount, with a visible "Перейти до оплати" button as fallback); else `navigateTo('/order/{id}')`.
- `/order/[id]`: `GET /api/orders/{id}` → number, status (i18n map: New/AwaitingPayment/Paid/InvoiceSent/Credit/Shipped/Cancelled), items, total, next-step copy per payment method; refetch every 5 s while status is `AwaitingPayment` (max 2 min).
- i18n `cart`: `title` "Кошик"/"Cart", `empty` "Кошик порожній"/"Your cart is empty", `toShop` "До магазину"/"Go to the shop", `qty` "К-сть"/"Qty", `remove` "Прибрати"/"Remove", `subtotal` "Разом"/"Subtotal", `checkout` "Оформити замовлення"/"Checkout"; `checkout`: `title` "Оформлення"/"Checkout", `delivery` "Доставка"/"Delivery", `np` "Нова пошта"/"Nova Poshta", `up` "Укрпошта"/"Ukrposhta", `pickup` "Самовивіз"/"Pickup", `city` "Місто"/"City", `warehouse` "Відділення"/"Branch", `payment` "Оплата"/"Payment", `liqpay` "Онлайн карткою (LiqPay)"/"Card online (LiqPay)", `invoice` "Безготівково за рахунком"/"Bank transfer by invoice", `credit` "Кредит 0 % у банках-партнерах"/"0 % credit via partner banks", `comment` "Коментар"/"Comment", `place` "Підтвердити замовлення"/"Place order", `deliveryNote` "Доставка за тарифами перевізника при отриманні"/"Delivery paid to the carrier on receipt", `pay` "Перейти до оплати"/"Go to payment"; `order`: `title` "Замовлення №{n}"/"Order #{n}", statuses…, `thanksInvoice` "Ми надішлемо рахунок на вашу пошту або в месенджер."/"We'll send the invoice by email or messenger.", `thanksCredit` "Менеджер зв'яжеться щодо кредиту."/"A manager will contact you about the credit.", `thanksPaid` "Оплату отримано. Дякуємо!"/"Payment received. Thank you!", `waiting` "Чекаємо підтвердження оплати…"/"Waiting for payment confirmation…".

- [ ] **Step 1: Write `cart.test.ts`** (pure functions): `mergeLine` adds qty to an existing key and appends a new key; different option sets are different lines; `cartTotals` → `{ count, subtotal }` (2×295000 + 1×6000 = 596000, count 3); `setQty 0` removes. Run → FAIL.
- [ ] **Step 2: Implement the store; run → PASS.**
- [ ] **Step 3: Build badge, cart, checkout, LiqPayForm, order page; wire the product page.**
- [ ] **Step 4: Verify SSR** — `curl -s http://localhost:3000/cart | grep -c 'Кошик порожній'` → `1`; `curl -s -o /dev/null -w '%{http_code}' http://localhost:3000/order/00000000-0000-0000-0000-000000000000` → `404`.
- [ ] **Step 5: Browser: add K5 with options, add a blade, open `/cart`** → two lines, subtotal correct, qty stepper works, reload keeps the cart (localStorage), badge shows 3.
- [ ] **Step 6: Browser: checkout with Invoice** → `/order/{id}` shows number, status "Нове" and the invoice copy; API log "Нове замовлення"; the cart is empty afterwards.
- [ ] **Step 7: Browser: checkout with LiqPay (sandbox keys in dev)** → the LiqPay form page appears with data/signature and auto-submits to `https://www.liqpay.ua/api/3/checkout` (stop at LiqPay's page; do not pay). Then: break the API (stop it), submit again → the error line shows, form intact, cart intact.
- [ ] **Step 8: Hydration** — fresh tab, console filtered by `Hydration` → none, with a non-empty cart in localStorage.
- [ ] **Step 9: Typecheck, tests, commit** `feat(shop): cart, checkout with LiqPay, order status`.

---

### Task 6: Sitemap, old-URL audit, production build

**Files:**
- Create: `frontend/server/api/__sitemap__/urls.ts`, `tools/check_old_urls.py`
- Modify: `frontend/nuxt.config.ts` (`sitemap: { sources: ['/api/__sitemap__/urls'] }`), `README.md` (audit command)

**Interfaces:**
- `server/api/__sitemap__/urls.ts` returns `[{ loc: '/shop/k5v1', _i18nTransform: true }, …]` for every product, category, post and vacancy from the API (each also under `/en`).
- `tools/check_old_urls.py BASE_URL`: reads `migration/urls.json`, requests each URL path (uk and en) against BASE_URL following redirects, prints `path → status (final)` and exits 1 if any final status is not 200 (except the old `/404` and `/en/404-1`, which must be 404/301→404).

- [ ] **Step 1: Sitemap source + config; verify** `curl -s http://localhost:3000/__sitemap__/uk-UA.xml | grep -c '<loc>'` → `≥ 25` and the EN file `≥ 20`.
- [ ] **Step 2: Write the audit script; run it against `http://localhost:3000`** → every old URL 200 or 301→200; fix any miss (likely `/en/vacancies` list and the `/en/*-1` content pages).
- [ ] **Step 3: Production build** — `npm run build`, run `.output` on 3100 with `NUXT_API_BASE=http://localhost:5000`, rerun the audit against 3100 → clean; 375px smoke of `/shop/k5v1` and `/checkout` on the dev server.
- [ ] **Step 4: Commit and push** `feat(site): sitemap sources and old-URL audit`.
