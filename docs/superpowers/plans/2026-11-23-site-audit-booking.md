# Site Audit Booking (site side) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** The K5 story ends with "order a site audit"; `/audit` offers a phone-only consultation or a paid audit with a date picked from Gospodar's free days; legal pages (public offer, privacy policy) exist and are linked under every phone form.

**Architecture:** Nuxt pages + the .NET site API. A paid audit is an ordinary site `Order` (one line: the "Аудит вітру на ділянці" product) with audit fields, paid through the existing LiqPay flow; on the first paid callback the API books it in Gospodar through new integration endpoints (`X-Api-Key`). Consultation is a `ContactRequest` + manager email + a Gospodar lead. Everything that talks to Gospodar degrades to "site + email" when the key is empty or Gospodar is down.

**Tech Stack:** Nuxt 3 + @nuxtjs/i18n, .NET 9 minimal APIs, EF Core + Npgsql, xunit, vitest. No new dependencies.

**Spec:** `docs/superpowers/specs/2026-11-23-site-audit-booking-design.md`. The Gospodar side (site key, site-sourced leads, `audit-slots`, `audit-bookings`) is a separate plan in the Gospodar repo; this plan codes against the contract pinned in the spec.

## Global Constraints

- Both locales for every string (`frontend/i18n/locales/uk.json`, `en.json`); every link through `localePath()`.
- Routes, verbatim: `/audit`, `/audit/consultation`, `/audit/book`, `/publichna-oferta`, `/polityka-konfidentsiynosti`.
- Booking window: from Kyiv-today + 7 days to Kyiv-today + 30 days, inclusive; time zone `Europe/Kyiv`.
- Audit product slug: `audit-vitrovih-resursiv`; price always read from the DB product, never from the client.
- Phone: client check `phone.replace(/\D/g, '').length >= 7`; server check trimmed length 7..30. Honeypot field `website`, rate limit `RateLimitingExtensions.Forms` — as in `FormsEndpoints` / `OrdersEndpoints`.
- Gospodar calls use header `X-Api-Key: {Gospodar:ApiKey}`; empty key = skip the call (log at Information), never fail the visitor's request.
- Copy fixed by the spec (uk): «Хочете перевірити, чи вигідно ставити вітряк у вас?», «Замовити аудит на своїй ділянці», «Замовити консультацію», «Оплатити та запланувати аудит одразу», «Погоджуюсь з умовами публічної оферти», «Надсилаючи форму, ви погоджуєтесь з політикою конфіденційності», «Консультація щодо аудиту ділянки».
- Style: existing tokens (`var(--green)`, `var(--ink-2)`, `var(--line)`), `.btn`, `.link`, `.field`, `.page.section-inner`; no template-landing patterns (identical card grids with icons, eyebrows, gradients); works at 375 px.
- `ponytail:` comment on every deliberate shortcut (no retry queue, no date hold).

## Review Focus

1. LiqPay posts the same callback twice, or Gospodar failed the first time → booking is retried on the next callback and never duplicated (Gospodar is idempotent by `externalId`; site books while `GospodarBookedAt` is null). Test: Task 5 `ShouldBook_*`.
2. Gospodar down, slow or key empty while a visitor opens `/audit/book` → page shows the "can't show dates" fallback with a consultation link, API answers 200 `{ unavailable: true }`, never 500. Test: Task 4 `GetAuditSlots_returns_null_when_gospodar_throws`.
3. Visitor (or a bot) posts a date outside the window or not in the free list → 400, no order created. Test: Task 4 `AuditWindow_rejects_*`.
4. Kyiv midnight: at 22:30 UTC on 30 Nov it is already 1 Dec in Kyiv → window starts 8 Dec. Test: Task 4 `AuditWindow_uses_kyiv_date`.
5. Paid amount ≠ order total (existing guard keeps the order unpaid) → no Gospodar booking happens. Covered by Task 5 `ShouldBook_false_unless_paid`.

---

### Task 1: Legal pages and privacy note under phone forms

