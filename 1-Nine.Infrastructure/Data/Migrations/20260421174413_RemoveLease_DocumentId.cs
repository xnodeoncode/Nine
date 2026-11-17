using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nine.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveLease_DocumentId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Leases_Documents_DocumentId",
                table: "Leases");

            migrationBuilder.DropIndex(
                name: "IX_Leases_DocumentId",
                table: "Leases");

            migrationBuilder.DropColumn(
                name: "DocumentId",
                table: "Leases");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "DocumentId",
                table: "Leases",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Leases_DocumentId",
                table: "Leases",
                column: "DocumentId");

            migrationBuilder.AddForeignKey(
                name: "FK_Leases_Documents_DocumentId",
                table: "Leases",
                column: "DocumentId",
                principalTable: "Documents",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
