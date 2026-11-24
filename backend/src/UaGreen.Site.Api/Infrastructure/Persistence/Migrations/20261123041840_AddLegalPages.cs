using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UaGreen.Site.Api.Infrastructure.Persistence.Migrations
{
    /// Seeds the public offer and privacy policy pages (drafts for a lawyer to edit in the back office).
    /// ON CONFLICT DO NOTHING: never overwrites what was already edited. Not in migration/content.json
    /// on purpose — the importer upserts and would overwrite those edits on every run.
    public partial class AddLegalPages : Migration
    {
        internal const string Seller = """
            <p>ПРОДАВЕЦЬ (ВИКОНАВЕЦЬ):</p><h3>Товариство з обмеженою відповідальністю «ЮА Грін»</h3><p>ЄДРПОУ 45033277</p><p>Адреса: Харківська область, с. Хроли, вул. Садова, буд. 1, кв. 117</p><p>Електронна пошта: hello@uagreen.co</p><p>Телефон: +38 (095) 717 37 72</p>
            """;

        internal const string SellerEn = """
            <p>SELLER (CONTRACTOR):</p><h3>UA Green LLC (ТОВ «ЮА Грін»)</h3><p>EDRPOU code 45033277</p><p>Address: Apt. 117, 1 Sadova St., Khroly village, Kharkiv Oblast, Ukraine</p><p>Email: hello@uagreen.co</p><p>Phone: +38 (095) 717 37 72</p>
            """;

        private const string OfferUk = $"""
            <p><em>Чернетка. Текст має перевірити юрист.</em></p>
            <h2>Публічна оферта на надання послуги «Аудит вітру на ділянці»</h2>
            <p>Товариство з обмеженою відповідальністю «ЮА Грін» (далі — «Виконавець») пропонує будь-якій фізичній або юридичній особі (далі — «Замовник») укласти договір про надання послуги на умовах цієї оферти.</p>
            <h3>1. Предмет</h3>
            <p>1.1. Виконавець надає послугу «Аудит вітру на ділянці»: аналіз модельних даних вітру за координатами ділянки, виїзд спеціаліста й огляд майданчика (рельєф, перешкоди, джерела турбулентності), орієнтовний замір вітру на місці, оцінку під'їзних шляхів, найближчої точки приєднання до електромережі та відстані до житла, підбір точки встановлення й рекомендованого типу та висоти турбіни.</p>
            <p>1.2. Результат послуги — письмовий звіт із попереднім прогнозом річного виробітку, рекомендаціями щодо розташування й турбіни та переліком виявлених обмежень.</p>
            <h3>2. Акцепт</h3>
            <p>2.1. Акцептом оферти є оплата послуги на сайті uagreen.co після проставлення позначки «Погоджуюсь з умовами публічної оферти». З моменту оплати договір вважається укладеним.</p>
            <h3>3. Ціна та оплата</h3>
            <p>3.1. Ціна послуги вказана на сайті на момент оплати. Оплата — 100 % передоплата онлайн через платіжний сервіс LiqPay.</p>
            <h3>4. Строк виконання</h3>
            <p>4.1. Дату виїзду Замовник обирає на сайті серед вільних дат. Виконавець підтверджує дату телефоном.</p>
            <p>4.2. Якщо обрана дата стала недоступною або Замовник просить перенести виїзд, Сторони погоджують нову дату за телефоном.</p>
            <p>4.3. Звіт надається Замовнику в електронному вигляді протягом 10 робочих днів після виїзду.</p>
            <h3>5. Обов'язки сторін</h3>
            <p>5.1. Замовник забезпечує доступ спеціаліста до ділянки в погоджений день і надає достовірні дані про її розташування.</p>
            <p>5.2. Виконавець надає послугу в погоджений строк і в обсязі, описаному в розділі 1.</p>
            <h3>6. Повернення коштів</h3>
            <p>6.1. До дати виїзду Замовник може відмовитися від послуги, звернувшись до менеджера Виконавця; кошти повертаються в повному обсязі на картку, з якої здійснено оплату.</p>
            <p>6.2. Після виїзду спеціаліста кошти не повертаються, крім випадків, коли послугу не надано з вини Виконавця.</p>
            <h3>7. Відповідальність</h3>
            <p>7.1. Звіт містить попередню оцінку на основі модельних даних і короткого заміру на місці; фактичний виробіток залежить від погоди та умов експлуатації.</p>
            <h3>8. Персональні дані</h3>
            <p>8.1. Оплачуючи послугу, Замовник погоджується на обробку персональних даних відповідно до <a href="/polityka-konfidentsiynosti">Політики конфіденційності</a>.</p>
            <h3>9. Інші умови</h3>
            <p>9.1. У випадках, не передбачених офертою, Сторони керуються чинним законодавством України. Спори вирішуються шляхом переговорів, а в разі недосягнення згоди — у судовому порядку.</p>
            {Seller}
            """;

        private const string OfferEn = $"""
            <p><em>Draft. To be reviewed by a lawyer.</em></p>
            <h2>Public offer for the "Wind Assessment on the Site" service</h2>
            <p>UA Green LLC (the "Contractor") offers any individual or legal entity (the "Customer") to conclude a service agreement on the terms of this offer.</p>
            <h3>1. Subject</h3>
            <p>1.1. The Contractor provides the "Wind Assessment on the Site" service: analysis of modelled wind data for the site coordinates, an on-site visit and survey (terrain, obstacles, sources of turbulence), a reference wind measurement, assessment of access roads, the nearest grid connection point and the distance to housing, and selection of the installation point and the recommended turbine type and height.</p>
            <p>1.2. The result is a written report with a preliminary annual energy forecast, recommendations on location and turbine, and a list of identified constraints.</p>
            <h3>2. Acceptance</h3>
            <p>2.1. The offer is accepted by paying for the service on uagreen.co after ticking "I accept the terms of the public offer". The agreement is concluded at the moment of payment.</p>
            <h3>3. Price and payment</h3>
            <p>3.1. The price is the one shown on the site at the moment of payment. Payment is 100 % in advance, online via LiqPay.</p>
            <h3>4. Timing</h3>
            <p>4.1. The Customer picks the visit date on the site from the available dates. The Contractor confirms the date by phone.</p>
            <p>4.2. If the chosen date becomes unavailable or the Customer asks to reschedule, the Parties agree on a new date by phone.</p>
            <p>4.3. The report is sent electronically within 10 business days after the visit.</p>
            <h3>5. Obligations</h3>
            <p>5.1. The Customer gives the specialist access to the site on the agreed day and provides accurate location details.</p>
            <p>5.2. The Contractor provides the service on the agreed date and in the scope described in section 1.</p>
            <h3>6. Refunds</h3>
            <p>6.1. Before the visit date the Customer may cancel by contacting the Contractor's manager; the full amount is refunded to the paying card.</p>
            <p>6.2. After the visit no refund is made, unless the service was not provided through the Contractor's fault.</p>
            <h3>7. Liability</h3>
            <p>7.1. The report is a preliminary estimate based on modelled data and a short on-site measurement; actual output depends on weather and operating conditions.</p>
            <h3>8. Personal data</h3>
            <p>8.1. By paying, the Customer agrees to the processing of personal data under the <a href="/en/polityka-konfidentsiynosti">Privacy Policy</a>.</p>
            <h3>9. Other terms</h3>
            <p>9.1. Matters not covered by this offer are governed by the laws of Ukraine. Disputes are settled by negotiation, failing which in court.</p>
            {SellerEn}
            """;

        private static readonly string PrivacyUk = $"""
            <p><em>Чернетка. Текст має перевірити юрист.</em></p>
            <p>Ця політика описує, як ТОВ «ЮА Грін» (далі — «Компанія») обробляє персональні дані відвідувачів сайту uagreen.co відповідно до Закону України «Про захист персональних даних».</p>
            <h3>1. Які дані ми збираємо</h3>
            <p>Ім'я, номер телефону, адресу електронної пошти, адресу ділянки або доставки, текст повідомлень і коментарів, резюме (для відгуків на вакансії), а також технічні дані відвідування (cookies, IP-адреса) для аналітики.</p>
            <h3>2. Навіщо</h3>
            <p>Щоб зв'язатися з вами на ваш запит, виконати замовлення чи послугу (зокрема аудит ділянки), вести облік клієнтів і замовлень, розглянути відгук на вакансію та покращувати сайт.</p>
            <h3>3. Кому ми передаємо дані</h3>
            <p>Платіжному сервісу LiqPay — для проведення оплати; перевізникам — для доставки замовлення; власній обліковій системі Компанії. Ми не продаємо персональні дані третім особам.</p>
            <h3>4. Скільки зберігаємо</h3>
            <p>Стільки, скільки потрібно для мети обробки та виконання вимог законодавства (зокрема бухгалтерського обліку), після чого дані видаляються або знеособлюються.</p>
            <h3>5. Ваші права</h3>
            <p>Ви можете дізнатися, які ваші дані ми обробляємо, вимагати їх виправлення чи видалення, відкликати згоду на обробку, звернувшись на hello@uagreen.co. Ви також маєте право звернутися до Уповноваженого Верховної Ради України з прав людини.</p>
            <h3>6. Згода</h3>
            <p>Надсилаючи форму на сайті або здійснюючи оплату, ви погоджуєтесь з цією політикою.</p>
            {Seller.Replace("ПРОДАВЕЦЬ (ВИКОНАВЕЦЬ):", "ВОЛОДІЛЕЦЬ ПЕРСОНАЛЬНИХ ДАНИХ:")}
            """;

        private static readonly string PrivacyEn = $"""
            <p><em>Draft. To be reviewed by a lawyer.</em></p>
            <p>This policy explains how UA Green LLC (the "Company") processes personal data of uagreen.co visitors under the Law of Ukraine "On Personal Data Protection".</p>
            <h3>1. What we collect</h3>
            <p>Name, phone number, email address, site or delivery address, the text of messages and comments, CVs (for job applications), and technical visit data (cookies, IP address) for analytics.</p>
            <h3>2. Why</h3>
            <p>To contact you on your request, fulfil an order or service (including the site audit), keep customer and order records, review job applications and improve the site.</p>
            <h3>3. Who we share it with</h3>
            <p>The LiqPay payment service, to process payments; carriers, to deliver orders; the Company's own accounting system. We do not sell personal data to third parties.</p>
            <h3>4. How long we keep it</h3>
            <p>As long as the purpose requires and the law (including accounting rules) demands, after which the data is deleted or anonymised.</p>
            <h3>5. Your rights</h3>
            <p>You may ask which of your data we process, have it corrected or deleted, or withdraw consent by writing to hello@uagreen.co. You may also complain to the Ukrainian Parliament Commissioner for Human Rights.</p>
            <h3>6. Consent</h3>
            <p>By submitting a form on the site or paying, you agree to this policy.</p>
            {SellerEn.Replace("SELLER (CONTRACTOR):", "DATA CONTROLLER:")}
            """;

        protected override void Up(MigrationBuilder migrationBuilder)
        {
            Seed(migrationBuilder, "page.offer", "uk", "Публічна оферта", OfferUk);
            Seed(migrationBuilder, "page.offer", "en", "Public offer", OfferEn);
            Seed(migrationBuilder, "page.privacy", "uk", "Політика конфіденційності", PrivacyUk);
            Seed(migrationBuilder, "page.privacy", "en", "Privacy policy", PrivacyEn);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM content WHERE key IN ('page.offer', 'page.privacy');");
        }

        // Dollar quoting: the HTML needs no escaping. The texts contain no "$b$".
        private static void Seed(MigrationBuilder mb, string key, string lang, string title, string html) =>
            mb.Sql($"INSERT INTO content (key, lang, title, body_html, updated_at) " +
                   $"VALUES ('{key}', '{lang}', '{title}', $b${html}$b$, now()) ON CONFLICT (key, lang) DO NOTHING;");
    }
}