**Files:**
- Create: `frontend/pages/publichna-oferta.vue`, `frontend/pages/polityka-konfidentsiynosti.vue`, `frontend/components/site/PrivacyNote.vue`
- Create: migration `backend/src/UaGreen.Site.Api/Infrastructure/Persistence/Migrations/<ts>_AddLegalPages.cs` (via `dotnet ef migrations add AddLegalPages`)
- Modify: `frontend/components/site/SiteFooter.vue:33-34`, `frontend/components/site/ContactForm.vue`, `frontend/components/vacancies/ApplicationForm.vue`, `frontend/pages/checkout.vue`, both locale files

**Interfaces:**
- Produces: `<PrivacyNote />` (no props) — one muted line with the link; used by Tasks 3 and 6. Content keys `page.offer`, `page.privacy`.

- [ ] **Step 1: Migration seeding the two content blocks without overwriting edits.** Empty `Up` model diff; body is `migrationBuilder.Sql(...)` with `INSERT INTO content (key, lang, title, body_html, updated_at) VALUES (...) ON CONFLICT (key, lang) DO NOTHING` for `page.offer`/`page.privacy` × `uk`/`en` (check the real table name in the snapshot). Use dollar quoting (`$b$…$b$`) for HTML. Do **not** add them to `migration/content.json` (the importer upserts and would overwrite admin edits). `Down`: delete those four rows.
  Draft copy, first paragraph of each: «Чернетка. Текст має перевірити юрист.» Seller: ТОВ «ЮА Грін», ЄДРПОУ 45033277 (take address/contacts wording from `page.terms` in `migration/content.json`).
  - Offer sections: предмет (послуга «Аудит вітру на ділянці», що входить — з опису товару), ціна (як на сайті на момент оплати), порядок оплати (LiqPay, 100 % передоплата), дата виконання (обрана клієнтом, підтверджується менеджером; перенесення за домовленістю), повернення коштів (за зверненням до менеджера до дати виїзду), результат (письмовий звіт), акцепт (оплата = згода з офертою), реквізити.
  - Privacy sections: які дані (ім'я, телефон, email, адреса ділянки, повідомлення, CV), навіщо (зв'язок, виконання замовлення, облік у CRM), кому передаємо (LiqPay — оплата; власна облікова система компанії), строк зберігання, права суб'єкта (ЗУ «Про захист персональних даних»), контакти.
  - EN: same structure, translated.
- [ ] **Step 2: Pages.** Copy `pages/umovi-vikoristannya-saytu.vue` with keys `page.offer` / `pages.offerTitle` («Публічна оферта» / "Public offer") and `page.privacy` / `pages.privacyTitle` («Політика конфіденційності» / "Privacy policy").
- [ ] **Step 3: Footer** — two more `<p><NuxtLink>` lines after `footer.terms`: `footer.offer`, `footer.privacy`.
- [ ] **Step 4: `PrivacyNote.vue`** — `<p class="privacy-note">` with `form.privacyNote` text where «політикою конфіденційності» is a `NuxtLink` to `/polityka-konfidentsiynosti` (use `<i18n-t keypath="form.privacyNote">` with a `link` slot). 13 px, `var(--ink-2)`. Place it right above the submit button in `ContactForm`, `ApplicationForm` and `checkout.vue`.
- [ ] **Step 5: Verify.** `cd frontend && npm run typecheck && npm test`; `cd backend && dotnet build && dotnet test`. Start API + Nuxt, open `/publichna-oferta`, `/en/polityka-konfidentsiynosti` → 200 with draft text; footer links work; `/contacts` shows the note with a working link.
- [ ] **Step 6: Commit** — `feat(site): public offer and privacy policy pages`

### Task 2: Story CTA and the `/audit` choice page

**Files:**
- Modify: `frontend/components/story/K5Story.vue:133-136` (the `v-if="i === 4"` block), both locale files
- Create: `frontend/pages/audit/index.vue`

**Interfaces:**
- Consumes: `GET /api/catalog/products/audit-vitrovih-resursiv` → `Product` (`frontend/types/api.ts`).
- Produces: links to `/audit/consultation`, `/audit/book`.

