# Чат із сайту — частина Господаря: план реалізації (v2, канал у наявній скриньці)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Чат із сайту стає ще одним каналом (`ChatChannel.Website`) наявної скриньки «Чати» (`/crm/chats`); бекенд сайту говорить із Господарем за окремим ключем; вступні питання для картки на сайті й їхня статистика керуються в налаштуваннях чатів.

**Architecture:** Наявні `ChatConversation` / `ChatMessage` отримують канал `Website = 5` і два поля (`StartPageUrl`, `StarterQuestionId`); нова таблиця `SiteChatQuestion`. Новий `SiteChatController` (`api/site-chat`, ключ `SiteChat:ApiKey`, токен відвідувача `X-Chat-Token`) записує вхідні так само, як вебхук Messenger (непрочитані, дзвіночок, live-події). Відповідь менеджера йде наявним `SendChatMessageHandler` — для `Website` без шлюзу, відвідувач забирає її опитуванням. Flutter: значення `website` в enum каналу, вимкнені для нього дії, вкладка «Сайт» у діалозі налаштувань чатів.

**Tech Stack:** .NET 9, EF Core + Npgsql, MediatR, FluentValidation, xUnit + FluentAssertions + NSubstitute (`TestDb`) + Testcontainers; Flutter web, Provider, Dio, mockito.

**Spec:** `docs/superpowers/specs/2026-11-23-site-chat-design.md` — читати разом із розділом «Поправка 2026-11-23: канал у наявній скриньці», вона має перевагу над старішими розділами.

**Парний план:** `docs/superpowers/plans/2026-11-23-site-chat-site.md` (сайт). Контракт між ними — «Контракт API для сайту» нижче.

## Global Constraints

