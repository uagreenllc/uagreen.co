using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UaGreen.Site.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CategoryDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "description_html_en",
                table: "categories",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "description_html_uk",
                table: "categories",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "name_cs",
                table: "categories",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "seo_desc_en",
                table: "categories",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "seo_desc_uk",
                table: "categories",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "seo_title_en",
                table: "categories",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "seo_title_uk",
                table: "categories",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "description_html_en",
                table: "categories");

            migrationBuilder.DropColumn(
                name: "description_html_uk",
                table: "categories");

            migrationBuilder.DropColumn(
                name: "name_cs",
                table: "categories");

            migrationBuilder.DropColumn(
                name: "seo_desc_en",
                table: "categories");

            migrationBuilder.DropColumn(
                name: "seo_desc_uk",
                table: "categories");

            migrationBuilder.DropColumn(
                name: "seo_title_en",
                table: "categories");

            migrationBuilder.DropColumn(
                name: "seo_title_uk",
                table: "categories");
        }
    }
}
