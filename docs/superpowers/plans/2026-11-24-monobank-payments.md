# Оплата через monobank: план реалізації

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Замінити LiqPay на інтернет-еквайринг monobank (кошик і аудит) і додати новий спосіб оплати «Покупка частинами monobank» з підтвердженням видачі з адмінки.

**Architecture:** Два типізовані HTTP-клієнти в `Infrastructure/Payments`: `MonobankAcquiring` для рахунків і `MonoPartsClient` для заявок. Банк лише «будить» сайт webhook-ом, а `PaymentSync` сам читає статус з банку своїм ключем і застосовує чисті правила переходів. Ці ж правила спрацьовують при відкритті замовлення, тож перевіряти підпис webhook не потрібно.

**Tech Stack:** .NET 9 minimal API + EF Core (Npgsql, snake_case) + xunit; Nuxt 3 (Vue, @nuxtjs/i18n, vitest); Flutter web (admin).

**Spec:** `docs/superpowers/specs/2026-11-24-monobank-payments-design.md`

## Global Constraints

- `PaymentMethod` = `Card`, `Invoice`, `Credit`, `MonoParts`. `Credit` (ручний «Кредит 0 %») не змінюється.
- Еквайринг: база `https://api.monobank.ua`, заголовок `X-Token`, `amount` у копійках (`long`), `ccy` = 980.
- Частинами: суми в гривнях; `signature` = `Base64(HMAC-SHA256(secret_utf8, body_bytes))` над тими самими байтами, що відправляються; заголовки `store-id`, `signature`, `Content-Type: application/json`.
- Пісочниця частинами: `https://u2-demo-ext.mono.st4g3.com`, `test_store_with_confirm` / `secret_98765432--123-123`. Прод: `https://u2.monobank.com.ua`.
- Частинами: `available_parts_count` = від 3 до `pay_parts_max` (3–25, порожнє → 6), `type` = `payment_installments`, `invoice.source` = `INTERNET`.
- Телефон для частинами — `+380XXXXXXXXX`, інакше 400 «Для покупки частинами потрібен український номер +380…».
- Тексти для клієнта — uk/en/cs. Тексти адмінки й листів — українською.
- Webhook-и відповідають 200 завжди; rate limit — політика `RateLimitingExtensions.Chat`.
- Жодного «Оплачено» без `success` на точну суму (`amount == Total × 100`, `ccy == 980`) або без погодження банком заявки частинами.

## Review Focus

1. Телефон з пробілами, дужками, без `+38` (`050 123 45 61`, `(050)1234561`) — має прийматися як `+380501234561`. Тест у Task 1.
2. Той самий webhook двічі або оновлення після «Оплачено» — один лист «Оплачено», одне бронювання аудиту. Тест у Task 4.
3. Сума з копійками (14 999,99 ₴) — рахунок на `1499999`, і `success` на `1499999` дає «Оплачено». Тести в Task 2 і Task 4.
4. Банк недоступний, коли менеджер ставить «Відправлено» — 400 з поясненням, статус не змінюється. Тест у Task 6.
5. Сміття або чужий id у webhook — 200, нічого не змінюється, без винятку. Тест у Task 5.

---

### Task 1: Клієнт «Покупки частинами»

**Files:**
- Create: `backend/src/UaGreen.Site.Api/Infrastructure/Payments/MonoParts.cs`
- Test: `backend/tests/UaGreen.Site.Tests/MonoPartsTests.cs`

**Interfaces:**
- Produces:
  - `MonoPartsOptions { string BaseUrl = "https://u2-demo-ext.mono.st4g3.com"; string StoreId = ""; string Secret = ""; bool Configured => StoreId != "" && Secret != "" }`
  - `sealed record MonoPartsState(string OrderId, string State, string SubState) { string Text => $"{State}/{SubState}" }` — JSON: `order_id`, `state`, `order_sub_state`.
  - `sealed class MonoPartsException(string message) : Exception(message)`
  - `sealed class MonoPartsClient(HttpClient http, IOptions<MonoPartsOptions> opts, IOptions<AppOptions> app)` з методами:
    - `Task<string> CreateAsync(Order o, string phone, int maxParts, CancellationToken ct)` — повертає банківський `order_id`;
    - `Task<MonoPartsState> StateAsync(string orderId, CancellationToken ct)`;
    - `Task<MonoPartsState> ConfirmAsync(string orderId, CancellationToken ct)`;
    - `Task<MonoPartsState> RejectAsync(string orderId, CancellationToken ct)`.
  - Статичні: `string Sign(byte[] body, string secret)`, `string? NormalizePhone(string? phone)`, `int MaxParts(string? setting)`, `int[] Parts(int max)`, `byte[] BuildCreateBody(Order o, string phone, int maxParts, string callbackUrl, DateOnly today)`.
  - Будь-яка не-2xx відповідь, `HttpRequestException` або таймаут → `MonoPartsException`: текст `message` з тіла банку або `HTTP {code}`.

- [ ] **Step 1: Write the failing tests** (`MonoPartsTests`)