- [ ] **Step 1: CTA.** In the step-5 `.cta` block: a `<p class="ask">{{ $t('story.auditAsk') }}</p>` above the button; button → `localePath('/audit')` with `story.cta` changed to «Замовити аудит на своїй ділянці» / "Order an audit of my site"; keep the shop link. `story.auditAsk` = «Хочете перевірити, чи вигідно ставити вітряк у вас?» / "Want to check whether a wind turbine pays off at your place?". Style `.ask` like `.step p` but `color: var(--ink)`, `font-weight: 600`, `margin: 0`.
- [ ] **Step 2: `/audit` page.** `useApiFetch<Product>('audit-product', '/catalog/products/audit-vitrovih-resursiv')` (404 if missing). Title `audit.title` («Аудит вітру на ділянці»); `product.shortDesc` as lead; price via `useMoney()` (old price struck through if present); `product.descriptionHtml` in `.prose`; then two choices side by side (stacked below 900 px), each a heading + one-line explanation + action:
  - `audit.consultTitle` «Замовити консультацію», `audit.consultText` «Залиште номер — інженер зателефонує, розпитає про ділянку й підкаже, чи є сенс в аудиті.», `.btn-ghost`-style link (outline: `border: 1.5px solid var(--green)`) → `/audit/consultation`.
  - `audit.bookTitle` «Оплатити та запланувати аудит одразу», `audit.bookText` «Оберіть вільну дату, оплатіть онлайн — і спеціаліст приїде у вибраний день.», `.btn` → `/audit/book`.
  `usePageHead({ title, description: shortDesc })`. EN for all keys.
- [ ] **Step 3: Verify.** `npm run typecheck`. In the browser: home `?motion=off` → step 5 shows the question and button → `/audit` shows product text, price and both choices; `/en/audit` works; 375 px layout has no horizontal scroll.
- [ ] **Step 4: Commit** — `feat(site): audit CTA at the end of the K5 story and /audit page`

### Task 3: Consultation (phone only) + Gospodar client key and lead push

**Files:**
- Modify: `backend/src/UaGreen.Site.Api/Infrastructure/Auth/AuthOptions.cs` (`GospodarOptions`), `.../Infrastructure/Auth/GospodarClient.cs`
- Create: `backend/src/UaGreen.Site.Api/Features/Audit/AuditEndpoints.cs`; register in `Program.cs` next to `FormsEndpoints.Map(app)`
- Modify: `infra/docker/docker-compose.prod.yml` (api env: `Gospodar__ApiKey: ${GOSPODAR_API_KEY:-}`), `infra/docker/.env.prod.example` (`GOSPODAR_API_KEY=` with comment «ключ інтеграції сайту в Господарі; staging — ключ тестової компанії»), `README.md` contracts list
- Create: `frontend/pages/audit/consultation.vue`
- Test: `backend/tests/UaGreen.Site.Tests/GospodarIntegrationTests.cs`

**Interfaces:**
- Produces:
  - `GospodarOptions.ApiKey : string` (default `""`).
  - `public sealed record GospodarLead(string ExternalId, string? Name, string? Phone, string? Email, string? Comment, string? ProductInterest);`
  - `GospodarClient.PushLeadAsync(GospodarLead lead, CancellationToken ct) : Task<bool>` — `POST /api/integration/leads`, true on 2xx; false (logged warning) on non-2xx / `HttpRequestException` / `TaskCanceledException`; returns false without a call when the key is empty.
  - private `GospodarClient.Integration(HttpMethod, string path) : HttpRequestMessage` adding `X-Api-Key` — reused by Task 4.
  - `POST /api/audit/consultation` body `public sealed record AuditConsultationDto(string Phone, string? Website);` → `{ ok: true }`.
- `GospodarClient` currently takes `(HttpClient, IOptions<JwtOptions>)`; add `IOptions<GospodarOptions>` and `ILogger<GospodarClient>`.

- [ ] **Step 1: Failing tests** with a stub `HttpMessageHandler` that records the request and returns a configured response:
  - `PushLead_sends_api_key_and_lead_body`: key `"k1"` → request `POST /api/integration/leads`, header `X-Api-Key: k1`, JSON has `externalId`, `phone`, `comment == "Консультація щодо аудиту ділянки"`; returns true on 201.
  - `PushLead_skips_when_key_empty`: no request made, returns false.
  - `PushLead_returns_false_on_503`.
