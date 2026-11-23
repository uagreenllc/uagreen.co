using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UaGreen.Site.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class BlogSeo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "no_follow",
                table: "blog_posts",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "no_index",
                table: "blog_posts",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "seo_keywords_en",
                table: "blog_posts",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "seo_keywords_uk",
                table: "blog_posts",
                type: "text",
                nullable: true);

            // Weblium had one author for the whole blog: it becomes the blog.author setting
            // (edited under Блог → Налаштування), posts keep an author only to override it.
            migrationBuilder.Sql("""
                INSERT INTO settings (key, value)
                SELECT 'blog.author', 'UA GREEN' WHERE EXISTS (SELECT 1 FROM blog_posts WHERE author = 'UA GREEN')
                ON CONFLICT (key) DO NOTHING;
                UPDATE blog_posts SET author = NULL WHERE author = 'UA GREEN';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "no_follow",
                table: "blog_posts");

            migrationBuilder.DropColumn(
                name: "no_index",
                table: "blog_posts");

            migrationBuilder.DropColumn(
                name: "seo_keywords_en",
                table: "blog_posts");

            migrationBuilder.DropColumn(
                name: "seo_keywords_uk",
                table: "blog_posts");
        }
    }
}
