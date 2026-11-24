using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UaGreen.Site.Api.Infrastructure.Persistence.Migrations
{
    /// Replaces the audit-only offer with one general offer (goods, audit, installation, foundation)
    /// and drops the "draft" note from both legal pages. Owner's decision: overwrites the offer text.
    public partial class GeneralOffer : Migration
    {
        private const string OfferUk = $"""
            <h2>Публічна оферта на продаж товарів і надання послуг</h2>
            <p>Товариство з обмеженою відповідальністю «ЮА Грін», ЄДРПОУ 45033277 (далі — «Продавець» або «Виконавець»), пропонує будь-якій фізичній або юридичній особі, фізичній особі — підприємцю (далі — «Покупець» або «Замовник») укласти договір купівлі-продажу товарів і/або надання послуг на умовах цієї оферти.</p>
            <h3>1. Терміни</h3>
            <p>1.1. «Сайт» — вебсайт https://uagreen.co з усіма його сторінками.</p>
            <p>1.2. «Товар» — обладнання й комплектуючі, представлені на Сайті: вітрогенератори, генератори, лопаті, щогли, контролери тощо.</p>
            <p>1.3. «Послуга» — роботи й послуги Виконавця, зокрема аудит вітру на ділянці, доставка, монтаж обладнання та улаштування (бетонування) фундаменту під щоглу.</p>
            <p>1.4. «Замовлення» — оформлена на Сайті або погоджена з менеджером заявка на Товар чи Послугу.</p>
            <h3>2. Предмет договору</h3>
            <p>2.1. Продавець передає у власність Покупця Товар, а Покупець оплачує й приймає його.</p>
            <p>2.2. Виконавець надає Замовнику Послуги, а Замовник оплачує й приймає їх.</p>
            <p>2.3. Опис, комплектація та ціни Товарів і Послуг наведені на Сайті. Обсяг, строки й вартість робіт на об'єкті Замовника (монтаж, фундамент) Сторони погоджують у рахунку або кошторисі до початку робіт.</p>
            <p>2.4. Зображення на Сайті ілюстративні: зовнішній вигляд Товару може дещо відрізнятися без зміни його характеристик.</p>
            <h3>3. Акцепт і укладення договору</h3>
            <p>3.1. Акцептом оферти є оформлення Замовлення на Сайті (зокрема проставлення позначки «Погоджуюсь з умовами публічної оферти», де вона є) або оплата рахунку Продавця. З цього моменту договір вважається укладеним.</p>
            <p>3.2. Договір є публічним (ст. 633 Цивільного кодексу України): його умови однакові для всіх.</p>
            <p>3.3. Продавець може змінювати оферту, розміщуючи нову редакцію на Сайті. Нова редакція не змінює умов уже оплачених Замовлень.</p>
            <h3>4. Ціна та оплата</h3>
            <p>4.1. Ціни зазначені на Сайті в гривнях. Ціна оплаченого Замовлення не змінюється.</p>
            <p>4.2. Способи оплати: онлайн карткою через LiqPay, безготівковий розрахунок за рахунком, оплата частинами або в кредит через банки-партнери. Умови кожного способу наведені на сторінці <a href="/oplata">«Оплата і доставка»</a>.</p>
            <p>4.3. Аудит вітру оплачується повністю наперед онлайн. Монтаж і фундамент оплачуються за рахунком; розмір авансу й остаточного платежу зазначаються в рахунку або кошторисі.</p>
            <p>4.4. Доставка оплачується за тарифами перевізника, якщо інше не зазначено на Сайті чи в рахунку.</p>
            <h3>5. Доставка й отримання Товару</h3>
            <p>5.1. Товар доставляється по Україні службами «Нова пошта» чи «Укрпошта», транспортом Продавця (якщо він пропонує таку доставку) або видається самовивозом.</p>
            <p>5.2. Продавець обробляє й підтверджує Замовлення протягом 3 робочих днів. Строк відвантаження залежить від наявності Товару; строк виготовлення Товару під замовлення повідомляє менеджер.</p>
            <p>5.3. Під час отримання Покупець перевіряє кількість, комплектність і зовнішній стан Товару в присутності перевізника чи представника Продавця й одразу повідомляє про виявлені недоліки.</p>
            <p>5.4. Ризик випадкового пошкодження Товару переходить до Покупця в момент отримання, право власності — в момент отримання, але не раніше повної оплати.</p>
            <h3>6. Надання послуг</h3>
            <p>6.1. Аудит вітру на ділянці включає аналіз модельних даних вітру за координатами ділянки, виїзд спеціаліста й огляд майданчика, орієнтовний замір вітру на місці, оцінку під'їзних шляхів і точки приєднання до електромережі, підбір місця встановлення, типу й висоти турбіни. Дату виїзду Замовник обирає на Сайті серед вільних дат, Виконавець підтверджує її телефоном. Письмовий звіт надсилається в електронному вигляді протягом 10 робочих днів після виїзду.</p>
            <p>6.2. Звіт аудиту містить попередню оцінку; фактичний виробіток залежить від погоди та умов експлуатації.</p>
            <p>6.3. Монтаж обладнання та улаштування (бетонування) фундаменту виконуються на об'єкті Замовника за погодженим кошторисом. Щогла встановлюється на фундамент після того, як бетон набере потрібну міцність; цей час входить у строк робіт.</p>
            <p>6.4. Замовник забезпечує доступ до ділянки й під'їзд техніки в погоджені дні, надає достовірні дані про ділянку й відповідає за наявність погоджень на розміщення обладнання на ній, якщо Сторони не домовилися інакше.</p>
            <p>6.5. Якщо погоджена дата стала недоступною для однієї зі Сторін, Сторони погоджують нову дату.</p>
            <p>6.6. Виконані роботи з монтажу й фундаменту Сторони оформлюють актом. Зауваження до робіт Замовник зазначає в акті або надсилає Виконавцю письмово.</p>
            <h3>7. Гарантія</h3>
            <p>7.1. Гарантійний строк на Товар — 12 місяців з дня отримання, на виконані роботи — 12 місяців з дня підписання акта, якщо в документах на Товар чи в кошторисі не зазначено інший строк.</p>
            <p>7.2. Гарантія діє за умови монтажу й експлуатації відповідно до інструкції. Вона не поширюється на витратні матеріали, пошкодження через неправильну експлуатацію, ремонт чи зміни без погодження з Продавцем, а також на наслідки стихійних явищ поза межами, на які розраховане обладнання.</p>
            <p>7.3. Про несправність Покупець повідомляє Продавця якомога швидше. Продавець організовує перевірку й у гарантійному випадку ремонтує або замінює Товар чи усуває недоліки робіт за власний рахунок.</p>
            <h3>8. Повернення та відмова від договору</h3>
            <p>8.1. Покупець-споживач може повернути Товар належної якості протягом 14 днів з дня отримання, якщо Товар не використовувався, збережені його товарний вигляд, комплектність, упаковка й документи. Доставку при поверненні оплачує Покупець. Товар, виготовлений або укомплектований під індивідуальне замовлення, поверненню не підлягає.</p>
            <p>8.2. Якщо Товар має недоліки, Покупець має права, передбачені Законом України «Про захист прав споживачів».</p>
            <p>8.3. Від аудиту Замовник може відмовитися до дати виїзду — кошти повертаються в повному обсязі. Після виїзду кошти не повертаються, крім випадків, коли послугу не надано з вини Виконавця.</p>
            <p>8.4. Від монтажу чи фундаменту Замовник може відмовитися до завершення робіт, оплативши вже виконану частину та придбані під його об'єкт матеріали; решта передоплати повертається.</p>
            <p>8.5. Кошти повертаються тим самим способом, яким здійснено оплату, протягом 7 робочих днів після погодження повернення.</p>
            <h3>9. Відповідальність</h3>
            <p>9.1. Сторони відповідають за невиконання договору відповідно до законодавства України.</p>
            <p>9.2. Продавець не відповідає за наслідки надання Покупцем недостовірних даних.</p>
            <p>9.3. Сторони звільняються від відповідальності, якщо виконання договору унеможливили обставини непереборної сили: воєнні дії, стихійні лиха, рішення органів влади тощо. Сторона, для якої вони настали, повідомляє про це іншу Сторону.</p>
            <h3>10. Персональні дані</h3>
            <p>10.1. Оформлюючи Замовлення, Покупець погоджується на обробку персональних даних відповідно до <a href="/polityka-konfidentsiynosti">Політики конфіденційності</a>.</p>
            <h3>11. Інші умови</h3>
            <p>11.1. У питаннях, не врегульованих офертою, Сторони керуються законодавством України. Спори вирішуються шляхом переговорів, а в разі недосягнення згоди — у суді.</p>
            {AddLegalPages.Seller}
            """;

        private const string OfferEn = $"""
            <h2>Public offer for the sale of goods and provision of services</h2>
            <p>UA Green LLC, EDRPOU code 45033277 (the "Seller" or "Contractor"), offers any individual, legal entity or sole proprietor (the "Buyer" or "Customer") to conclude a sale and/or service agreement on the terms of this offer.</p>
            <h3>1. Definitions</h3>
            <p>1.1. "Site" means the website https://uagreen.co and all its pages.</p>
            <p>1.2. "Goods" means the equipment and components presented on the Site: wind turbines, generators, blades, masts, controllers, etc.</p>
            <p>1.3. "Services" means the Contractor's works and services, including the wind assessment on the site, delivery, equipment installation and building (concreting) the mast foundation.</p>
            <p>1.4. "Order" means a request for Goods or Services placed on the Site or agreed with a manager.</p>
            <h3>2. Subject</h3>
            <p>2.1. The Seller transfers the Goods into the Buyer's ownership, and the Buyer pays for and accepts them.</p>
            <p>2.2. The Contractor provides the Services, and the Customer pays for and accepts them.</p>
            <p>2.3. Descriptions, contents and prices of Goods and Services are given on the Site. The scope, timing and cost of works at the Customer's site (installation, foundation) are agreed in an invoice or estimate before the works start.</p>
            <p>2.4. Images on the Site are illustrative: the Goods may look slightly different without any change in specifications.</p>
            <h3>3. Acceptance</h3>
            <p>3.1. The offer is accepted by placing an Order on the Site (including ticking "I accept the terms of the public offer" where shown) or by paying the Seller's invoice. The agreement is concluded at that moment.</p>
            <p>3.2. The agreement is public (Art. 633 of the Civil Code of Ukraine): its terms are the same for everyone.</p>
            <p>3.3. The Seller may amend the offer by publishing a new version on the Site. A new version does not change the terms of Orders already paid.</p>
            <h3>4. Price and payment</h3>
            <p>4.1. Prices are shown on the Site in hryvnias. The price of a paid Order does not change.</p>
            <p>4.2. Payment methods: online by card via LiqPay, bank transfer against an invoice, instalments or credit through partner banks. The terms of each method are on the <a href="/en/oplata">Payment and delivery</a> page.</p>
            <p>4.3. The wind assessment is paid 100 % in advance online. Installation and foundation works are paid against an invoice; the advance and final payment are stated in the invoice or estimate.</p>
            <p>4.4. Delivery is charged at the carrier's rates unless stated otherwise on the Site or in the invoice.</p>
            <h3>5. Delivery and receipt of Goods</h3>
            <p>5.1. Goods are delivered across Ukraine by Nova Poshta or Ukrposhta, by the Seller's own transport (where offered), or collected by the Buyer.</p>
            <p>5.2. The Seller processes and confirms an Order within 3 business days. Shipping time depends on stock; for made-to-order Goods the manager states the lead time.</p>
            <p>5.3. On receipt the Buyer checks the quantity, completeness and condition of the Goods in the presence of the carrier or the Seller's representative and reports any defects immediately.</p>
            <p>5.4. The risk of accidental damage passes to the Buyer on receipt; ownership passes on receipt but not before full payment.</p>
            <h3>6. Services</h3>
            <p>6.1. The wind assessment includes analysis of modelled wind data for the site coordinates, an on-site visit and survey, a reference wind measurement, assessment of access roads and the grid connection point, and selection of the installation point and the turbine type and height. The Customer picks the visit date on the Site from the available dates; the Contractor confirms it by phone. A written report is sent electronically within 10 business days after the visit.</p>
            <p>6.2. The assessment report is a preliminary estimate; actual output depends on weather and operating conditions.</p>
            <p>6.3. Equipment installation and building (concreting) the foundation are carried out at the Customer's site under an agreed estimate. The mast is mounted once the concrete has reached the required strength; this time is part of the work schedule.</p>
            <p>6.4. The Customer provides access to the site and for machinery on the agreed days, gives accurate site details and is responsible for any approvals needed to place the equipment there, unless the Parties agree otherwise.</p>
            <p>6.5. If an agreed date becomes unavailable for either Party, the Parties agree on a new date.</p>
            <p>6.6. Completed installation and foundation works are documented in an acceptance certificate. The Customer states any remarks in the certificate or sends them to the Contractor in writing.</p>
            <h3>7. Warranty</h3>
            <p>7.1. The warranty is 12 months from receipt for Goods and 12 months from signing the acceptance certificate for works, unless the product documents or the estimate state otherwise.</p>
            <p>7.2. The warranty requires installation and operation according to the manual. It does not cover consumables, damage from improper use, repairs or modifications not agreed with the Seller, or effects of natural events beyond the equipment's design limits.</p>
            <p>7.3. The Buyer reports a fault to the Seller as soon as possible. The Seller arranges an inspection and, in a warranty case, repairs or replaces the Goods or remedies the works at its own cost.</p>
            <h3>8. Returns and cancellation</h3>
            <p>8.1. A consumer Buyer may return Goods of proper quality within 14 days of receipt if they are unused and their appearance, completeness, packaging and documents are preserved. Return shipping is paid by the Buyer. Goods made or configured to the Buyer's individual order cannot be returned.</p>
            <p>8.2. If the Goods are defective, the Buyer has the rights provided by the Law of Ukraine "On Consumer Rights Protection".</p>
            <p>8.3. The Customer may cancel the wind assessment before the visit date for a full refund. After the visit no refund is made, unless the service was not provided through the Contractor's fault.</p>
            <p>8.4. The Customer may cancel installation or foundation works before they are completed by paying for the part already done and the materials bought for the Customer's site; the rest of the prepayment is refunded.</p>
            <p>8.5. Refunds are made by the original payment method within 7 business days after the refund is agreed.</p>
            <h3>9. Liability</h3>
            <p>9.1. The Parties are liable for non-performance under the laws of Ukraine.</p>
            <p>9.2. The Seller is not liable for consequences of inaccurate data provided by the Buyer.</p>
            <p>9.3. The Parties are released from liability where performance is prevented by force majeure: hostilities, natural disasters, decisions of public authorities, etc. The affected Party notifies the other Party.</p>
            <h3>10. Personal data</h3>
            <p>10.1. By placing an Order, the Buyer agrees to the processing of personal data under the <a href="/en/polityka-konfidentsiynosti">Privacy Policy</a>.</p>
            <h3>11. Other terms</h3>
            <p>11.1. Matters not covered by this offer are governed by the laws of Ukraine. Disputes are settled by negotiation, failing which in court.</p>
            {AddLegalPages.SellerEn}
            """;

        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Dollar quoting: the HTML needs no escaping. The texts contain no "$b$".
            migrationBuilder.Sql($"UPDATE content SET body_html = $b${OfferUk}$b$, updated_at = now() WHERE key = 'page.offer' AND lang = 'uk';");
            migrationBuilder.Sql($"UPDATE content SET body_html = $b${OfferEn}$b$, updated_at = now() WHERE key = 'page.offer' AND lang = 'en';");
            migrationBuilder.Sql("""
                UPDATE content SET body_html = regexp_replace(body_html,
                    '<p>\s*<em>\s*(Чернетка\. Текст має перевірити юрист\.|Draft\. To be reviewed by a lawyer\.)\s*</em>\s*</p>\s*', ''),
                    updated_at = now()
                WHERE key = 'page.privacy';
                """);
        }

        // ponytail: text-only migration, nothing to undo.
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