- [ ] **Step 2: Run** `cd backend && dotnet test --filter GospodarIntegrationTests` → FAIL (members missing).
- [ ] **Step 3: Implement** `ApiKey`, `GospodarLead`, `PushLeadAsync`, `Integration(...)`.
- [ ] **Step 4: Run** the filter → PASS.
- [ ] **Step 5: Endpoint.** `AuditEndpoints.Map`: `POST /api/audit/consultation` — honeypot → `{ ok: true }`; phone 7..30 else `ProblemException.BadRequest("Phone is required")`; save `ContactRequest { Name = "", Phone, Message = "Консультація щодо аудиту ділянки" }`; `PushLeadAsync(new(c.Id.ToString(), null, phone, null, "Консультація щодо аудиту ділянки", "Аудит вітру на ділянці"))`; then `NotifyManagersAsync("Консультація щодо аудиту: " + phone, body)` where body ends with «У Господар передано: так/ні». `.AllowAnonymous().RequireRateLimiting(RateLimitingExtensions.Forms)`.
- [ ] **Step 6: Page `/audit/consultation`.** Heading `audit.consultTitle`, one line `audit.consultLead` («Залиште номер — ми зателефонуємо протягом робочого дня.»), one `tel` field (`form.phone`), honeypot, `PrivacyNote`, submit `form.send`; on done `audit.consultDone` («Дякуємо! Ми зателефонуємо.») + link back to `/audit`. Use `useForm` exactly like `ContactForm.vue`.
- [ ] **Step 7: Verify.** `dotnet test`, `npm run typecheck`. With `Gospodar__ApiKey` empty: submit the form → 200, row in `contact_requests`, log line about skipped lead; admin «Контактні запити» shows it. Empty / short phone → inline error, no request.
- [ ] **Step 8: Commit** — `feat(site): phone-only audit consultation, pushed to Gospodar CRM`

### Task 4: Audit slots and booking API (order with audit fields)

**Files:**
- Modify: `backend/src/UaGreen.Site.Api/Domain/Orders.cs`, `.../Features/Orders/OrdersEndpoints.cs` (`OrderDto`, `ToDto`), `.../Features/Audit/AuditEndpoints.cs`, `.../Infrastructure/Auth/GospodarClient.cs`
- Create: `backend/src/UaGreen.Site.Api/Features/Audit/AuditWindow.cs`; migration `<ts>_AddAuditBooking`
- Test: `backend/tests/UaGreen.Site.Tests/AuditWindowTests.cs`, extend `GospodarIntegrationTests.cs`

**Interfaces:**
- Consumes: Task 3 `Integration(...)`, `GospodarOptions.ApiKey`.
- Produces:
  - `Order` new nullable props: `DateOnly? AuditDate`, `string? AuditAddress` (max 300), `DateTime? OfferAcceptedAt`, `DateTime? GospodarBookedAt`, `bool? AuditDateWasFree`.
  - `OrderDto` gains `DateOnly? AuditDate, string? AuditAddress, bool? AuditConfirmed` (`AuditConfirmed = AuditDateWasFree == true`), appended after `LiqPay`.
  - `public static class AuditWindow { public static (DateOnly From, DateOnly To) For(DateTime utcNow); public static bool IsBookable(DateOnly date, DateTime utcNow, IReadOnlyCollection<DateOnly> free); public const string ProductSlug = "audit-vitrovih-resursiv"; }` — `From = kyivToday + 7`, `To = kyivToday + 30`; Kyiv via `TimeZoneInfo.FindSystemTimeZoneById("Europe/Kyiv")`.
  - `GospodarClient.GetAuditSlotsAsync(DateOnly from, DateOnly to, CancellationToken ct) : Task<IReadOnlyList<DateOnly>?>` — `GET /api/integration/audit-slots?from=yyyy-MM-dd&to=yyyy-MM-dd` → `{ dates: [...] }`; null on empty key / non-2xx / exception.
  - `GET /api/audit/slots` → `public sealed record AuditSlotsDto(DateOnly From, DateOnly To, IReadOnlyList<DateOnly> Dates, decimal Price, decimal? OldPrice, bool Unavailable);`
  - `POST /api/audit/bookings` body `public sealed record AuditBookingRequest(string Name, string Phone, string? Email, string Address, string? Comment, DateOnly Date, bool OfferAccepted, string? Lang, string? Website);` → `OrderDto` with `LiqPay` filled.

