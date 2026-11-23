using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UaGreen.Site.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AdminQuality : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "oberig_monitoring",
                table: "products",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "admin_note",
                table: "orders",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "crm_attempts",
                table: "orders",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "crm_deal_id",
                table: "orders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "crm_error",
                table: "orders",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "crm_last_attempt_at",
                table: "orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "crm_synced_at",
                table: "orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "crm_attempts",
                table: "contact_requests",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "crm_error",
                table: "contact_requests",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "crm_last_attempt_at",
                table: "contact_requests",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "crm_synced_at",
                table: "contact_requests",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "note",
                table: "contact_requests",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true);

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
                defaultValue: "New");

            migrationBuilder.CreateTable(
                name: "order_events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    actor = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    text = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_order_events", x => x.id);
                    table.ForeignKey(
                        name: "fk_order_events_orders_order_id",
                        column: x => x.order_id,
                        principalTable: "orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_order_events_order_id_at",
                table: "order_events",
                columns: new[] { "order_id", "at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "order_events");

            migrationBuilder.DropColumn(
                name: "oberig_monitoring",
                table: "products");

            migrationBuilder.DropColumn(
                name: "admin_note",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "crm_attempts",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "crm_deal_id",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "crm_error",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "crm_last_attempt_at",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "crm_synced_at",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "crm_attempts",
                table: "contact_requests");

            migrationBuilder.DropColumn(
                name: "crm_error",
                table: "contact_requests");

            migrationBuilder.DropColumn(
                name: "crm_last_attempt_at",
                table: "contact_requests");

            migrationBuilder.DropColumn(
                name: "crm_synced_at",
                table: "contact_requests");

            migrationBuilder.DropColumn(
                name: "note",
                table: "contact_requests");

            migrationBuilder.DropColumn(
                name: "note",
                table: "applications");

            migrationBuilder.DropColumn(
                name: "status",
                table: "applications");
        }
    }
}