```csharp
[Fact] public void Sign_matches_python_vector() => Assert.Equal("3gXVwnp3FqizzUICf5cwhp7PGVBqt+vGeLeHKS3dRfg=",
    MonoPartsClient.Sign(Encoding.UTF8.GetBytes("{\"order_id\":\"a0eebc99-9c0b-4ef8-bb6d-000000000004\"}"), "secret_98765432--123-123"));

[Theory]
[InlineData("0501234561", "+380501234561")]
[InlineData("+38 (050) 123-45-61", "+380501234561")]
[InlineData("380501234561", "+380501234561")]
[InlineData("050 123 45 61", "+380501234561")]
[InlineData("12345", null)]
[InlineData("+48501234561", null)]
[InlineData(null, null)]
public void NormalizePhone(string? input, string? expected) => Assert.Equal(expected, MonoPartsClient.NormalizePhone(input));

[Theory] [InlineData(null, 6)] [InlineData("", 6)] [InlineData("abc", 6)] [InlineData("3", 3)] [InlineData("25", 25)]
public void MaxParts(string? s, int expected) => Assert.Equal(expected, MonoPartsClient.MaxParts(s));

[Fact] public void Parts_from_three_to_max() => Assert.Equal([3, 4, 5, 6], MonoPartsClient.Parts(6));

[Fact] public void BuildCreateBody_has_bank_fields()
// Order { Id, Number = 1001, Total = 14999.99m, Items = [{ ProductName = "К5", Qty = 1, LineTotal = 14999.99m }] },
// phone "+380501234561", maxParts 4, callback "https://uagreen.co/api/payments/monoparts/callback", today 2026-11-24.
// Parse the JSON and assert:
//   store_order_id == o.Id.ToString(); client_phone == "+380501234561"; total_sum == 14999.99;
//   invoice.date == "2026-11-24"; invoice.number == "1001"; invoice.source == "INTERNET";
//   available_programs[0].available_parts_count == [3,4]; available_programs[0].type == "payment_installments";
//   products[0] == { name: "К5", count: 1, sum: 14999.99 }; result_callback == the callback.
```

- [ ] **Step 2: Run, expect a compile failure**

Run: `dotnet test backend/tests/UaGreen.Site.Tests --filter MonoPartsTests`
Expected: build error `MonoPartsClient` not found.

- [ ] **Step 3: Implement `MonoParts.cs`**

- `NormalizePhone`: бере лише цифри. 10 цифр з `0` на початку → `+38` + цифри. 12 цифр з `380` на початку → `+` + цифри. Інакше `null`.
- `MaxParts`: `int.TryParse`, у межах 3–25, інакше 6.
- `products`: по одному на позицію замовлення, `sum` = `LineTotal`.
- Запит: серіалізувати тіло в `byte[]` один раз → `ByteArrayContent` + підпис з тих самих байтів.
- Callback URL: `{AppOptions.PublicBaseUrl.TrimEnd('/')}/api/payments/monoparts/callback`.

- [ ] **Step 4: Run tests, expect PASS**

Run: `dotnet test backend/tests/UaGreen.Site.Tests --filter MonoPartsTests`

- [ ] **Step 5: Commit** — `feat(payments): monobank pay-in-parts client`

### Task 2: Клієнт інтернет-еквайрингу

**Files:**
- Create: `backend/src/UaGreen.Site.Api/Infrastructure/Payments/MonobankAcquiring.cs`
- Test: `backend/tests/UaGreen.Site.Tests/MonobankAcquiringTests.cs`

**Interfaces:**
- Produces:
  - `MonobankOptions { string Token = ""; bool Configured => Token != "" }`
  - `sealed record MonoInvoice(string InvoiceId, string PageUrl)` — JSON: `invoiceId`, `pageUrl`.
  - `sealed record MonoInvoiceStatus(string InvoiceId, string Status, long Amount, int Ccy)` — JSON: `invoiceId`, `status`, `amount`, `ccy`.
  - `sealed class MonobankException(string message) : Exception(message)`
  - `sealed class MonobankAcquiring(HttpClient http, IOptions<MonobankOptions> opts, IOptions<AppOptions> app)` з методами:
    - `Task<MonoInvoice> CreateInvoiceAsync(Order o, CancellationToken ct)` — `POST /api/merchant/invoice/create`;
    - `Task<MonoInvoiceStatus> GetStatusAsync(string invoiceId, CancellationToken ct)` — `GET /api/merchant/invoice/status?invoiceId=`.
  - Статичні:
    - `long Kopecks(decimal uah)` — `Math.Round(uah * 100, MidpointRounding.AwayFromZero)`;
    - `object BuildInvoice(Order o, string baseUrl)`;
    - `bool Reusable(string? status)` — `true` для `null`, `created`, `processing`.
  - Помилки → `MonobankException` (як у Task 1).

- [ ] **Step 1: Write the failing tests**

