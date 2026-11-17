using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nine.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPropertyExpense_EndReason : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EndReason",
                table: "PropertyExpenses",
                type: "TEXT",
                maxLength: 50,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EndReason",
                table: "PropertyExpenses");
        }
    }
}
