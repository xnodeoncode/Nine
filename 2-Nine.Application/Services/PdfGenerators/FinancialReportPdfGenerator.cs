using Nine.Core.Entities;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Nine.Application.Services.PdfGenerators;

public class FinancialReportPdfGenerator
{
    public FinancialReportPdfGenerator()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public byte[] GenerateIncomeStatementPdf(IncomeStatement statement)
    {

        var document = QuestPDF.Fluent.Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.Letter);
                page.Margin(2, Unit.Centimetre);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontSize(11));

                page.Header()
                    .Text(text =>
                    {
                        text.Span("INCOME STATEMENT\n").FontSize(20).Bold();
                        text.Span($"{(statement.PropertyName ?? "All Properties")}\n").FontSize(14).SemiBold();
                        text.Span($"Period: {statement.StartDate:MMM dd, yyyy} - {statement.EndDate:MMM dd, yyyy}")
                            .FontSize(10).Italic();
                    });

                page.Content()
                    .PaddingVertical(1, Unit.Centimetre)
                    .Column(column =>
                    {
                        column.Spacing(20);

                        // Income Section
                        column.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(3);
                                columns.RelativeColumn(1);
                            });

                            table.Cell().Element(HeaderStyle).Text("INCOME");
                            table.Cell().Element(HeaderStyle).Text("");

                            table.Cell().PaddingLeft(15).Text("Rent Income");
                            table.Cell().AlignRight().Text(statement.TotalRentIncome.ToString("C"));

                            table.Cell().PaddingLeft(15).Text("Other Income");
                            table.Cell().AlignRight().Text(statement.TotalOtherIncome.ToString("C"));

                            table.Cell().Element(SubtotalStyle).Text("Total Income");
                            table.Cell().Element(SubtotalStyle).AlignRight().Text(statement.TotalIncome.ToString("C"));
                        });

                        // Expenses Section
                        column.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(3);
                                columns.RelativeColumn(1);
                            });

                            table.Cell().Element(HeaderStyle).Text("EXPENSES");
                            table.Cell().Element(HeaderStyle).Text("");

                            table.Cell().PaddingLeft(15).Text("Maintenance & Repairs");
                            table.Cell().AlignRight().Text(statement.MaintenanceExpenses.ToString("C"));

                            table.Cell().PaddingLeft(15).Text("Utilities");
                            table.Cell().AlignRight().Text(statement.UtilityExpenses.ToString("C"));

                            table.Cell().PaddingLeft(15).Text("Insurance");
                            table.Cell().AlignRight().Text(statement.InsuranceExpenses.ToString("C"));

                            table.Cell().PaddingLeft(15).Text("Property Taxes");
                            table.Cell().AlignRight().Text(statement.TaxExpenses.ToString("C"));

                            table.Cell().PaddingLeft(15).Text("Management Fees");
                            table.Cell().AlignRight().Text(statement.ManagementFees.ToString("C"));

                            table.Cell().PaddingLeft(15).Text("Mortgage");
                            table.Cell().AlignRight().Text(statement.MortgageExpenses.ToString("C"));

                            table.Cell().PaddingLeft(15).Text("Other Expenses");
                            table.Cell().AlignRight().Text(statement.OtherExpenses.ToString("C"));

                            table.Cell().Element(SubtotalStyle).Text("Total Expenses");
                            table.Cell().Element(SubtotalStyle).AlignRight().Text(statement.TotalExpenses.ToString("C"));
                        });

                        // Net Income Section
                        column.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(3);
                                columns.RelativeColumn(1);
                            });

                            table.Cell().Element(TotalStyle).Text("NET INCOME");
                            table.Cell().Element(TotalStyle).AlignRight().Text(statement.NetIncome.ToString("C"));

                            table.Cell().PaddingLeft(15).Text("Profit Margin");
                            table.Cell().AlignRight().Text($"{statement.ProfitMargin:F2}%");
                        });
                    });

                page.Footer()
                    .AlignCenter()
                    .Text(x =>
                    {
                        x.Span("Generated on ");
                        x.Span(DateTime.Now.ToString("MMM dd, yyyy HH:mm")).SemiBold();
                    });
            });
        });

        return document.GeneratePdf();
    }

    public byte[] GenerateRentRollPdf(List<RentRollItem> rentRoll, DateTime asOfDate)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        var document = QuestPDF.Fluent.Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.Letter.Landscape());
                page.Margin(1, Unit.Centimetre);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontSize(9));

                page.Header()
                    .Text(text =>
                    {
                        text.Span("RENT ROLL REPORT\n").FontSize(18).Bold();
                        text.Span($"As of {asOfDate:MMM dd, yyyy}").FontSize(12).Italic();
                    });

                page.Content()
                    .PaddingVertical(0.5f, Unit.Centimetre)
                    .Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(2);
                            columns.RelativeColumn(2);
                            columns.RelativeColumn(2);
                            columns.RelativeColumn(1);
                            columns.RelativeColumn(2);
                            columns.RelativeColumn(1);
                            columns.RelativeColumn(1);
                            columns.RelativeColumn(1);
                            columns.RelativeColumn(1);
                            columns.RelativeColumn(1);
                        });

                        // Header
                        table.Header(header =>
                        {
                            header.Cell().Element(HeaderCellStyle).Text("Property");
                            header.Cell().Element(HeaderCellStyle).Text("Address");
                            header.Cell().Element(HeaderCellStyle).Text("Tenant");
                            header.Cell().Element(HeaderCellStyle).Text("Status");
                            header.Cell().Element(HeaderCellStyle).Text("Lease Period");
                            header.Cell().Element(HeaderCellStyle).AlignRight().Text("Rent");
                            header.Cell().Element(HeaderCellStyle).AlignRight().Text("Deposit");
                            header.Cell().Element(HeaderCellStyle).AlignRight().Text("Paid");
                            header.Cell().Element(HeaderCellStyle).AlignRight().Text("Due");
                            header.Cell().Element(HeaderCellStyle).AlignRight().Text("Balance");
                        });

                        // Rows
                        foreach (var item in rentRoll)
                        {
                            table.Cell().Text(item.PropertyName);
                            table.Cell().Text(item.PropertyAddress);
                            table.Cell().Text(item.TenantName ?? "Vacant");
                            table.Cell().Text(item.LeaseStatus);
                            table.Cell().Text($"{item.LeaseStartDate:MM/dd/yyyy} - {item.LeaseEndDate:MM/dd/yyyy}");
                            table.Cell().AlignRight().Text(item.MonthlyRent.ToString("C"));
                            table.Cell().AlignRight().Text(item.SecurityDeposit.ToString("C"));
                            table.Cell().AlignRight().Text(item.TotalPaid.ToString("C"));
                            table.Cell().AlignRight().Text(item.TotalDue.ToString("C"));
                            table.Cell().AlignRight().Text(item.Balance.ToString("C"));
                        }

                        // Footer
                        table.Footer(footer =>
                        {
                            footer.Cell().ColumnSpan(5).Element(FooterCellStyle).Text("TOTALS");
                            footer.Cell().Element(FooterCellStyle).AlignRight().Text(rentRoll.Sum(r => r.MonthlyRent).ToString("C"));
                            footer.Cell().Element(FooterCellStyle).AlignRight().Text(rentRoll.Sum(r => r.SecurityDeposit).ToString("C"));
                            footer.Cell().Element(FooterCellStyle).AlignRight().Text(rentRoll.Sum(r => r.TotalPaid).ToString("C"));
                            footer.Cell().Element(FooterCellStyle).AlignRight().Text(rentRoll.Sum(r => r.TotalDue).ToString("C"));
                            footer.Cell().Element(FooterCellStyle).AlignRight().Text(rentRoll.Sum(r => r.Balance).ToString("C"));
                        });
                    });

                page.Footer()
                    .AlignCenter()
                    .Text(x =>
                    {
                        x.Span("Page ");
                        x.CurrentPageNumber();
                        x.Span(" of ");
                        x.TotalPages();
                        x.Span(" | Generated on ");
                        x.Span(DateTime.Now.ToString("MMM dd, yyyy HH:mm"));
                    });
            });
        });

        return document.GeneratePdf();
    }

    public byte[] GeneratePropertyPerformancePdf(List<PropertyPerformance> performance, DateTime startDate, DateTime endDate)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        var document = QuestPDF.Fluent.Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.Letter.Landscape());
                page.Margin(1, Unit.Centimetre);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontSize(10));

                page.Header()
                    .Text(text =>
                    {
                        text.Span("PROPERTY PERFORMANCE REPORT\n").FontSize(18).Bold();
                        text.Span($"Period: {startDate:MMM dd, yyyy} - {endDate:MMM dd, yyyy}").FontSize(12).Italic();
                    });

                page.Content()
                    .PaddingVertical(0.5f, Unit.Centimetre)
                    .Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(2);
                            columns.RelativeColumn(2);
                            columns.RelativeColumn(1);
                            columns.RelativeColumn(1);
                            columns.RelativeColumn(1);
                            columns.RelativeColumn(1);
                            columns.RelativeColumn(1);
                        });

                        // Header
                        table.Header(header =>
                        {
                            header.Cell().Element(HeaderCellStyle).Text("Property");
                            header.Cell().Element(HeaderCellStyle).Text("Address");
                            header.Cell().Element(HeaderCellStyle).AlignRight().Text("Income");
                            header.Cell().Element(HeaderCellStyle).AlignRight().Text("Expenses");
                            header.Cell().Element(HeaderCellStyle).AlignRight().Text("Net Income");
                            header.Cell().Element(HeaderCellStyle).AlignRight().Text("ROI %");
                            header.Cell().Element(HeaderCellStyle).AlignRight().Text("Occupancy %");
                        });

                        // Rows
                        foreach (var item in performance)
                        {
                            table.Cell().Text(item.PropertyName);
                            table.Cell().Text(item.PropertyAddress);
                            table.Cell().AlignRight().Text(item.TotalIncome.ToString("C"));
                            table.Cell().AlignRight().Text(item.TotalExpenses.ToString("C"));
                            table.Cell().AlignRight().Text(item.NetIncome.ToString("C"));
                            table.Cell().AlignRight().Text($"{item.ROI:F2}%");
                            table.Cell().AlignRight().Text($"{item.OccupancyRate:F1}%");
                        }

                        // Footer
                        table.Footer(footer =>
                        {
                            footer.Cell().ColumnSpan(2).Element(FooterCellStyle).Text("TOTALS");
                            footer.Cell().Element(FooterCellStyle).AlignRight().Text(performance.Sum(p => p.TotalIncome).ToString("C"));
                            footer.Cell().Element(FooterCellStyle).AlignRight().Text(performance.Sum(p => p.TotalExpenses).ToString("C"));
                            footer.Cell().Element(FooterCellStyle).AlignRight().Text(performance.Sum(p => p.NetIncome).ToString("C"));
                            footer.Cell().Element(FooterCellStyle).AlignRight().Text($"{performance.Average(p => p.ROI):F2}%");
                            footer.Cell().Element(FooterCellStyle).AlignRight().Text($"{performance.Average(p => p.OccupancyRate):F1}%");
                        });
                    });

                page.Footer()
                    .AlignCenter()
                    .Text(x =>
                    {
                        x.Span("Generated on ");
                        x.Span(DateTime.Now.ToString("MMM dd, yyyy HH:mm"));
                    });
            });
        });

        return document.GeneratePdf();
    }

    public byte[] GenerateTaxReportPdf(List<TaxReportData> taxReports)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        var document = QuestPDF.Fluent.Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.Letter);
                page.Margin(2, Unit.Centimetre);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontSize(10));

                page.Header()
                    .Text(text =>
                    {
                        text.Span("SCHEDULE E - SUPPLEMENTAL INCOME AND LOSS\n").FontSize(16).Bold();
                        text.Span($"Tax Year {taxReports.First().Year}\n").FontSize(12).SemiBold();
                        text.Span("Rental Real Estate and Royalties").FontSize(10).Italic();
                    });

                page.Content()
                    .PaddingVertical(1, Unit.Centimetre)
                    .Column(column =>
                    {
                        foreach (var report in taxReports)
                        {
                            column.Item().PaddingBottom(15).Column(propertyColumn =>
                            {
                                propertyColumn.Item().Text(report.PropertyName ?? "Property").FontSize(12).Bold();
                                
                                propertyColumn.Item().PaddingTop(5).Table(table =>
                                {
                                    table.ColumnsDefinition(columns =>
                                    {
                                        columns.RelativeColumn(3);
                                        columns.RelativeColumn(1);
                                    });

                                    table.Cell().Element(SectionHeaderStyle).Text("INCOME");
                                    table.Cell().Element(SectionHeaderStyle).Text("");

                                    table.Cell().PaddingLeft(10).Text("3. Rents received");
                                    table.Cell().AlignRight().Text(report.TotalRentIncome.ToString("C"));

                                    table.Cell().Element(SectionHeaderStyle).PaddingTop(10).Text("EXPENSES");
                                    table.Cell().Element(SectionHeaderStyle).PaddingTop(10).Text("");

                                    table.Cell().PaddingLeft(10).Text("5. Advertising");
                                    table.Cell().AlignRight().Text(report.Advertising.ToString("C"));

                                    table.Cell().PaddingLeft(10).Text("7. Cleaning and maintenance");
                                    table.Cell().AlignRight().Text(report.Cleaning.ToString("C"));

                                    table.Cell().PaddingLeft(10).Text("9. Insurance");
                                    table.Cell().AlignRight().Text(report.Insurance.ToString("C"));

                                    table.Cell().PaddingLeft(10).Text("11. Legal and professional fees");
                                    table.Cell().AlignRight().Text(report.Legal.ToString("C"));

                                    table.Cell().PaddingLeft(10).Text("12. Management fees");
                                    table.Cell().AlignRight().Text(report.Management.ToString("C"));

                                    table.Cell().PaddingLeft(10).Text("13. Mortgage Expense (see Form 1098, Box 1 for the tax-deductible interest portion)");
                                    table.Cell().AlignRight().Text(report.MortgageExpense.ToString("C"));

                                    table.Cell().PaddingLeft(10).Text("14. Repairs");
                                    table.Cell().AlignRight().Text(report.Repairs.ToString("C"));

                                    table.Cell().PaddingLeft(10).Text("15. Supplies");
                                    table.Cell().AlignRight().Text(report.Supplies.ToString("C"));

                                    table.Cell().PaddingLeft(10).Text("16. Taxes");
                                    table.Cell().AlignRight().Text(report.Taxes.ToString("C"));

                                    table.Cell().PaddingLeft(10).Text("17. Utilities");
                                    table.Cell().AlignRight().Text(report.Utilities.ToString("C"));

                                    table.Cell().PaddingLeft(10).Text("18. Depreciation");
                                    table.Cell().AlignRight().Text(report.DepreciationAmount.ToString("C"));

                                    table.Cell().PaddingLeft(10).Text("19. Other");
                                    table.Cell().AlignRight().Text(report.Other.ToString("C"));

                                    table.Cell().Element(SubtotalStyle).Text("20. Total expenses");
                                    table.Cell().Element(SubtotalStyle).AlignRight().Text((report.TotalExpenses + report.DepreciationAmount).ToString("C"));

                                    table.Cell().Element(TotalStyle).PaddingTop(5).Text("21. Net rental income or (loss)");
                                    table.Cell().Element(TotalStyle).PaddingTop(5).AlignRight().Text(report.TaxableIncome.ToString("C"));
                                });
                            });

                            if (taxReports.Count > 1 && report != taxReports.Last())
                            {
                                column.Item().LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
                            }
                        }
                    });

                page.Footer()
                    .AlignCenter()
                    .Text(x =>
                    {
                        x.Span("Page ");
                        x.CurrentPageNumber();
                        x.Span(" | Generated on ");
                        x.Span(DateTime.Now.ToString("MMM dd, yyyy HH:mm")).SemiBold();
                        x.Span("\nNote: This is an estimated report. Please consult with a tax professional for accurate filing.");
                    });
            });
        });

        return document.GeneratePdf();
    }

    private static IContainer HeaderStyle(IContainer container)
    {
        return container.BorderBottom(1).BorderColor(Colors.Black).PaddingBottom(5).PaddingTop(10).DefaultTextStyle(x => x.SemiBold().FontSize(12));
    }

    private static IContainer SubtotalStyle(IContainer container)
    {
        return container.BorderTop(1).BorderColor(Colors.Grey.Medium).PaddingTop(5).PaddingBottom(5).DefaultTextStyle(x => x.SemiBold());
    }

    private static IContainer TotalStyle(IContainer container)
    {
        return container.BorderTop(2).BorderColor(Colors.Black).PaddingTop(8).DefaultTextStyle(x => x.Bold().FontSize(12));
    }

    private static IContainer HeaderCellStyle(IContainer container)
    {
        return container.BorderBottom(1).BorderColor(Colors.Black).PaddingBottom(5).DefaultTextStyle(x => x.SemiBold());
    }

    private static IContainer FooterCellStyle(IContainer container)
    {
        return container.BorderTop(2).BorderColor(Colors.Black).PaddingTop(5).DefaultTextStyle(x => x.Bold());
    }

    private static IContainer SectionHeaderStyle(IContainer container)
    {
        return container.DefaultTextStyle(x => x.SemiBold().FontSize(11));
    }
}
