using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace UaGreen.Site.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PageViews : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "page_views",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    path = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    visitor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    entry = table.Column<bool>(type: "boolean", nullable: false),
                    source = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    country = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_page_views", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_page_views_at",
                table: "page_views",
                column: "at");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "page_views");
        }
    }
}
