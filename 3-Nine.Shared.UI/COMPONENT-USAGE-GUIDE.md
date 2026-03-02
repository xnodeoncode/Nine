# Nine.UI.Shared - Component Usage Guide

This guide provides detailed usage examples and patterns for all shared components in the Nine.UI.Shared library.

## Table of Contents

1. [Common Components](#common-components)
   - [Modal](#modal)
   - [Card](#card)
   - [DataTable](#datatable)
   - [FormField](#formfield)
2. [Layout Components](#layout-components)
   - [SharedMainLayout](#sharedmainlayout)
3. [Feature Components](#feature-components)
   - [Notification Components](#notification-components)
4. [Testing Patterns](#testing-patterns)

---

## Common Components

### Modal

**File**: `Components/Common/Modal.razor`

Display content in an overlay dialog with backdrop, header, body, and footer sections.

#### Basic Example

```razor
@page "/modal-demo"

<button class="btn btn-primary" @onclick="OpenModal">Open Modal</button>

<Modal IsVisible="@isModalVisible"
       Title="My Modal"
       OnClose="@CloseModal">
    <ChildContent>
        <p>This is the modal content.</p>
    </ChildContent>
    <FooterContent>
        <button class="btn btn-secondary" @onclick="CloseModal">Close</button>
    </FooterContent>
</Modal>

@code {
    private bool isModalVisible = false;

    private void OpenModal() => isModalVisible = true;
    private void CloseModal() => isModalVisible = false;
}
```

#### All Parameters

```csharp
[Parameter] public bool IsVisible { get; set; }                      // Controls modal visibility
[Parameter] public string Title { get; set; } = "";                  // Modal header title
[Parameter] public ModalSize Size { get; set; } = ModalSize.Default; // Small, Default, Large, ExtraLarge
[Parameter] public ModalPosition Position { get; set; }              // Top, Centered
    = ModalPosition.Top;
[Parameter] public bool ShowCloseButton { get; set; } = true;        // Show X button in header
[Parameter] public bool CloseOnBackdropClick { get; set; } = true;   // Close when clicking backdrop
[Parameter] public EventCallback OnClose { get; set; }               // Called when modal closes
[Parameter] public RenderFragment? HeaderContent { get; set; }       // Custom header (overrides Title)
[Parameter] public RenderFragment? ChildContent { get; set; }        // Main modal body
[Parameter] public RenderFragment? FooterContent { get; set; }       // Footer buttons/actions
```

#### Size Variants

```razor
<!-- Small Modal -->
<Modal Size="ModalSize.Small" Title="Small Modal" IsVisible="@showSmall">
    <ChildContent>Compact content</ChildContent>
</Modal>

<!-- Large Modal -->
<Modal Size="ModalSize.Large" Title="Large Modal" IsVisible="@showLarge">
    <ChildContent>More content space</ChildContent>
</Modal>

<!-- Extra Large Modal -->
<Modal Size="ModalSize.ExtraLarge" Title="Extra Large" IsVisible="@showXL">
    <ChildContent>Maximum content space</ChildContent>
</Modal>
```

#### Positioning

```razor
<!-- Top Positioned (default) -->
<Modal Position="ModalPosition.Top" Title="Top Modal" IsVisible="@show">
    <ChildContent>Modal appears at top of viewport</ChildContent>
</Modal>

<!-- Centered -->
<Modal Position="ModalPosition.Centered" Title="Centered Modal" IsVisible="@show">
    <ChildContent>Modal appears in viewport center</ChildContent>
</Modal>
```

#### Custom Header

```razor
<Modal IsVisible="@isVisible" OnClose="@HandleClose">
    <HeaderContent>
        <div class="d-flex align-items-center">
            <i class="bi bi-exclamation-triangle-fill text-warning me-2"></i>
            <h5 class="mb-0">Warning: Data Will Be Lost</h5>
        </div>
    </HeaderContent>
    <ChildContent>
        <p>Are you sure you want to proceed? This action cannot be undone.</p>
    </ChildContent>
    <FooterContent>
        <button class="btn btn-warning" @onclick="Proceed">Proceed</button>
        <button class="btn btn-secondary" @onclick="HandleClose">Cancel</button>
    </FooterContent>
</Modal>
```

#### Form in Modal

```razor
<Modal IsVisible="@showEditModal"
       Title="Edit User"
       Size="ModalSize.Large"
       CloseOnBackdropClick="false"
       OnClose="@CancelEdit">
    <ChildContent>
        <EditForm Model="@editModel" OnValidSubmit="@SaveUser">
            <DataAnnotationsValidator />
            <ValidationSummary />

            <FormField Label="Name" Required="true">
                <InputText class="form-control" @bind-Value="editModel.Name" />
            </FormField>

            <FormField Label="Email" Required="true">
                <InputText type="email" class="form-control" @bind-Value="editModel.Email" />
            </FormField>

            <FormField Label="Role">
                <InputSelect class="form-select" @bind-Value="editModel.Role">
                    <option value="User">User</option>
                    <option value="Admin">Admin</option>
                </InputSelect>
            </FormField>
        </EditForm>
    </ChildContent>
    <FooterContent>
        <button type="submit" class="btn btn-primary" @onclick="SaveUser">Save Changes</button>
        <button type="button" class="btn btn-secondary" @onclick="CancelEdit">Cancel</button>
    </FooterContent>
</Modal>
```

#### Confirmation Dialog Pattern

```razor
@code {
    private bool showDeleteConfirm = false;
    private int itemToDelete;

    private void ShowDeleteConfirmation(int itemId)
    {
        itemToDelete = itemId;
        showDeleteConfirm = true;
    }

    private async Task ConfirmDelete()
    {
        await DeleteItem(itemToDelete);
        showDeleteConfirm = false;
        // Show success message
    }
}

<Modal IsVisible="@showDeleteConfirm"
       Title="Confirm Delete"
       Size="ModalSize.Small"
       Position="ModalPosition.Centered"
       OnClose="@(() => showDeleteConfirm = false)">
    <ChildContent>
        <div class="text-center">
            <i class="bi bi-trash fs-1 text-danger"></i>
            <p class="mt-3">Are you sure you want to delete this item?</p>
            <p class="text-muted small">This action cannot be undone.</p>
        </div>
    </ChildContent>
    <FooterContent>
        <button class="btn btn-danger" @onclick="ConfirmDelete">
            <i class="bi bi-trash"></i> Delete
        </button>
        <button class="btn btn-secondary" @onclick="@(() => showDeleteConfirm = false)">
            Cancel
        </button>
    </FooterContent>
</Modal>
```

---

### Card

**File**: `Components/Common/Card.razor`

Container for related content with optional header, body, and footer sections.

#### Basic Example

```razor
<Card Title="User Profile">
    <ChildContent>
        <p><strong>Name:</strong> John Doe</p>
        <p><strong>Email:</strong> john@example.com</p>
    </ChildContent>
</Card>
```

#### All Parameters

```csharp
[Parameter] public string Title { get; set; } = "";                // Card title in header
[Parameter] public bool FullHeight { get; set; }                   // Stretch to 100% height
[Parameter] public string CssClass { get; set; } = "";            // Additional CSS classes
[Parameter] public string HeaderCssClass { get; set; } = "";      // Header-specific classes
[Parameter] public string BodyCssClass { get; set; } = "";        // Body-specific classes
[Parameter] public string FooterCssClass { get; set; } = "";      // Footer-specific classes
[Parameter] public RenderFragment? HeaderContent { get; set; }     // Custom header
[Parameter] public RenderFragment? ChildContent { get; set; }      // Card body content
[Parameter] public RenderFragment? FooterContent { get; set; }     // Card footer
```

#### Card with Footer

```razor
<Card Title="Recent Orders">
    <ChildContent>
        <ul class="list-unstyled">
            <li>Order #12345 - $299.99</li>
            <li>Order #12346 - $149.50</li>
            <li>Order #12347 - $89.99</li>
        </ul>
    </ChildContent>
    <FooterContent>
        <a href="/orders" class="btn btn-sm btn-outline-primary">View All Orders</a>
    </FooterContent>
</Card>
```

#### Custom Header with Actions

```razor
<Card CssClass="shadow">
    <HeaderContent>
        <div class="d-flex justify-content-between align-items-center">
            <h5 class="mb-0">Dashboard Stats</h5>
            <div>
                <button class="btn btn-sm btn-outline-secondary" @onclick="Refresh">
                    <i class="bi bi-arrow-clockwise"></i>
                </button>
            </div>
        </div>
    </HeaderContent>
    <ChildContent>
        <div class="row">
            <div class="col-md-4">
                <h3>@totalUsers</h3>
                <small class="text-muted">Total Users</small>
            </div>
            <div class="col-md-4">
                <h3>@activeUsers</h3>
                <small class="text-muted">Active Users</small>
            </div>
            <div class="col-md-4">
                <h3>@revenue</h3>
                <small class="text-muted">Revenue</small>
            </div>
        </div>
    </ChildContent>
</Card>
```

#### Full Height Card (Dashboard Layout)

```razor
<div class="row" style="height: 400px;">
    <div class="col-md-6">
        <Card Title="Activity Feed" FullHeight="true" BodyCssClass="overflow-auto">
            <ChildContent>
                @foreach (var activity in activities)
                {
                    <div class="mb-2 pb-2 border-bottom">
                        <strong>@activity.User</strong> @activity.Action
                        <br />
                        <small class="text-muted">@activity.Time.ToString("g")</small>
                    </div>
                }
            </ChildContent>
        </Card>
    </div>
    <div class="col-md-6">
        <Card Title="Quick Stats" FullHeight="true">
            <ChildContent>
                <!-- Stats content -->
            </ChildContent>
        </Card>
    </div>
</div>
```

#### Styled Cards

```razor
<!-- Primary Card -->
<Card Title="Important Notice"
      CssClass="border-primary"
      HeaderCssClass="bg-primary text-white">
    <ChildContent>
        This is an important notice.
    </ChildContent>
</Card>

<!-- Success Card -->
<Card Title="Success"
      CssClass="border-success"
      HeaderCssClass="bg-success text-white">
    <ChildContent>
        Operation completed successfully!
    </ChildContent>
</Card>

<!-- Shadow Card -->
<Card Title="Featured Content" CssClass="shadow-lg">
    <ChildContent>
        This card has a prominent shadow.
    </ChildContent>
</Card>
```

---

### DataTable

**File**: `Components/Common/DataTable.razor`

Display tabular data with customizable headers and row templates.

#### Basic Example

```razor
<DataTable Items="@users" TItem="User">
    <HeaderTemplate>
        <th>Name</th>
        <th>Email</th>
        <th>Role</th>
    </HeaderTemplate>
    <RowTemplate Context="user">
        <td>@user.Name</td>
        <td>@user.Email</td>
        <td>@user.Role</td>
    </RowTemplate>
</DataTable>

@code {
    private List<User> users = new();
}
```

#### All Parameters

```csharp
[Parameter] public IEnumerable<TItem>? Items { get; set; }         // Data collection
[Parameter] public RenderFragment? HeaderTemplate { get; set; }     // Table header row
[Parameter] public RenderFragment<TItem>? RowTemplate { get; set; } // Row template
[Parameter] public RenderFragment? EmptyMessage { get; set; }       // Custom empty message
[Parameter] public bool ShowHeader { get; set; } = true;            // Display header
[Parameter] public string TableCssClass { get; set; }               // Table CSS classes
    = "table table-striped table-hover";
```

#### With Actions Column

```razor
<DataTable Items="@orders" TItem="Order">
    <HeaderTemplate>
        <th>Order #</th>
        <th>Customer</th>
        <th>Total</th>
        <th>Status</th>
        <th>Actions</th>
    </HeaderTemplate>
    <RowTemplate Context="order">
        <td>@order.OrderNumber</td>
        <td>@order.CustomerName</td>
        <td>@order.Total.ToString("C")</td>
        <td>
            <span class="badge bg-@GetStatusColor(order.Status)">
                @order.Status
            </span>
        </td>
        <td>
            <button class="btn btn-sm btn-outline-primary"
                    @onclick="() => ViewOrder(order.Id)">
                <i class="bi bi-eye"></i>
            </button>
            <button class="btn btn-sm btn-outline-secondary"
                    @onclick="() => EditOrder(order.Id)">
                <i class="bi bi-pencil"></i>
            </button>
            <button class="btn btn-sm btn-outline-danger"
                    @onclick="() => DeleteOrder(order.Id)">
                <i class="bi bi-trash"></i>
            </button>
        </td>
    </RowTemplate>
</DataTable>
```

#### Custom Empty State

```razor
<DataTable Items="@products" TItem="Product">
    <HeaderTemplate>
        <th>Name</th>
        <th>SKU</th>
        <th>Price</th>
        <th>Stock</th>
    </HeaderTemplate>
    <RowTemplate Context="product">
        <td>@product.Name</td>
        <td>@product.SKU</td>
        <td>@product.Price.ToString("C")</td>
        <td>@product.Stock</td>
    </RowTemplate>
    <EmptyMessage>
        <div class="text-center py-5">
            <i class="bi bi-inbox fs-1 text-muted"></i>
            <p class="mt-3 mb-1 fw-bold">No Products Found</p>
            <p class="text-muted">Start by adding your first product.</p>
            <button class="btn btn-primary mt-2" @onclick="ShowAddProduct">
                <i class="bi bi-plus-circle"></i> Add Product
            </button>
        </div>
    </EmptyMessage>
</DataTable>
```

#### Sortable Table Pattern

```razor
@page "/sortable-users"

<DataTable Items="@SortedUsers" TItem="User" TableCssClass="table table-bordered">
    <HeaderTemplate>
        <th @onclick="() => SetSortColumn(nameof(User.Name))" style="cursor: pointer;">
            Name @GetSortIcon(nameof(User.Name))
        </th>
        <th @onclick="() => SetSortColumn(nameof(User.Email))" style="cursor: pointer;">
            Email @GetSortIcon(nameof(User.Email))
        </th>
        <th @onclick="() => SetSortColumn(nameof(User.Created))" style="cursor: pointer;">
            Created @GetSortIcon(nameof(User.Created))
        </th>
    </HeaderTemplate>
    <RowTemplate Context="user">
        <td>@user.Name</td>
        <td>@user.Email</td>
        <td>@user.Created.ToString("d")</td>
    </RowTemplate>
</DataTable>

@code {
    private List<User> users = new();
    private string sortColumn = nameof(User.Name);
    private bool sortAscending = true;

    private IEnumerable<User> SortedUsers => sortColumn switch
    {
        nameof(User.Name) => sortAscending
            ? users.OrderBy(u => u.Name)
            : users.OrderByDescending(u => u.Name),
        nameof(User.Email) => sortAscending
            ? users.OrderBy(u => u.Email)
            : users.OrderByDescending(u => u.Email),
        nameof(User.Created) => sortAscending
            ? users.OrderBy(u => u.Created)
            : users.OrderByDescending(u => u.Created),
        _ => users
    };

    private void SetSortColumn(string column)
    {
        if (sortColumn == column)
        {
            sortAscending = !sortAscending;
        }
        else
        {
            sortColumn = column;
            sortAscending = true;
        }
    }

    private string GetSortIcon(string column)
    {
        if (sortColumn != column) return "";
        return sortAscending ? "↑" : "↓";
    }
}
```

#### Nested Data with Expandable Rows

```razor
@foreach (var order in orders)
{
    <tr @onclick="() => ToggleDetails(order.Id)" style="cursor: pointer;">
        <td>
            <i class="bi @(expandedOrders.Contains(order.Id) ? "bi-chevron-down" : "bi-chevron-right")"></i>
            @order.OrderNumber
        </td>
        <td>@order.CustomerName</td>
        <td>@order.Total.ToString("C")</td>
    </tr>

    @if (expandedOrders.Contains(order.Id))
    {
        <tr>
            <td colspan="3" class="bg-light">
                <DataTable Items="@order.Items" TItem="OrderItem" ShowHeader="false">
                    <RowTemplate Context="item">
                        <td class="ps-4">@item.ProductName</td>
                        <td>Qty: @item.Quantity</td>
                        <td>@item.Price.ToString("C")</td>
                    </RowTemplate>
                </DataTable>
            </td>
        </tr>
    }
}

@code {
    private HashSet<int> expandedOrders = new();

    private void ToggleDetails(int orderId)
    {
        if (expandedOrders.Contains(orderId))
            expandedOrders.Remove(orderId);
        else
            expandedOrders.Add(orderId);
    }
}
```

---

### FormField

**File**: `Components/Common/FormField.razor`

Consistent form field layout with label, input, help text, and validation.

#### Basic Example

```razor
<FormField Label="Email" Required="true">
    <InputText class="form-control" @bind-Value="model.Email" />
</FormField>
```

#### All Parameters

```csharp
[Parameter] public string Label { get; set; } = "";          // Field label text
[Parameter] public bool Required { get; set; }               // Show required indicator (*)
[Parameter] public string HelpText { get; set; } = "";       // Help text below input
[Parameter] public string CssClass { get; set; } = "";       // Container CSS classes
[Parameter] public string LabelCssClass { get; set; } = "";  // Label-specific classes
[Parameter] public RenderFragment? ChildContent { get; set; } // Input control
```

#### Complete Form Example

```razor
@page "/create-user"

<h3>Create New User</h3>

<EditForm Model="@model" OnValidSubmit="@HandleSubmit">
    <DataAnnotationsValidator />
    <ValidationSummary class="alert alert-danger" />

    <FormField Label="Full Name" Required="true">
        <InputText class="form-control" @bind-Value="model.FullName" />
        <ValidationMessage For="@(() => model.FullName)" />
    </FormField>

    <FormField Label="Email Address" Required="true" HelpText="We'll never share your email">
        <InputText type="email" class="form-control" @bind-Value="model.Email" />
        <ValidationMessage For="@(() => model.Email)" />
    </FormField>

    <FormField Label="Phone Number" HelpText="Optional contact number">
        <InputText class="form-control" @bind-Value="model.Phone" placeholder="+1 (555) 123-4567" />
    </FormField>

    <FormField Label="Role" Required="true">
        <InputSelect class="form-select" @bind-Value="model.Role">
            <option value="">-- Select Role --</option>
            <option value="User">User</option>
            <option value="Admin">Administrator</option>
            <option value="Manager">Manager</option>
        </InputSelect>
        <ValidationMessage For="@(() => model.Role)" />
    </FormField>

    <FormField Label="Date of Birth">
        <InputDate class="form-control" @bind-Value="model.DateOfBirth" />
    </FormField>

    <FormField Label="Active">
        <div class="form-check">
            <InputCheckbox class="form-check-input" @bind-Value="model.IsActive" id="isActive" />
            <label class="form-check-label" for="isActive">
                User account is active
            </label>
        </div>
    </FormField>

    <FormField Label="Bio" HelpText="Optional description (max 500 characters)">
        <InputTextArea class="form-control" rows="4" @bind-Value="model.Bio" />
    </FormField>

    <div class="mt-3">
        <button type="submit" class="btn btn-primary">Create User</button>
        <button type="button" class="btn btn-secondary" @onclick="Cancel">Cancel</button>
    </div>
</EditForm>

@code {
    private CreateUserModel model = new();

    private async Task HandleSubmit()
    {
        // Save user
        await UserService.CreateAsync(model);
        NavigationManager.NavigateTo("/users");
    }

    private void Cancel()
    {
        NavigationManager.NavigateTo("/users");
    }
}
```

#### Horizontal Form Layout

```razor
<style>
    .form-horizontal .form-label {
        text-align: right;
    }
</style>

<EditForm Model="@model" OnValidSubmit="@HandleSubmit" class="form-horizontal">
    <div class="row mb-3">
        <FormField Label="Name" Required="true" CssClass="col-md-6" LabelCssClass="col-md-4">
            <InputText class="form-control" @bind-Value="model.Name" />
        </FormField>
    </div>

    <div class="row mb-3">
        <FormField Label="Email" Required="true" CssClass="col-md-6" LabelCssClass="col-md-4">
            <InputText type="email" class="form-control" @bind-Value="model.Email" />
        </FormField>
    </div>
</EditForm>
```

#### Custom Input Components

```razor
<FormField Label="Profile Picture" HelpText="JPG or PNG, max 2MB">
    <InputFile class="form-control" OnChange="@HandleFileSelected" accept="image/*" />
</FormField>

<FormField Label="Color Theme">
    <InputRadioGroup @bind-Value="model.Theme" class="btn-group" role="group">
        <InputRadio Value="@("light")" class="btn-check" id="theme-light" />
        <label class="btn btn-outline-primary" for="theme-light">Light</label>

        <InputRadio Value="@("dark")" class="btn-check" id="theme-dark" />
        <label class="btn btn-outline-primary" for="theme-dark">Dark</label>

        <InputRadio Value="@("auto")" class="btn-check" id="theme-auto" />
        <label class="btn btn-outline-primary" for="theme-auto">Auto</label>
    </InputRadioGroup>
</FormField>

<FormField Label="Price Range">
    <div class="d-flex gap-2 align-items-center">
        <InputNumber class="form-control" @bind-Value="model.MinPrice" placeholder="Min" />
        <span>to</span>
        <InputNumber class="form-control" @bind-Value="model.MaxPrice" placeholder="Max" />
    </div>
</FormField>
```

---

## Layout Components

### SharedMainLayout

**File**: `Components/Layout/SharedMainLayout.razor`

Base layout structure providing consistent navigation, header, content area, and footer across products.

#### Basic Example

```razor
@inherits LayoutComponentBase
@layout SharedMainLayout

<SharedMainLayout Theme="@currentTheme">
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
        <footer class="text-center py-3">
            © 2026 Nine. All rights reserved.
        </footer>
    </FooterContent>
</SharedMainLayout>

@code {
    private string currentTheme = "light";
}
```

#### All Parameters

```csharp
[Parameter] public string Theme { get; set; } = "light";                      // Theme name
[Parameter] public RenderFragment? SidebarContent { get; set; }               // Navigation sidebar
[Parameter] public RenderFragment? AuthorizedHeaderContent { get; set; }      // Header (authenticated)
[Parameter] public RenderFragment? NotAuthorizedContent { get; set; }         // Header (anonymous)
[Parameter] public RenderFragment? ChildContent { get; set; }                 // Main content
[Parameter] public RenderFragment? FooterContent { get; set; }                // Footer content
```

#### Complete Layout Implementation

```razor
<!-- MainLayout.razor -->
@inherits LayoutComponentBase

<SharedMainLayout Theme="@_themeService.CurrentTheme">
    <SidebarContent>
        <nav class="nav flex-column">
            <NavLink class="nav-link" href="/" Match="NavLinkMatch.All">
                <i class="bi bi-house-door"></i> Dashboard
            </NavLink>
            <NavLink class="nav-link" href="/users">
                <i class="bi bi-people"></i> Users
            </NavLink>
            <NavLink class="nav-link" href="/orders">
                <i class="bi bi-cart"></i> Orders
            </NavLink>
            <NavLink class="nav-link" href="/reports">
                <i class="bi bi-graph-up"></i> Reports
            </NavLink>
            <NavLink class="nav-link" href="/settings">
                <i class="bi bi-gear"></i> Settings
            </NavLink>
        </nav>
    </SidebarContent>

    <AuthorizedHeaderContent>
        <div class="d-flex align-items-center gap-3">
            <!-- Search -->
            <div class="input-group" style="max-width: 300px;">
                <span class="input-group-text">
                    <i class="bi bi-search"></i>
                </span>
                <input type="text" class="form-control" placeholder="Search..." />
            </div>

            <!-- Notifications -->
            <NotificationBell />

            <!-- User Dropdown -->
            <div class="dropdown">
                <button class="btn btn-link text-decoration-none"
                        type="button"
                        id="userMenu"
                        data-bs-toggle="dropdown">
                    <img src="@userAvatar" class="rounded-circle" width="32" height="32" />
                    <span class="ms-2">@userName</span>
                </button>
                <ul class="dropdown-menu dropdown-menu-end">
                    <li><a class="dropdown-item" href="/profile">Profile</a></li>
                    <li><a class="dropdown-item" href="/settings">Settings</a></li>
                    <li><hr class="dropdown-divider" /></li>
                    <li><a class="dropdown-item" @onclick="Logout">Logout</a></li>
                </ul>
            </div>
        </div>
    </AuthorizedHeaderContent>

    <NotAuthorizedContent>
        <div class="d-flex gap-2">
            <a href="/login" class="btn btn-outline-primary">Login</a>
            <a href="/register" class="btn btn-primary">Sign Up</a>
        </div>
    </NotAuthorizedContent>

    <ChildContent>
        <ErrorBoundary>
            <ChildContent>
                <div class="container-fluid py-3">
                    @Body
                </div>
            </ChildContent>
            <ErrorContent Context="exception">
                <div class="alert alert-danger">
                    <h4>An error occurred</h4>
                    <p>@exception.Message</p>
                </div>
            </ErrorContent>
        </ErrorBoundary>
    </ChildContent>

    <FooterContent>
        <footer class="bg-light border-top py-3">
            <div class="container-fluid">
                <div class="row">
                    <div class="col-md-6">
                        © 2026 Nine. All rights reserved.
                    </div>
                    <div class="col-md-6 text-end">
                        <a href="/privacy">Privacy</a> |
                        <a href="/terms">Terms</a> |
                        <a href="/support">Support</a>
                    </div>
                </div>
            </div>
        </footer>
    </FooterContent>
</SharedMainLayout>

@code {
    [Inject] private IThemeService _themeService { get; set; } = default!;

    private string userName = "John Doe";
    private string userAvatar = "/images/default-avatar.png";

    private void Logout()
    {
        // Handle logout
    }
}
```

---

## Feature Components

### Notification Components

**Files**:

- `Features/Notifications/NotificationBell.razor`
- `Features/Notifications/NotificationCenter.razor`
- `Features/Notifications/NotificationPreferences.razor`

**Status**: ⚠️ These are placeholder components awaiting `INotificationService` implementation.

#### NotificationBell Usage (Planned)

```razor
<!-- In SharedMainLayout header -->
<AuthorizedHeaderContent>
    <NotificationBell />
    <UserProfile />
</AuthorizedHeaderContent>
```

#### NotificationCenter Usage (Planned)

```razor
@page "/notifications"

<h3>Notification Center</h3>

<NotificationCenter />
```

#### NotificationPreferences Usage (Planned)

```razor
@page "/settings/notifications"

<h3>Notification Settings</h3>

<NotificationPreferences />
```

---

## Testing Patterns

### Basic Component Test

```csharp
using Nine.UI.Shared.Components.Common;
using Bunit;
using FluentAssertions;
using Xunit;

namespace Nine.UI.Shared.Tests.Components.Common;

public class CardTests : TestContext
{
    [Fact]
    public void Card_Renders_Title_Successfully()
    {
        // Arrange & Act
        var cut = Render<Card>(parameters => parameters
            .Add(p => p.Title, "Test Card")
        );

        // Assert
        cut.Markup.Should().Contain("Test Card");
    }

    [Fact]
    public void Card_Renders_ChildContent()
    {
        // Arrange & Act
        var cut = Render<Card>(parameters => parameters
            .Add(p => p.Title, "Card")
            .AddChildContent("<p>Content</p>")
        );

        // Assert
        cut.Markup.Should().Contain("<p>Content</p>");
    }
}
```

### Testing Components with HTML Content

Use `AddChildContent()` for HTML markup:

```csharp
[Fact]
public void FormField_Renders_Input_Control()
{
    // Arrange & Act
    var cut = Render<FormField>(parameters => parameters
        .Add(p => p.Label, "Email")
        .AddChildContent("<input class='form-control' />")
    );

    // Assert
    cut.Markup.Should().Contain("<input class='form-control'");
}
```

### Testing Components with AuthorizeView

Components using `AuthorizeView` require authorization context:

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using System.Security.Claims;
using System.Threading.Tasks;

public class SharedMainLayoutTests : TestContext
{
    public SharedMainLayoutTests()
    {
        // Register authorization services
        var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.Name, "testuser"),
            new Claim(ClaimTypes.Role, "Admin")
        }, "Test"));

        var authState = Task.FromResult(new AuthenticationState(user));

        Services.AddSingleton<AuthenticationStateProvider>(
            new TestAuthStateProvider(authState));
        Services.AddSingleton<IAuthorizationService>(
            new TestAuthorizationService());
        Services.AddSingleton<IAuthorizationPolicyProvider>(
            new TestAuthorizationPolicyProvider());
    }

    [Fact]
    public void SharedMainLayout_Renders_AuthorizedContent()
    {
        // Arrange & Act
        var cut = RenderLayoutWithAuth(parameters => parameters
            .AddChildContent("<p>Test content</p>")
        );

        // Assert
        cut.Markup.Should().Contain("Test content");
    }

    // Helper method to wrap in CascadingAuthenticationState
    private IRenderedComponent<SharedMainLayout> RenderLayoutWithAuth(
        Action<ComponentParameterCollectionBuilder<SharedMainLayout>> parameters)
    {
        return Render<CascadingAuthenticationState>(cascadingParams =>
        {
            cascadingParams.AddChildContent<SharedMainLayout>(parameters);
        }).FindComponent<SharedMainLayout>();
    }
}

// Test authorization helper classes
public class TestAuthStateProvider : AuthenticationStateProvider
{
    private readonly Task<AuthenticationState> _authState;
    public TestAuthStateProvider(Task<AuthenticationState> authState)
        => _authState = authState;
    public override Task<AuthenticationState> GetAuthenticationStateAsync()
        => _authState;
}

public class TestAuthorizationService : IAuthorizationService
{
    public Task<AuthorizationResult> AuthorizeAsync(
        ClaimsPrincipal user, object? resource,
        IEnumerable<IAuthorizationRequirement> requirements)
        => Task.FromResult(AuthorizationResult.Success());

    public Task<AuthorizationResult> AuthorizeAsync(
        ClaimsPrincipal user, object? resource, string policyName)
        => Task.FromResult(AuthorizationResult.Success());
}

public class TestAuthorizationPolicyProvider : IAuthorizationPolicyProvider
{
    public Task<AuthorizationPolicy> GetDefaultPolicyAsync()
        => Task.FromResult(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

    public Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
        => Task.FromResult<AuthorizationPolicy?>(
            new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

    public Task<AuthorizationPolicy?> GetFallbackPolicyAsync()
        => Task.FromResult<AuthorizationPolicy?>(null);
}
```

### Running Tests

```bash
# Run all tests
cd /home/cisguru/Source/Nine
dotnet test 6-Tests/Nine.UI.Shared.Tests

# Run specific test class
dotnet test 6-Tests/Nine.UI.Shared.Tests --filter "FullyQualifiedName~CardTests"

# Run with detailed output
dotnet test 6-Tests/Nine.UI.Shared.Tests --logger "console;verbosity=detailed"
```

---

## Best Practices Summary

1. **Component Parameters**: Always document with XML comments
2. **HTML Content**: Use `AddChildContent()` in tests for proper HTML rendering
3. **Authorization**: Wrap auth-aware components in `CascadingAuthenticationState`
4. **Customization**: Provide `CssClass` parameters for styling flexibility
5. **Defaults**: Set sensible default values for all parameters
6. **Events**: Use `EventCallback` for component events
7. **Testing**: Achieve >80% code coverage for all components
8. **Documentation**: Update this guide when adding new components

---

## Version History

- **v0.2.0** (January 2026) - Complete test suite, documentation
- **v0.1.0** (December 2025) - Initial component library release

## Support

- See [README.md](README.md) for architecture and conventions
- Check test files for usage examples
- Refer to implementation plan for roadmap
