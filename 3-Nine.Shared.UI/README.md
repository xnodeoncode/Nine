# Nine.UI.Shared - Shared UI Component Library

## Overview

`Nine.UI.Shared` is a Razor Class Library (RCL) that provides reusable UI components, layouts, and assets shared between Nine and Nine.Professional. This library eliminates code duplication and ensures a consistent user experience across all Nine products.

## Purpose

- **Single-Source Development**: Build UI components once, use in multiple products
- **Consistency**: Maintain uniform UI/UX across all Nine applications
- **Maintainability**: Fix bugs and add features in one place
- **Testing**: Comprehensive test coverage with bUnit (47 unit tests, 100% passing)
- **Scalability**: Clear architecture for adding new shared components

## Architecture

### Folder Structure

```
Nine.UI.Shared/
├── Components/
│   ├── Common/           # Generic, reusable UI components
│   │   ├── Modal.razor
│   │   ├── Card.razor
│   │   ├── DataTable.razor
│   │   └── FormField.razor
│   └── Layout/           # Layout components
│       └── SharedMainLayout.razor
├── Features/             # Feature-specific shared components
│   └── Notifications/
│       ├── NotificationBell.razor
│       ├── NotificationCenter.razor
│       └── NotificationPreferences.razor
├── wwwroot/
│   ├── css/              # Shared stylesheets
│   └── js/               # Shared JavaScript
└── 6-Tests/Nine.UI.Shared.Tests/  # Unit tests (bUnit)
```

### Design Principles

1. **Components/Common**: Generic, reusable UI elements (modals, cards, tables, forms)
2. **Components/Layout**: Shared layout structures and navigation
3. **Features**: Domain-specific components organized by feature area
4. **Stateless by Default**: Components receive data via parameters
5. **Customizable**: Support CSS classes, custom content, and event callbacks

## Dependencies

- **Can Reference**: `Nine.Application`, `Nine.Core`
- **Can Reference**: Microsoft.AspNetCore.Components packages
- **Cannot Reference**: Product-specific projects (Nine, Professional)

## Getting Started

### Adding the Shared Library to Your Product

1. Add project reference:

   ```xml
   <ProjectReference Include="..\3-Nine.UI.Shared\Nine.UI.Shared.csproj" />
   ```

2. Add using directive in `_Imports.razor`:

   ```razor
   @using Nine.UI.Shared.Components.Common
   @using Nine.UI.Shared.Components.Layout
   @using Nine.UI.Shared.Features.Notifications
   ```

3. Use components in your pages:
   ```razor
   <Modal IsVisible="@showModal" Title="Example">
       <ChildContent>Modal content here</ChildContent>
   </Modal>
   ```

## Component Usage Guide

### Modal Component

**Purpose**: Display content in an overlay dialog

**Basic Usage**:

```razor
<Modal IsVisible="@isVisible"
       Title="My Modal"
       OnClose="@HandleClose">
    <ChildContent>
        <p>Modal content goes here</p>
    </ChildContent>
    <FooterContent>
        <button class="btn btn-primary" @onclick="HandleSave">Save</button>
        <button class="btn btn-secondary" @onclick="HandleClose">Cancel</button>
    </FooterContent>
</Modal>
```

**Parameters**:

- `IsVisible` (bool): Controls visibility
- `Title` (string): Modal title
- `Size` (ModalSize): Small, Default, Large, ExtraLarge
- `Position` (ModalPosition): Top, Centered
- `ShowCloseButton` (bool): Show X button (default: true)
- `CloseOnBackdropClick` (bool): Close on backdrop click (default: true)
- `OnClose` (EventCallback): Called when modal closes
- `HeaderContent` (RenderFragment): Custom header (overrides Title)
- `ChildContent` (RenderFragment): Main modal body
- `FooterContent` (RenderFragment): Footer buttons/actions

**Example - Confirmation Dialog**:

```razor
<Modal IsVisible="@showConfirm"
       Title="Confirm Delete"
       Size="ModalSize.Small"
       Position="ModalPosition.Centered"
       OnClose="@(() => showConfirm = false)">
    <ChildContent>
        Are you sure you want to delete this item?
    </ChildContent>
    <FooterContent>
        <button class="btn btn-danger" @onclick="ConfirmDelete">Delete</button>
        <button class="btn btn-secondary" @onclick="@(() => showConfirm = false)">Cancel</button>
    </FooterContent>
</Modal>
```

