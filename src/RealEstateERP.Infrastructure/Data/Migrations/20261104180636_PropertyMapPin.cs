using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RealEstateERP.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class PropertyMapPin : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "Latitude",
                table: "Assets",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Longitude",
                table: "Assets",
                type: "float",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Latitude",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "Longitude",
                table: "Assets");
        }
    }
}
