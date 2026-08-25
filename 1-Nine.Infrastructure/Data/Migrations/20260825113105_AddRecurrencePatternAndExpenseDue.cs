using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nine.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddRecurrencePatternAndExpenseDue : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Frequency",
                table: "PropertyExpenses",
                newName: "RecurrenceFrequency");

            migrationBuilder.AddColumn<int>(
                name: "RecurrenceAnchorMonth",
                table: "PropertyExpenses",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RecurrenceDueDayOfMonth",
                table: "PropertyExpenses",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ExpenseDues",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    PropertyExpenseId = table.Column<Guid>(type: "TEXT", nullable: false),
                    PropertyId = table.Column<Guid>(type: "TEXT", nullable: false),
                    DueDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    AmountDue = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    WaivedOn = table.Column<DateTime>(type: "TEXT", nullable: true),
                    WaivedReason = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    OrganizationId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    LastModifiedOn = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsSampleData = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsArchived = table.Column<bool>(type: "INTEGER", nullable: false),
                    ArchivedOn = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ArchivedBy = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExpenseDues", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExpenseDues_PropertyExpenses_PropertyExpenseId",
                        column: x => x.PropertyExpenseId,
                        principalTable: "PropertyExpenses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ExpensePayments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ExpenseDueId = table.Column<Guid>(type: "TEXT", nullable: false),
                    AmountPaid = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PaidOn = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    OrganizationId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    LastModifiedOn = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsSampleData = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsArchived = table.Column<bool>(type: "INTEGER", nullable: false),
                    ArchivedOn = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ArchivedBy = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExpensePayments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExpensePayments_ExpenseDues_ExpenseDueId",
                        column: x => x.ExpenseDueId,
                        principalTable: "ExpenseDues",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ExpenseDues_DueDate",
                table: "ExpenseDues",
                column: "DueDate");

            migrationBuilder.CreateIndex(
                name: "IX_ExpenseDues_OrganizationId",
                table: "ExpenseDues",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_ExpenseDues_PropertyExpenseId",
                table: "ExpenseDues",
                column: "PropertyExpenseId");

            migrationBuilder.CreateIndex(
                name: "IX_ExpenseDues_PropertyId",
                table: "ExpenseDues",
                column: "PropertyId");

            migrationBuilder.CreateIndex(
                name: "IX_ExpensePayments_ExpenseDueId",
                table: "ExpensePayments",
                column: "ExpenseDueId");

            migrationBuilder.CreateIndex(
                name: "IX_ExpensePayments_OrganizationId",
                table: "ExpensePayments",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_ExpensePayments_PaidOn",
                table: "ExpensePayments",
                column: "PaidOn");

            // Data migration: pre-existing PropertyExpense rows had no due date
            // captured (only a Frequency). Backfill RecurrenceDueDayOfMonth /
            // RecurrenceAnchorMonth from EffectiveDate's day/month as a reasonable
            // default so the now-mandatory recurrence fields aren't left null for
            // historical rows. New rows going forward always get an explicit
            // user-selected due date instead (see PropertyExpenseService.CreateExpenseAsync).
            migrationBuilder.Sql(@"
                UPDATE PropertyExpenses
                SET RecurrenceDueDayOfMonth = CAST(strftime('%d', EffectiveDate) AS INTEGER)
                WHERE RecurrenceFrequency <> 'One-Time'
                  AND RecurrenceDueDayOfMonth IS NULL;

                UPDATE PropertyExpenses
                SET RecurrenceAnchorMonth = CAST(strftime('%m', EffectiveDate) AS INTEGER)
                WHERE RecurrenceFrequency IN ('Quarterly', 'Annual')
                  AND RecurrenceAnchorMonth IS NULL;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ExpensePayments");

            migrationBuilder.DropTable(
                name: "ExpenseDues");

            migrationBuilder.DropColumn(
                name: "RecurrenceAnchorMonth",
                table: "PropertyExpenses");

            migrationBuilder.DropColumn(
                name: "RecurrenceDueDayOfMonth",
                table: "PropertyExpenses");

            migrationBuilder.RenameColumn(
                name: "RecurrenceFrequency",
                table: "PropertyExpenses",
                newName: "Frequency");
        }
    }
}
