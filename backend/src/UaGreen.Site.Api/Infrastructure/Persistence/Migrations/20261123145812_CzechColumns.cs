using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UaGreen.Site.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CzechColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "city_cs",
                table: "vacancies",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "conditions_html_cs",
                table: "vacancies",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "department_cs",
                table: "vacancies",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "employment_type_cs",
                table: "vacancies",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "experience_cs",
                table: "vacancies",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "language_cs",
                table: "vacancies",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "nice_to_have_html_cs",
                table: "vacancies",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "requirements_html_cs",
                table: "vacancies",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "title_cs",
                table: "vacancies",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "author_cs",
                table: "reviews",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "text_cs",
                table: "reviews",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "description_html_cs",
                table: "products",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "faq_html_cs",
                table: "products",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "name_cs",
                table: "products",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "seo_desc_cs",
                table: "products",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "seo_title_cs",
                table: "products",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "short_desc_cs",
                table: "products",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "specs_html_cs",
                table: "products",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "name_cs",
                table: "product_options",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "value_cs",
                table: "product_option_values",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "body_html_cs",
                table: "blog_posts",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "excerpt_cs",
                table: "blog_posts",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "seo_desc_cs",
                table: "blog_posts",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "seo_keywords_cs",
                table: "blog_posts",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "seo_title_cs",
                table: "blog_posts",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "title_cs",
                table: "blog_posts",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "city_cs",
                table: "vacancies");

            migrationBuilder.DropColumn(
                name: "conditions_html_cs",
                table: "vacancies");

            migrationBuilder.DropColumn(
                name: "department_cs",
                table: "vacancies");

            migrationBuilder.DropColumn(
                name: "employment_type_cs",
                table: "vacancies");

            migrationBuilder.DropColumn(
                name: "experience_cs",
                table: "vacancies");

            migrationBuilder.DropColumn(
                name: "language_cs",
                table: "vacancies");

            migrationBuilder.DropColumn(
                name: "nice_to_have_html_cs",
                table: "vacancies");

            migrationBuilder.DropColumn(
                name: "requirements_html_cs",
                table: "vacancies");

            migrationBuilder.DropColumn(
                name: "title_cs",
                table: "vacancies");

            migrationBuilder.DropColumn(
                name: "author_cs",
                table: "reviews");

            migrationBuilder.DropColumn(
                name: "text_cs",
                table: "reviews");

            migrationBuilder.DropColumn(
                name: "description_html_cs",
                table: "products");

            migrationBuilder.DropColumn(
                name: "faq_html_cs",
                table: "products");

            migrationBuilder.DropColumn(
                name: "name_cs",
                table: "products");

            migrationBuilder.DropColumn(
                name: "seo_desc_cs",
                table: "products");

            migrationBuilder.DropColumn(
                name: "seo_title_cs",
                table: "products");

            migrationBuilder.DropColumn(
                name: "short_desc_cs",
                table: "products");

            migrationBuilder.DropColumn(
                name: "specs_html_cs",
                table: "products");

            migrationBuilder.DropColumn(
                name: "name_cs",
                table: "product_options");

            migrationBuilder.DropColumn(
                name: "value_cs",
                table: "product_option_values");

            migrationBuilder.DropColumn(
                name: "body_html_cs",
                table: "blog_posts");

            migrationBuilder.DropColumn(
                name: "excerpt_cs",
                table: "blog_posts");

            migrationBuilder.DropColumn(
                name: "seo_desc_cs",
                table: "blog_posts");

            migrationBuilder.DropColumn(
                name: "seo_keywords_cs",
                table: "blog_posts");

            migrationBuilder.DropColumn(
                name: "seo_title_cs",
                table: "blog_posts");

            migrationBuilder.DropColumn(
                name: "title_cs",
                table: "blog_posts");
        }
    }
}
