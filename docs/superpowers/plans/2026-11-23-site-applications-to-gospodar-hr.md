# Site job applications → Gospodar HR Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Every job application from uagreen.co becomes a Gospodar candidate in the «Новий» column (with CV, on the linked vacancy), and the site admin only shows the ones that failed to arrive.

**Architecture:** Gospodar gets two company-key integration endpoints (list open vacancies; create candidate from a multipart form, idempotent by `externalId`, merging into an existing candidate matched by phone digits/email). The site keeps each application as an outbox row, pushes it from the existing `CrmSyncWorker` minute loop with the existing backoff, deletes the local CV after Gospodar accepts it, and lets HR link each site vacancy to a Gospodar vacancy from a dropdown.

**Tech Stack:** ASP.NET Core 9, EF Core (Npgsql, snake_case), MediatR + FluentValidation (Gospodar), minimal APIs (site), xUnit + FluentAssertions, Flutter web admin.

**Spec:** `docs/superpowers/specs/2026-11-23-site-applications-to-gospodar-hr-design.md`

## Global Constraints

- Two repos. **Gospodar:** new worktree `D:\Work\Gospodar-wt-site-candidates`, branch `claude/site-candidates-integration` from `origin/main` (never touch `D:\Work\Gospodar`, it holds someone's uncommitted work on `feature/qc-tags`). **Site:** this worktree, branch `claude/feedback-hr-review-c23a09`.
- Source string on candidates: `Сайт uagreen.co`. Fallback vacancy title: `Відгуки з сайту`; its specialty: `Без посади` (find by name, create if missing).
- `externalId` = `site-app-{Application.Id:N}` (41 chars; Gospodar column 64).
- Comment marker for merged applications: `[site:{externalId}]`. Comment author for integration comments: `Guid.Empty`, displayed as `Сайт uagreen.co`.
- CV: PDF / DOC / DOCX only, ≤ 10 MB, magic bytes checked in Gospodar (`FileMagicByteValidator`).
- Merge rule: existing candidate (phone via `BinotelCallPayload.NormalizePhone` on both sides, or email case-insensitive) in `Rejected`/`Reserve` → `Stage = New`, `RejectionReason = null`, `VacancyId = new vacancy`; any other stage → comment only.
- Gospodar HR forms' duplicate check (`CandidateContacts.FindDuplicateError`) stays unchanged.
- Site: no 3-day window for applications; `CrmSync.MaxAttempts` (20) and `CrmSync.Due` backoff reused.
- Gospodar migrations: after adding one, strip older `.Designer.cs` files so only the newest 3 keep `BuildTargetModel` (see Gospodar `CLAUDE.md`).
- Nothing is deployed, pushed or merged without the user's explicit OK (Task 8).

## Review Focus

1. CV file name in Cyrillic (`Резюме Іван.pdf`) must arrive in Gospodar intact → Task 3 + Task 5 tests use a Cyrillic name.
2. Site vacancy linked to a Gospodar vacancy that is later closed → candidate still lands on it; linked id unknown in this company → fallback vacancy (Task 3 test).
3. Gospodar answers 400 → site keeps the CV, stores the error, row stays undelivered and visible (Task 5 test).
4. CV file already gone from site disk (or no CV) → application still goes without CV instead of failing 20 times (Task 5 test).
5. Same person, email in other case and no phone → merges, not a second card (Task 3 test).

---

## Gospodar

### Task 1: `Candidate.ExternalId` + migration

**Files:**
- Modify: `backend/src/UaGreen.Domain/Entities/Hr/Candidate.cs`
- Modify: `backend/src/UaGreen.Infrastructure/Persistence/Configurations/HrConfiguration.cs` (`CandidateConfiguration`)
- Create: migration `AddCandidateExternalId` in `backend/src/UaGreen.Infrastructure/Persistence/Migrations/`

**Interfaces:**
- Produces: `Candidate.ExternalId : string?` (column `external_id`, max 64, unique with `company_id` where not null).

- [ ] **Step 1:** Create the worktree: `git -C D:/Work/Gospodar worktree add -b claude/site-candidates-integration D:/Work/Gospodar-wt-site-candidates origin/main`. Expected: new folder on the new branch.
- [ ] **Step 2:** Add `public string? ExternalId { get; set; }` to `Candidate` (doc comment: id of the application in the system that sent it, e.g. `site-app-…`; null for candidates HR typed in).
- [ ] **Step 3:** In `CandidateConfiguration`: `b.Property(x => x.ExternalId).HasMaxLength(64);` and `b.HasIndex(x => new { x.CompanyId, x.ExternalId }).IsUnique().HasFilter("external_id IS NOT NULL");`
- [ ] **Step 4:** `dotnet ef migrations add AddCandidateExternalId --project backend/src/UaGreen.Infrastructure --startup-project backend/src/UaGreen.Api`. Expected: migration adds one nullable column + one unique filtered index, nothing else (read the generated file; if it contains unrelated `AlterColumn`s, stop and report).
- [ ] **Step 5:** Strip the now 4th-newest `.Designer.cs` to its attributes, same as the older ones. Run `dotnet build backend/UaGreen.sln`. Expected: build succeeds.
- [ ] **Step 6:** Commit `feat(hr): candidates.external_id for integration idempotency`.

### Task 2: `GET /api/integration/vacancies`

**Files:**
- Create: `backend/src/UaGreen.Application/Hr/Integration/SiteApplications.cs` (query part)
- Create: `backend/src/UaGreen.Api/Controllers/IntegrationCandidatesController.cs`
- Test: `backend/tests/UaGreen.Api.IntegrationTests/IntegrationCandidateEndpointTests.cs`

**Interfaces:**
- Produces: `record IntegrationVacancyDto(Guid Id, string Name)`; `record GetIntegrationVacanciesQuery : IRequest<List<IntegrationVacancyDto>>` (open vacancies of the current company, `Name = Title ?? Specialty.Name`, ordered by name); controller `IntegrationCandidatesController` at route `api/integration`, `[Authorize(AuthenticationSchemes = ApiKeyAuthenticationHandler.SchemeName, Policy = ServicePolicies.IntegrationCompany)]`, action `GET vacancies`.

- [ ] **Step 1: Write the failing tests** in `IntegrationCandidateEndpointTests` (`IClassFixture<WebAppFactory>`, a `CompanyAsync()` helper modelled on `IntegrationLeadEndpointTests.CompanyAsync` that creates a company with an integration key, a specialty, an open vacancy «Токар» and a closed vacancy «Менеджер з продажу», returning their ids):
  - `Vacancies_lists_only_open_ones_of_the_key_company`: `GET /api/integration/vacancies` → 200, contains `{ id = tokarId, name = "Токар" }`, does not contain the closed one, and contains no vacancy of another company created in the same test.
  - `Vacancies_without_key_is_401`: no `X-Api-Key` → 401.
- [ ] **Step 2:** Run `dotnet test backend/tests/UaGreen.Api.IntegrationTests --filter IntegrationCandidateEndpointTests`. Expected: FAIL (404).
- [ ] **Step 3:** Implement the query + handler and the controller action.
- [ ] **Step 4:** Re-run. Expected: PASS.
- [ ] **Step 5:** Commit `feat(hr): integration endpoint lists open vacancies`.

### Task 3: `POST /api/integration/candidates`

**Files:**
- Modify: `backend/src/UaGreen.Application/Hr/Integration/SiteApplications.cs` (command part)
- Modify: `backend/src/UaGreen.Api/Controllers/IntegrationCandidatesController.cs`
- Modify: `backend/src/UaGreen.Application/Hr/Queries/CandidateQueries.cs:72` (author name)
- Test: `backend/tests/UaGreen.Api.IntegrationTests/IntegrationCandidateEndpointTests.cs`

**Interfaces:**
- Consumes: `Candidate.ExternalId` (Task 1); `UploadResumeCommand`, `AddCandidateCommentCommand` (existing, via `IMediator`); `BinotelCallPayload.NormalizePhone`.
- Produces:
  - `record IntegrationCv(string FileName, string ContentType, long SizeBytes, Stream Content)`
  - `record CreateIntegrationCandidateCommand(string ExternalId, string FullName, string? Phone, string? Email, string? Message, Guid? VacancyId, string? SiteVacancyTitle, IntegrationCv? Cv) : IRequest<Result<IntegrationCandidateResult>>`
  - `record IntegrationCandidateResult(Guid CandidateId, bool Created)`
  - Handler constants `SourceName = "Сайт uagreen.co"`, `FallbackVacancyTitle = "Відгуки з сайту"`, `FallbackSpecialtyName = "Без посади"`, `static string Marker(string externalId) => $"[site:{externalId}]"`.
  - HTTP: `POST /api/integration/candidates`, `multipart/form-data` fields `externalId, fullName, phone, email, message, vacancyId, siteVacancyTitle` + file `cv`; `[RequestSizeLimit(12_000_000)]`. Responses `201 { candidateId }` created, `200 { candidateId }` replay/merged, `400 { error }` (validation or `cv_invalid`).

Handler order: (1) replay — candidate with this `ExternalId`, or a comment containing `Marker(externalId)` → `Ok(id, false)`; (2) vacancy — `VacancyId` if it exists in the company (any status), else the fallback vacancy (find by `Title`, else create `Status = Open` with the fallback specialty, find/create by name); (3) match — load `(Id, Phone, Email, Stage, VacancyId)` of the company's candidates and match `NormalizePhone` equality or case-insensitive email (`// ponytail: in-memory match over all candidates — fine for hundreds; move to a normalized column if it grows`); (4a) matched → comment `"{Marker} Повторний відгук з сайту на «{new vacancy name}» (раніше: «{old vacancy name}»)\n{Message}"` via `AddCandidateCommentCommand(id, Guid.Empty, body)` (cut to 4000), apply the merge rule, attach CV via `UploadResumeCommand`, `Ok(id, false)`; (4b) not matched → new `Candidate { Stage = New, Source = SourceName, ExternalId, VacancyId, FullName, Phone, Email, Notes = "Вакансія на сайті: {SiteVacancyTitle}\n{Message}" (≤ 4000) }`, save, attach CV, `Ok(id, true)`.

Validator: `ExternalId` not empty, ≤ 60; `FullName` not empty, ≤ 300; phone or email required; `Phone` ≤ 50; `Email` ≤ 200; `Message` ≤ 4000; `SiteVacancyTitle` ≤ 200.

Controller CV check before sending the command: content type in {`application/pdf`, `application/msword`, `application/vnd.openxmlformats-officedocument.wordprocessingml.document`}, length ≤ 10 MB, `FileMagicByteValidator.Matches(first 1024 bytes, type)`; otherwise `400 { error = "cv_invalid" }`.

- [ ] **Step 1: Write the failing tests** (post with `MultipartFormDataContent`; a minimal PDF is the bytes of `"%PDF-1.4\n%%EOF"`):
  - `New_application_becomes_a_new_candidate_with_cv_and_a_retry_returns_the_same_one`: 201; candidate has `Stage == New`, `Source == "Сайт uagreen.co"`, `VacancyId == tokarId`, `ExternalId` as sent, `Notes` starts with `"Вакансія на сайті: Токар"`, one attachment with `FileName == "Резюме Іван.pdf"`; same post again → 200 with the same `candidateId`, still one candidate and one attachment.
  - `Unknown_or_foreign_vacancy_goes_to_the_site_fallback_vacancy`: `vacancyId` omitted → candidate's vacancy `Title == "Відгуки з сайту"`, specialty name `"Без посади"`; a second application with another company's vacancy id → same fallback vacancy (only one fallback vacancy exists).
  - `Closed_linked_vacancy_still_receives_the_candidate`: `vacancyId = closedManagerId` → candidate on that vacancy.
  - `Known_phone_in_reserve_returns_to_new_on_the_new_vacancy`: existing candidate phone `"067 123 45 67"`, `Stage = Reserve`, `RejectionReason = "пізніше"`, on the manager vacancy; post phone `"+380671234567"` for Токар → 200 with the existing id; `Stage == New`, `RejectionReason == null`, `VacancyId == tokarId`; one comment containing `"[site:"` and `"раніше: «Менеджер з продажу»"`; same post again → still one comment.
  - `Known_email_in_active_stage_only_gets_a_comment`: existing `Email = "Ivan@Example.com"`, `Stage = Scheduled`; post `email = "ivan@example.com"`, no phone → 200, stage and vacancy unchanged, one comment; candidate detail (`GET /api/recruitment/candidates/{id}` as an admin) shows that comment's author as `"Сайт uagreen.co"`.
  - `Cv_that_is_not_a_document_is_rejected`: `cv` declared `application/pdf` with PNG bytes → 400 `cv_invalid`, no candidate created.
  - `Wrong_key_is_401`.
- [ ] **Step 2:** Run the filter from Task 2. Expected: the new tests FAIL.
- [ ] **Step 3:** Implement command, validator, handler, controller action; in `CandidateQueries.cs:72` use `"Сайт uagreen.co"` when `AuthorId == Guid.Empty`.
- [ ] **Step 4:** Re-run. Expected: all `IntegrationCandidateEndpointTests` PASS. Then run `dotnet test backend/tests/UaGreen.Api.IntegrationTests --filter "RecruitmentTests|IntegrationLeadEndpointTests"`. Expected: PASS (no regressions in HR forms).
- [ ] **Step 5:** Commit `feat(hr): site applications arrive as candidates via integration key`.

---

## Site

### Task 4: Data model + migration

**Files:**
- Modify: `backend/src/UaGreen.Site.Api/Domain/Content.cs` (`Vacancy`)
- Modify: `backend/src/UaGreen.Site.Api/Domain/Inbox.cs` (`Application`, delete `ApplicationStatus`)
- Create: migration `ApplicationsToGospodar` in `backend/src/UaGreen.Site.Api/Infrastructure/Persistence/Migrations/`

**Interfaces:**
- Produces: `Vacancy.GospodarVacancyId : Guid?`; `Application : ICrmLinked` with real columns `GospodarCandidateId : Guid?`, `GospodarSyncedAt : DateTime?`, `GospodarError : string?`, `GospodarAttempts : int`, `GospodarLastAttemptAt : DateTime?`, and explicit interface members mapping `ICrmLinked.CrmDealId ↔ GospodarCandidateId`, `CrmSyncedAt ↔ GospodarSyncedAt`, `CrmError ↔ GospodarError`, `CrmAttempts ↔ GospodarAttempts`, `CrmLastAttemptAt ↔ GospodarLastAttemptAt`. `IsRead`, `Status`, `Note` and `enum ApplicationStatus` are removed.

- [ ] **Step 1:** Make the model changes; fix compile errors only where they reference the removed members by deleting those usages (admin endpoints are reworked in Task 6 — for now remove the `PATCH` endpoint and the `unread`/`status` filters in `AdminInboxEndpoints`, and the `Unread`/`New` counts in `AdminDashboardEndpoints` → `Undelivered = count(GospodarCandidateId == null)`).
- [ ] **Step 2:** `dotnet ef migrations add ApplicationsToGospodar --project backend/src/UaGreen.Site.Api`. Expected: adds 5 columns to `applications` + 1 to `vacancies`, drops `is_read`, `status`, `note`; nothing else.
- [ ] **Step 3:** `dotnet build backend/UaGreen.Site.sln && dotnet test backend/tests/UaGreen.Site.Tests`. Expected: build OK, existing tests PASS.
- [ ] **Step 4:** Commit `feat(site): applications carry Gospodar sync state; vacancies link to Gospodar`.

### Task 5: Push applications from `CrmSync` / `CrmSyncWorker`

**Files:**
- Modify: `backend/src/UaGreen.Site.Api/Infrastructure/Crm/CrmSync.cs`
- Test: `backend/tests/UaGreen.Site.Tests/CrmSyncTests.cs`

**Interfaces:**
- Consumes: Task 4 model; `FileStorage.OpenPrivate/DeletePrivate`.
- Produces:
  - ctor `CrmSync(IHttpClientFactory http, IOptions<GospodarOptions> opts, FileStorage storage, ILogger<CrmSync> log)`
  - `public static string ExternalId(Application a)` → `$"site-app-{a.Id:N}"`
  - `public async Task PushAsync(Application a, Vacancy? v, CancellationToken ct)` — no-op when `GospodarCandidateId` is set; multipart to `/api/integration/candidates` (fields per Task 3; `vacancyId` = `v?.GospodarVacancyId`, `siteVacancyTitle` = `v?.TitleUk`; `cv` part only when `CvKey` opens; buffer the content for Content-Length like `SendAsync` does); success → `candidateId` from JSON, `GospodarSyncedAt`, then `DeletePrivate(CvKey)`, `CvKey = null`, `CvFileName = null`; failure → same attempt/error bookkeeping as leads (reuse one private helper for the status→message mapping).
  - `public sealed record GospodarVacancyOption(Guid Id, string Name)`; `public async Task<IReadOnlyList<GospodarVacancyOption>?> GetVacanciesAsync(CancellationToken ct)` — `GET /api/integration/vacancies`, null on any failure / no key.
  - `CrmSyncWorker.TickAsync(AppDbContext db, CrmSync sync, CancellationToken ct)` (signature unchanged) gets a third loop: applications with `GospodarCandidateId == null && GospodarAttempts < MaxAttempts`, oldest first, `Take(50)`, filtered by `CrmSync.Due`, with their vacancies loaded in one query; no time window.

- [ ] **Step 1: Write the failing tests** (a multipart-aware fake handler records each part's `ContentDisposition.Name` → string value, and the `cv` part's file name + bytes; the test `FileStorage` uses a temp `PrivateRoot`). Update the existing tests' `new CrmSync(...)` calls to the new ctor.
  - `Tick_sends_old_and_new_applications_with_cv_and_deletes_the_cv_only_after_success`: application A (7 days old, CV `Резюме Іван.pdf` on disk, vacancy linked to a Gospodar id) and B (now, no CV, no vacancy); fake returns 201 `{candidateId}` → both sent; A's parts: `externalId == "site-app-{A.Id:N}"`, `vacancyId` = linked id, `siteVacancyTitle` = vacancy title, `cv` file name `Резюме Іван.pdf`; B has no `vacancyId` and no `cv` part; A's CV file no longer on disk and `CvKey == null`; both have `GospodarCandidateId` set.
  - `Rejected_application_keeps_its_cv_and_error`: fake returns 400 `{"error":"phone_or_email_required"}` → `GospodarCandidateId == null`, `GospodarAttempts == 1`, `GospodarError` contains `400`, CV file still on disk.
  - `Missing_cv_file_does_not_block_the_application`: `CvKey` points to a file that is not on disk → sent without `cv` part, succeeds.
  - `GetVacancies_returns_null_without_key`.
- [ ] **Step 2:** `dotnet test backend/tests/UaGreen.Site.Tests --filter CrmSyncTests`. Expected: new tests FAIL.
- [ ] **Step 3:** Implement.
- [ ] **Step 4:** Re-run the filter, then the whole site test project. Expected: PASS.
- [ ] **Step 5:** Commit `feat(site): applications go to Gospodar HR with the CV, with retries`.

### Task 6: Admin API

**Files:**
- Modify: `backend/src/UaGreen.Site.Api/Features/Admin/AdminInboxEndpoints.cs` (applications group)
- Modify: `backend/src/UaGreen.Site.Api/Features/Admin/AdminVacanciesEndpoints.cs`
- Modify: `backend/src/UaGreen.Site.Api/Features/Admin/AdminDashboardEndpoints.cs` (if Task 4 left anything)

**Interfaces:**
- Consumes: Task 5 `CrmSync.PushAsync(Application, Vacancy?, ct)`, `GetVacanciesAsync`.
- Produces (HTTP, used by Task 7):
  - `GET /api/admin/applications?q=&page=&pageSize=` → only `GospodarCandidateId == null`; item fields `id, vacancyId, vacancyTitle, name, phone, email, message, hasCv, cvFileName, createdAt, gospodarError, gospodarAttempts`.
  - `GET /api/admin/applications/{id}/cv` (unchanged), `DELETE /api/admin/applications/{id}` (unchanged).
  - `POST /api/admin/applications/{id}/resend` → resets `GospodarAttempts = 0`, `GospodarLastAttemptAt = null`, pushes now, saves, returns the item (whatever the outcome).
  - `VacancyUpsert` / `VacancyAdminDto` gain `Guid? GospodarVacancyId` (last positional parameter); `VacancyAdminDto` loses `int Applications`.
  - `GET /api/admin/vacancies/gospodar` → `GospodarVacancyOption[]` (`[]` when Gospodar is unreachable or no key).
  - `GET /api/admin/summary` → `applications: { undelivered }`.

- [ ] **Step 1:** Implement. Register nothing new in DI (`CrmSync` is already scoped).
- [ ] **Step 2:** `dotnet build backend/UaGreen.Site.sln && dotnet test backend/tests/UaGreen.Site.Tests`. Expected: PASS.
- [ ] **Step 3:** Commit `feat(site): admin lists only undelivered applications; vacancy ↔ Gospodar link`.

### Task 7: Admin UI (Flutter)

**Files:**
- Modify: `admin/lib/pages_content.dart` (vacancy form + list)
- Modify: `admin/lib/pages_inbox.dart` (`ApplicationsPage`; delete `applicationStatuses`, `_appTone`)
- Modify: `admin/lib/main.dart:66-67` (section)
- Modify: `admin/lib/dashboard.dart` (tile)

**Interfaces:**
- Consumes: Task 6 HTTP shapes.

- [ ] **Step 1:** Vacancy form: add after `isActive` the field `F('gospodarVacancyId', 'Вакансія в Господарі', t: T.select, choices: …, hint: 'Сюди в Господарі потрапляють відгуки. Порожньо — «Відгуки з сайту».')`; choices = `[(null, '— Відгуки з сайту —'), …from GET /admin/vacancies/gospodar as (id, name)]`; if the list is empty and the row already has an id, keep `(id, 'Вакансія Господаря (недоступна)')` so saving does not wipe it. Remove the «N відгуків» `MetaChip`, `_warn`, and `deleteWarning`/`warning` usage.
- [ ] **Step 2:** `ApplicationsPage`: list of undelivered applications — title `name · phone`, subtitle vacancy title (or `Без вакансії (загальний)`) + `gospodarError` in error tone, trailing date, CV download button (`hasCv`), «Надіслати ще раз» button (`POST …/resend`; on success with no error remove the row from the list, otherwise update it), delete button with `confirmDelete`. Empty text: `Усі відгуки доставлені в Господар`. No filters, no dialog.
- [ ] **Step 3:** `main.dart`: `Section('applications', 'Не доставлені відгуки', Icons.sync_problem_outlined, const ['Hr'], () => const ApplicationsPage(), badge: (s) => _n(s['applications']?['undelivered']), group: _hr)`.
- [ ] **Step 4:** `dashboard.dart`: show the applications tile only when `undelivered > 0`, label `Відгуки не доставлені в Господар`, accent error, `onTap: nav.go('applications')`.
- [ ] **Step 5:** `cd admin && flutter analyze`. Expected: `No issues found!`
- [ ] **Step 6:** Commit `feat(admin): undelivered applications list; Gospodar vacancy dropdown`.

---

### Task 8: Verify, then ship with the user's OK

- [ ] **Step 1:** Gospodar: `dotnet test backend/UaGreen.sln` in the Gospodar worktree. Site: `dotnet test backend/UaGreen.Site.sln`. Expected: all PASS (report any pre-existing failures separately with their output).
- [ ] **Step 2:** Request a whole-branch review of both branches.
- [ ] **Step 3 (user OK required):** push both branches, open PRs (Gospodar: `uagreenllc/Gospodar` → `main`; site → `main`).
- [ ] **Step 4 (user OK required):** deploy Gospodar to erp.uagreen.co with the `gospodar-deploy` skill; deploy the site to staging (`infra/scripts/deploy.sh`, see memory `uagreen-site-deploy-from-windows`).
- [ ] **Step 5:** Link the 6 staging vacancies via `PUT /api/admin/vacancies/{id}` (`gospodarVacancyId`):

  | Site vacancy (id) | Gospodar vacancy (id) |
  |---|---|
  | Слюсар-зварювальник `cb11ef01-11a2-48c3-85ab-bcc6de04d894` | Зварювальник `2ad15c58-c902-422b-baf6-803080c52a45` |
  | Слюсар-електронщик `8342a07d-79ce-4459-bd97-92708547e0a5` | Слюсар-електронщик `b63c6149-a21d-4cd9-927c-8c200f8f7b33` |
  | Слюсар металообробки `41a3210f-a672-4ed9-a995-2564ee51ed63` | Слюсар металообробки `fd278bb1-0b58-4a62-a1a4-d2bec1336322` |
  | Токар `c46e16c4-0547-439c-a0b1-7ce7071ac850` | Токар `d12c628a-9410-40b0-aa30-efdfda3cc1e8` |
  | Фрезерувальник `6c4c50c0-0333-4b68-8294-c317f50b2dea` | Фрезерувальник `b0874241-8966-45e2-a054-249f47243009` |
  | Формувальник `17f10d25-bd7a-4e36-b842-0dc474330503` | Формовщик `27efaec7-7880-4ae6-8632-9bf222ca4502` |

- [ ] **Step 6:** The user sets `GOSPODAR_INTEGRATION_KEY` on the staging server (secret — never typed by the agent). Then one test application on new.uagreen.co → within a minute a card in «Новий» in Gospodar with the CV; delete that test candidate in Gospodar afterwards.