### Card Component

**Purpose**: Container for related content with header, body, and footer

**Basic Usage**:

```razor
<Card Title="Card Title">
    <ChildContent>
        Card body content
    </ChildContent>
</Card>
```

**Parameters**:

- `Title` (string): Card title in header
- `FullHeight` (bool): Stretch to container height
- `CssClass` (string): Additional CSS classes
- `HeaderCssClass`, `BodyCssClass`, `FooterCssClass` (string): Section-specific classes
- `HeaderContent` (RenderFragment): Custom header (overrides Title)
- `ChildContent` (RenderFragment): Main card body
- `FooterContent` (RenderFragment): Card footer

**Example - Dashboard Widget**:

```razor
<Card Title="Recent Activity" FullHeight="true" CssClass="shadow">
    <ChildContent>
        <ul>
            @foreach (var activity in recentActivities)
            {
                <li>@activity.Description - @activity.Date</li>
            }
        </ul>
    </ChildContent>
    <FooterContent>
        <a href="/activity">View All</a>
    </FooterContent>
</Card>
```

### DataTable Component

**Purpose**: Display tabular data with customizable headers and rows

**Basic Usage**:

```razor
<DataTable Items="@users" TItem="User">
    <HeaderTemplate>
        <th>Name</th>
        <th>Email</th>
        <th>Status</th>
    </HeaderTemplate>
    <RowTemplate Context="user">
        <td>@user.Name</td>
        <td>@user.Email</td>
        <td>@user.Status</td>
    </RowTemplate>
</DataTable>
```

**Parameters**:

- `Items` (IEnumerable<TItem>): Data collection
- `HeaderTemplate` (RenderFragment): Table header row
- `RowTemplate` (RenderFragment<TItem>): Template for each data row
- `EmptyMessage` (RenderFragment): Custom message when no data
- `ShowHeader` (bool): Display header (default: true)
- `TableCssClass` (string): CSS classes (default: "table-striped table-hover")

**Example - With Empty State**:

```razor
<DataTable Items="@orders" TItem="Order" TableCssClass="table table-bordered">
    <HeaderTemplate>
        <th>Order #</th>
        <th>Customer</th>
        <th>Total</th>
        <th>Status</th>
    </HeaderTemplate>
    <RowTemplate Context="order">
        <td>@order.OrderNumber</td>
        <td>@order.CustomerName</td>
        <td>@order.Total.ToString("C")</td>
        <td><span class="badge bg-@order.StatusColor">@order.Status</span></td>
    </RowTemplate>
    <EmptyMessage>
        <div class="text-center py-4">
            <i class="bi bi-inbox fs-1 text-muted"></i>
            <p>No orders found</p>
        </div>
    </EmptyMessage>
</DataTable>
```

### FormField Component

**Purpose**: Consistent form field layout with label, input, and help text

**Basic Usage**:

```razor
<FormField Label="Email" Required="true">
    <input type="email" class="form-control" @bind="email" />
</FormField>
```

**Parameters**:

- `Label` (string): Field label text
- `Required` (bool): Show required indicator (\*)
- `HelpText` (string): Help text below input
- `CssClass` (string): Additional container classes
- `LabelCssClass` (string): Label-specific classes
- `ChildContent` (RenderFragment): Input control

**Example - Complete Form**:

```razor
<EditForm Model="@model" OnValidSubmit="@HandleSubmit">
    <DataAnnotationsValidator />

    <FormField Label="Full Name" Required="true">
        <InputText class="form-control" @bind-Value="model.Name" />
    </FormField>

    <FormField Label="Email" Required="true" HelpText="We'll never share your email">
        <InputText type="email" class="form-control" @bind-Value="model.Email" />
    </FormField>

    <FormField Label="Phone" HelpText="Optional contact number">
        <InputText class="form-control" @bind-Value="model.Phone" />
    </FormField>

    <button type="submit" class="btn btn-primary">Submit</button>
</EditForm>
```

### SharedMainLayout Component

**Purpose**: Base layout structure with sidebar, header, and content areas

**Basic Usage**:

