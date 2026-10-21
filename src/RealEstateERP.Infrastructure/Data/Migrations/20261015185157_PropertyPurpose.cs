using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RealEstateERP.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class PropertyPurpose : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IncludesAgency",
                table: "SubscriptionPlans",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Purpose",
                table: "Assets",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IncludesAgency",
                table: "SubscriptionPlans");

            migrationBuilder.DropColumn(
                name: "Purpose",
                table: "Assets");
        }
    }
}