```csharp
[Theory] [InlineData("14999.99", 1499999)] [InlineData("15000", 1500000)] [InlineData("0.005", 1)]
public void Kopecks(string uah, long expected) => Assert.Equal(expected, MonobankAcquiring.Kopecks(decimal.Parse(uah, CultureInfo.InvariantCulture)));

[Fact] public void BuildInvoice_fields()
// Order { Id, Number = 42, Total = 15000m }, baseUrl "https://uagreen.co/".
// Serialize and assert:
//   amount == 1500000; ccy == 980;
//   merchantPaymInfo.reference == o.Id.ToString(); merchantPaymInfo.destination == "UA GREEN, замовлення №42";
//   redirectUrl == "https://uagreen.co/order/{o.Id}"; webHookUrl == "https://uagreen.co/api/payments/monobank/webhook";
//   no "basketOrder" property.

[Theory] [InlineData(null, true)] [InlineData("created", true)] [InlineData("processing", true)]
[InlineData("failure", false)] [InlineData("expired", false)] [InlineData("reversed", false)] [InlineData("success", false)]
public void Reusable(string? s, bool expected) => Assert.Equal(expected, MonobankAcquiring.Reusable(s));
```

- [ ] **Step 2: Run, expect a compile failure**

Run: `dotnet test backend/tests/UaGreen.Site.Tests --filter MonobankAcquiringTests`

- [ ] **Step 3: Implement `MonobankAcquiring.cs`**

Без `basketOrder`: поставити `// ponytail: no basketOrder — monobank requires it only with ПРРО; add item list when fiscalization is switched on in the cabinet.`

- [ ] **Step 4: Run tests, expect PASS**

- [ ] **Step 5: Commit** — `feat(payments): monobank acquiring client`

### Task 3: Модель, міграція, конфіг; LiqPay геть

**Files:**
- Modify:
  - `backend/src/UaGreen.Site.Api/Domain/Orders.cs`
  - `Infrastructure/Persistence/AppDbContext.cs` (конфіг `Order`)
  - `Program.cs`
  - `appsettings.json`, `appsettings.Development.json`
  - `Features/Orders/PaymentSettings.cs`
  - `Features/Admin/AdminContentEndpoints.cs` (payments PUT)
  - `Features/Content/ContentEndpoints.cs` (`/settings/public`)
  - `Features/Audit/AuditBooking.cs`
  - `Infrastructure/Crm/CrmSync.cs:117`
  - `Features/Orders/OrdersEndpoints.cs`, `Features/Audit/AuditEndpoints.cs` — лише щоб збиралося: прибрати LiqPay, `payUrl` поки `null`
  - `Features/Admin/AdminOrdersEndpoints.cs` — лише перейменування DTO-поля
  - `infra/docker/docker-compose.prod.yml`, `infra/docker/.env.prod.example`, `README.md`
- Create: `Infrastructure/Persistence/Migrations/<ts>_MonobankPayments.cs`
- Delete:
  - `Infrastructure/Payments/LiqPay.cs`
  - `backend/tests/UaGreen.Site.Tests/LiqPayTests.cs`
- Test: `PaymentSettingsTests.cs`, `AuditBookingTests.cs`, `GospodarIntegrationTests.cs`

**Interfaces:**
- Consumes: `MonobankOptions`, `MonoPartsOptions`, `MonobankAcquiring`, `MonoPartsClient` (Tasks 1–2).
- Produces:
  - `enum PaymentMethod { Card, Invoice, Credit, MonoParts }`
  - `Order.PaymentStatus` (string?, max 64; колонка перейменована з `liq_pay_status`), `Order.PaymentRef` (string?, max 64, індекс), `Order.PayUrl` (string?, max 500), `Order.PaymentCheckedAt` (DateTime?)
  - `OrderDto`: замість `LiqPay` — `string? PayUrl, string? PaymentStatus`. `OrdersEndpoints.ToDto(Order o)` без другого аргументу.
  - `OrderAdminDto.PaymentStatus` замість `LiqPayStatus`.
  - `PaymentSettings`:
    - `const string PartsMax = "pay_parts_max"`;
    - `static IReadOnlyList<PaymentMethod> Unconfigured(MonobankOptions card, MonoPartsOptions parts)`;
    - `static void Validate(Dictionary<string, string?> req, IReadOnlyDictionary<string, string> current)`.
  - `AuditBooking.ToRequest(Order o, string paymentRef)`, `AuditBooking.SendAsync(Order o, string paymentRef, GospodarClient, Notifier, CancellationToken)`.

- [ ] **Step 1: Update the tests first**

  - **`AuditBookingTests`:** `ToRequest(o, "mono:inv_1")` → `r.PaymentRef == "mono:inv_1"`.
  - **`GospodarIntegrationTests`:** `"liqpay:123"` → `"mono:inv_1"`.
  - **`PaymentSettingsTests`** (виправлення наявної помилки: сторінка надсилає лише змінене, а `Validate` раніше видаляв усе ненадіслане):

```csharp
[Fact] public void Left_out_keys_are_not_touched()
{   var req = new Dictionary<string, string?> { ["pay_Credit"] = "off" };
    PaymentSettings.Validate(req, new Dictionary<string, string> { ["pay_iban"] = "UA213223130000026007233566001" });
    Assert.Equal(["pay_Credit"], req.Keys); }
[Fact] public void All_off_counts_current_values()
    => Bad(new() { ["pay_MonoParts"] = "off" },
           current: new() { ["pay_Card"] = "off", ["pay_Invoice"] = "off", ["pay_Credit"] = "off" });
[Theory] [InlineData("2")] [InlineData("26")] [InlineData("abc")]
public void Parts_max_out_of_range(string v) => Bad(new() { ["pay_parts_max"] = v });
[Fact] public void Parts_max_ok() { var r = new Dictionary<string, string?> { ["pay_parts_max"] = " 10 " };
    PaymentSettings.Validate(r, new Dictionary<string, string>()); Assert.Equal("10", r["pay_parts_max"]); }
[Fact] public void Unconfigured_lists_methods_without_keys()
    => Assert.Equal([PaymentMethod.Card, PaymentMethod.MonoParts],
       PaymentSettings.Unconfigured(new MonobankOptions(), new MonoPartsOptions()));
```

