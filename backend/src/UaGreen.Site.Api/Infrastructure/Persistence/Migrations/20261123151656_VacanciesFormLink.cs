using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UaGreen.Site.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class VacanciesFormLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // /vacancies linked "Не знайшли підходящої вакансії?" to a Google Form; the page now
            // has its own button + form (ApplicationForm), so drop that link paragraph (uk and en).
            migrationBuilder.Sql("""
                UPDATE content
                SET body_html = regexp_replace(body_html, '<p>\s*<a href="https://forms\.gle/[^"]*">[^<]*</a>\s*</p>', '', 'g')
                WHERE key = 'page.vacancies';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