```razor
@inherits LayoutComponentBase
@layout SharedMainLayout

<SharedMainLayout Theme="@theme">
    <SidebarContent>
        <NavMenu />
    </SidebarContent>
    <AuthorizedHeaderContent>
        <UserProfile />
    </AuthorizedHeaderContent>
    <ChildContent>
        @Body
    </ChildContent>
    <FooterContent>
        <footer>© 2026 Nine</footer>
    </FooterContent>
</SharedMainLayout>
```

**Parameters**:

- `Theme` (string): Theme name (e.g., "light", "dark")
- `SidebarContent` (RenderFragment): Navigation sidebar
- `AuthorizedHeaderContent` (RenderFragment): Header for authenticated users
- `NotAuthorizedContent` (RenderFragment): Header for anonymous users
- `ChildContent` (RenderFragment): Main page content
- `FooterContent` (RenderFragment): Footer content

### Notification Components

**Note**: Notification components are feature placeholders awaiting NotificationService implementation.

**NotificationBell** - Notification indicator with dropdown
**NotificationCenter** - Full notification management page
**NotificationPreferences** - User notification settings

## Adding New Shared Components

### 1. Determine Component Category

- **Common**: Generic UI element (button, input, dialog) → `Components/Common/`
- **Layout**: Layout structure (header, footer, nav) → `Components/Layout/`
- **Feature**: Domain-specific (notifications, reports) → `Features/[FeatureName]/`

### 2. Create Component File

```bash
cd 3-Nine.UI.Shared
# For common components
touch Components/Common/YourComponent.razor
# For feature components
mkdir -p Features/YourFeature
touch Features/YourFeature/YourComponent.razor
```

### 3. Define Component

```razor
@namespace Nine.UI.Shared.Components.Common

<div class="your-component @CssClass">
    @ChildContent
</div>

@code {
    /// <summary>
    /// Additional CSS classes to apply
    /// </summary>
    [Parameter] public string CssClass { get; set; } = "";

    /// <summary>
    /// Content to render inside the component
    /// </summary>
    [Parameter] public RenderFragment? ChildContent { get; set; }
}
```

### 4. Write Tests

```bash
cd 6-Tests/Nine.UI.Shared.Tests
touch Components/Common/YourComponentTests.cs
```

```csharp
using Nine.UI.Shared.Components.Common;
using Bunit;
using FluentAssertions;
using Xunit;

namespace Nine.UI.Shared.Tests.Components.Common;

public class YourComponentTests : TestContext
{
    [Fact]
    public void YourComponent_Renders_Successfully()
    {
        // Arrange & Act
        var cut = Render<YourComponent>(parameters => parameters
            .AddChildContent("Test Content")
        );

        // Assert
        cut.Markup.Should().Contain("Test Content");
        cut.Markup.Should().Contain("your-component");
    }
}
```

### 5. Update Product \_Imports.razor

```razor
@using Nine.UI.Shared.Components.Common
```

### 6. Use in Product Pages

```razor
<YourComponent>Content here</YourComponent>
```

## Component Parameter Conventions

### Standard Parameters

- **CssClass**: Additional CSS classes to apply to root element
- **ChildContent**: Main content inside component
- **OnClick / OnChange**: Event callbacks
- **IsVisible / IsOpen**: Visibility toggles

### Naming Conventions

- Use **PascalCase** for all parameters
- Prefix boolean parameters with **Is/Has/Show** (e.g., `IsVisible`, `HasError`, `ShowHeader`)
- Event callbacks use **On** prefix (e.g., `OnClose`, `OnSave`, `OnItemSelected`)

### Documentation

Add XML documentation for all parameters:

```csharp
/// <summary>
/// The title displayed in the modal header
/// </summary>
[Parameter] public string Title { get; set; } = "";
```

## Styling Guidelines

### CSS Scoping

Components use CSS isolation (`ComponentName.razor.css`) to prevent style leakage:

```css
/* SharedMainLayout.razor.css */
.page {
  display: flex;
  height: 100vh;
}
```

### Bootstrap Integration

Components use Bootstrap 5 classes:

- Layout: `container`, `row`, `col`
- Spacing: `m-*`, `p-*`, `mb-3`
- Display: `d-flex`, `d-none`, `d-block`
- Components: `btn`, `card`, `modal`, `table`

### Custom Styling

Allow customization via `CssClass` parameters:

```razor
<Card CssClass="shadow-lg border-primary">
    Content
</Card>
```

## Testing Requirements

All shared components **must** have unit tests.

### Test Setup

