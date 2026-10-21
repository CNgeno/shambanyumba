using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RealEstateERP.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class Sprint11Automation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ReceiptEmailedAtUtc",
                table: "Payments",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "EmailedAtUtc",
                table: "Invoices",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AutoInvoiceDay",
                table: "Businesses",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "AutoInvoiceEnabled",
                table: "Businesses",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "AutoPenaltiesEnabled",
                table: "Businesses",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "EmailInvoicesToTenants",
                table: "Businesses",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "EmailReceiptsToTenants",
                table: "Businesses",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateOnly>(
                name: "LastAutoInvoiceMonth",
                table: "Businesses",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "LastAutomationRunOn",
                table: "Businesses",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastAutomationSummary",
                table: "Businesses",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaymentInstructions",
                table: "Businesses",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PenaltyGraceDays",
                table: "Businesses",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PenaltyPercent",
                table: "Businesses",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReceiptEmailedAtUtc",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "EmailedAtUtc",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "AutoInvoiceDay",
                table: "Businesses");

            migrationBuilder.DropColumn(
                name: "AutoInvoiceEnabled",
                table: "Businesses");

            migrationBuilder.DropColumn(
                name: "AutoPenaltiesEnabled",
                table: "Businesses");

            migrationBuilder.DropColumn(
                name: "EmailInvoicesToTenants",
                table: "Businesses");

            migrationBuilder.DropColumn(
                name: "EmailReceiptsToTenants",
                table: "Businesses");

            migrationBuilder.DropColumn(
                name: "LastAutoInvoiceMonth",
                table: "Businesses");

            migrationBuilder.DropColumn(
                name: "LastAutomationRunOn",
                table: "Businesses");

            migrationBuilder.DropColumn(
                name: "LastAutomationSummary",
                table: "Businesses");

            migrationBuilder.DropColumn(
                name: "PaymentInstructions",
                table: "Businesses");

            migrationBuilder.DropColumn(
                name: "PenaltyGraceDays",
                table: "Businesses");

            migrationBuilder.DropColumn(
                name: "PenaltyPercent",
                table: "Businesses");
        }
    }
}
