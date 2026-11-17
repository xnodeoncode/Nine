using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nine.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddRepairIdToDocument : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "RepairId",
                table: "Documents",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Documents_RepairId",
                table: "Documents",
                column: "RepairId");

            migrationBuilder.AddForeignKey(
                name: "FK_Documents_Repairs_RepairId",
                table: "Documents",
                column: "RepairId",
                principalTable: "Repairs",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Documents_Repairs_RepairId",
                table: "Documents");

            migrationBuilder.DropIndex(
                name: "IX_Documents_RepairId",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "RepairId",
                table: "Documents");
        }
    }
}
