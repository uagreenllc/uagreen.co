using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UaGreen.Site.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditBooking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "audit_address",
                table: "orders",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "audit_date",
                table: "orders",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "audit_date_was_free",
                table: "orders",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "gospodar_booked_at",
                table: "orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "offer_accepted_at",
                table: "orders",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "audit_address",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "audit_date",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "audit_date_was_free",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "gospodar_booked_at",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "offer_accepted_at",
                table: "orders");
        }
    }
}