Наявні тести з `pay_LiqPay` перевести на `pay_Card`. Тест «left out → cleared» видалити.

- [ ] **Step 2: Run, expect compile failures**

Run: `dotnet test backend/tests/UaGreen.Site.Tests`

- [ ] **Step 3: Implement**

  - **Перейменування:** `PaymentMethod.LiqPay` → `Card` скрізь у бекенді; `LiqPayStatus` → `PaymentStatus`.
  - **Видалення:** `LiqPayService`, його реєстрація, `LiqPayOptions`, `/api/payments/liqpay/callback`, `LiqPayCheckoutDto`.
  - **`Validate`:**
    - не додає відсутні ключі;
    - перевірку «хоча б один увімкнений» робить на `current` з накладеним `req`;
    - `pay_parts_max` — trim, ціле 3–25 або порожнє, інакше 400 «Максимум платежів — від 3 до 25».
  - **Admin PUT:** перед `Validate` прочитати поточні `pay_*` рядки в словник.
  - **`/settings/public`:** після вибірки для кожного методу з `Unconfigured(...)` поставити `pay_{m}` = `"off"`.
  - **`CrmSync`:** `Card` → «картка (monobank)», `Invoice` → «переказ на IBAN», `Credit` → «кредит 0%», `MonoParts` → «частинами (monobank)».
  - **`Program.cs`:**
    - `Configure<MonobankOptions>("Monobank")`, `Configure<MonoPartsOptions>("MonoParts")`;
    - `AddHttpClient<MonobankAcquiring>`: база `https://api.monobank.ua`, таймаут 20 с;
    - `AddHttpClient<MonoPartsClient>`: база з `MonoPartsOptions.BaseUrl`, таймаут 20 с;
    - без resilience-ретраїв (create еквайрингу не ідемпотентний);
    - коментар про forwarded headers: «LiqPay URLs» → «payment callback URLs».
  - **`appsettings.json`:** `"Monobank": { "Token": "" }`, `"MonoParts": { "BaseUrl": "https://u2-demo-ext.mono.st4g3.com", "StoreId": "", "Secret": "" }`.
  - **`appsettings.Development.json`:** `MonoParts` з пісочницею і публічними `test_store_with_confirm` / `secret_98765432--123-123`.
  - **Compose:** `Monobank__Token: ${MONOBANK_TOKEN:-}`, `MonoParts__BaseUrl: ${MONOPARTS_BASE_URL:-https://u2.monobank.com.ua}`, `MonoParts__StoreId: ${MONOPARTS_STORE_ID:-}`, `MonoParts__Secret: ${MONOPARTS_SECRET:-}`.
  - **`.env.prod.example`:** ці чотири змінні з коментарем «порожньо = спосіб оплати прихований; staging — тестовий токен з api.monobank.ua і пісочниця частинами».
  - **README:** рядок контрактів `LiqPay__*` → `Monobank__Token`, `MonoParts__*`. Абзац про `LIQPAY_SANDBOX` → «Перед продом: бойовий `MONOBANK_TOKEN` (тестовий токен вважає будь-яку картку успішною — оплачений тестом аудит забронює справжній виїзд у Господарі) і `MONOPARTS_BASE_URL=https://u2.monobank.com.ua` з бойовим store-id».
  - **Міграція:** `dotnet ef migrations add MonobankPayments --project backend/src/UaGreen.Site.Api -o Infrastructure/Persistence/Migrations`. Перевірити, що EF згенерував `RenameColumn` (а не drop+add). В `Up` додати:
    - `UPDATE orders SET payment_method = 'Card' WHERE payment_method = 'LiqPay'`;
    - `UPDATE settings SET key = 'pay_Card' WHERE key = 'pay_LiqPay'`.

    В `Down` — зворотне.

- [ ] **Step 4: Run all backend tests, expect PASS**

Run: `dotnet test backend/tests/UaGreen.Site.Tests`
Expected: усі зелені; `grep -rni liqpay backend/src` порожній (крім старих міграцій).

- [ ] **Step 5: Commit** — `refactor(payments): drop LiqPay, add monobank model and config`

### Task 4: `PaymentSync` — правила переходів і оновлення з банку

**Files:**
- Create: `backend/src/UaGreen.Site.Api/Features/Orders/PaymentSync.cs`
- Modify: `Program.cs` (`AddScoped<PaymentSync>()`)
- Test: `backend/tests/UaGreen.Site.Tests/PaymentSyncTests.cs`

