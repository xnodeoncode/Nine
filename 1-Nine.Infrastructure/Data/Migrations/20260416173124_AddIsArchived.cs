using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nine.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddIsArchived : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ArchivedBy",
                table: "WorkflowAuditLogs",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ArchivedOn",
                table: "WorkflowAuditLogs",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsArchived",
                table: "WorkflowAuditLogs",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ArchivedBy",
                table: "UserProfiles",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ArchivedOn",
                table: "UserProfiles",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsArchived",
                table: "UserProfiles",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ArchivedBy",
                table: "Tours",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ArchivedOn",
                table: "Tours",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsArchived",
                table: "Tours",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ArchivedBy",
                table: "Tenants",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ArchivedOn",
                table: "Tenants",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsArchived",
                table: "Tenants",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ArchivedBy",
                table: "SecurityDeposits",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ArchivedOn",
                table: "SecurityDeposits",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsArchived",
                table: "SecurityDeposits",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ArchivedBy",
                table: "SecurityDepositInvestmentPools",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ArchivedOn",
                table: "SecurityDepositInvestmentPools",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsArchived",
                table: "SecurityDepositInvestmentPools",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ArchivedBy",
                table: "SecurityDepositDividends",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ArchivedOn",
                table: "SecurityDepositDividends",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsArchived",
                table: "SecurityDepositDividends",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ArchivedBy",
                table: "Repairs",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ArchivedOn",
                table: "Repairs",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsArchived",
                table: "Repairs",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ArchivedBy",
                table: "RentalApplications",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ArchivedOn",
                table: "RentalApplications",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsArchived",
                table: "RentalApplications",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ArchivedBy",
                table: "ProspectiveTenants",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ArchivedOn",
                table: "ProspectiveTenants",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsArchived",
                table: "ProspectiveTenants",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ArchivedBy",
                table: "Properties",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ArchivedOn",
                table: "Properties",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsArchived",
                table: "Properties",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ArchivedBy",
                table: "Payments",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ArchivedOn",
                table: "Payments",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsArchived",
                table: "Payments",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ArchivedBy",
                table: "OrganizationSMSSettings",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ArchivedOn",
                table: "OrganizationSMSSettings",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsArchived",
                table: "OrganizationSMSSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ArchivedBy",
                table: "OrganizationSettings",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ArchivedOn",
                table: "OrganizationSettings",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsArchived",
                table: "OrganizationSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ArchivedBy",
                table: "OrganizationEmailSettings",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ArchivedOn",
                table: "OrganizationEmailSettings",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsArchived",
                table: "OrganizationEmailSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ArchivedBy",
                table: "Notifications",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ArchivedOn",
                table: "Notifications",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsArchived",
                table: "Notifications",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ArchivedBy",
                table: "NotificationPreferences",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ArchivedOn",
                table: "NotificationPreferences",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsArchived",
                table: "NotificationPreferences",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ArchivedBy",
                table: "Notes",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ArchivedOn",
                table: "Notes",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsArchived",
                table: "Notes",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ArchivedBy",
                table: "MaintenanceRequests",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ArchivedOn",
                table: "MaintenanceRequests",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsArchived",
                table: "MaintenanceRequests",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ArchivedBy",
                table: "Leases",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ArchivedOn",
                table: "Leases",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsArchived",
                table: "Leases",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ArchivedBy",
                table: "LeaseOffers",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ArchivedOn",
                table: "LeaseOffers",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsArchived",
                table: "LeaseOffers",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ArchivedBy",
                table: "Invoices",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ArchivedOn",
                table: "Invoices",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsArchived",
                table: "Invoices",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ArchivedBy",
                table: "Inspections",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ArchivedOn",
                table: "Inspections",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsArchived",
                table: "Inspections",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ArchivedBy",
                table: "Documents",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ArchivedOn",
                table: "Documents",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsArchived",
                table: "Documents",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ArchivedBy",
                table: "ChecklistTemplates",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ArchivedOn",
                table: "ChecklistTemplates",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsArchived",
                table: "ChecklistTemplates",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ArchivedBy",
                table: "ChecklistTemplateItems",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ArchivedOn",
                table: "ChecklistTemplateItems",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsArchived",
                table: "ChecklistTemplateItems",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ArchivedBy",
                table: "Checklists",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ArchivedOn",
                table: "Checklists",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsArchived",
                table: "Checklists",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ArchivedBy",
                table: "ChecklistItems",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ArchivedOn",
                table: "ChecklistItems",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsArchived",
                table: "ChecklistItems",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ArchivedBy",
                table: "CalendarSettings",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ArchivedOn",
                table: "CalendarSettings",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsArchived",
                table: "CalendarSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ArchivedBy",
                table: "CalendarEvents",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ArchivedOn",
                table: "CalendarEvents",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsArchived",
                table: "CalendarEvents",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ArchivedBy",
                table: "ApplicationScreenings",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ArchivedOn",
                table: "ApplicationScreenings",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsArchived",
                table: "ApplicationScreenings",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.UpdateData(
                table: "ChecklistTemplateItems",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0002-000000000001"),
                columns: new[] { "ArchivedBy", "ArchivedOn", "IsArchived" },
                values: new object[] { null, null, false });

            migrationBuilder.UpdateData(
                table: "ChecklistTemplateItems",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0002-000000000002"),
                columns: new[] { "ArchivedBy", "ArchivedOn", "IsArchived" },
                values: new object[] { null, null, false });

            migrationBuilder.UpdateData(
                table: "ChecklistTemplateItems",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0002-000000000003"),
                columns: new[] { "ArchivedBy", "ArchivedOn", "IsArchived" },
                values: new object[] { null, null, false });

            migrationBuilder.UpdateData(
                table: "ChecklistTemplateItems",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0002-000000000004"),
                columns: new[] { "ArchivedBy", "ArchivedOn", "IsArchived" },
                values: new object[] { null, null, false });

            migrationBuilder.UpdateData(
                table: "ChecklistTemplateItems",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0002-000000000005"),
                columns: new[] { "ArchivedBy", "ArchivedOn", "IsArchived" },
                values: new object[] { null, null, false });

            migrationBuilder.UpdateData(
                table: "ChecklistTemplateItems",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0002-000000000006"),
                columns: new[] { "ArchivedBy", "ArchivedOn", "IsArchived" },
                values: new object[] { null, null, false });

            migrationBuilder.UpdateData(
                table: "ChecklistTemplateItems",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0002-000000000007"),
                columns: new[] { "ArchivedBy", "ArchivedOn", "IsArchived" },
                values: new object[] { null, null, false });

            migrationBuilder.UpdateData(
                table: "ChecklistTemplateItems",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0002-000000000008"),
                columns: new[] { "ArchivedBy", "ArchivedOn", "IsArchived" },
                values: new object[] { null, null, false });

            migrationBuilder.UpdateData(
                table: "ChecklistTemplateItems",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0002-000000000009"),
                columns: new[] { "ArchivedBy", "ArchivedOn", "IsArchived" },
                values: new object[] { null, null, false });

            migrationBuilder.UpdateData(
                table: "ChecklistTemplateItems",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0002-000000000010"),
                columns: new[] { "ArchivedBy", "ArchivedOn", "IsArchived" },
                values: new object[] { null, null, false });

            migrationBuilder.UpdateData(
                table: "ChecklistTemplateItems",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0002-000000000011"),
                columns: new[] { "ArchivedBy", "ArchivedOn", "IsArchived" },
                values: new object[] { null, null, false });

            migrationBuilder.UpdateData(
                table: "ChecklistTemplateItems",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0002-000000000012"),
                columns: new[] { "ArchivedBy", "ArchivedOn", "IsArchived" },
                values: new object[] { null, null, false });

            migrationBuilder.UpdateData(
                table: "ChecklistTemplateItems",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0002-000000000013"),
                columns: new[] { "ArchivedBy", "ArchivedOn", "IsArchived" },
                values: new object[] { null, null, false });

            migrationBuilder.UpdateData(
                table: "ChecklistTemplateItems",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0002-000000000014"),
                columns: new[] { "ArchivedBy", "ArchivedOn", "IsArchived" },
                values: new object[] { null, null, false });

            migrationBuilder.UpdateData(
                table: "ChecklistTemplateItems",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0002-000000000015"),
                columns: new[] { "ArchivedBy", "ArchivedOn", "IsArchived" },
                values: new object[] { null, null, false });

            migrationBuilder.UpdateData(
                table: "ChecklistTemplateItems",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0002-000000000016"),
                columns: new[] { "ArchivedBy", "ArchivedOn", "IsArchived" },
                values: new object[] { null, null, false });

            migrationBuilder.UpdateData(
                table: "ChecklistTemplateItems",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0002-000000000017"),
                columns: new[] { "ArchivedBy", "ArchivedOn", "IsArchived" },
                values: new object[] { null, null, false });

            migrationBuilder.UpdateData(
                table: "ChecklistTemplateItems",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0002-000000000018"),
                columns: new[] { "ArchivedBy", "ArchivedOn", "IsArchived" },
                values: new object[] { null, null, false });

            migrationBuilder.UpdateData(
                table: "ChecklistTemplateItems",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0002-000000000019"),
                columns: new[] { "ArchivedBy", "ArchivedOn", "IsArchived" },
                values: new object[] { null, null, false });

            migrationBuilder.UpdateData(
                table: "ChecklistTemplateItems",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0002-000000000020"),
                columns: new[] { "ArchivedBy", "ArchivedOn", "IsArchived" },
                values: new object[] { null, null, false });

            migrationBuilder.UpdateData(
                table: "ChecklistTemplateItems",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0002-000000000021"),
                columns: new[] { "ArchivedBy", "ArchivedOn", "IsArchived" },
                values: new object[] { null, null, false });

            migrationBuilder.UpdateData(
                table: "ChecklistTemplateItems",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0002-000000000022"),
                columns: new[] { "ArchivedBy", "ArchivedOn", "IsArchived" },
                values: new object[] { null, null, false });

            migrationBuilder.UpdateData(
                table: "ChecklistTemplateItems",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0002-000000000023"),
                columns: new[] { "ArchivedBy", "ArchivedOn", "IsArchived" },
                values: new object[] { null, null, false });

            migrationBuilder.UpdateData(
                table: "ChecklistTemplateItems",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0002-000000000024"),
                columns: new[] { "ArchivedBy", "ArchivedOn", "IsArchived" },
                values: new object[] { null, null, false });

            migrationBuilder.UpdateData(
                table: "ChecklistTemplateItems",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0002-000000000025"),
                columns: new[] { "ArchivedBy", "ArchivedOn", "IsArchived" },
                values: new object[] { null, null, false });

            migrationBuilder.UpdateData(
                table: "ChecklistTemplateItems",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0002-000000000026"),
                columns: new[] { "ArchivedBy", "ArchivedOn", "IsArchived" },
                values: new object[] { null, null, false });

            migrationBuilder.UpdateData(
                table: "ChecklistTemplateItems",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0002-000000000027"),
                columns: new[] { "ArchivedBy", "ArchivedOn", "IsArchived" },
                values: new object[] { null, null, false });

            migrationBuilder.UpdateData(
                table: "ChecklistTemplateItems",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0002-000000000028"),
                columns: new[] { "ArchivedBy", "ArchivedOn", "IsArchived" },
                values: new object[] { null, null, false });

            migrationBuilder.UpdateData(
                table: "ChecklistTemplateItems",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0002-000000000029"),
                columns: new[] { "ArchivedBy", "ArchivedOn", "IsArchived" },
                values: new object[] { null, null, false });

            migrationBuilder.UpdateData(
                table: "ChecklistTemplateItems",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0002-000000000030"),
                columns: new[] { "ArchivedBy", "ArchivedOn", "IsArchived" },
                values: new object[] { null, null, false });

            migrationBuilder.UpdateData(
                table: "ChecklistTemplateItems",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0002-000000000031"),
                columns: new[] { "ArchivedBy", "ArchivedOn", "IsArchived" },
                values: new object[] { null, null, false });

            migrationBuilder.UpdateData(
                table: "ChecklistTemplateItems",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0002-000000000032"),
                columns: new[] { "ArchivedBy", "ArchivedOn", "IsArchived" },
                values: new object[] { null, null, false });

            migrationBuilder.UpdateData(
                table: "ChecklistTemplates",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000001"),
                columns: new[] { "ArchivedBy", "ArchivedOn", "IsArchived" },
                values: new object[] { null, null, false });

            migrationBuilder.UpdateData(
                table: "ChecklistTemplates",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000002"),
                columns: new[] { "ArchivedBy", "ArchivedOn", "IsArchived" },
                values: new object[] { null, null, false });

            migrationBuilder.UpdateData(
                table: "ChecklistTemplates",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000003"),
                columns: new[] { "ArchivedBy", "ArchivedOn", "IsArchived" },
                values: new object[] { null, null, false });

            migrationBuilder.UpdateData(
                table: "ChecklistTemplates",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000004"),
                columns: new[] { "ArchivedBy", "ArchivedOn", "IsArchived" },
                values: new object[] { null, null, false });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ArchivedBy",
                table: "WorkflowAuditLogs");

            migrationBuilder.DropColumn(
                name: "ArchivedOn",
                table: "WorkflowAuditLogs");

            migrationBuilder.DropColumn(
                name: "IsArchived",
                table: "WorkflowAuditLogs");

            migrationBuilder.DropColumn(
                name: "ArchivedBy",
                table: "UserProfiles");

            migrationBuilder.DropColumn(
                name: "ArchivedOn",
                table: "UserProfiles");

            migrationBuilder.DropColumn(
                name: "IsArchived",
                table: "UserProfiles");

            migrationBuilder.DropColumn(
                name: "ArchivedBy",
                table: "Tours");

            migrationBuilder.DropColumn(
                name: "ArchivedOn",
                table: "Tours");

            migrationBuilder.DropColumn(
                name: "IsArchived",
                table: "Tours");

            migrationBuilder.DropColumn(
                name: "ArchivedBy",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "ArchivedOn",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "IsArchived",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "ArchivedBy",
                table: "SecurityDeposits");

            migrationBuilder.DropColumn(
                name: "ArchivedOn",
                table: "SecurityDeposits");

            migrationBuilder.DropColumn(
                name: "IsArchived",
                table: "SecurityDeposits");

            migrationBuilder.DropColumn(
                name: "ArchivedBy",
                table: "SecurityDepositInvestmentPools");

            migrationBuilder.DropColumn(
                name: "ArchivedOn",
                table: "SecurityDepositInvestmentPools");

            migrationBuilder.DropColumn(
                name: "IsArchived",
                table: "SecurityDepositInvestmentPools");

            migrationBuilder.DropColumn(
                name: "ArchivedBy",
                table: "SecurityDepositDividends");

            migrationBuilder.DropColumn(
                name: "ArchivedOn",
                table: "SecurityDepositDividends");

            migrationBuilder.DropColumn(
                name: "IsArchived",
                table: "SecurityDepositDividends");

            migrationBuilder.DropColumn(
                name: "ArchivedBy",
                table: "Repairs");

            migrationBuilder.DropColumn(
                name: "ArchivedOn",
                table: "Repairs");

            migrationBuilder.DropColumn(
                name: "IsArchived",
                table: "Repairs");

            migrationBuilder.DropColumn(
                name: "ArchivedBy",
                table: "RentalApplications");

            migrationBuilder.DropColumn(
                name: "ArchivedOn",
                table: "RentalApplications");

            migrationBuilder.DropColumn(
                name: "IsArchived",
                table: "RentalApplications");

            migrationBuilder.DropColumn(
                name: "ArchivedBy",
                table: "ProspectiveTenants");

            migrationBuilder.DropColumn(
                name: "ArchivedOn",
                table: "ProspectiveTenants");

            migrationBuilder.DropColumn(
                name: "IsArchived",
                table: "ProspectiveTenants");

            migrationBuilder.DropColumn(
                name: "ArchivedBy",
                table: "Properties");

            migrationBuilder.DropColumn(
                name: "ArchivedOn",
                table: "Properties");

            migrationBuilder.DropColumn(
                name: "IsArchived",
                table: "Properties");

            migrationBuilder.DropColumn(
                name: "ArchivedBy",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "ArchivedOn",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "IsArchived",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "ArchivedBy",
                table: "OrganizationSMSSettings");

            migrationBuilder.DropColumn(
                name: "ArchivedOn",
                table: "OrganizationSMSSettings");

            migrationBuilder.DropColumn(
                name: "IsArchived",
                table: "OrganizationSMSSettings");

            migrationBuilder.DropColumn(
                name: "ArchivedBy",
                table: "OrganizationSettings");

            migrationBuilder.DropColumn(
                name: "ArchivedOn",
                table: "OrganizationSettings");

            migrationBuilder.DropColumn(
                name: "IsArchived",
                table: "OrganizationSettings");

            migrationBuilder.DropColumn(
                name: "ArchivedBy",
                table: "OrganizationEmailSettings");

            migrationBuilder.DropColumn(
                name: "ArchivedOn",
                table: "OrganizationEmailSettings");

            migrationBuilder.DropColumn(
                name: "IsArchived",
                table: "OrganizationEmailSettings");

            migrationBuilder.DropColumn(
                name: "ArchivedBy",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "ArchivedOn",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "IsArchived",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "ArchivedBy",
                table: "NotificationPreferences");

            migrationBuilder.DropColumn(
                name: "ArchivedOn",
                table: "NotificationPreferences");

            migrationBuilder.DropColumn(
                name: "IsArchived",
                table: "NotificationPreferences");

            migrationBuilder.DropColumn(
                name: "ArchivedBy",
                table: "Notes");

            migrationBuilder.DropColumn(
                name: "ArchivedOn",
                table: "Notes");

            migrationBuilder.DropColumn(
                name: "IsArchived",
                table: "Notes");

            migrationBuilder.DropColumn(
                name: "ArchivedBy",
                table: "MaintenanceRequests");

            migrationBuilder.DropColumn(
                name: "ArchivedOn",
                table: "MaintenanceRequests");

            migrationBuilder.DropColumn(
                name: "IsArchived",
                table: "MaintenanceRequests");

            migrationBuilder.DropColumn(
                name: "ArchivedBy",
                table: "Leases");

            migrationBuilder.DropColumn(
                name: "ArchivedOn",
                table: "Leases");

            migrationBuilder.DropColumn(
                name: "IsArchived",
                table: "Leases");

            migrationBuilder.DropColumn(
                name: "ArchivedBy",
                table: "LeaseOffers");

            migrationBuilder.DropColumn(
                name: "ArchivedOn",
                table: "LeaseOffers");

            migrationBuilder.DropColumn(
                name: "IsArchived",
                table: "LeaseOffers");

            migrationBuilder.DropColumn(
                name: "ArchivedBy",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "ArchivedOn",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "IsArchived",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "ArchivedBy",
                table: "Inspections");

            migrationBuilder.DropColumn(
                name: "ArchivedOn",
                table: "Inspections");

            migrationBuilder.DropColumn(
                name: "IsArchived",
                table: "Inspections");

            migrationBuilder.DropColumn(
                name: "ArchivedBy",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "ArchivedOn",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "IsArchived",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "ArchivedBy",
                table: "ChecklistTemplates");

            migrationBuilder.DropColumn(
                name: "ArchivedOn",
                table: "ChecklistTemplates");

            migrationBuilder.DropColumn(
                name: "IsArchived",
                table: "ChecklistTemplates");

            migrationBuilder.DropColumn(
                name: "ArchivedBy",
                table: "ChecklistTemplateItems");

            migrationBuilder.DropColumn(
                name: "ArchivedOn",
                table: "ChecklistTemplateItems");

            migrationBuilder.DropColumn(
                name: "IsArchived",
                table: "ChecklistTemplateItems");

            migrationBuilder.DropColumn(
                name: "ArchivedBy",
                table: "Checklists");

            migrationBuilder.DropColumn(
                name: "ArchivedOn",
                table: "Checklists");

            migrationBuilder.DropColumn(
                name: "IsArchived",
                table: "Checklists");

            migrationBuilder.DropColumn(
                name: "ArchivedBy",
                table: "ChecklistItems");

            migrationBuilder.DropColumn(
                name: "ArchivedOn",
                table: "ChecklistItems");

            migrationBuilder.DropColumn(
                name: "IsArchived",
                table: "ChecklistItems");

            migrationBuilder.DropColumn(
                name: "ArchivedBy",
                table: "CalendarSettings");

            migrationBuilder.DropColumn(
                name: "ArchivedOn",
                table: "CalendarSettings");

            migrationBuilder.DropColumn(
                name: "IsArchived",
                table: "CalendarSettings");

            migrationBuilder.DropColumn(
                name: "ArchivedBy",
                table: "CalendarEvents");

            migrationBuilder.DropColumn(
                name: "ArchivedOn",
                table: "CalendarEvents");

            migrationBuilder.DropColumn(
                name: "IsArchived",
                table: "CalendarEvents");

            migrationBuilder.DropColumn(
                name: "ArchivedBy",
                table: "ApplicationScreenings");

            migrationBuilder.DropColumn(
                name: "ArchivedOn",
                table: "ApplicationScreenings");

            migrationBuilder.DropColumn(
                name: "IsArchived",
                table: "ApplicationScreenings");
        }
    }
}