- [ ] **Step 1: Failing tests**
  - `AuditWindow_uses_kyiv_date`: `For(new DateTime(2026,11,30,22,30,0,DateTimeKind.Utc))` → `(2026-12-08, 2026-12-31)`.
  - `AuditWindow_rejects_dates_outside_window_or_not_free`: free = {12-08, 12-10}; `IsBookable(12-08)` true; `12-09` false (not free); `12-07` false (too early, even if listed); `2027-01-01` false (too late, even if listed).
  - `GetAuditSlots_parses_dates_and_sends_key`: stub returns `{"dates":["2026-12-08","2026-12-10"]}` → two dates; request URL has `from=2026-12-08&to=2026-12-31`, header `X-Api-Key`.
  - `GetAuditSlots_returns_null_when_gospodar_throws`: stub throws `HttpRequestException` → null.
- [ ] **Step 2: Run** `dotnet test --filter "AuditWindowTests|GospodarIntegrationTests"` → FAIL.
- [ ] **Step 3: Implement** `AuditWindow`, `GetAuditSlotsAsync`.
- [ ] **Step 4: Run** → PASS.
- [ ] **Step 5: Order fields + migration.** Props above; `AuditAddress` `HasMaxLength(300)`; `dotnet ef migrations add AddAuditBooking -p src/UaGreen.Site.Api`. Extend `OrderDto`/`ToDto`; add `auditDate: string | null; auditAddress: string | null; auditConfirmed: boolean | null` to `Order` in `frontend/types/api.ts`.
- [ ] **Step 6: `GET /api/audit/slots`.** Load product by slug (404 if missing; price/oldPrice from it). `(from,to) = AuditWindow.For(UtcNow)`; `dates = await GetAuditSlotsAsync(...)`; `Unavailable = dates is null`; `Dates = dates?.Where(d => d >= from && d <= to) ?? []`. `.AllowAnonymous()`. `ponytail:` no cache — Gospodar is queried per page view; add output cache if it shows up in load.
- [ ] **Step 7: `POST /api/audit/bookings`.** Honeypot → fake `OrderDto` like `OrdersEndpoints.CreateAsync`. Validate: name 2..120, phone 7..30, address 5..300, `OfferAccepted == true` (`"Offer must be accepted"`), comment ≤ 1000. Fetch fresh slots; `!AuditWindow.IsBookable(req.Date, UtcNow, slots ?? [])` → `ProblemException.BadRequest("Date is not available", ErrorCodes.DateUnavailable)` (add `DateUnavailable = "DATE_UNAVAILABLE"` to `Common/Errors.cs`). Create `Order` like `CreateAsync` with: `Status = AwaitingPayment`, `PaymentMethod = LiqPay`, `DeliveryMethod = Pickup`, one `OrderItem` (product name in `lang`, qty 1, price from DB), `Total = Subtotal = price`, `Comment`, `AuditDate`, `AuditAddress`, `OfferAcceptedAt = UtcNow`. Build LiqPay checkout (existing `BuildCheckout`). Notify managers «Аудит очікує оплати №{n} — {date:dd.MM.yyyy}». Rate limit + anonymous.
- [ ] **Step 8: Verify.** `dotnet test`; run API; `curl localhost:5000/api/audit/slots` with empty key → `unavailable: true`, `dates: []`, price 15000; POST with a date outside the window → 400 `DATE_UNAVAILABLE`.
- [ ] **Step 9: Commit** — `feat(site): audit slots and booking API on top of orders`

### Task 5: Book in Gospodar on the paid LiqPay callback

**Files:**
- Create: `backend/src/UaGreen.Site.Api/Features/Audit/AuditBooking.cs`
- Modify: `.../Features/Orders/OrdersEndpoints.cs` (`LiqPayCallbackAsync`), `.../Infrastructure/Auth/GospodarClient.cs`, `README.md` (deploy note)
- Test: `backend/tests/UaGreen.Site.Tests/AuditBookingTests.cs`, extend `GospodarIntegrationTests.cs`