**Interfaces:**
- Consumes: `MonobankAcquiring.GetStatusAsync`, `MonoPartsClient.StateAsync`, `AuditBooking.ShouldBook/SendAsync`, `Notifier.NotifyManagersAsync`.
- Produces:
  - `enum PayNotice { None, Paid, AmountMismatch, PartsApproved, PartsFailed, PartsCancelled }`
  - `static PayNotice PaymentSync.ApplyCard(Order o, MonoInvoiceStatus s, DateTime now)`
  - `static PayNotice PaymentSync.ApplyParts(Order o, MonoPartsState s, DateTime now)`
  - `static bool PaymentSync.Waiting(Order o)` — `AwaitingPayment` з `PaymentRef`, або `MonoParts` + `Paid` + `PaymentStatus` починається з `IN_PROCESS/`
  - `Task PaymentSync.RefreshAsync(Order o, CancellationToken ct)` — кидає `MonobankException`/`MonoPartsException`
  - `Task PaymentSync.RefreshIfStaleAsync(Order o, CancellationToken ct)` — лише якщо `Waiting(o)` і `PaymentCheckedAt` старший за 10 с; помилки банку → лог, без винятку

- [ ] **Step 1: Write the failing tests** (лише чисті `Apply*`)

```csharp
// Card, Total = 14999.99m, Status = AwaitingPayment:
ApplyCard(success, 1499999, 980) → Paid; o.Status == Paid; o.PaidAt == now; o.PaymentStatus == "success"
ApplyCard(success, 1499999, 980) again on the now-Paid order → None; PaidAt unchanged
ApplyCard(success, 1000, 980) → AmountMismatch; Status still AwaitingPayment
ApplyCard(failure, …) → None; PaymentStatus == "failure"; Status AwaitingPayment
// MonoParts, Status = AwaitingPayment:
ApplyParts(IN_PROCESS/WAITING_FOR_CLIENT) → None
ApplyParts(IN_PROCESS/WAITING_FOR_STORE_CONFIRM) → PartsApproved; Status Paid; PaidAt == now
ApplyParts(SUCCESS/ACTIVE) → PartsApproved; Status Paid
ApplyParts(FAIL/EXCEEDED_SUM_LIMIT) → PartsFailed; Status New
ApplyParts(FAIL/REJECTED_BY_STORE) on a Cancelled order → None; Status Cancelled
// MonoParts, Status = Paid, PaymentStatus = "IN_PROCESS/WAITING_FOR_STORE_CONFIRM":
ApplyParts(FAIL/CLIENT_PUSH_TIMEOUT) → PartsCancelled; Status New; PaidAt null
// MonoParts, Status = Shipped, PaymentStatus = "SUCCESS/ACTIVE":
ApplyParts(SUCCESS/DONE) → None; Status Shipped; PaymentStatus == "SUCCESS/DONE"
```

- [ ] **Step 2: Run, expect compile failure** — `dotnet test … --filter PaymentSyncTests`

- [ ] **Step 3: Implement `PaymentSync.cs`**

`Apply*` завжди записують `PaymentStatus` (картка — `status`, частинами — `State.Text`) і `PaymentCheckedAt = now`.

`RefreshAsync`:
1. Читає банк за `PaymentRef`, викликає `Apply*`, зберігає.
2. Шле лист за `PayNotice`:
   - `Paid` — «Оплачено замовлення №{n} — {total:N0} грн»;
   - `AmountMismatch` — «Сума оплати №{n} не збігається — перевірте в кабінеті monobank» + `log.LogWarning`;
   - `PartsApproved` — «Покупку частинами погоджено, замовлення №{n} можна відвантажувати»;
   - `PartsFailed` — «Банк не погодив покупку частинами №{n}: {SubState}»;
   - `PartsCancelled` — «Банк скасував покупку частинами №{n} — не відвантажуйте без оплати».
3. Після збереження, якщо `Paid` і `AuditBooking.ShouldBook(o)`: `SendAsync(o, $"mono:{o.PaymentRef}", …)` на окремому `CancellationTokenSource(60 с)`, потім знову зберегти. Так само, як зараз у LiqPay-колбеку.

- [ ] **Step 4: Run tests, expect PASS**

- [ ] **Step 5: Commit** — `feat(payments): sync order status from monobank`

### Task 5: Публічні ендпоінти — оформлення, оплата, webhook-и, аудит

**Files:**
- Modify:
  - `Features/Orders/OrdersEndpoints.cs`
  - `Features/Audit/AuditEndpoints.cs`
- Create: `Features/Orders/PaymentWebhooks.cs`
- Test: `backend/tests/UaGreen.Site.Tests/PaymentWebhooksTests.cs`

**Interfaces:**
- Consumes: Tasks 1–4.
- Produces:
  - `static string? PaymentWebhooks.ExtractId(string body, string field)` — `invoiceId` або `order_id`; будь-що невалідне → `null`
  - `POST /api/payments/monobank/webhook`
  - `POST /api/payments/monoparts/callback`
  - `POST /api/orders/{id:guid}/pay` → `{ payUrl: string }`
  - `GET /api/orders/{id}`, який оновлює статус, якщо він застарів

- [ ] **Step 1: Write the failing tests**

