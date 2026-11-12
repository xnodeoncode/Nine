using Nine.Shared.UI.Components.Common;
using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;

namespace Nine.Shared.UI.Tests.Components.Common;

public class ModalTests : BunitContext
{
    [Fact]
    public void Modal_Renders_When_IsVisible_Is_True()
    {
        // Arrange & Act
        var cut = Render<Modal>(parameters => parameters
            .Add(p => p.IsVisible, true)
            .Add(p => p.Title, "Test Modal")
            .Add(p => p.ChildContent, (RenderFragment)(builder => builder.AddContent(0, "Test Content")))
        );

        // Assert
        Assert.Contains("Test Modal", cut.Markup);
        Assert.Contains("Test Content", cut.Markup);
        Assert.Contains("modal fade show d-block", cut.Markup);
    }

    [Fact]
    public void Modal_DoesNotRender_When_IsOpen_Is_False()
    {
        // Arrange & Act
        var cut = Render<Modal>(parameters => parameters
            .Add(p => p.IsVisible, false)
            .Add(p => p.Title, "Test Modal")
        );

        // Assert
        Assert.True(string.IsNullOrWhiteSpace(cut.Markup));
    }

    [Fact]
    public void Modal_Renders_With_Small_Size()
    {
        // Arrange & Act
        var cut = Render<Modal>(parameters => parameters
            .Add(p => p.IsVisible, true)
            .Add(p => p.Title, "Small Modal")
            .Add(p => p.Size, ModalSize.Small)
        );

        // Assert
        Assert.Contains("modal-sm", cut.Markup);
    }

    [Fact]
    public void Modal_Renders_With_Large_Size()
    {
        // Arrange & Act
        var cut = Render<Modal>(parameters => parameters
            .Add(p => p.IsVisible, true)
            .Add(p => p.Title, "Large Modal")
            .Add(p => p.Size, ModalSize.Large)
        );

        // Assert
        Assert.Contains("modal-lg", cut.Markup);
    }

    [Fact]
    public void Modal_Renders_With_ExtraLarge_Size()
    {
        // Arrange & Act
        var cut = Render<Modal>(parameters => parameters
            .Add(p => p.IsVisible, true)
            .Add(p => p.Title, "Extra Large Modal")
            .Add(p => p.Size, ModalSize.ExtraLarge)
        );

        // Assert
        Assert.Contains("modal-xl", cut.Markup);
    }

    [Fact]
    public void Modal_Renders_Centered()
    {
        // Arrange & Act
        var cut = Render<Modal>(parameters => parameters
            .Add(p => p.IsVisible, true)
            .Add(p => p.Title, "Centered Modal")
            .Add(p => p.Position, ModalPosition.Centered)
        );

        // Assert
        Assert.Contains("modal-dialog-centered", cut.Markup);
    }

    [Fact]
    public void Modal_Renders_With_Backdrop()
    {
        // Arrange & Act
        var cut = Render<Modal>(parameters => parameters
            .Add(p => p.IsVisible, true)
            .Add(p => p.Title, "Modal with Backdrop")
            .Add(p => p.CloseOnBackdropClick, true)
        );

        // Assert
        Assert.Contains("rgba(0,0,0,0.5)", cut.Markup);
    }

    [Fact]
    public void Modal_Calls_OnClose_When_Close_Button_Clicked()
    {
        // Arrange
        var closeCalled = false;
        var cut = Render<Modal>(parameters => parameters
            .Add(p => p.IsVisible, true)
            .Add(p => p.Title, "Test Modal")
            .Add(p => p.OnClose, EventCallback.Factory.Create(this, () => closeCalled = true))
        );

        // Act
        var closeButton = cut.Find(".btn-close");
        closeButton.Click();

        // Assert
        Assert.True(closeCalled);
    }

    [Fact]
    public void Modal_Renders_Footer_Content()
    {
        // Arrange & Act
        var cut = Render<Modal>(parameters => parameters
            .Add(p => p.IsVisible, true)
            .Add(p => p.Title, "Modal with Footer")
            .Add(p => p.FooterContent, (RenderFragment)(builder => builder.AddContent(0, "Footer Content")))
        );

        // Assert
        Assert.Contains("Footer Content", cut.Markup);
        Assert.Contains("modal-footer", cut.Markup);
    }

    [Fact]
    public void Modal_DoesNotRender_Footer_When_No_Content()
    {
        // Arrange & Act
        var cut = Render<Modal>(parameters => parameters
            .Add(p => p.IsVisible, true)
            .Add(p => p.Title, "Modal without Footer")
        );

        // Assert
        Assert.DoesNotContain("modal-footer", cut.Markup);
    }
}
