using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nine.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RenameIsAvailableToIsActive : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // SQLite 3.25.0+ supports RENAME COLUMN directly.
            // EF Core's RenameColumn abstraction triggers a full table-rebuild path
            // that throws NotSupportedException on SQLite, so we use raw SQL instead.
            migrationBuilder.Sql("ALTER TABLE \"Properties\" RENAME COLUMN \"IsAvailable\" TO \"IsActive\";");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE \"Properties\" RENAME COLUMN \"IsActive\" TO \"IsAvailable\";");
        }
    }
}