```csharp
[Theory]
[InlineData("{\"invoiceId\":\"p2_9ZgpZVsl3\",\"status\":\"success\"}", "invoiceId", "p2_9ZgpZVsl3")]
[InlineData("{\"order_id\":\"a0eebc99-9c0b-4ef8-bb6d-000000000004\"}", "order_id", "a0eebc99-9c0b-4ef8-bb6d-000000000004")]
[InlineData("not json", "invoiceId", null)]
[InlineData("{\"invoiceId\":42}", "invoiceId", null)]
[InlineData("", "order_id", null)]
public void ExtractId(string body, string field, string? expected) => Assert.Equal(expected, PaymentWebhooks.ExtractId(body, field));
```

- [ ] **Step 2: Run, expect compile failure**

- [ ] **Step 3: Implement**

  - **Webhook-и** (анонімні, `DisableAntiforgery`, `RequireRateLimiting(Chat)`):
    - прочитати сире тіло → `ExtractId` → `db.Orders.FirstOrDefault(o => o.PaymentRef == id)`;
    - якщо знайдено → `sync.RefreshAsync` у `try` з логом;
    - завжди `Results.Ok()`.
  - **`POST /api/orders`:**
    - спосіб з `PaymentSettings.Unconfigured(...)` → 400 «Payment method is turned off»;
    - `MonoParts`: `NormalizePhone(phone)` до збереження; `null` → 400 «Для покупки частинами потрібен український номер +380…»;
    - статуси при створенні: `Card` і `MonoParts` → `AwaitingPayment`, `Credit` → `Credit`, `Invoice` → `New`;
    - після `SaveChanges`:
      - `Card` → `CreateInvoiceAsync` → `PaymentRef`, `PayUrl`; `MonobankException` → лог;
      - `MonoParts` → `CreateAsync(o, phone, MaxParts(setting pay_parts_max))` → `PaymentRef`, `PaymentStatus = "IN_PROCESS/WAITING_FOR_CLIENT"`; `MonoPartsException` → `Status = New`, `PaymentStatus = "FAIL/CREATE_ERROR"`, у листі менеджерам рядок «monobank: {message}»;
    - зберегти, лист «Нове замовлення …» як зараз, повернути `ToDto(o)`.
  - **`POST /api/orders/{id}/pay`** (`RequireRateLimiting(Chat)`):
    - лише `Card` + `AwaitingPayment`, інакше 400 «Замовлення вже не очікує оплати карткою»;
    - `PayUrl != null && Reusable(PaymentStatus)` → повернути наявний;
    - інакше `CreateInvoiceAsync` → перезаписати `PaymentRef`/`PayUrl`, `PaymentStatus = null`, зберегти;
    - `MonobankException` → 502 «Не вдалося зв'язатися з monobank, спробуйте пізніше».
  - **`GET /api/orders/{id}`:** завантажити з трекінгом → `sync.RefreshIfStaleAsync` → `ToDto`.
  - **`AuditEndpoints`:**
    - `/audit/slots` повертає `Unavailable = true`, якщо `Card` вимкнена (`pay_Card` = `off`) або в `Unconfigured`; Господаря тоді не питати;
    - `/audit/bookings` у тому ж випадку → 400 «Payment method is turned off»;
    - `PaymentMethod = Card`, далі рахунок як у кошику, `ToDto(order)`.

- [ ] **Step 4: Run all backend tests, expect PASS**

- [ ] **Step 5: Commit** — `feat(payments): monobank checkout, pay retry and webhooks`

### Task 6: Адмінка API — підтвердження видачі, відкликання, блок суми

**Files:**
- Create: `Features/Admin/AdminPaymentRules.cs`
- Modify: `Features/Admin/AdminOrdersEndpoints.cs`
- Test: `backend/tests/UaGreen.Site.Tests/AdminPaymentRulesTests.cs`

**Interfaces:**
- Consumes: `PaymentSync.RefreshAsync`, `RefreshIfStaleAsync`, `MonoPartsClient.ConfirmAsync`, `RejectAsync`.
- Produces:
  - `enum BankStep { None, Confirm, Reject, Block }`
  - `static (BankStep Step, string? Message) AdminPaymentRules.ForStatus(Order o, OrderStatus to)`
  - `static bool AdminPaymentRules.TotalLocked(Order o)` — (`Card` або `MonoParts`) + `AwaitingPayment` + `PaymentRef != null`

- [ ] **Step 1: Write the failing tests**

```csharp
// Order: MonoParts, PaymentRef = "x".
(PaymentStatus "IN_PROCESS/WAITING_FOR_STORE_CONFIRM", to Shipped)   → Confirm
(PaymentStatus "IN_PROCESS/WAITING_FOR_CLIENT",        to Shipped)   → Block, "Клієнт ще не підтвердив покупку частинами"
(PaymentStatus "SUCCESS/ACTIVE",                       to Shipped)   → None
(PaymentStatus "FAIL/EXCEEDED_SUM_LIMIT",              to Shipped)   → None
(PaymentStatus "IN_PROCESS/WAITING_FOR_CLIENT",        to Cancelled) → Reject
(PaymentStatus "IN_PROCESS/WAITING_FOR_STORE_CONFIRM", to Cancelled) → Reject
(PaymentStatus "SUCCESS/ACTIVE",                       to Cancelled) → None
(PaymentStatus "IN_PROCESS/WAITING_FOR_STORE_CONFIRM", to Paid)      → None
// Card order, any status → None.
// TotalLocked: Card+AwaitingPayment+ref → true; Card+AwaitingPayment+no ref → false; MonoParts+Paid+ref → false; Invoice → false.

[Fact] public async Task Confirm_when_bank_down_throws_MonoPartsException()
// MonoPartsClient over HttpClient(stub handler that throws HttpRequestException) → Assert.ThrowsAsync<MonoPartsException>(ConfirmAsync("x")).
```

