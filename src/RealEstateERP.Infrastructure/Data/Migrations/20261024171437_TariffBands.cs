using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RealEstateERP.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class TariffBands : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "BandFrom",
                table: "SubscriptionPlans",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "BandTo",
                table: "SubscriptionPlans",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsQuoted",
                table: "SubscriptionPlans",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Metric",
                table: "SubscriptionPlans",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BandFrom",
                table: "SubscriptionPlans");

            migrationBuilder.DropColumn(
                name: "BandTo",
                table: "SubscriptionPlans");

            migrationBuilder.DropColumn(
                name: "IsQuoted",
                table: "SubscriptionPlans");

            migrationBuilder.DropColumn(
                name: "Metric",
                table: "SubscriptionPlans");
        }
    }
}
