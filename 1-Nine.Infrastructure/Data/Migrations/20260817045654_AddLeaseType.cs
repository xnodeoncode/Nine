using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nine.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddLeaseType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LeaseType",
                table: "Leases",
                type: "TEXT",
                maxLength: 50,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LeaseType",
                table: "Leases");
        }
    }
}
