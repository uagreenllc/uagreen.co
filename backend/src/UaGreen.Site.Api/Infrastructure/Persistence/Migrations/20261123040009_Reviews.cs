using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UaGreen.Site.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Reviews : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "reviews",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    photo_key = table.Column<string>(type: "text", nullable: true),
                    text_uk = table.Column<string>(type: "text", nullable: false),
                    text_en = table.Column<string>(type: "text", nullable: true),
                    author_uk = table.Column<string>(type: "text", nullable: false),
                    author_en = table.Column<string>(type: "text", nullable: true),
                    published = table.Column<bool>(type: "boolean", nullable: false),
                    sort = table.Column<int>(type: "integer", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_reviews", x => x.id);
                });

            // The two testimonials from the old `home.testimonials` HTML block (identical on staging
            // and in migration/content.json) become rows; the block itself is no longer rendered.
            migrationBuilder.InsertData(
                table: "reviews",
                columns: ["id", "photo_key", "text_uk", "text_en", "author_uk", "author_en", "published", "sort", "updated_at"],
                values: new object[,]
                {
                    {
                        new Guid("3f0c8a52-6d1e-4b7a-9c2f-1a5e7d9b0c11"), "642bdd1bf08dbf000dfe2403.jpg",
                        "Я була приємно здивована таким зручним процесом замовлення. Доставка нашої вітроелектростанції на 1 кВт була вчасною. Усі задоволені ціною та якістю нашої нової енергетичної системи. Дякуємо вам!",
                        "I was pleasantly surprised by how easy the ordering process was. Our 1 kW wind power system was delivered on time. We are very satisfied with the price and quality of our new energy system. Thank you!",
                        "Анна", "Anna", true, 0, new DateTime(2026, 11, 23, 0, 0, 0, DateTimeKind.Utc),
                    },
                    {
                        new Guid("8b4d2e17-0a9c-4f3b-b6e8-5c7a1d2f3e22"), "6a3a72b89cc6e9c73c4258d4.jpg",
                        "Знайшов компанію в TikTok і звернувся за консультацією щодо вітрогенерації. Мені детально пояснили всі нюанси та допомогли обрати оптимальний вітрогенератор для приватного будинку. Робота команди залишила лише позитивні враження — професійно, якісно та без зайвих обіцянок. Рекомендую всім, хто шукає надійне рішення для енергонезалежності.",
                        "I found the company on TikTok and reached out for advice on wind power generation. They explained all the details clearly and helped me choose the right wind turbine for my private home. I had a great experience with the team — professional, high-quality service, and no empty promises. I highly recommend them to anyone looking for a reliable energy independence solution.",
                        "Валерій, Кременчук", "Valeriy, Kremenchuk", true, 1, new DateTime(2026, 11, 23, 0, 0, 0, DateTimeKind.Utc),
                    },
                });
            migrationBuilder.Sql("DELETE FROM content WHERE key = 'home.testimonials';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "reviews");
        }
    }
}
