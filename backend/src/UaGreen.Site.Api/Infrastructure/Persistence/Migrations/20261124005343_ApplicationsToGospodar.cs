using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UaGreen.Site.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ApplicationsToGospodar : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "is_read",
                table: "applications");

            migrationBuilder.DropColumn(
                name: "note",
                table: "applications");

            migrationBuilder.DropColumn(
                name: "status",
                table: "applications");

            migrationBuilder.AddColumn<Guid>(
                name: "gospodar_vacancy_id",
                table: "vacancies",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "gospodar_attempts",
                table: "applications",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "gospodar_candidate_id",
                table: "applications",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "gospodar_error",
                table: "applications",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "gospodar_last_attempt_at",
                table: "applications",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "gospodar_synced_at",
                table: "applications",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "gospodar_vacancy_id",
                table: "vacancies");

            migrationBuilder.DropColumn(
                name: "gospodar_attempts",
                table: "applications");

            migrationBuilder.DropColumn(
                name: "gospodar_candidate_id",
                table: "applications");

            migrationBuilder.DropColumn(
                name: "gospodar_error",
                table: "applications");

            migrationBuilder.DropColumn(
                name: "gospodar_last_attempt_at",
                table: "applications");

            migrationBuilder.DropColumn(
                name: "gospodar_synced_at",
                table: "applications");

            migrationBuilder.AddColumn<bool>(
                name: "is_read",
                table: "applications",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "note",
                table: "applications",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "status",
                table: "applications",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "");
        }
    }
}