**Interfaces:**
- Consumes: Task 4 order fields, `Integration(...)`.
- Produces:
  - `public sealed record GospodarAuditBooking(string ExternalId, string Name, string Phone, string? Email, string Address, string? Comment, DateOnly Date, decimal Amount, DateTime PaidAt, string PaymentRef, DateTime OfferAcceptedAt);`
  - `public sealed record GospodarAuditBooked(Guid DealId, Guid OrderId, Guid TripId, bool DateWasFree);`
  - `GospodarClient.BookAuditAsync(GospodarAuditBooking b, CancellationToken ct) : Task<GospodarAuditBooked?>` — `POST /api/integration/audit-bookings`; 200/201 → body; anything else → null (logged).
  - `public static class AuditBooking { public static bool ShouldBook(Order o); public static GospodarAuditBooking ToRequest(Order o, long? paymentId); public static Task SendAsync(Order o, long? paymentId, GospodarClient g, Notifier n, CancellationToken ct); }`
    - `ShouldBook` = `o.Status == Paid && o.AuditDate != null && o.GospodarBookedAt == null`.
    - `ToRequest`: `ExternalId = o.Id`, `Amount = o.Total`, `PaidAt = o.PaidAt!.Value`, `PaymentRef = $"liqpay:{paymentId}"`, `OfferAcceptedAt = o.OfferAcceptedAt ?? o.CreatedAt`.
    - `SendAsync`: success → set `GospodarBookedAt = UtcNow`, `AuditDateWasFree = r.DateWasFree`; if `!DateWasFree` email «Аудит №{n}: дата {d} уже зайнята — Господар створив задачу на перенесення». Failure → email «Аудит №{n} оплачено, але НЕ передано в Господар» with every `ToRequest` field (name, phone, email, address, date, amount, payment ref). Caller saves.

- [ ] **Step 1: Failing tests**
  - `ShouldBook_false_unless_paid`: AwaitingPayment + AuditDate → false; Paid + AuditDate → true; Paid without AuditDate → false.
  - `ShouldBook_false_once_booked`: Paid + AuditDate + `GospodarBookedAt` set → false.
  - `ToRequest_maps_payment_and_dates`: order Total 15000, PaidAt, AuditDate 2026-12-08, paymentId 123 → `Amount 15000`, `PaymentRef "liqpay:123"`, `Date 2026-12-08`, `ExternalId == order.Id.ToString()`.
  - `BookAudit_parses_response`: stub 201 `{"dealId":…,"orderId":…,"tripId":…,"dateWasFree":false}` → `DateWasFree == false`; JSON body sent has camelCase `externalId`, `date: "2026-12-08"`, `paymentRef`.
- [ ] **Step 2: Run** `dotnet test --filter "AuditBookingTests|GospodarIntegrationTests"` → FAIL.
- [ ] **Step 3: Implement** `BookAuditAsync`, `AuditBooking`.
- [ ] **Step 4: Run** → PASS.
- [ ] **Step 5: Wire the callback.** In `LiqPayCallbackAsync`, after the existing paid branch and **after** `SaveChangesAsync` (so a Gospodar failure never loses the paid status): `if (AuditBooking.ShouldBook(order)) { await AuditBooking.SendAsync(order, cb.PaymentId, gospodar, notifier, ct); await db.SaveChangesAsync(ct); }`. Because `ShouldBook` checks `GospodarBookedAt`, a later LiqPay callback for the same order retries a failed booking. `ponytail:` no retry queue — LiqPay's repeated callbacks + the manager email are the retry; add a background retry if failures show up. Add `GospodarClient gospodar` to the handler's parameters.
- [ ] **Step 6: README deploy note:** prod must run with `LIQPAY_SANDBOX=false` (status `sandbox` counts as paid and would book real trips); staging uses the test company's `GOSPODAR_API_KEY`.
- [ ] **Step 7: Verify.** `dotnet test`. Manual: create a booking (Task 4 curl), post a signed sandbox callback for it (build `data`/`signature` with `LiqPayService.Sign` in a scratch script) with Gospodar key empty → order `Paid`, `GospodarBookedAt` null, «НЕ передано» email logged; post it again → `SendAsync` runs again (retry), still no duplicate order.
- [ ] **Step 8: Commit** — `feat(site): book paid audits in Gospodar from the LiqPay callback`

### Task 6: `/audit/book` page, calendar and order status

**Files:**
- Create: `frontend/utils/calendar.ts`, `frontend/utils/calendar.test.ts`, `frontend/components/audit/AuditCalendar.vue`, `frontend/pages/audit/book.vue`
- Modify: `frontend/pages/order/[id].vue`, `frontend/types/api.ts` (`AuditSlots`, `AuditBookingRequest`), both locale files

