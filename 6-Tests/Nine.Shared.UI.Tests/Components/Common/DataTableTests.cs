using Nine.Shared.UI.Components.Common;
using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;

namespace Nine.Shared.UI.Tests.Components.Common;

public class DataTableTests : BunitContext
{
    private class TestItem
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }

    [Fact]
    public void DataTable_Renders_With_Empty_Items()
    {
        // Arrange & Act
        var cut = Render<DataTable<TestItem>>(parameters => parameters
            .Add(p => p.Items, new List<TestItem>())
        );

        // Assert
        Assert.Contains("No data available", cut.Markup);
    }

    [Fact]
    public void DataTable_Renders_With_Null_Items()
    {
        // Arrange & Act
        var cut = Render<DataTable<TestItem>>(parameters => parameters
            .Add(p => p.Items, null)
        );

        // Assert
        Assert.Contains("No data available", cut.Markup);
    }

    [Fact]
    public void DataTable_Renders_Header_Template()
    {
        // Arrange
        var items = new List<TestItem>
        {
            new TestItem { Id = 1, Name = "Test" }
        };

        // Act
        var cut = Render<DataTable<TestItem>>(parameters => parameters
            .Add(p => p.Items, items)
            .Add(p => p.HeaderTemplate, (RenderFragment)(builder =>
            {
                builder.OpenElement(0, "th");
                builder.AddContent(1, "ID");
                builder.CloseElement();
                builder.OpenElement(2, "th");
                builder.AddContent(3, "Name");
                builder.CloseElement();
            }))
        );

        // Assert
        Assert.Contains("ID", cut.Markup);
        Assert.Contains("Name", cut.Markup);
        Assert.Contains("<thead>", cut.Markup);
    }

    [Fact]
    public void DataTable_Renders_Row_Template()
    {
        // Arrange
        var items = new List<TestItem>
        {
            new TestItem { Id = 1, Name = "Item 1", Description = "Description 1" },
            new TestItem { Id = 2, Name = "Item 2", Description = "Description 2" }
        };

        // Act
        var cut = Render<DataTable<TestItem>>(parameters => parameters
            .Add(p => p.Items, items)
            .Add(p => p.RowTemplate, (RenderFragment<TestItem>)(item => builder =>
            {
                builder.OpenElement(0, "td");
                builder.AddContent(1, item.Id);
                builder.CloseElement();
                builder.OpenElement(2, "td");
                builder.AddContent(3, item.Name);
                builder.CloseElement();
            }))
        );

        // Assert
        Assert.Contains("Item 1", cut.Markup);
        Assert.Contains("Item 2", cut.Markup);
        Assert.Contains("<tbody>", cut.Markup);
    }

    [Fact]
    public void DataTable_Renders_Multiple_Rows()
    {
        // Arrange
        var items = new List<TestItem>
        {
            new TestItem { Id = 1, Name = "First" },
            new TestItem { Id = 2, Name = "Second" },
            new TestItem { Id = 3, Name = "Third" }
        };

        // Act
        var cut = Render<DataTable<TestItem>>(parameters => parameters
            .Add(p => p.Items, items)
            .Add(p => p.RowTemplate, (RenderFragment<TestItem>)(item => builder =>
            {
                builder.OpenElement(0, "td");
                builder.AddContent(1, item.Name);
                builder.CloseElement();
            }))
        );

        // Assert
        var tbody = cut.Find("tbody");
        var rows = tbody.QuerySelectorAll("tr");
        Assert.Equal(3, rows.Length);
        Assert.Contains("First", cut.Markup);
        Assert.Contains("Second", cut.Markup);
        Assert.Contains("Third", cut.Markup);
    }

    [Fact]
    public void DataTable_Applies_Custom_CssClass()
    {
        // Arrange & Act
        var cut = Render<DataTable<TestItem>>(parameters => parameters
            .Add(p => p.Items, new List<TestItem>())
            .Add(p => p.TableCssClass, "custom-table-class")
        );

        // Assert
        Assert.Contains("custom-table-class", cut.Markup);
    }

    [Fact]
    public void DataTable_Renders_With_Custom_EmptyMessage()
    {
        // Arrange & Act
        var cut = Render<DataTable<TestItem>>(parameters => parameters
            .Add(p => p.Items, new List<TestItem>())
            .Add(p => p.EmptyMessage, "Custom empty message")
        );

        // Assert
        Assert.Contains("Custom empty message", cut.Markup);
        Assert.DoesNotContain("No data available", cut.Markup);
    }

    [Fact]
    public void DataTable_Has_Bootstrap_Table_Classes()
    {
        // Arrange & Act
        var cut = Render<DataTable<TestItem>>(parameters => parameters
            .Add(p => p.Items, new List<TestItem>())
        );

        // Assert
        Assert.Contains("table", cut.Markup);
        Assert.Contains("table-striped", cut.Markup);
        Assert.Contains("table-hover", cut.Markup);
    }
}