- [ ] **Step 2: Run, expect compile failure**

- [ ] **Step 3: Implement**

  - **`PATCH /{id}/status`:** для `MonoParts` з `PaymentRef` і `to` ∈ {`Shipped`, `Cancelled`}:
    1. `await sync.RefreshAsync(o)`;
    2. `ForStatus`:
       - `Block` → 400 з повідомленням;
       - `Confirm`/`Reject` → виклик банку, `o.PaymentStatus = state.Text`, `Log(… "monobank: видачу підтверджено")` або «monobank: заявку відкликано»;
    3. будь-який `MonoPartsException`/`MonobankException` → 400 «Монобанк не прийняв зміну: {message}». Статус не змінюється, нічого не зберігається.
  - **`PUT /{id}`:** якщо `TotalLocked(o)` і новий `Total` ≠ старий → 400 «Суму вже передано в банк. Скасуйте замовлення й оформіть нове».
  - **`GET /{id}`:** трекінг → `RefreshIfStaleAsync` → DTO.

- [ ] **Step 4: Run all backend tests, expect PASS**

- [ ] **Step 5: Commit** — `feat(admin): confirm or reject monobank parts on status change`

### Task 7: Сайт (Nuxt)

**Files:**
- Modify:
  - `frontend/types/api.ts`
  - `frontend/pages/checkout.vue`
  - `frontend/pages/audit/book.vue`
  - `frontend/pages/order/[id].vue`
  - `frontend/i18n/locales/{uk,en,cs}.json`
- Delete: `frontend/components/shop/LiqPayForm.vue`

**Interfaces:**
- Consumes: `OrderDto.payUrl`, `paymentStatus`; `POST /api/orders/{id}/pay` → `{ payUrl }`; `pay_parts_max` у публічних налаштуваннях.
- Produces: `PaymentMethod = 'Card' | 'Invoice' | 'Credit' | 'MonoParts'`; `Order.payUrl: string | null`; `Order.paymentStatus: string | null` (поле `liqPay` видалити).

- [ ] **Step 1: Implement**

  - **`checkout.vue`:**
    - методи `['Card', 'Invoice', 'Credit', 'MonoParts']`;
    - після створення: `order.payUrl` → `window.location.href = order.payUrl` (показати `checkout.redirecting`), інакше `navigateTo` на сторінку замовлення;
    - підказка для `MonoParts` бере `{n}` з `settings.pay_parts_max || 6`.
  - **`audit/book.vue`:** те саме замість `LiqPayForm`.
  - **`order/[id].vue`:**
    - опитування до 16 хв (`left = 192`);
    - `note` за таблицею спеки:
      - `Card` + `AwaitingPayment` → `order.waiting` + кнопка `order.pay`: `POST /orders/{id}/pay`, потім `location.href`; помилку показати під кнопкою;
      - `MonoParts` + `AwaitingPayment` → `order.partsWaiting`;
      - `MonoParts` + `Paid` → `order.partsApproved`;
      - `MonoParts` + `New` + `paymentStatus` починається з `FAIL/` → `order.partsFail.{SubState}` (або `order.partsFail.other`) + `order.partsFallback`;
      - `FAIL/CREATE_ERROR` → лише `order.partsFallback`.
  - **i18n uk** (en/cs — переклади тих самих рядків):
    - `checkout.payment_Card` «Онлайн карткою»;
    - `checkout.payment_Card_hint` «Visa / Mastercard, Apple Pay, Google Pay через monobank.»;
    - `checkout.payment_MonoParts` «Покупка частинами monobank»;
    - `checkout.payment_MonoParts_hint` «До {n} платежів без переплати. Підтвердження в застосунку monobank.»;
    - `order.waiting` «Чекаємо підтвердження оплати від monobank. Ця сторінка оновиться сама.»;
    - `order.pay` «Оплатити»;
    - `order.payError` «Не вдалося відкрити оплату. Спробуйте за хвилину.»;
    - `order.partsWaiting` «Підтвердіть покупку частинами в застосунку monobank протягом 15 хвилин. Ця сторінка оновиться сама.»;
    - `order.partsApproved` «Покупку частинами оформлено. Ми готуємо замовлення до відправлення.»;
    - `order.partsFallback` «Менеджер зв'яжеться з вами, щоб запропонувати інший спосіб оплати.»;
    - `order.partsFail`:
      - `CLIENT_NOT_FOUND` «Цей номер не прив'язаний до monobank.»;
      - `EXCEEDED_SUM_LIMIT` «Сума більша за ваш ліміт покупки частинами.»;
      - `EXISTS_OTHER_OPEN_ORDER` «У вас уже є незавершена заявка на покупку частинами.»;
      - `NOT_ENOUGH_MONEY_FOR_INIT_DEBIT` «Недостатньо коштів для першого платежу.»;
      - `REJECTED_BY_CLIENT` «Заявку відхилено в застосунку.»;
      - `CLIENT_PUSH_TIMEOUT` «Заявку не підтвердили за 15 хвилин.»;
      - `other` «Банк не погодив заявку.».

    Видалити ключі `payment_LiqPay*`.