**Interfaces:**
- Consumes: `GET /api/audit/slots` → `AuditSlots { from: string; to: string; dates: string[]; price: number; oldPrice: number | null; unavailable: boolean }`; `POST /api/audit/bookings` → `Order`; `<PrivacyNote />`; `<LiqPayForm v-bind="order.liqPay" />`.
- Produces:
  - `calendarWeeks(from: string, to: string, free: string[]): CalendarDay[][]` with `CalendarDay = { iso: string; day: number; inWindow: boolean; free: boolean }` — Monday-first weeks covering `from..to`, padded at both ends with out-of-window days.
  - `<AuditCalendar v-model="date" :slots="slots" />` (`date: string | null`, ISO).

- [ ] **Step 1: Failing vitest** `calendar.test.ts`:
  - `starts weeks on Monday and pads`: `calendarWeeks('2026-12-08','2026-12-31',[])` → first week starts `2026-12-07` (Mon) with `inWindow: false`, last week ends `2027-01-03` (Sun); every row has 7 days.
  - `marks only listed in-window days as free`: free `['2026-12-08','2026-12-10','2027-01-02']` → `12-08` and `12-10` free, `2027-01-02` not free (outside window).
- [ ] **Step 2: Run** `cd frontend && npx vitest run utils/calendar.test.ts` → FAIL.
- [ ] **Step 3: Implement `calendarWeeks`** with plain UTC date arithmetic on `YYYY-MM-DD` strings (no time zone shifts; the API already returns Kyiv dates).
- [ ] **Step 4: Run** → PASS.
- [ ] **Step 5: `AuditCalendar.vue`** — month label(s) above, weekday header (`audit.weekdays` array: «Пн»…«Нд» / "Mon"…"Sun"), grid of `<button type="button">` per day: free → enabled, `aria-pressed` when selected (filled `var(--green)`), others `disabled` and muted; out-of-window days blank. Keyboard: native button focus is enough.
- [ ] **Step 6: `/audit/book` page.** `useApiFetch<AuditSlots>('audit-slots', '/audit/slots')`. Heading `audit.bookTitle`, price line. If `unavailable || !dates.length`: `audit.noDates` («Зараз не можемо показати вільні дати. Залиште номер — підберемо дату разом.») + link to `/audit/consultation`; no form. Else: `AuditCalendar`, then fields name, phone, email, `audit.address` («Адреса ділянки» + hint «Населений пункт, вулиця або орієнтир»), comment (`textarea`, optional), checkbox `audit.offer` with «публічної оферти» linking `/publichna-oferta` (`i18n-t`), honeypot, `PrivacyNote`, submit `audit.pay` («Оплатити {price}»). Client validation: date chosen (`audit.pickDate` «Оберіть дату»), name ≥ 2, phone, address ≥ 5, offer checked (`audit.offerRequired` «Потрібна згода з умовами оферти»). On success render `LiqPayForm` (as `checkout.vue`). API error code `DATE_UNAVAILABLE` → show `audit.dateTaken` («Цю дату щойно зайняли — оберіть іншу») and `refreshNuxtData('audit-slots')`.
- [ ] **Step 7: Order page.** When `order.auditDate`: replace the `note` for `Paid` with `audit.booked` («Аудит заплановано на {date}. Адреса: {address}.») if `auditConfirmed`, else `audit.paidPending` («Оплату отримано. Менеджер зателефонує, щоб підтвердити дату {date}.»); `AwaitingPayment` keeps `order.waiting`. Format date with the existing `formatDate`.
- [ ] **Step 8: Verify.** `npm run typecheck && npm test`. Browser, with API key empty: `/audit/book` shows the no-dates fallback. Then temporarily stub dates (point `Gospodar__BaseUrl` at a tiny local stub, or set the key and use the staging test company once its endpoints exist): pick a date, submit invalid form → inline errors; valid → redirect to LiqPay sandbox; after a sandbox payment `/order/{id}` shows the audit line. Check 375 px.
- [ ] **Step 9: Commit** — `feat(site): /audit/book with free-date calendar and LiqPay`

---

**Release order (from the spec):** Tasks 1–3 can ship with the site launch (Gospodar key may stay empty). Task 6's page goes live on prod only after the Gospodar plan is deployed; until then it shows the no-dates fallback.
