using Nine.Shared.UI.Components.Common;
using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;

namespace Nine.Shared.UI.Tests.Components.Common;

public class FormFieldTests : BunitContext
{
    [Fact]
    public void FormField_Renders_Label()
    {
        // Arrange & Act
        var cut = Render<FormField>(parameters => parameters
            .Add(p => p.Label, "Test Label")
        );

        // Assert
        Assert.Contains("Test Label", cut.Markup);
        Assert.Contains("form-label", cut.Markup);
    }

    [Fact]
    public void FormField_Renders_ChildContent()
    {
        // Arrange & Act
        var cut = Render<FormField>(parameters => parameters
            .Add(p => p.Label, "Test Field")
            .AddChildContent("<input class='form-control' />")
        );

        // Assert
        Assert.Contains("input", cut.Markup);
        Assert.Contains("form-control", cut.Markup);
    }

    [Fact]
    public void FormField_Shows_Required_Indicator_When_Required_Is_True()
    {
        // Arrange & Act
        var cut = Render<FormField>(parameters => parameters
            .Add(p => p.Label, "Required Field")
            .Add(p => p.Required, true)
        );

        // Assert
        Assert.Contains("*", cut.Markup);
        Assert.Contains("text-danger", cut.Markup);
    }

    [Fact]
    public void FormField_Does_Not_Show_Required_Indicator_When_Required_Is_False()
    {
        // Arrange & Act
        var cut = Render<FormField>(parameters => parameters
            .Add(p => p.Label, "Optional Field")
            .Add(p => p.Required, false)
        );

        // Assert
        Assert.Contains("Optional Field", cut.Markup);
        Assert.DoesNotContain("text-danger", cut.Markup);
    }

    [Fact]
    public void FormField_Renders_HelpText()
    {
        // Arrange & Act
        var cut = Render<FormField>(parameters => parameters
            .Add(p => p.Label, "Test Field")
            .Add(p => p.HelpText, "This is help text")
        );

        // Assert
        Assert.Contains("This is help text", cut.Markup);
        Assert.Contains("form-text", cut.Markup);
    }

    [Fact]
    public void FormField_Does_Not_Render_HelpText_When_Null()
    {
        // Arrange & Act
        var cut = Render<FormField>(parameters => parameters
            .Add(p => p.Label, "Test Field")
        );

        // Assert
        Assert.DoesNotContain("form-text", cut.Markup);
    }

    [Fact]
    public void FormField_Has_Form_Group_Class()
    {
        // Arrange & Act
        var cut = Render<FormField>(parameters => parameters
            .Add(p => p.Label, "Test Field")
        );

        // Assert
        Assert.Contains("mb-3", cut.Markup);
    }

    [Fact]
    public void FormField_Applies_Custom_CssClass()
    {
        // Arrange & Act
        var cut = Render<FormField>(parameters => parameters
            .Add(p => p.Label, "Test Field")
            .Add(p => p.CssClass, "custom-field")
        );

        // Assert
        Assert.Contains("custom-field", cut.Markup);
    }

    [Fact]
    public void FormField_Renders_Complete_Structure()
    {
        // Arrange & Act
        var cut = Render<FormField>(parameters => parameters
            .Add(p => p.Label, "Complete Field")
            .Add(p => p.Required, true)
            .Add(p => p.HelpText, "Help text")
            .AddChildContent("<input class='form-control' />")
        );

        // Assert
        Assert.Contains("Complete Field", cut.Markup);
        Assert.Contains("*", cut.Markup);
        Assert.Contains("Help text", cut.Markup);
        Assert.Contains("input", cut.Markup);
    }
}