- [ ] **Step 2: Verify** — `cd frontend && npm run typecheck && npm test`. Очікувано: без помилок; `grep -rni liqpay frontend --include=*.vue --include=*.ts --include=*.json -l | grep -v node_modules | grep -v .nuxt` порожній.

- [ ] **Step 3: Commit** — `feat(site): monobank card and pay-in-parts checkout`

### Task 8: Адмінка (Flutter)

**Files:**
- Modify:
  - `admin/lib/pages_site.dart` (`PaymentsPage`)
  - `admin/lib/pages_orders.dart`
  - `backend/src/UaGreen.Site.Api/Features/Admin/AdminContentEndpoints.cs` (новий GET)

**Interfaces:**
- Produces: `GET /api/admin/payments/configured` (Admin) → `{ "Card": bool, "MonoParts": bool }`.

- [ ] **Step 1: Implement**

  - **`PaymentsPage._methods`:**

    | Ключ | Іконка | Назва | Опис |
    |---|---|---|---|
    | `Card` | `Icons.credit_card` | «Онлайн карткою — monobank» | зі спеки |
    | `Invoice` | — | — | без змін |
    | `Credit` | — | «Кредит 0 % (державна програма)» | «Менеджер зв'язується з клієнтом і оформлює кредит у банку-партнері.» |
    | `MonoParts` | `Icons.event_repeat` | «Покупка частинами monobank» | зі спеки |

  - **`MonoParts`:** під описом `kvField(p, 'pay_parts_max', 'Максимум платежів', helper: 'від 3 до 25; порожньо — 6', validate: 3..25)`.
  - **`load`:** тягне `/admin/payments` і `/admin/payments/configured`, кладе в результат ключі `_cfg_Card` / `_cfg_MonoParts` (`'0'`/`'1'`). `save` прибирає `_cfg_*` з `changes` перед `put`. Сервер їх не знає, а `Validate` відкидає невідомі ключі. Якщо `_cfg_{key} == '0'`, у картці рядок «Не налаштовано на сервері — сайт цей спосіб не показує».
  - **`pages_orders.dart`:**
    - `payments` = `{'Card': 'Картка (monobank)', 'Invoice': 'Вручну (переказ на IBAN)', 'Credit': 'Кредит 0%', 'MonoParts': 'Частинами (monobank)'}`;
    - `liqPayStatus` → `paymentStatus`, підпис «Банк»;
    - «повернення робиться в LiqPay/банку окремо» → «повернення робиться в кабінеті monobank або банку окремо»;
    - «LiqPay не підтверджував оплату» → «Банк не підтверджував оплату».

- [ ] **Step 2: Verify** — `cd admin && flutter analyze` без нових зауважень.

- [ ] **Step 3: Commit** — `feat(admin): monobank payment methods and bank status`

### Task 9: Тексти, документи, локальна перевірка

**Files:**
- Modify:
  - `migration/content.json` (`page.oplata` uk/en: пункт про LiqPay)
  - `docs/PLAN.md:131`

- [ ] **Step 1: Тексти**

  - **`content.json` uk:**
    - пункт «Онлайн оплата банківською картою (LiqPay)…» → «Онлайн оплата карткою через monobank: Visa / Mastercard, Apple Pay, Google Pay — оплачуєте одразу на сайті.»;
    - новий пункт «Покупка частинами monobank: до 25 платежів без переплати для клієнтів monobank, підтвердження в застосунку.»;
    - пункт «Кредит 0 %» без змін.

    en — те саме англійською.
  - **`PLAN.md:131`:** «Оплата частинами monobank — підключено (2026-11-24), ПриватБанк — за потреби.»

- [ ] **Step 2: Локальний прогін** (рецепт з пам'яті `uagreen-site-local-run`)

Запустити API (Development: пісочниця частинами) і Nuxt. Перевірити:
1. **Кошик без `Monobank__Token`:** «Онлайн карткою» не показується, `/audit/book` показує «немає вільних дат».
2. **«Покупка частинами» з телефоном `+380501234564`:** замовлення → сторінка «Підтвердіть…» → протягом 10 с «Покупку частинами оформлено» (оновлення при читанні; callback до localhost не дійде).
3. **Адмінка → «Відправлено»:** у банку `SUCCESS/ACTIVE`, подія в історії. Окреме замовлення → «Скасовано» → `FAIL/REJECTED_BY_STORE`.
4. **Телефон `+380501234563`:** «Сума більша за ваш ліміт…», статус «Нове», лист менеджерам у лозі.
5. **Телефон `12345` з «Покупкою частинами»:** помилка під формою, кошик цілий.
6. **Адмінка → Платіжні системи:** вимкнути «Кредит 0 %» → реквізити IBAN лишилися.
7. **Якщо власник дав тестовий токен еквайрингу:** картка → сторінка monobank → тестова картка → `/order/{id}` → «Оплачено».

- [ ] **Step 3: Commit** — `docs: monobank payments copy and plan`