- Канал: `ChatChannel.Website = 5`; у Flutter `ChatChannel.website(5)`, підпис «Сайт». У C#-іменах не вживати голе `Site*` для каналу — «Site» вже означає об'єкт клієнта (`CustomerSite`); інтеграція й питання — `SiteChat*`.
- `ExternalChatId` розмови сайту = SHA-256-хеш токена відвідувача (`ContractSigning.HashToken`), токен — `ContractSigning.NewToken()` (43 символи); у БД токена немає.
- `DisplayName` нової розмови: `Гість #{N}`, де N = кількість розмов каналу `Website` у компанії (з видаленими) + 1; ім'я з віджета замінює його.
- Повідомлення відвідувача: 1–2000 символів після `Trim()`, лише текст; помилки `chat_text_required`, `chat_text_too_long`. Мова старту `uk`/`en` (`chat_lang_invalid`), сторінка до 500 (`chat_page_url_too_long`), ім'я до 128 (`chat_name_too_long`), телефон 7–40 (`chat_phone_invalid`).
- Вступне питання в розмові — вихідне повідомлення з `SentByUserId = null` (як автопривітання бота); для відвідувача автор «UA Green»; менеджер для відвідувача — лише `FirstName`.
- Вхідне повідомлення сайту: як `MessengerInboxHandler.RecordAsync` — `UnreadCount++`, `LastInboundAt`, `LastMessageAt`, `LastMessagePreview`, закрита розмова відкривається, `MessageChangedAsync` + `ConversationChangedAsync`; якщо до нього `UnreadCount == 0` — `ChatInbox.RingBellAsync` **і** (лише для `Website`) зовнішнє сповіщення тим самим отримувачам через `IExternalNotificationDispatcher` (тип `chat_message_received`, посилання — те саме, що будує Flutter для `chat_message_received`).
- Телефон із віджета — через `ChatInbox.ApplyPhoneAsync` (сам прив'язує картку клієнта, нову не створює).
- Для `Website` недоступні: файли, стікери, реакції, пересилання, редагування, «почати за телефоном» (`chat_channel_unsupported` / наявні `*_forbidden`); видалення повідомлення й чату — лише в Господарі (як Messenger).
- Ключ чату не дорівнює ключу Оберега, не відкриває `/api/integration/*` і не оновлює `companies.integration_last_seen_at`; компанія — за `SiteChat:CompanyCode` (за замовчуванням `UAGREEN`).
- Права: скринька — наявний `crm.chat`; питання й статистика — `crm.manage_settings`. Видаляти питання не можна — лише вимикати.
- «Ліди» питання = розмови з цим `StarterQuestionId`, чия картка клієнта має угоду, створену не раніше за початок розмови.

### Контракт API для сайту (`SiteChatController`, `[Route("api/site-chat")]`)

Усі запити — `X-Api-Key: <SiteChat:ApiKey>`. Запити до розмови — ще `X-Chat-Token`. JSON camelCase.

| Метод і шлях | Тіло | Відповідь |
|---|---|---|
| `GET /questions?lang=uk\|en` | — | `200 [{ id, text }]` — активні за `OrderNo`; для `en` лише з `TextEn` |
| `POST /questions/{id}/impressions` | — | `204`; `404` |
| `POST /conversations` | `{ text, lang, pageUrl?, questionId? }` | `201 { conversationId, token, messages: [Msg] }`; `400 { error }` |
| `GET /conversations/{id}/messages` | — | `200 [Msg]` (усі невидалені, за `SentAt`); `404` id/токен не збіглися |
| `POST /conversations/{id}/messages` | `{ text }` | `201 Msg`; `400`; `404` |
| `PUT /conversations/{id}/contact` | `{ name?, phone }` | `204`; `400`; `404` |

`Msg = { id, text, at, fromVisitor, authorName }` (`authorName`: null для відвідувача, `UA Green` для повідомлень без автора, ім'я менеджера).

## Review Focus

- Менеджер видалив повідомлення — наступне опитування відвідувача його не повертає. Тест — Task 4.
- Менеджер видалив увесь чат, а відвідувач пише знову з тим самим токеном — розмова відновлюється (як `MessengerInboxHandler` знімає `IsDeleted`), а не `404`. Тест — Task 3.
- Текст із `<b>`/`<script>` зберігається й віддається дослівно; Flutter показує його текстом. Тест — Task 3.
- Порожній або довший за 64 символи токен — `404` без пошуку. Тест — Task 3.
- Аватарка розмови сайту — `null`, не `400` (Flutter питає `/avatar` для кожного рядка списку). Тест — Task 4.

---

### Task 0: Робоче місце — ГОТОВО (780abccb2)

---

### Task 1: Канал `Website`, поля розмови, питання, засів

**Files:**
- Modify: `backend/src/UaGreen.Domain/Enums/ChatChannel.cs`, `backend/src/UaGreen.Domain/Entities/Crm/ChatConversation.cs`
- Create: `backend/src/UaGreen.Domain/Entities/Crm/SiteChatQuestion.cs`
- Modify: `backend/src/UaGreen.Infrastructure/Persistence/Configurations/ChatConfiguration.cs`, `IApplicationDbContext.cs`, `AppDbContext.cs`, `backend/tests/UaGreen.Application.UnitTests/TestDb.cs`
- Create: міграція `AddWebsiteChat`
- Modify: `backend/src/UaGreen.Infrastructure/Persistence/Seeders/DatabaseSeeder.cs` (`SeedCompanyCatalogueAsync`)
- Create: `backend/src/UaGreen.Application/Crm/Chat/SiteChat.cs`
- Test: `backend/tests/UaGreen.Api.IntegrationTests/Crm/SiteChatSeedTests.cs`

**Interfaces:**
- Produces:
  - `ChatChannel.Website = 5` (з doc-коментарем: віджет на сайті компанії, відвідувач опитує `api/site-chat`, шлюзу немає).
  - `ChatConversation.StartPageUrl` (`string?`, 500), `ChatConversation.StarterQuestionId` (`Guid?`, FK на `SiteChatQuestion`, `SetNull`).
  - `SiteChatQuestion : BaseEntity { string TextUk; string? TextEn; bool IsActive = true; int OrderNo; int ShownCount }`, таблиця `crm_site_chat_questions`, `TextUk`/`TextEn` до 300, `HasQueryFilter(x => !x.IsDeleted)`; DbSet `SiteChatQuestions` у трьох місцях.
  - `public static class SiteChat { public const string SourceName = "Чат на сайті"; public const string Author = "UA Green"; }`

Засів `SeedSiteChatAsync(Guid companyId)` — лише для `Code == "UAGREEN"`: питання — якщо в компанії їх 0; `DealSource` «Чат на сайті» — якщо нема (`OrderNo = Count + 1`). Питання (`OrderNo` 1..11, uk / en):
1. Бажаєте замовити аудит на своїй ділянці? / Would you like to book a wind assessment of your site?
2. Маєте питання по наших станціях? / Any questions about our wind turbines?
3. Чи потрібна вам консультація по наших послугах? / Do you need a consultation on our services?
4. Хочете дізнатися, чи достатньо вітру на вашій ділянці? / Want to find out whether there is enough wind on your site?
5. Порахувати, за скільки окупиться вітрова станція саме у вашому випадку? / Shall we calculate how quickly a wind turbine would pay off in your case?
6. Шукаєте спосіб не залежати від відключень світла? / Looking for a way to stop depending on power outages?
7. Підказати, яка станція вам підійде — на 1 кВт чи на 5 кВт? / Not sure which turbine suits you — 1 kW or 5 kW?
8. Станцію шукаєте для дому чи для бізнесу? / Is the turbine for your home or for your business?
9. Потрібні окремо лопаті, генератор чи щогла? Допоможемо підібрати сумісні. / Need blades, a generator or a mast separately? We'll help you pick compatible parts.
10. Розповісти, як проходить встановлення — від огляду ділянки до запуску? / Want to know how installation works — from the site survey to start-up?
11. Є питання щодо доставки чи оплати? / Any questions about delivery or payment?

- [ ] **Step 1: Failing tests** — `SiteChatSeedTests` (`IClassFixture<WebAppFactory>`, `db.CurrentCompanyId` = id компанії `UAGREEN`):

```csharp
[Fact] public async Task Ua_green_gets_the_eleven_questions_and_the_chat_source()
    // 11 питань за OrderNo; [0].TextUk == "Бажаєте замовити аудит на своїй ділянці?"; [10].TextUk == "Є питання щодо доставки чи оплати?"
    // усі IsActive, TextEn != null, ShownCount == 0; DealSources містить "Чат на сайті"
[Fact] public async Task Reseeding_adds_nothing()            // SeedCompanyCatalogueAsync(uagreen) ще раз → 11 і одне джерело
[Fact] public async Task Website_conversation_keeps_page_and_question()  // ChatConversation { Channel = Website, StartPageUrl, StarterQuestionId } зберігається й читається
```

- [ ] **Step 2:** `dotnet test backend/tests/UaGreen.Api.IntegrationTests --filter SiteChatSeedTests` → FAIL.
- [ ] **Step 3:** Реалізувати; міграція `dotnet ef migrations add AddWebsiteChat --project backend/src/UaGreen.Infrastructure --startup-project backend/src/UaGreen.Api` (правило `CLAUDE.md` про три `.Designer.cs`).
- [ ] **Step 4:** → PASS; `dotnet test backend/tests/UaGreen.Application.UnitTests --filter "Category!=Integration"` → PASS.
- [ ] **Step 5:** Commit `feat(chat): website channel, starter questions and seed`.

---

### Task 2: Ключ сайту, питання й покази

**Files:**
- Modify: `backend/src/UaGreen.Api/Auth/ApiKeyAuthenticationHandler.cs`, `backend/src/UaGreen.Api/Program.cs` (схема :294-298, політики :321-323), `backend/src/UaGreen.Shared/Constants/ServicePolicies.cs`, `backend/src/UaGreen.Api/appsettings.json`, `backend/tests/UaGreen.Api.IntegrationTests/WebAppFactory.cs`
- Create: `backend/src/UaGreen.Application/Crm/Chat/SiteChatVisitor.cs` (запити й команди відвідувача — тут і в Task 3), `backend/src/UaGreen.Api/Controllers/SiteChatController.cs`
- Test: `backend/tests/UaGreen.Api.IntegrationTests/Crm/SiteChatAuthTests.cs`, `SiteChatQuestionsTests.cs`

**Interfaces:**
- Produces:
  - `ApiKeyOptions.SiteChatKey`, `ApiKeyOptions.SiteChatCompanyCode` (з `SiteChat:ApiKey`, `SiteChat:CompanyCode` ?? `"UAGREEN"`); конфіг `"SiteChat": { "ApiKey": "", "CompanyCode": "UAGREEN" }`.
  - Принципал ключа чату: `Name = "site-chat"`, `"company" = <id компанії за кодом>`, `"site_chat" = "true"`, **без** `integration_name`.
  - `ServicePolicies.SiteChat = "integration.site_chat"` (схема `ApiKey`, `RequireClaim("site_chat")`); `ServicePolicies.IntegrationCompany` додатково `RequireClaim("integration_name")`.
  - `WebAppFactory.SiteChatKey = "test-site-chat-key"`.
  - `record SiteChatQuestionDto(Guid Id, string Text)`; `record SiteChatQuestionsQuery(string? Lang) : IRequest<List<SiteChatQuestionDto>>`; `record RecordSiteChatImpressionCommand(Guid QuestionId) : IRequest<Result>` (`not_found`).
  - `SiteChatController` `[Route("api/site-chat")]`, `[Authorize(AuthenticationSchemes = ApiKeyAuthenticationHandler.SchemeName, Policy = ServicePolicies.SiteChat)]`: `GET questions`, `POST questions/{id:guid}/impressions`.

Гілка в `HandleAuthenticateAsync` — до перевірки `ConfiguredKey`: непорожній `SiteChatKey` + `FixedTimeEquals` → компанія `IgnoreQueryFilters().Where(c => c.IsActive && c.Code == SiteChatCompanyCode)`; нема — `Fail`. `IntegrationLastSeenAt` не чіпати. Покази — `ExecuteUpdateAsync(... ShownCount + 1)`.

- [ ] **Step 1: Failing tests:**

```csharp
// SiteChatAuthTests
[Fact] public async Task Site_chat_key_reads_questions()              // 200
[Fact] public async Task Site_chat_key_cannot_post_leads()            // POST /api/integration/leads → 403
[Fact] public async Task Oberig_global_key_cannot_use_site_chat()     // "dev-oberig-key-change-in-prod" → 403
[Fact] public async Task Company_key_cannot_use_site_chat()           // ключ компанії (див. IntegrationLeadEndpointTests) → 403
[Fact] public async Task No_key_is_401()
[Fact] public async Task Site_chat_key_does_not_touch_last_seen()     // integration_last_seen_at UAGREEN не змінився
// SiteChatQuestionsTests
[Fact] public async Task Uk_lists_active_in_order()                   // вимкнути №2 → 10, [0].Text == "Бажаєте замовити аудит на своїй ділянці?"
[Fact] public async Task En_skips_questions_without_translation()     // №3 TextEn = null → en без нього, тексти англійські
[Fact] public async Task Impression_increments_counter()              // двічі → 204, ShownCount == 2
[Fact] public async Task Impression_for_unknown_question_is_404()
```

- [ ] **Step 2:** `dotnet test backend/tests/UaGreen.Api.IntegrationTests --filter "SiteChatAuthTests|SiteChatQuestionsTests"` → FAIL.
- [ ] **Step 3:** Реалізувати.
- [ ] **Step 4:** → PASS; також `--filter "IntegrationLead|WhatsAppBridgeEndpointTests"` → PASS (інші ключі не зламались).
- [ ] **Step 5:** Commit `feat(chat): site chat API key, starter questions and impressions`.

---

### Task 3: Розмова відвідувача (вхідні)

**Files:**
- Modify: `backend/src/UaGreen.Application/Crm/Chat/SiteChatVisitor.cs`, `backend/src/UaGreen.Api/Controllers/SiteChatController.cs`
- Test: `backend/tests/UaGreen.Application.UnitTests/Crm/SiteChatVisitorTests.cs` (логіка на `TestDb`, як `MessengerChatTests`), `backend/tests/UaGreen.Api.IntegrationTests/Crm/SiteChatEndpointTests.cs` (HTTP і токен)

**Interfaces:**
- Consumes: `ChatInbox.RingBellAsync`, `ChatInbox.ApplyPhoneAsync`, `ChatConversationLoader.OneAsync`, `IChatNotifier`, `ChatMessageDto.From`, `RecordTelegramMessageHandler.Preview/Clip`, `IExternalNotificationDispatcher`, `ContractSigning.NewToken/HashToken`, `SiteChat`.
- Produces:
  - `record SiteChatMessageDto(Guid Id, string Text, DateTime At, bool FromVisitor, string? AuthorName)`
  - `record StartSiteChatResult(Guid ConversationId, string Token, List<SiteChatMessageDto> Messages)`
  - `record StartSiteChatCommand(string? Text, string? Lang, string? PageUrl, Guid? QuestionId) : IRequest<Result<StartSiteChatResult>>`
  - `record PostSiteChatMessageCommand(Guid ConversationId, string? Token, string? Text) : IRequest<Result<SiteChatMessageDto>>`
  - `record SetSiteChatContactCommand(Guid ConversationId, string? Token, string? Name, string? Phone) : IRequest<Result>`
  - `record SiteChatMessagesQuery(Guid ConversationId, string? Token) : IRequest<Result<List<SiteChatMessageDto>>>`
  - Пошук розмови відвідувача: `IgnoreQueryFilters()` + `CompanyId == db.CurrentCompanyId` + `Channel == Website` + `Id` + `ExternalChatId == HashToken(token)`; токен порожній або > 64 → `not_found` (контролер → 404). Нове повідомлення відвідувача в розмову з `IsDeleted` знімає `IsDeleted` (як `MessengerInboxHandler`); читання видаленої розмови — `not_found`.
  - Маршрути: `POST conversations` → `201`, `GET conversations/{id:guid}/messages`, `POST conversations/{id:guid}/messages` → `201`, `PUT conversations/{id:guid}/contact` → `204`; токен — `[FromHeader(Name = "X-Chat-Token")]`.

Старт: розмова `Channel = Website`, `ExternalChatId = HashToken(token)`, `DisplayName = "Гість #N"` (Global Constraints), `StartPageUrl`, `StarterQuestionId` (питання, якщо існує — активне чи ні; неіснуючий id ігнорувати). З питанням — спершу вихідне повідомлення з його текстом (`TextEn` для `en`, якщо є, інакше `TextUk`), `SentByUserId = null`, `DeliveryStatus = Sent`, `SentAt` на 1 мс раніше за повідомлення відвідувача. Далі — однаковий для старту й наступних шлях запису вхідного (Global Constraints). Контакт: ім'я → `DisplayName`; телефон → `ApplyPhoneAsync`; потім `ConversationChangedAsync`.

- [ ] **Step 1: Failing tests** — unit (`SiteChatVisitorTests`, `NSubstitute` для `IChatNotifier`, `INotificationService`, `IExternalNotificationDispatcher`):

```csharp
[Fact] public async Task Start_opens_a_website_conversation_named_guest()
    // Channel == Website, DisplayName == "Гість #1", UnreadCount == 1, LastMessagePreview == текст, ExternalChatId == HashToken(result.Token), result.Token.Length == 43
[Fact] public async Task Second_conversation_is_guest_two()
[Fact] public async Task Start_with_question_puts_the_question_first()
    // messages[0] { FromVisitor:false, AuthorName:"UA Green", Text: питання №6 uk }, messages[1] — відвідувач; StarterQuestionId == №6
[Fact] public async Task English_start_uses_the_english_question()
[Fact] public async Task Disabled_question_still_counts_as_the_starter()
[Fact] public async Task Text_is_stored_verbatim()                    // "<b>x</b><script>alert(1)</script>" → той самий Text
[Fact] public async Task First_unread_rings_the_bell_and_telegram_once()
    // старт + ще одне повідомлення → RingBell (SendToUserAsync "chat_message_received") 1 раз, DispatchToUserAsync 1 раз на отримувача
    // UnreadCount = 0 (менеджер прочитав) → наступне повідомлення знову дзвонить і диспатчить
[Fact] public async Task Visitor_message_reopens_a_closed_conversation()
[Fact] public async Task Visitor_message_restores_a_deleted_conversation()
[Fact] public async Task Contact_phone_links_the_customer_card()   // клієнт із +380671234567 → CustomerId; ім'я → DisplayName
[Theory, InlineData(""), InlineData("   ")] public async Task Empty_text_is_rejected(string t)   // chat_text_required
[Fact] public async Task Too_long_text_is_rejected()                 // 2001 → chat_text_too_long; 2000 → ok
[Fact] public async Task Bad_lang_and_phone_are_rejected()           // "de" → chat_lang_invalid; телефон "123" → chat_phone_invalid
```

Integration (`SiteChatEndpointTests`, `SiteChatKey`):

```csharp
[Fact] public async Task Start_then_read_with_the_token()        // POST → 201; GET з токеном → 200 і 1 повідомлення
[Fact] public async Task Wrong_missing_or_long_token_is_404()    // чужий / без / 65 символів → 404
[Fact] public async Task Manager_sees_the_site_chat_in_the_inbox()  // admin GET /api/chat/conversations?search=<текст> → рядок з channel "Website"
```

- [ ] **Step 2:** `dotnet test backend/tests/UaGreen.Application.UnitTests --filter SiteChatVisitorTests` і `dotnet test backend/tests/UaGreen.Api.IntegrationTests --filter SiteChatEndpointTests` → FAIL.
- [ ] **Step 3:** Реалізувати.
- [ ] **Step 4:** → PASS.
- [ ] **Step 5:** Commit `feat(chat): website visitor conversations`.

---

### Task 4: Канал `Website` у діях менеджера

**Files:**
- Modify: `backend/src/UaGreen.Application/Crm/Chat/ChatCommands.cs` (`SendChatMessageHandler` :87-160, `ImportChatHistoryHandler` :374-378), `backend/src/UaGreen.Application/Crm/Chat/ChatExperience.cs` (`Avatar` :115-133, `DeleteMessage` :532-559, `DeleteChat` :562-573), `backend/src/UaGreen.Application/Crm/Chat/ChatDtos.cs` (`ChatConversationDto`), `backend/src/UaGreen.Application/Crm/Chat/ChatQueries.cs` (`ChatConversationLoader.Project` :22-33)
- Test: `backend/tests/UaGreen.Application.UnitTests/Crm/WebsiteChatManagerTests.cs`

**Interfaces:**
- Consumes: `SiteChatMessagesQuery` (Task 3) — для перевірки, що бачить відвідувач.
- Produces:
  - `SendChatMessageHandler`: гілка `Website` — `NewOutbound(conversation, text, now)` + `db.ChatMessages.Add`, без шлюзу й зовнішніх id; далі наявний спільний хвіст (призначення першим, хто відповів, `UnreadCount = 0`, live-події).
  - `ImportChatHistoryHandler`: `Website` → `Result.Ok(0)`.
  - `Avatar`: `Website` → `null`.
  - `DeleteMessage`: `Website` — шлях м'якого видалення, як Messenger (:538-543).
  - `DeleteChat`: `Website` — без звернення до `Peer`, як WhatsApp/Messenger.
  - `ChatConversationDto` + два параметри в кінці: `string? StartPageUrl = null, string? StarterQuestion = null` (текст питання uk); заповнити і в `From`, і в `ChatConversationLoader.Project`.

- [ ] **Step 1: Failing tests** (`TestDb`):

```csharp
[Fact] public async Task Reply_to_a_website_chat_is_stored_without_a_gateway()
    // SendChatMessageCommand → IsSuccess; повідомлення Outbound, DeliveryStatus Sent, ExternalMessageId/Key null; AssignedUserId == менеджер; UnreadCount 0
    // жоден шлюз (Telegram/WhatsApp/Messenger) не викликано
[Fact] public async Task Visitor_sees_the_reply_with_the_managers_first_name()   // SiteChatMessagesQuery → останнє { FromVisitor:false, AuthorName: FirstName }
[Fact] public async Task Deleted_reply_disappears_for_the_visitor()               // DeleteMessage → SiteChatMessagesQuery без нього
[Fact] public async Task Deleting_a_website_chat_does_not_need_a_peer()           // DeleteChat → без винятку, IsDeleted
[Fact] public async Task Website_avatar_is_null()
[Fact] public async Task Importing_website_history_is_a_no_op()                   // Ok(0)
[Fact] public async Task Conversation_dto_carries_page_and_question()             // OneAsync → StartPageUrl, StarterQuestion == текст uk
[Fact] public async Task Website_attachments_are_unsupported()                    // Upload → ChatOperationException "chat_channel_unsupported"
```

- [ ] **Step 2:** `dotnet test backend/tests/UaGreen.Application.UnitTests --filter WebsiteChatManagerTests` → FAIL.
- [ ] **Step 3:** Реалізувати.
- [ ] **Step 4:** → PASS; увесь `UaGreen.Application.UnitTests` → PASS (наявні тести чатів не зламались).
- [ ] **Step 5:** Commit `feat(chat): managers answer website chats from the inbox`.

---

### Task 5: Питання й статистика (API налаштувань)

**Files:**
- Create: `backend/src/UaGreen.Application/Crm/Chat/SiteChatQuestionAdmin.cs`
- Modify: `backend/src/UaGreen.Api/Controllers/ChatController.cs`
- Test: `backend/tests/UaGreen.Api.IntegrationTests/Crm/SiteChatQuestionAdminTests.cs`

**Interfaces:**
- Produces:
  - `record SiteChatQuestionStatsDto(Guid Id, string TextUk, string? TextEn, bool IsActive, int OrderNo, int Shown, int Answered, int Leads)` — `Answered` = розмови з цим `StarterQuestionId` (з видаленими); `Leads` за Global Constraints.
  - `record SiteChatQuestionStatsQuery : IRequest<List<SiteChatQuestionStatsDto>>` (за `OrderNo`).
  - `record CreateSiteChatQuestionCommand(string? TextUk, string? TextEn) : IRequest<Result<Guid>>` (`OrderNo = max + 1`); `record UpdateSiteChatQuestionCommand(Guid Id, string? TextUk, string? TextEn, bool IsActive) : IRequest<Result>`; коди `chat_question_text_required`, `chat_question_text_too_long` (300), `not_found`.
  - Маршрути в `ChatController` під `crm.manage_settings`: `GET api/chat/website/questions`, `POST api/chat/website/questions` → `201 { id }`, `PUT api/chat/website/questions/{id:guid}` → `204`.

- [ ] **Step 1: Failing tests:**

```csharp
[Fact] public async Task Stats_count_shown_answered_and_leads()
    // №1: 3 покази; 2 розмови з ним; у однієї картка клієнта з угодою, створеною після старту → 3 / 2 / 1; інші — нулі
[Fact] public async Task A_deal_older_than_the_chat_is_not_a_lead()
[Fact] public async Task Create_and_disable()      // POST {textUk:"Нове?"} → 201; PUT IsActive=false → у GET /api/site-chat/questions його нема
[Fact] public async Task Needs_crm_settings()      // користувач лише з crm.chat → 403 на GET і POST
[Fact] public async Task Empty_uk_text_is_rejected()  // 400 chat_question_text_required
```

- [ ] **Step 2:** `dotnet test backend/tests/UaGreen.Api.IntegrationTests --filter SiteChatQuestionAdminTests` → FAIL.
- [ ] **Step 3:** Реалізувати.
- [ ] **Step 4:** → PASS.
- [ ] **Step 5:** Commit `feat(chat): website starter questions admin and stats`.

---

### Task 6: Flutter — канал «Сайт» у скриньці

**Files:**
- Modify: `frontend/lib/features/crm/domain/chat_models.dart` (`ChatChannel` :18-31, `ChatConversationModel` :161-304), `frontend/lib/features/crm/presentation/widgets/comms_widgets.dart` (:11-50, :91, :172), `communications_context_panel.dart` (:271-277), `communications_thread.dart` (:245-252 підзаголовок, :898-916 вставка скріншота, :970-986 «Файли угоди»), `chat_experience_widgets.dart` (:682-707 меню «+», :1145-1147 текст видалення чату), `communications_list.dart` (:57-69 порожній стан), `frontend/lib/features/crm/presentation/screens/chats_screen.dart` (:259-303 `_sendDocument`)
- Create: `frontend/assets/icons/website.svg` (проста піктограма глобуса, стиль інших `glyphAsset`; перевірити, що `assets/icons/` уже в `pubspec.yaml`)
- Modify: `frontend/lib/l10n/app_en.arb`, `app_uk.arb` (нові ключі — перед блоком `tourStep*`)
- Test: `frontend/test/features/crm/chat_models_test.dart`, `frontend/test/features/crm/chats_screen_test.dart` (або новий `website_chat_test.dart` поруч)

**Interfaces:**
- Produces:
  - `ChatChannel.website(5)`: `label` «Сайт», `brandColor` — зелений бренду UA Green з теми, `glyphAsset` `assets/icons/website.svg`; `canReact`/`canForward`/`canEdit`/`canStartByPhone` → false; `deletesOnlyHere` → true; новий `bool get canSendFiles => this != ChatChannel.website` — ховає «+», вставку скріншота, «Файли угоди», `_sendDocument`.
  - Фільтр-пігулка «Сайт» є; порожній стан для неї — `commsEmptyWebsite` («Чатів із сайту ще не було» / "No website chats yet"), без заклику «підключити».
  - `ChatConversationModel.startPageUrl`, `starterQuestion` (з JSON `startPageUrl`, `starterQuestion`).
  - Підзаголовок треду для `website`: додати `l10n.chatFromPage(url)` («зі сторінки {url}» / "from {url}") і `l10n.chatStarterQuestion(q)` («питання: {q}» / "question: {q}"), коли є.
  - `communications_context_panel`: `website` → `null` (як Messenger).

- [ ] **Step 1: Failing tests:**

```dart
// chat_models_test.dart
test('website channel parses from name and code', ...)     // 'Website' і 5 → ChatChannel.website
test('conversation reads start page and starter question', ...)
// chats_screen_test.dart (або website_chat_test.dart)
testWidgets('website thread hides attachments, reactions and editing', ...)   // немає кнопки «+», у меню повідомлення немає «Реакція»/«Редагувати»
testWidgets('website thread shows the start page and the question', ...)      // текст «зі сторінки /shop/k5v1» і «питання: …» у підзаголовку
```

- [ ] **Step 2:** `cd frontend; flutter test test/features/crm` → FAIL.
- [ ] **Step 3:** Реалізувати; `flutter gen-l10n`.
- [ ] **Step 4:** → PASS; `flutter analyze` чистий.
- [ ] **Step 5:** Commit `feat(chat): website channel in the chats inbox`.

---

### Task 7: Flutter — вкладка «Сайт» у налаштуваннях чатів

**Files:**
- Create: `frontend/lib/features/crm/presentation/widgets/site_chat_settings_section.dart`
- Modify: `frontend/lib/features/crm/data/` (репозиторій чатів — методи нижче), `frontend/lib/features/crm/domain/chat_models.dart` (`SiteChatQuestionStats`), `frontend/lib/features/crm/presentation/viewmodels/chat_viewmodel.dart` (новий VM поруч із `MessengerSettingsViewModel` :929-999), `frontend/lib/main.dart` (реєстрація поруч із :1189-1190), `frontend/lib/features/crm/presentation/widgets/chat_settings_dialog.dart` (:140-178), arb-файли
- Test: `frontend/test/features/crm/site_chat_settings_test.dart`

**Interfaces:**
- Consumes: `GET/POST api/chat/website/questions`, `PUT api/chat/website/questions/{id}` (Task 5).
- Produces:
  - `SiteChatQuestionStats { id, textUk, textEn?, isActive, orderNo, shown, answered, leads; double get answerRate => shown == 0 ? 0 : answered / shown; }`
  - Репозиторій: `Future<List<SiteChatQuestionStats>> siteChatQuestions()`, `Future<String> createSiteChatQuestion({required String textUk, String? textEn})`, `Future<void> updateSiteChatQuestion(String id, {required String textUk, String? textEn, required bool isActive})`.
  - `class SiteChatSettingsViewModel extends ChangeNotifier { load(); create(textUk, textEn) → Future<bool>; update(q, {textUk, textEn, isActive}) → Future<bool>; items; isLoading; error }` — `create` з порожнім `textUk` повертає `false` без запиту.
  - Вкладка `ChatChannel.website` у `chat_settings_dialog` → `SiteChatSettingsSection`: таблиця «Питання · Показано · Відповіли (%) · Ліди», перемикач активності, редагування uk/en у `ResponsiveFormDialog`, «Додати питання». Текст-підказка: вмикається на сайті ключем `SiteChat:ApiKey`.
  - Рядки: `siteChatQuestionsTitle` Starter questions / Питання для чату; `siteChatShown` Shown / Показано; `siteChatAnswered` Answered / Відповіли; `siteChatLeads` Leads / Ліди; `siteChatAddQuestion` Add question / Додати питання; `siteChatTextUk` Text (Ukrainian) / Текст українською; `siteChatTextEn` Text (English, optional) / Текст англійською (необов'язково); `siteChatHint` The widget appears on the site once the site has the chat key. / Віджет з'являється на сайті, щойно сайту видано ключ чату.

- [ ] **Step 1: Failing tests:**

```dart
test('answerRate is zero without impressions and a ratio otherwise', ...)  // (0,0) → 0; (40,10) → 0.25
test('create with empty Ukrainian text does not call the API', ...)
test('update sends isActive and reloads', ...)
testWidgets('website tab lists questions with stats', ...)   // рядок «Маєте питання по наших станціях?» і «7,8%»/«7.8%»
```

- [ ] **Step 2:** `flutter test test/features/crm/site_chat_settings_test.dart` → FAIL.
- [ ] **Step 3:** Реалізувати; `flutter gen-l10n`.
- [ ] **Step 4:** → PASS; `flutter analyze`; `flutter test test/features/crm` → PASS.
- [ ] **Step 5:** Commit `feat(chat): website starter questions in chat settings`.

---

### Task 8: Конфіг і фінальна перевірка

- [ ] **Step 1:** Поруч з `INTEGRATION__APIKEY` у продакшн-шаблоні env і compose Господаря (`grep -rn INTEGRATION__APIKEY infra`) додати `SITECHAT__APIKEY=` (порожньо = вимкнено) і `SITECHAT__COMPANYCODE=UAGREEN`, прокинути в контейнер API.
- [ ] **Step 2:** `dotnet test backend/tests/UaGreen.Application.UnitTests --filter "Category!=Integration"` → PASS.
- [ ] **Step 3:** `dotnet test backend/tests/UaGreen.Api.IntegrationTests` → PASS.
- [ ] **Step 4:** `cd frontend; flutter analyze; flutter test` → PASS.
- [ ] **Step 5:** Ручна перевірка: Господар локально з `SiteChat:ApiKey`; `curl` старт розмови з питанням → у `/crm/chats` «Гість #N» з піктограмою сайту, сторінкою й питанням; відповідь менеджера → `curl GET …/messages` показує її з іменем; «Створити ліда» → угода; у налаштуваннях чатів вкладка «Сайт» показує 1 / 1 / 1. Скріншоти.
- [ ] **Step 6:** Commit `chore(chat): SiteChat env settings`.
