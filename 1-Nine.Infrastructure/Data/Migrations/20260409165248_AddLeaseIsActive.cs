using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nine.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLeaseIsActive : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "Leases",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_Leases_IsActive",
                table: "Leases",
                column: "IsActive");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Leases_IsActive",
                table: "Leases");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "Leases");
        }
    }
}