Tests use:

- **xUnit**: Test framework
- **bUnit 2.4.2**: Blazor component testing
- **FluentAssertions 8.8.0**: Readable assertions

### Test Template

```csharp
public class ComponentTests : TestContext
{
    [Fact]
    public void Component_Renders_Parameter_Value()
    {
        // Arrange & Act
        var cut = Render<Component>(parameters => parameters
            .Add(p => p.ParameterName, "value")
        );

        // Assert
        cut.Markup.Should().Contain("value");
    }
}
```

### Running Tests

```bash
cd /home/cisguru/Source/Nine
dotnet test 6-Tests/Nine.UI.Shared.Tests
```

**Current Status**: 47 tests, 100% passing

## Best Practices

### 1. Keep Components Generic

❌ **Avoid**: Product-specific logic

```razor
@if (IsNineEdition)
{
    <div>Nine-only content</div>
}
```

✅ **Better**: Use parameters for customization

```razor
@if (ShowAdvancedFeatures)
{
    <div>Advanced content</div>
}
```

### 2. Support Composition

Components should support both content and customization:

```razor
<Card>
    <HeaderContent>
        <h3>Custom Header</h3>
    </HeaderContent>
    <ChildContent>
        Main content
    </ChildContent>
</Card>
```

### 3. Provide Sensible Defaults

```csharp
[Parameter] public string CssClass { get; set; } = "";
[Parameter] public bool ShowHeader { get; set; } = true;
[Parameter] public ModalSize Size { get; set; } = ModalSize.Default;
```

### 4. Document Complex Behavior

Use XML comments and examples:

```csharp
/// <summary>
/// Displays a modal dialog overlay.
/// The modal can be closed by clicking the close button, pressing Escape,
/// or clicking the backdrop (if CloseOnBackdropClick is true).
/// </summary>
```

## Troubleshooting

### Build Errors

**Problem**: Component not found

```
error CS0246: The type or namespace name 'Modal' could not be found
```

**Solution**: Add using directive to `_Imports.razor`:

```razor
@using Nine.UI.Shared.Components.Common
```

### CSS Not Applying

**Problem**: Component styles not showing

**Solution**: Ensure scoped CSS files are included:

1. Check `ComponentName.razor.css` exists
2. Verify CSS isolation is enabled in project
3. Rebuild the project

### Test Failures

**Problem**: AuthorizeView components fail in tests

**Solution**: Add authorization context:

```csharp
public class LayoutTests : TestContext
{
    public LayoutTests()
    {
        // Add test authorization
        Services.AddSingleton<AuthenticationStateProvider>(
            new TestAuthStateProvider());
    }
}
```

## Contributing

### Before Submitting

1. ✅ Add XML documentation to all public members
2. ✅ Write unit tests (target >80% coverage)
3. ✅ Test in both Nine and Professional
4. ✅ Update this README if adding new patterns
5. ✅ Follow existing naming conventions

### Review Checklist

- [ ] Component is generic and reusable
- [ ] Parameters have XML documentation
- [ ] Unit tests cover main scenarios
- [ ] CSS uses scoped styles
- [ ] Bootstrap classes used where appropriate
- [ ] No product-specific dependencies

## Resources

- [Blazor Component Documentation](https://learn.microsoft.com/aspnet/core/blazor/components/)
- [bUnit Testing Guide](https://bunit.dev/)
- [Bootstrap 5 Documentation](https://getbootstrap.com/docs/5.0/)
- [Razor Class Libraries](https://learn.microsoft.com/aspnet/core/razor-pages/ui-class)

## Version History

- **v0.2.0** (January 2026)
  - ✅ Phase 3.5: Features structure (Notifications moved to Features/)
  - ✅ Phase 6: Complete test suite (47 tests, 100% passing)
  - ✅ Phase 7: Documentation and guidelines
- **v0.1.0** (December 2025)
  - Initial RCL creation
  - SharedMainLayout, Common components (Modal, Card, DataTable, FormField)
  - Notification placeholders
  - Static assets (CSS, JS)

## Support

For questions or issues:

1. Check this README
2. Review existing component implementations
3. Consult the test suite for usage examples
4. Refer to the [11-Shared-UI-Implementation-Plan.md](../../Documents/Orion/Projects/Nine/Plans%20Pending%20Scope/11-Shared-UI-Implementation-Plan.md)
