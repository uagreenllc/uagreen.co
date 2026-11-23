# Чат із сайту — частина сайту uagreen.co: план реалізації

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Віджет чату на кожній сторінці сайту і картка зі вступним питанням через 30 с; усе листування йде в Господар через бекенд сайту.

**Architecture:** API сайту — тонкий проксі `/api/chat/*` → `Господар /api/site-chat/*` з ключем `SiteChat:ApiKey`; нічого про чат у базі сайту не зберігає. Без ключа чат вимкнений. Nuxt: клієнтський компонент `SiteChat` у `layouts/default.vue`, логіка картки — чисті функції в `utils/`, стан і опитування — `composables/useChat.ts`.

**Tech Stack:** .NET 9 minimal API, `Microsoft.Extensions.Http.Resilience`, xUnit; Nuxt 3, `@nuxtjs/i18n`, Tailwind, vitest.

**Spec:** `docs/superpowers/specs/2026-11-23-site-chat-design.md`

**Парний план:** `docs/superpowers/plans/2026-11-23-site-chat-gospodar.md`. Цей план починати після Task 3 того плану (є API відвідувача); наскрізну перевірку (Task 5) — після Task 7 того плану. Розмови з сайту живуть у наявній скриньці «Чати» Господаря як канал «Сайт» (див. поправку в спеку).

## Global Constraints

- Контракт Господаря — таблиця «Контракт API для сайту» в парному плані. Ключ — заголовок `X-Api-Key`, токен відвідувача — `X-Chat-Token` (той самий заголовок від браузера до сайту і від сайту до Господаря).
- Без `SiteChat:ApiKey` (порожньо): `GET /api/chat/questions` → `{ enabled: false, questions: [] }`, решта `/api/chat/*` → `404`; віджет і картка не рендеряться.
- Господар недоступний (мережа / таймаут / 5xx): `503 { code: "CHAT_UNAVAILABLE", message }`; віджет пропонує наявну форму контактів (`ContactForm`, `/api/contact`).
- Картка: через `30 000` мс; не показувати, якщо в браузері вже є розмова, на `/cart`, `/checkout`, `/order`, `/order/**` (і з префіксом `/en`), і `7` днів після закриття.
- Питання — випадкове з активних, «липке» за `localStorage`; вимкнене → інше; на `/en/` — англійський текст (сервер віддає лише перекладені).
- Опитування: `3 000` мс при відкритому вікні, `30 000` мс при закритому, якщо розмова є; пауза, коли вкладка прихована.
- Ключі `localStorage`: `uag_chat` (`{ conversationId, token }`), `uag_chat_q` (id питання), `uag_chat_dismissed` (мс). Кожне читання/запис — у `try/catch`.
- Повідомлення — лише текст (`{{ }}`, ніколи `v-html`), `white-space: pre-wrap; overflow-wrap: anywhere`.
- Компонент лише клієнтський (`<ClientOnly>` + `Lazy…`), SSR-сторінки не важчають.
- Ліміти частоти: старт розмови — `5` на годину з IP (`RateLimiting:ChatStart`); решта `/api/chat/*` — `60` на хвилину з IP (`RateLimiting:Chat`).
- Пастка для ботів: поле `website` у старті розмови (як у формах).

## Review Focus

- `localStorage` кидає виняток (приватний режим, заблоковане сховище) — чат працює в межах сторінки, нічого не падає. Тест — Task 3.
- Збережена розмова більше не відкривається (`404`: інша БД, зіпсований токен) — сховище чиститься, наступне повідомлення стартує нову розмову, опитування не крутить `404` вічно. Перевірка — Task 4, крок ручної перевірки.
- Подвійне натискання «Надіслати» — одне повідомлення (`send` нічого не робить, поки `sending`). Перевірка — Task 4.
- Господар падає посеред розмови — у вікні з'являється примітка, опитування триває й саме відновлюється. Перевірка — Task 5.
- Довгий текст без пробілів чи URL — переноситься всередині вікна, на ширині 360 px немає горизонтальної прокрутки. Перевірка — Task 4.

---

