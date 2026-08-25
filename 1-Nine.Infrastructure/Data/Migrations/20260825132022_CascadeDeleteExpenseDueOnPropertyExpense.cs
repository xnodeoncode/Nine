using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nine.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class CascadeDeleteExpenseDueOnPropertyExpense : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ExpenseDues_PropertyExpenses_PropertyExpenseId",
                table: "ExpenseDues");

            migrationBuilder.AddForeignKey(
                name: "FK_ExpenseDues_PropertyExpenses_PropertyExpenseId",
                table: "ExpenseDues",
                column: "PropertyExpenseId",
                principalTable: "PropertyExpenses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ExpenseDues_PropertyExpenses_PropertyExpenseId",
                table: "ExpenseDues");

            migrationBuilder.AddForeignKey(
                name: "FK_ExpenseDues_PropertyExpenses_PropertyExpenseId",
                table: "ExpenseDues",
                column: "PropertyExpenseId",
                principalTable: "PropertyExpenses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
