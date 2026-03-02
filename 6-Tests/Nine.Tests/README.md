# Nine E2E Tests with Playwright

End-to-end UI testing for Nine property management system using Microsoft Playwright.

## What's Tested

These tests automate the **Phase 5.5 Multi-Organization Management** testing scenarios from `PROPERTY-TENANT-LIFECYCLE-ROADMAP.md`:

1. **Scenario 1:** Owner (Carlos) - Full access across all organizations
2. **Scenario 2:** Administrator (Alice) - Manage single organization
3. **Scenario 3:** PropertyManager (Bob) - View/edit assigned properties
4. **Scenario 4:** User (Lisa) - Read-only access
5. **Scenario 5:** Cross-organization data isolation
6. **Scenario 6:** Owner organization switching

## Prerequisites

**Your application must be running** before executing tests:

```bash
# Start the application (from /4-Nine)
dotnet watch

# Or use the watch task
```

The tests expect the app at: `https://localhost:5001`

## Running Tests

### All Tests

```bash
cd Nine.Tests
dotnet test
```

### Specific Test

```bash
dotnet test --filter "FullyQualifiedName~Scenario4_User_HasReadOnlyAccess"
```

### With Browser UI (Headed Mode)

By default, tests run headless. To see the browser:

```bash
# Set environment variable
export HEADED=1
dotnet test

# Or in PowerShell
$env:HEADED=1
dotnet test
```

### Specific Browser

```bash
# Chromium (default)
dotnet test

# Firefox
export BROWSER=firefox
dotnet test

# WebKit (Safari engine)
export BROWSER=webkit
dotnet test
```

## Test User Accounts

Tests use these accounts (must exist in your database):

| User            | Email             | Password | Role            | Organization  |
| --------------- | ----------------- | -------- | --------------- | ------------- |
| Owner           | owner1@nine.local | Today123 | Owner           | Multiple orgs |
| Administrator   | jc@example.com    | Today123 | Administrator   | Nine        |
| PropertyManager | jh@example.com    | Today123 | PropertyManager | Nine        |
| User            | mya@example.com   | Today123 | User            | Nine        |

**Ensure these users exist before running tests!**

**Organizations in test database:**

- Nine
- Nine - Colorado

## Debugging Failed Tests

### Screenshots on Failure

Playwright automatically captures screenshots when tests fail:

```
Nine.Tests/bin/Debug/net9.0/playwright-screenshots/
```

### Trace Viewer

Enable trace recording for detailed debugging:

```csharp
// In PageTest, add:
[SetUp]
public async Task Setup()
{
    await Context.Tracing.StartAsync(new()
    {
        Screenshots = true,
        Snapshots = true
    });
}

[TearDown]
public async Task Teardown()
{
    await Context.Tracing.StopAsync(new()
    {
        Path = "trace.zip"
    });
}
```

Then view traces:

```bash
pwsh bin/Debug/net9.0/playwright.ps1 show-trace trace.zip
```

### Slow Motion

Run tests in slow motion to watch interactions:

```csharp
// Modify test to launch browser with slow motion
await Page.Context.Browser.NewPageAsync(new()
{
    SlowMo = 1000 // 1 second delay between actions
});
```

## CI/CD Integration

Tests are designed for CI/CD pipelines:

```yaml
# GitHub Actions example
- name: Install Playwright Browsers
  run: pwsh Nine.Tests/bin/Debug/net9.0/playwright.ps1 install --with-deps

- name: Run E2E Tests
  run: dotnet test Nine.Tests
```

## Updating Tests

When adding new features:

1. Add test methods to `PropertyManagementTests.cs`
2. Follow naming convention: `Scenario{N}_{Description}`
3. Use descriptive assertions with custom messages
4. Keep tests isolated (no shared state between tests)

## Playwright Best Practices

### Use Role-Based Selectors

```csharp
// Good
await Page.GetByRole(AriaRole.Button, new() { Name = "Add Property" }).ClickAsync();

// Avoid
await Page.ClickAsync(".btn-primary");
```

### Wait for Elements

```csharp
// Good - explicit wait
await Page.WaitForSelectorAsync("h1:has-text('Properties')");

// Avoid - arbitrary delays
await Task.Delay(2000);
```

### Assertions

```csharp
// Good - Playwright assertions (auto-wait)
await Expect(Page.GetByText("Dashboard")).ToBeVisibleAsync();

// Avoid - NUnit assertions (no waiting)
Assert.That(await Page.Locator("text=Dashboard").IsVisibleAsync(), Is.True);
```

## Troubleshooting

### Tests can't connect to app

- Verify app is running: `curl https://localhost:5001`
- Check `BaseUrl` in tests matches your app URL
- Ensure SSL certificate is trusted

### Login failures

- Verify test users exist in database
- Check passwords match (case-sensitive!)
- Inspect login page selectors (may have changed)

### Element not found

- Use `await Page.PauseAsync()` to debug
- Inspect selector with browser dev tools
- Check if element is in shadow DOM or iframe

### Browsers not installed

```bash
pwsh bin/Debug/net9.0/playwright.ps1 install
```

## Resources

- [Playwright .NET Documentation](https://playwright.dev/dotnet/)
- [NUnit Playwright Integration](https://playwright.dev/dotnet/docs/test-runners)
- [Selectors Guide](https://playwright.dev/dotnet/docs/selectors)
- [Assertions](https://playwright.dev/dotnet/docs/test-assertions)

---

**Note:** These are E2E tests - they test the full stack (UI → API → Database). Keep them focused on critical user workflows to avoid slow, brittle test suites.