### Task 1: Клієнт Господаря для чату

**Files:**
- Create: `backend/src/UaGreen.Site.Api/Infrastructure/Chat/SiteChatOptions.cs`, `backend/src/UaGreen.Site.Api/Infrastructure/Chat/GospodarChatClient.cs`
- Modify: `backend/src/UaGreen.Site.Api/Program.cs` (поруч з `AddHttpClient<GospodarClient>` :36-41)
- Test: `backend/tests/UaGreen.Site.Tests/GospodarChatClientTests.cs`

**Interfaces:**
- Produces:
  - `public sealed class SiteChatOptions { public string ApiKey { get; set; } = ""; public bool Enabled => !string.IsNullOrWhiteSpace(ApiKey); }` — секція `SiteChat`.
  - `public sealed record ChatProxyResult(int Status, string? Json);`
  - `public sealed class GospodarChatClient(HttpClient http, IOptions<SiteChatOptions> opts) { Task<ChatProxyResult> SendAsync(HttpMethod method, string path, object? body, string? chatToken, CancellationToken ct); }` — `path` відносно `/api/site-chat` (напр. `"/conversations"`); додає `X-Api-Key`, і `X-Chat-Token`, якщо токен непорожній; повертає статус і тіло як є; `HttpRequestException` / таймаут (не скасування `ct`) → `new ChatProxyResult(503, null)`.
  - Реєстрація: `AddHttpClient<GospodarChatClient>` з `BaseAddress = Gospodar:BaseUrl`, `Timeout = 10 s`, `.AddStandardResilienceHandler(o => o.Retry.DisableForUnsafeHttpMethods())` — POST повідомлення не повторюється (інакше дублікати).

