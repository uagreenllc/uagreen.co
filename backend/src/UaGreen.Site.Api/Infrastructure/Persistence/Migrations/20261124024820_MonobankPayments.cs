using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UaGreen.Site.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MonobankPayments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Keep the old LiqPay statuses: rename, not drop + add.
            migrationBuilder.RenameColumn(
                name: "liq_pay_status",
                table: "orders",
                newName: "payment_status");

            migrationBuilder.AlterColumn<string>(
                name: "payment_status",
                table: "orders",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "pay_url",
                table: "orders",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "payment_checked_at",
                table: "orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "payment_ref",
                table: "orders",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_orders_payment_ref",
                table: "orders",
                column: "payment_ref");

            migrationBuilder.Sql("UPDATE orders SET payment_method = 'Card' WHERE payment_method = 'LiqPay';");
            migrationBuilder.Sql("UPDATE settings SET key = 'pay_Card' WHERE key = 'pay_LiqPay';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE settings SET key = 'pay_LiqPay' WHERE key = 'pay_Card';");
            migrationBuilder.Sql("UPDATE orders SET payment_method = 'LiqPay' WHERE payment_method = 'Card';");

            migrationBuilder.DropIndex(
                name: "ix_orders_payment_ref",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "pay_url",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "payment_checked_at",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "payment_ref",
                table: "orders");

            migrationBuilder.AlterColumn<string>(
                name: "payment_status",
                table: "orders",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64,
                oldNullable: true);

            migrationBuilder.RenameColumn(
                name: "payment_status",
                table: "orders",
                newName: "liq_pay_status");
        }
    }
}
