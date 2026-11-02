using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RealEstateERP.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class StaysModification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MonthlyDiscountPercent",
                schema: "booking",
                table: "StayUnits");

            migrationBuilder.DropColumn(
                name: "WeeklyDiscountPercent",
                schema: "booking",
                table: "StayUnits");

            migrationBuilder.AddColumn<int>(
                name: "MaxNights",
                schema: "booking",
                table: "StayUnits",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "StayDiscounts",
                schema: "booking",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StayUnitId = table.Column<int>(type: "int", nullable: false),
                    MinNights = table.Column<int>(type: "int", nullable: false),
                    Percent = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                    BusinessId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StayDiscounts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StayDiscounts_Businesses_BusinessId",
                        column: x => x.BusinessId,
                        principalTable: "Businesses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StayDiscounts_StayUnits_StayUnitId",
                        column: x => x.StayUnitId,
                        principalSchema: "booking",
                        principalTable: "StayUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StayDiscounts_BusinessId",
                schema: "booking",
                table: "StayDiscounts",
                column: "BusinessId");

            migrationBuilder.CreateIndex(
                name: "IX_StayDiscounts_StayUnitId",
                schema: "booking",
                table: "StayDiscounts",
                column: "StayUnitId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StayDiscounts",
                schema: "booking");

            migrationBuilder.DropColumn(
                name: "MaxNights",
                schema: "booking",
                table: "StayUnits");

            migrationBuilder.AddColumn<int>(
                name: "MonthlyDiscountPercent",
                schema: "booking",
                table: "StayUnits",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "WeeklyDiscountPercent",
                schema: "booking",
                table: "StayUnits",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }
    }
}