- [ ] **Step 1: Failing tests** (рукописний `StubHandler : HttpMessageHandler`, що запам'ятовує запит і віддає задану відповідь):

```csharp
[Fact] public async Task Sends_key_token_and_path()
    // SendAsync(Post, "/conversations/abc/messages", new { text = "Привіт" }, "tok", ct)
    // запит: POST https://erp.test/api/site-chat/conversations/abc/messages, X-Api-Key == "k", X-Chat-Token == "tok", тіло {"text":"Привіт"}
[Fact] public async Task No_token_no_header()                 // chatToken null → нема X-Chat-Token
[Fact] public async Task Passes_status_and_body_through()     // stub 201 + "{\"id\":\"m1\"}" → Status 201, Json той самий
[Fact] public async Task Network_failure_is_503()             // stub кидає HttpRequestException → Status 503, Json null
[Fact] public async Task Caller_cancellation_is_not_swallowed()  // скасований ct → OperationCanceledException
```

- [ ] **Step 2:** `dotnet test backend/tests/UaGreen.Site.Tests --filter GospodarChatClientTests` → FAIL.
- [ ] **Step 3:** Реалізувати; зареєструвати `Configure<SiteChatOptions>(GetSection("SiteChat"))` і клієнт.
- [ ] **Step 4:** → PASS.
- [ ] **Step 5:** Commit `feat(chat): Gospodar chat client`.

---

### Task 2: Публічні ендпоінти `/api/chat`

**Files:**
- Create: `backend/src/UaGreen.Site.Api/Features/Chat/ChatEndpoints.cs`
- Modify: `backend/src/UaGreen.Site.Api/Program.cs` (виклик `ChatEndpoints.Map(app)` поруч із :141-147; `AddMemoryCache()`, якщо ще нема), `backend/src/UaGreen.Site.Api/Infrastructure/RateLimiting/RateLimitingExtensions.cs`, `backend/src/UaGreen.Site.Api/appsettings.json`, `infra/docker/.env.prod.example`, `infra/docker/docker-compose.prod.yml`
- Test: `backend/tests/UaGreen.Site.Tests/ChatEndpointsTests.cs`

**Interfaces:**
- Consumes: `GospodarChatClient`, `SiteChatOptions` (Task 1).
- Produces:
  - `RateLimitingExtensions.ChatStart = "chat-start"` (fixed window, `RateLimiting:ChatStart:PermitLimit` = 5, `:Window` = `01:00:00`), `RateLimitingExtensions.Chat = "chat"` (token bucket за IP, `RateLimiting:Chat:PermitLimit` = 60, `:Window` = `00:01:00`).
  - `public sealed record StartChatRequest(string? Text, string? Lang, string? PageUrl, Guid? QuestionId, string? Website);`
  - `public static class ChatEndpoints { public static void Map(IEndpointRouteBuilder app); internal static IResult ToResult(ChatProxyResult r); }` — `ToResult`: `503` → `{ code: "CHAT_UNAVAILABLE", message: "Chat is unavailable" }` зі статусом 503; інакше статус Господаря і його JSON без змін (`Results.Content(json, "application/json", statusCode)`; без тіла — `Results.StatusCode`).
  - Маршрути (усі `AllowAnonymous`, `DisableAntiforgery`):

    | Сайт | Господар | Ліміт |
    |---|---|---|
    | `GET /api/chat/questions?lang=` | `GET /questions?lang=` | `chat` |
    | `POST /api/chat/questions/{id:guid}/impressions` | `POST /questions/{id}/impressions` | `chat` |
    | `POST /api/chat/conversations` | `POST /conversations` (без `website`) | `chat-start` |
    | `GET /api/chat/conversations/{id:guid}/messages` | `GET /conversations/{id}/messages` | `chat` |
    | `POST /api/chat/conversations/{id:guid}/messages` | `POST /conversations/{id}/messages` | `chat` |
    | `PUT /api/chat/conversations/{id:guid}/contact` | `PUT /conversations/{id}/contact` | `chat` |

  - `GET /api/chat/questions` відповідає `{ enabled, questions: [{ id, text }] }`; успішну відповідь Господаря кешує в `IMemoryCache` під `chat-questions:{lang}` на 5 хв; невдачу не кешує й віддає `{ enabled: true, questions: [] }`. `lang` — лише `uk`/`en`, інакше `uk`.
  - Вимкнено (`!Enabled`) — усе, крім `questions`, → `404`.
  - Заповнений `website` у старті → `204` без звернення до Господаря.
  - Текст, мову, телефон перевіряє Господар; сайт не дублює правил.
  - Конфіг: `"SiteChat": { "ApiKey": "" }`, `"RateLimiting": { …, "ChatStart": { "Window": "01:00:00", "PermitLimit": 5 }, "Chat": { "Window": "00:01:00", "PermitLimit": 60 } }`; `.env.prod.example`: `SITE_CHAT_API_KEY=` з коментарем «той самий, що SITECHAT__APIKEY у Господарі; порожньо — чат вимкнено (так і лишаємо на staging)»; compose: `SiteChat__ApiKey: ${SITE_CHAT_API_KEY:-}`.

- [ ] **Step 1: Failing tests** — `ChatEndpointsTests` (лише `ToResult`, бо в проєкті тестів немає `WebApplicationFactory`; виконати `IResult` на `DefaultHttpContext` з `RequestServices` з `AddLogging()` і прочитати тіло):

```csharp
[Fact] public async Task Unavailable_becomes_chat_unavailable()   // new ChatProxyResult(503, null) → 503, тіло {"code":"CHAT_UNAVAILABLE",…}
[Fact] public async Task Gospodar_json_passes_through()           // (201, "{\"id\":\"m1\"}") → 201, application/json, тіло те саме
[Fact] public async Task Empty_body_keeps_the_status()            // (204, null) → 204
[Fact] public async Task Gospodar_404_stays_404()                 // (404, null) → 404
```

- [ ] **Step 2:** `dotnet test backend/tests/UaGreen.Site.Tests --filter ChatEndpointsTests` → FAIL.
- [ ] **Step 3:** Реалізувати ендпоінти, ліміти, конфіг і env.
- [ ] **Step 4:** → PASS; `dotnet build backend` без попереджень.
- [ ] **Step 5:** Ручна перевірка з порожнім ключем: `curl http://localhost:5000/api/chat/questions` → `{"enabled":false,"questions":[]}`; `curl -X POST http://localhost:5000/api/chat/conversations -H "Content-Type: application/json" -d '{"text":"x"}'` → 404.
- [ ] **Step 6:** Commit `feat(chat): public chat proxy endpoints`.

---

### Task 3: Логіка картки й сховища

**Files:**
- Create: `frontend/utils/chatTeaser.ts`, `frontend/utils/chatStorage.ts`, `frontend/types/chat.ts`
- Test: `frontend/utils/chatTeaser.test.ts`, `frontend/utils/chatStorage.test.ts`

**Interfaces:**
- Produces:
  - `types/chat.ts`: `export interface ChatMsg { id: string; text: string; at: string; fromVisitor: boolean; authorName: string | null }`, `export interface ChatQuestion { id: string; text: string }`.
  - `utils/chatTeaser.ts`:
    - `export const TEASER_DELAY_MS = 30_000`, `export const DISMISS_MS = 7 * 24 * 60 * 60 * 1000`
    - `export function isTeaserBlockedPath(path: string): boolean`
    - `export function teaserAllowed(o: { path: string; hasConversation: boolean; dismissedAt: number | null; now: number; hasQuestion: boolean }): boolean`
    - `export function pickQuestion<T extends { id: string }>(questions: T[], storedId: string | null, random: () => number = Math.random): T | null`
  - `utils/chatStorage.ts`:
    - `export interface StoredChat { conversationId: string; token: string }`
    - `export function parseStoredChat(raw: string | null): StoredChat | null`
    - `export const chatStorage = { load(): StoredChat | null; save(c: StoredChat): void; clear(): void; questionId(): string | null; setQuestionId(id: string): void; dismissedAt(): number | null; setDismissedAt(ms: number): void }` — ключі з Global Constraints; кожен доступ у `try/catch`, при помилці — `null` / нічого.

- [ ] **Step 1: Failing tests:**

```ts
// chatTeaser.test.ts
describe('isTeaserBlockedPath', () => {
  it('blocks cart, checkout and order pages in both languages', () => {
    for (const p of ['/cart', '/checkout', '/order', '/order/123', '/en/cart', '/en/checkout/', '/en/order/abc'])
      expect(isTeaserBlockedPath(p)).toBe(true)
  })
  it('allows everything else', () => {
    for (const p of ['/', '/en', '/shop/k5v1', '/en/shop', '/orders-info', '/blog/cart-tips'])
      expect(isTeaserBlockedPath(p)).toBe(false)
  })
})
describe('teaserAllowed', () => {
  const base = { path: '/', hasConversation: false, dismissedAt: null, now: 1_000_000_000_000, hasQuestion: true }
  it('shows on a normal page', () => expect(teaserAllowed(base)).toBe(true))
  it('hides when a conversation exists', () => expect(teaserAllowed({ ...base, hasConversation: true })).toBe(false))
  it('hides without a question', () => expect(teaserAllowed({ ...base, hasQuestion: false })).toBe(false))
  it('hides for 7 days after dismissal, then shows again', () => {
    expect(teaserAllowed({ ...base, dismissedAt: base.now - DISMISS_MS + 1 })).toBe(false)
    expect(teaserAllowed({ ...base, dismissedAt: base.now - DISMISS_MS })).toBe(true)
  })
  it('hides on blocked paths', () => expect(teaserAllowed({ ...base, path: '/checkout' })).toBe(false))
})
describe('pickQuestion', () => {
  const qs = [{ id: 'a' }, { id: 'b' }, { id: 'c' }]
  it('keeps the stored question', () => expect(pickQuestion(qs, 'b', () => 0)).toEqual({ id: 'b' }))
  it('picks randomly when nothing is stored', () => expect(pickQuestion(qs, null, () => 0.7)).toEqual({ id: 'c' }))
  it('picks again when the stored one was disabled', () => expect(pickQuestion(qs, 'zzz', () => 0)).toEqual({ id: 'a' }))
  it('returns null for no questions', () => expect(pickQuestion([], 'a')).toBeNull())
})

// chatStorage.test.ts
describe('parseStoredChat', () => {
  it('reads a valid record', () => expect(parseStoredChat('{"conversationId":"c1","token":"t"}')).toEqual({ conversationId: 'c1', token: 't' }))
  it('rejects garbage', () => {
    for (const raw of [null, '', 'nope', '[]', '{"conversationId":1,"token":"t"}', '{"conversationId":"c1"}'])
      expect(parseStoredChat(raw)).toBeNull()
  })
})
describe('chatStorage when localStorage throws', () => {
  // vi.stubGlobal('localStorage', { getItem() { throw new Error('denied') }, setItem() { throw new Error('denied') }, removeItem() { throw new Error('denied') } })
  it('reads nothing and writes without throwing', () => {
    expect(chatStorage.load()).toBeNull()
    expect(() => chatStorage.save({ conversationId: 'c', token: 't' })).not.toThrow()
    expect(chatStorage.dismissedAt()).toBeNull()
  })
})
```

- [ ] **Step 2:** `cd frontend; npx vitest run utils/chatTeaser.test.ts utils/chatStorage.test.ts` → FAIL.
- [ ] **Step 3:** Реалізувати. `pickQuestion` при відсутньому збереженому — `questions[Math.floor(random() * questions.length)]`.
- [ ] **Step 4:** → PASS; `npm test` → PASS.
- [ ] **Step 5:** Commit `feat(chat): teaser rules and chat storage`.

---

### Task 4: Віджет

**Files:**
- Create: `frontend/composables/useChat.ts`, `frontend/components/site/SiteChat.vue`
- Modify: `frontend/layouts/default.vue`, `frontend/i18n/locales/uk.json`, `frontend/i18n/locales/en.json`

**Interfaces:**
- Consumes: `apiUrl` (`composables/useApi.ts`), `chatStorage`, `teaserAllowed`, `pickQuestion`, `TEASER_DELAY_MS` (Task 3); ендпоінти Task 2; `ContactForm` (`components/site/ContactForm.vue`).
- Produces:
  - `export function useChat()` → `{ enabled, questions, messages, isOpen, unread, sending, unavailable, contactSaved, hasConversation, init(), send(text: string, questionId?: string): Promise<boolean>, saveContact(name: string, phone: string): Promise<boolean>, recordImpression(id: string): void, setOpen(v: boolean): void, stop(): void }`
    - `init()`: `GET /chat/questions?lang=<locale>`; `enabled=false` → більше нічого; є збережена розмова → завантажити повідомлення й почати опитування.
    - `send`: поки `sending` — одразу `false`; без розмови → `POST /chat/conversations { text, lang, pageUrl: route.fullPath, questionId, website: '' }`, зберегти `{ conversationId, token }`; з розмовою → `POST …/messages`. `503` → `unavailable = true`.
    - Опитування: `setTimeout`-цикл, `3000` / `30000` мс (див. Global Constraints), `document.hidden` — пропустити такт, `visibilitychange` — одразу опитати. Нові повідомлення не від відвідувача при закритому вікні → `unread += n`; `setOpen(true)` обнуляє. `404` → `chatStorage.clear()`, скинути `messages`, зупинити опитування. Успіх після `503` → `unavailable = false`.
  - `SiteChat.vue`: кругла кнопка в правому нижньому куті (`aria-label` = `chat.open`, бейдж `unread`); вікно (заголовок «UA Green», привітання `chat.greeting`, повідомлення: відвідувач праворуч, решта ліворуч із `authorName ?? 'UA Green'`, поле + «Надіслати», Enter — надіслати, Shift+Enter — новий рядок); після першого повідомлення відвідувача, доки `!contactSaved`, — блок «Залиште телефон» (ім'я необов'язкове, телефон) → `saveContact`; `unavailable` без розмови → замість поля `ContactForm`, з розмовою — примітка `chat.unavailable`. На ширині < 640 px вікно на всю ширину знизу. Картка: таймер `TEASER_DELAY_MS` після монтування; якщо `teaserAllowed(...)` (шлях із `useRoute()`, перевіряти в момент показу) — показати картку з `pickQuestion(questions, chatStorage.questionId())`, зберегти id, один раз `recordImpression`; × → `chatStorage.setDismissedAt(Date.now())`; відповідь із картки → `send(text, question.id)` і відкрити вікно.
  - `layouts/default.vue`: після `<SiteFooter />` — `<ClientOnly><LazySiteChat /></ClientOnly>`.
  - Рядки `chat.*` (uk / en): `open` Чат з менеджером / Chat with us; `greeting` Вітаємо! Чим допомогти? / Hi! How can we help?; `placeholder` Напишіть повідомлення… / Type a message…; `send` Надіслати / Send; `close` Закрити / Close; `contactPrompt` Залиште телефон — передзвонимо / Leave your phone number and we'll call you back; `contactName` Ім'я (необов'язково) / Name (optional); `contactPhone` Телефон / Phone; `contactSave` Зберегти / Save; `contactSaved` Дякуємо, передзвонимо! / Thanks, we'll call you back!; `unavailable` Чат тимчасово недоступний. Залиште контакти — ми зв'яжемося. / Chat is unavailable right now. Leave your details and we'll get back to you.; `failed` Не вдалося надіслати. Спробуйте ще раз. / Couldn't send. Please try again.

- [ ] **Step 1:** Реалізувати `useChat`, `SiteChat.vue`, підключення в layout і рядки.
- [ ] **Step 2:** `npm run typecheck` і `npm test` → PASS.
- [ ] **Step 3: Перевірка без Господаря** (dev-сервер сайту через preview, API локально з порожнім ключем): кнопки чату немає, у консолі нема помилок, SSR-HTML головної не містить розмітки чату.
- [ ] **Step 4: Перевірка з фейковим Господарем** — тимчасово виставити `SiteChat:ApiKey` і `Gospodar:BaseUrl` на локальний Господар (після Task 3 парного плану) або, якщо його нема, на заглушку, що віддає `503`:
  - кнопка з'являється; через 30 с — картка з питанням; на `/cart` — не з'являється; × і перезавантаження — не з'являється;
  - двічі швидко натиснути «Надіслати» — в розмові одне повідомлення;
  - текст із 300 символів без пробілів переноситься; на 360 px немає горизонтальної прокрутки;
  - зіпсувати `token` у `uag_chat` у DevTools → після `404` сховище чисте, наступне повідомлення стартує нову розмову;
  - `503` без розмови → показується форма контактів.
  - Скріншоти десктоп і 360 px.
- [ ] **Step 5:** Commit `feat(chat): site chat widget and starter question teaser`.

---

### Task 5: Наскрізна перевірка з Господарем

Після Task 7 парного плану. Обидва застосунки локально (рецепт локального запуску сайту — у пам'яті проєкту / README; Господар — його README).

- [ ] **Step 1:** Згенерувати ключ (`openssl rand -base64 32`), виставити `SITECHAT__APIKEY` у Господарі й `SiteChat__ApiKey` + `Gospodar__BaseUrl` у сайті.
- [ ] **Step 2:** Відвідувач відповідає на картку → у `/crm/chats` Господаря розмова «Гість #N» з піктограмою сайту, стартовим питанням і сторінкою; прийшло сповіщення.
- [ ] **Step 3:** Менеджер відповідає → у віджеті за ≤ 3 с відповідь з ім'ям менеджера; зі згорнутим вікном — бейдж непрочитаного.
- [ ] **Step 4:** Відвідувач лишає телефон → видно в скриньці (картка клієнта підтягнулась, якщо телефон відомий); «Створити ліда» → угода, вона видна поруч із чатом.
- [ ] **Step 5:** Зупинити Господар посеред розмови → у віджеті примітка про недоступність; запустити знову → чат відновлюється без перезавантаження сторінки.
- [ ] **Step 6:** У налаштуваннях чатів, вкладка «Сайт», у відповіденого питання «Показано» ≥ 1, «Відповіли» 1, «Ліди» 1.
- [ ] **Step 7:** Скріншоти віджета і скриньки. Staging (`new.uagreen.co`) лишається без ключа, доки власник не вирішить інакше.
