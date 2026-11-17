using Nine.Shared.UI.Components.Common;
using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;

namespace Nine.Shared.UI.Tests.Components.Common;

public class CardTests : BunitContext
{
    [Fact]
    public void Card_Renders_With_Title()
    {
        // Arrange & Act
        var cut = Render<Card>(parameters => parameters
            .Add(p => p.Title, "Test Card")
        );

        // Assert
        Assert.Contains("Test Card", cut.Markup);
        Assert.Contains("card-header", cut.Markup);
    }

    [Fact]
    public void Card_Renders_Body_Content()
    {
        // Arrange & Act
        var cut = Render<Card>(parameters => parameters
            .Add(p => p.ChildContent, (RenderFragment)(builder => builder.AddContent(0, "Body Content")))
        );

        // Assert
        Assert.Contains("Body Content", cut.Markup);
        Assert.Contains("card-body", cut.Markup);
    }

    [Fact]
    public void Card_Renders_Header_Content()
    {
        // Arrange & Act
        var cut = Render<Card>(parameters => parameters
            .Add(p => p.HeaderContent, (RenderFragment)(builder => builder.AddContent(0, "Custom Header")))
        );

        // Assert
        Assert.Contains("Custom Header", cut.Markup);
        Assert.Contains("card-header", cut.Markup);
    }

    [Fact]
    public void Card_Renders_Footer_Content()
    {
        // Arrange & Act
        var cut = Render<Card>(parameters => parameters
            .Add(p => p.FooterContent, (RenderFragment)(builder => builder.AddContent(0, "Footer Content")))
        );

        // Assert
        Assert.Contains("Footer Content", cut.Markup);
        Assert.Contains("card-footer", cut.Markup);
    }

    [Fact]
    public void Card_DoesNotRender_Header_When_No_Title_Or_HeaderContent()
    {
        // Arrange & Act
        var cut = Render<Card>(parameters => parameters
            .Add(p => p.ChildContent, (RenderFragment)(builder => builder.AddContent(0, "Just Body")))
        );

        // Assert
        Assert.DoesNotContain("card-header", cut.Markup);
    }

    [Fact]
    public void Card_DoesNotRender_Footer_When_No_FooterContent()
    {
        // Arrange & Act
        var cut = Render<Card>(parameters => parameters
            .Add(p => p.ChildContent, (RenderFragment)(builder => builder.AddContent(0, "Just Body")))
        );

        // Assert
        Assert.DoesNotContain("card-footer", cut.Markup);
    }

    [Fact]
    public void Card_Prefers_HeaderContent_Over_Title()
    {
        // Arrange & Act
        var cut = Render<Card>(parameters => parameters
            .Add(p => p.Title, "Title Text")
            .Add(p => p.HeaderContent, (RenderFragment)(builder => builder.AddContent(0, "Custom Header")))
        );

        // Assert
        Assert.Contains("Custom Header", cut.Markup);
        Assert.DoesNotContain("Title Text", cut.Markup);
    }

    [Fact]
    public void Card_Renders_With_Custom_CssClass()
    {
        // Arrange & Act
        var cut = Render<Card>(parameters => parameters
            .Add(p => p.CssClass, "custom-card-class")
            .Add(p => p.ChildContent, (RenderFragment)(builder => builder.AddContent(0, "Content")))
        );

        // Assert
        Assert.Contains("custom-card-class", cut.Markup);
    }

    [Fact]
    public void Card_Renders_All_Sections_Together()
    {
        // Arrange & Act
        var cut = Render<Card>(parameters => parameters
            .Add(p => p.Title, "Card Title")
            .Add(p => p.ChildContent, (RenderFragment)(builder => builder.AddContent(0, "Body")))
            .Add(p => p.FooterContent, (RenderFragment)(builder => builder.AddContent(0, "Footer")))
        );

        // Assert
        Assert.Contains("Card Title", cut.Markup);
        Assert.Contains("Body", cut.Markup);
        Assert.Contains("Footer", cut.Markup);
        Assert.Contains("card-header", cut.Markup);
        Assert.Contains("card-body", cut.Markup);
        Assert.Contains("card-footer", cut.Markup);
    }
}
