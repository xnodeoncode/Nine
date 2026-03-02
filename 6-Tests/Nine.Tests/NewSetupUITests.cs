using Microsoft.Playwright.NUnit;
using Microsoft.Playwright;

namespace Nine.Tests;

[Parallelizable(ParallelScope.Self)]
[TestFixture]

public class NewSetupUITests : PageTest
{

    private const string BaseUrl = "http://localhost:5197";
    private const int KeepBrowserOpenSeconds = 30; // Set to 0 to close immediately
    
    public override BrowserNewContextOptions ContextOptions()
    {
        return new BrowserNewContextOptions
        {
            IgnoreHTTPSErrors = true,
            BaseURL = BaseUrl,
            RecordVideoDir = Path.Combine(Directory.GetCurrentDirectory(), "test-videos"),
            RecordVideoSize = new RecordVideoSize { Width = 1280, Height = 720 }
        };
    }

    [Test, Order(1)]
    public async Task CreateNewAccount()
    {
        await Page.GotoAsync("http://localhost:5197/");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Create Account" }).ClickAsync();
        await Page.GetByRole(AriaRole.Textbox, new() { Name = "Organization Name" }).ClickAsync();
        await Page.GetByRole(AriaRole.Textbox, new() { Name = "Organization Name" }).FillAsync("Nine");
        await Page.GetByRole(AriaRole.Textbox, new() { Name = "Organization Name" }).PressAsync("Tab");
        await Page.Locator("select[id='Input.State']").SelectOptionAsync(new[] { "TX" });
        await Page.Locator("select[id='Input.State']").PressAsync("Tab");
        await Page.GetByRole(AriaRole.Textbox, new() { Name = "Email" }).FillAsync("owner1@aquiis.com");
        await Page.GetByRole(AriaRole.Textbox, new() { Name = "Email" }).PressAsync("Tab");
        await Page.GetByRole(AriaRole.Textbox, new() { Name = "First Name" }).FillAsync("Solid");
        await Page.GetByRole(AriaRole.Textbox, new() { Name = "First Name" }).PressAsync("Tab");
        await Page.GetByRole(AriaRole.Textbox, new() { Name = "Last Name" }).FillAsync("One");
        await Page.GetByRole(AriaRole.Textbox, new() { Name = "Last Name" }).PressAsync("Tab");
        await Page.GetByRole(AriaRole.Textbox, new() { Name = "Password", Exact = true }).FillAsync("SamplePassword2025!");
        await Page.GetByRole(AriaRole.Textbox, new() { Name = "Password", Exact = true }).PressAsync("Tab");
        await Page.GetByRole(AriaRole.Textbox, new() { Name = "Confirm Password" }).FillAsync("SamplePassword2025!");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Register" }).ClickAsync();

        //await Page.WaitForSelectorAsync("h1:has-text('Register confirmation')");

        // await Page.GetByRole(AriaRole.Heading, new() { Name = "Register confirmation" }).ClickAsync();
        //await Page.GetByRole(AriaRole.Link, new() { Name = "Click here to confirm your account" }).ClickAsync();

        //await Page.WaitForSelectorAsync("h1:has-text('Confirm email')");

        // await Page.GetByText("Thank you for confirming your").ClickAsync();
        //await Page.GetByRole(AriaRole.Link, new() { Name = "Home" }).ClickAsync();
        //await Page.GetByRole(AriaRole.Button, new() { Name = "Sign In" }).ClickAsync();

        //await Page.WaitForSelectorAsync("h1:has-text('Log in')");

        // await Page.GetByRole(AriaRole.Heading, new() { Name = "Log in", Exact = true }).ClickAsync();
        // await Page.GetByRole(AriaRole.Textbox, new() { Name = "Email" }).ClickAsync();
        // await Page.GetByRole(AriaRole.Textbox, new() { Name = "Email" }).FillAsync("owner1@aquiis.com");
        // await Page.GetByRole(AriaRole.Textbox, new() { Name = "Email" }).PressAsync("Tab");
        // await Page.GetByRole(AriaRole.Textbox, new() { Name = "Password" }).FillAsync("Today123");
        // await Page.GetByRole(AriaRole.Button, new() { Name = "Log in" }).ClickAsync();

        await Page.WaitForSelectorAsync("text=Dashboard");

        await Page.GetByRole(AriaRole.Heading, new() { Name = "Property Management Dashboard", Exact= true }).ClickAsync();
        

    }

    [Test, Order(2)]
    public async Task AddProperty()
    {
        await Page.GotoAsync("http://localhost:5197/");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Sign In" }).ClickAsync();
        await Page.GetByRole(AriaRole.Textbox, new() { Name = "Email" }).ClickAsync();
        await Page.GetByRole(AriaRole.Textbox, new() { Name = "Email" }).FillAsync("owner1@aquiis.com");
        await Page.GetByRole(AriaRole.Textbox, new() { Name = "Email" }).PressAsync("Tab");
        await Page.GetByRole(AriaRole.Textbox, new() { Name = "Password" }).FillAsync("SamplePassword2025!");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Log in" }).ClickAsync();
        
        // Wait for login to complete
        await Page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);
        await Page.WaitForSelectorAsync("h1:has-text('Property Management Dashboard')");
        
        await Page.GetByRole(AriaRole.Link, new() { Name = "Properties" }).ClickAsync();
        await Page.GetByRole(AriaRole.Button, new() { Name = "Add Property" }).ClickAsync();
        await Page.GetByRole(AriaRole.Textbox, new() { Name = "Enter property address" }).ClickAsync();
        await Page.GetByRole(AriaRole.Textbox, new() { Name = "Enter property address" }).FillAsync("369 Crescent Drive");
        await Page.GetByRole(AriaRole.Textbox, new() { Name = "Enter property address" }).PressAsync("Tab");
        await Page.GetByRole(AriaRole.Textbox, new() { Name = "e.g., Apt 2B, Unit" }).PressAsync("Tab");
        await Page.GetByRole(AriaRole.Textbox, new() { Name = "City" }).FillAsync("New Orleans");
        await Page.GetByRole(AriaRole.Textbox, new() { Name = "City" }).PressAsync("Tab");
        await Page.Locator("select[name=\"Model.State\"]").SelectOptionAsync(new[] { "LA" });
        await Page.Locator("select[name=\"Model.State\"]").PressAsync("Tab");
        await Page.GetByRole(AriaRole.Textbox, new() { Name = "#####-####" }).FillAsync("70119");
        await Page.Locator("select[name=\"Model.PropertyType\"]").SelectOptionAsync(new[] { "House" });
        await Page.GetByPlaceholder("0.00").ClickAsync();
        await Page.GetByPlaceholder("0.00").FillAsync("1800");
        await Page.Locator("input[name=\"Model.Bedrooms\"]").ClickAsync();
        await Page.Locator("input[name=\"Model.Bedrooms\"]").FillAsync("4");
        await Page.Locator("input[name=\"Model.Bedrooms\"]").PressAsync("Tab");
        await Page.Locator("input[name=\"Model.Bathrooms\"]").FillAsync("4.5");
        await Page.Locator("input[name=\"Model.Bathrooms\"]").PressAsync("Tab");
        await Page.Locator("input[name=\"Model.SquareFeet\"]").FillAsync("2500");
        await Page.Locator("input[name=\"Model.SquareFeet\"]").PressAsync("Tab");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Create Property" }).ClickAsync();
        
        // Verify property was created successfully
        await Page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);
        await Expect(Page.GetByText("369 Crescent Drive").First).ToBeVisibleAsync();

        await Page.GetByRole(AriaRole.Button, new() { Name = "Add Property" }).ClickAsync();
        await Page.GetByRole(AriaRole.Textbox, new() { Name = "Enter property address" }).ClickAsync();
        await Page.GetByRole(AriaRole.Textbox, new() { Name = "Enter property address" }).FillAsync("354 Maple Avenue");
        await Page.GetByRole(AriaRole.Textbox, new() { Name = "Enter property address" }).PressAsync("Tab");
        await Page.GetByRole(AriaRole.Textbox, new() { Name = "e.g., Apt 2B, Unit" }).PressAsync("Tab");
        await Page.GetByRole(AriaRole.Textbox, new() { Name = "City" }).FillAsync("Los Angeles");
        await Page.GetByRole(AriaRole.Textbox, new() { Name = "City" }).PressAsync("Tab");
        await Page.Locator("select[name=\"Model.State\"]").SelectOptionAsync(new[] { "CA" });
        await Page.Locator("select[name=\"Model.State\"]").PressAsync("Tab");
        await Page.GetByRole(AriaRole.Textbox, new() { Name = "#####-####" }).FillAsync("90210");
        await Page.Locator("select[name=\"Model.PropertyType\"]").SelectOptionAsync(new[] { "House" });
        await Page.GetByPlaceholder("0.00").ClickAsync();
        await Page.GetByPlaceholder("0.00").FillAsync("4900");
        await Page.Locator("input[name=\"Model.Bedrooms\"]").ClickAsync();
        await Page.Locator("input[name=\"Model.Bedrooms\"]").FillAsync("4");
        await Page.Locator("input[name=\"Model.Bedrooms\"]").PressAsync("Tab");
        await Page.Locator("input[name=\"Model.Bathrooms\"]").FillAsync("4.5");
        await Page.Locator("input[name=\"Model.Bathrooms\"]").PressAsync("Tab");
        await Page.Locator("input[name=\"Model.SquareFeet\"]").FillAsync("3200");
        await Page.Locator("input[name=\"Model.SquareFeet\"]").PressAsync("Tab");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Create Property" }).ClickAsync();

        // Verify property was created successfully
        await Page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);
        await Expect(Page.GetByText("354 Maple Avenue").First).ToBeVisibleAsync();
    }

    // [Test, Order(3)]
    // public async Task AddProspect()
    // {
    //     await Page.GotoAsync("http://localhost:5197/");
    //     await Page.GetByRole(AriaRole.Button, new() { Name = "Sign In" }).ClickAsync();
    //     await Page.GetByRole(AriaRole.Textbox, new() { Name = "Email" }).ClickAsync();
    //     await Page.GetByRole(AriaRole.Textbox, new() { Name = "Email" }).FillAsync("owner1@aquiis.com");
    //     await Page.GetByRole(AriaRole.Textbox, new() { Name = "Email" }).PressAsync("Tab");
    //     await Page.GetByRole(AriaRole.Textbox, new() { Name = "Password" }).FillAsync("Today123");
    //     await Page.GetByRole(AriaRole.Button, new() { Name = "Log in" }).ClickAsync();

    //     await Page.GetByRole(AriaRole.Link, new() { Name = "Prospects" }).ClickAsync();
    //     await Page.GetByRole(AriaRole.Button, new() { Name = " Add New Prospect" }).ClickAsync();

    //     await Page.Locator("input[name=\"newProspect.FirstName\"]").ClickAsync();
    //     await Page.Locator("input[name=\"newProspect.FirstName\"]").FillAsync("Mya");
    //     await Page.Locator("input[name=\"newProspect.FirstName\"]").PressAsync("Tab");
    //     await Page.Locator("input[name=\"newProspect.LastName\"]").ClickAsync();
    //     await Page.Locator("input[name=\"newProspect.LastName\"]").FillAsync("Smith");
    //     await Page.Locator("input[name=\"newProspect.LastName\"]").PressAsync("Tab");
    //     await Page.Locator("input[name=\"newProspect.Email\"]").FillAsync("mya@gmail.com");
    //     await Page.Locator("input[name=\"newProspect.Email\"]").PressAsync("Tab");
    //     await Page.Locator("input[name=\"newProspect.Phone\"]").FillAsync("504-234-3600");
    //     await Page.Locator("input[name=\"newProspect.Phone\"]").PressAsync("Tab");
    //     await Page.Locator("input[name=\"newProspect.DateOfBirth\"]").FillAsync("1993-09-29");
    //     await Page.GetByRole(AriaRole.Textbox, new() { Name = "e.g., Driver's License #" }).ClickAsync();
    //     await Page.GetByRole(AriaRole.Textbox, new() { Name = "e.g., Driver's License #" }).FillAsync("12345678");
    //     await Page.Locator("select[name=\"newProspect.IdentificationState\"]").SelectOptionAsync(new[] { "LA" });
    //     await Page.Locator("select[name=\"newProspect.Source\"]").SelectOptionAsync(new[] { "Zillow" });
    //     await Page.Locator("select[name=\"newProspect.InterestedPropertyId\"]").SelectOptionAsync(new[] { "354 Maple Avenue" });
    //     await Page.Locator("input[name=\"newProspect.DesiredMoveInDate\"]").FillAsync("2026-01-01");
    //     await Page.GetByRole(AriaRole.Button, new() { Name = "Save Prospect" }).ClickAsync();

    //      // Verify property was created successfully
    //     await Page.WaitForSelectorAsync("h1:has-text('Prospective Tenants')");
    //     await Expect(Page.GetByText("Mya Smith").First).ToBeVisibleAsync();
    // }

    // [Test, Order(4)]
    // public async Task ScheduleAndCompleteTour()
    // {
    //     await Page.GotoAsync("http://localhost:5197/");
    //     await Page.GetByRole(AriaRole.Button, new() { Name = "Sign In" }).ClickAsync();
    //     await Page.GetByRole(AriaRole.Textbox, new() { Name = "Email" }).ClickAsync();
    //     await Page.GetByRole(AriaRole.Textbox, new() { Name = "Email" }).FillAsync("owner1@aquiis.com");
    //     await Page.GetByRole(AriaRole.Textbox, new() { Name = "Email" }).PressAsync("Tab");
    //     await Page.GetByRole(AriaRole.Textbox, new() { Name = "Password" }).FillAsync("Today123");
    //     await Page.GetByRole(AriaRole.Button, new() { Name = "Log in" }).ClickAsync();

    //     await Page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);

    //     await Page.GetByRole(AriaRole.Link, new() { Name = "Prospects" }).ClickAsync();
    //     await Page.GetByTitle("Schedule Tour").ClickAsync();

    //     await Page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);

    //     await Page.GetByRole(AriaRole.Button, new() { Name = "Schedule Tour" }).ClickAsync();

    //     await Page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);

    //     await Page.GetByRole(AriaRole.Button, new() { Name = "Complete Tour", Exact = true }).ClickAsync();

    //     await Page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);

    //     // await Page.GetByRole(AriaRole.Button, new() { Name = " Continue Editing" }).ClickAsync();
    //     await Page.GetByRole(AriaRole.Button, new() { Name = " Check All" }).First.ClickAsync();
    //     await Page.GetByRole(AriaRole.Button, new() { Name = " Check All" }).Nth(1).ClickAsync();
    //     await Page.GetByRole(AriaRole.Button, new() { Name = " Check All" }).Nth(2).ClickAsync();
    //     await Page.GetByRole(AriaRole.Button, new() { Name = " Check All" }).Nth(3).ClickAsync();
    //     await Page.GetByRole(AriaRole.Button, new() { Name = " Check All" }).Nth(4).ClickAsync();
    //     await Page.GetByRole(AriaRole.Button, new() { Name = " Check All" }).Nth(5).ClickAsync();

    //     await Page.GetByRole(AriaRole.Textbox, new() { Name = "Enter value" }).First.ClickAsync();
    //     await Page.GetByRole(AriaRole.Textbox, new() { Name = "Enter value" }).First.FillAsync("1800");
    //     await Page.GetByRole(AriaRole.Textbox, new() { Name = "e.g., $" }).ClickAsync();
    //     await Page.GetByRole(AriaRole.Textbox, new() { Name = "e.g., $" }).FillAsync("1800");

    //     await Page.Locator("div:nth-child(10) > .card-header > .btn").ClickAsync();
    //     await Page.Locator("div:nth-child(11) > .card-header > .btn").ClickAsync();

    //     await Page.GetByText("Interested", new() { Exact = true }).ClickAsync();

    //     await Page.GetByRole(AriaRole.Button, new() { Name = " Save Progress" }).ClickAsync();

    //     await Page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);

    //     await Page.GetByRole(AriaRole.Button, new() { Name = " Mark as Complete" }).ClickAsync();

    //     await Page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);

    //     await Page.GetByRole(AriaRole.Button, new() { Name = " Generate PDF" }).ClickAsync();

    //     await Page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);

    //     var page1 = await Page.RunAndWaitForPopupAsync(async () =>
    //     {
    //         await Page.GetByRole(AriaRole.Button, new() { Name = " View PDF" }).ClickAsync();
    //     });

    //     await Page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);

    //     await Page.GetByRole(AriaRole.Link, new() { Name = "Dashboard" }).ClickAsync();
    //     await Page.GetByRole(AriaRole.Button, new() { Name = "Logout" }).ClickAsync();
    // }

    // [Test, Order(5)]
    // public async Task SubmitApplication()
    // {
    //     await Page.GotoAsync("http://localhost:5197/");
    //     await Page.GetByRole(AriaRole.Button, new() { Name = "Sign In" }).ClickAsync();
    //     await Page.GetByRole(AriaRole.Textbox, new() { Name = "Email" }).ClickAsync();
    //     await Page.GetByRole(AriaRole.Textbox, new() { Name = "Email" }).FillAsync("owner1@aquiis.com");
    //     await Page.GetByRole(AriaRole.Textbox, new() { Name = "Email" }).PressAsync("Tab");
    //     await Page.GetByRole(AriaRole.Textbox, new() { Name = "Password" }).FillAsync("Today123");
    //     await Page.GetByRole(AriaRole.Button, new() { Name = "Log in" }).ClickAsync();

    //     await Page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);

    //     await Page.GetByRole(AriaRole.Link, new() { Name = "Prospects" }).ClickAsync();

    //     await Page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);

    //     await Page.GetByRole(AriaRole.Button, new() { Name = " Apply" }).ClickAsync();

    //     await Page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);

    //     await Page.GetByRole(AriaRole.Textbox, new() { Name = "Main St" }).ClickAsync();
    //     await Page.GetByRole(AriaRole.Textbox, new() { Name = "Main St" }).FillAsync("123 Main Street");
    //     await Page.GetByRole(AriaRole.Textbox, new() { Name = "Main St" }).PressAsync("Tab");
    //     await Page.GetByRole(AriaRole.Textbox, new() { Name = "Los Angeles" }).FillAsync("Los Angeles");
    //     await Page.GetByRole(AriaRole.Textbox, new() { Name = "Los Angeles" }).PressAsync("Tab");
    //     await Page.Locator("select[name=\"applicationModel.CurrentState\"]").SelectOptionAsync(new[] { "CA" });
    //     await Page.Locator("select[name=\"applicationModel.CurrentState\"]").PressAsync("Tab");
    //     await Page.GetByRole(AriaRole.Textbox, new() { Name = "90210" }).FillAsync("90210");
    //     await Page.GetByRole(AriaRole.Textbox, new() { Name = "90210" }).PressAsync("Tab");
    //     await Page.Locator("input[name=\"applicationModel.CurrentRent\"]").FillAsync("1500");
    //     await Page.Locator("input[name=\"applicationModel.CurrentRent\"]").PressAsync("Tab");
    //     await Page.GetByRole(AriaRole.Textbox, new() { Name = "John Smith" }).FillAsync("John Smith");
    //     await Page.GetByRole(AriaRole.Textbox, new() { Name = "John Smith" }).PressAsync("Tab");
    //     await Page.GetByRole(AriaRole.Textbox, new() { Name = "(555) 123-" }).FillAsync("555-123-4567");
    //     await Page.GetByRole(AriaRole.Textbox, new() { Name = "(555) 123-" }).PressAsync("Tab");
    //     await Page.GetByRole(AriaRole.Textbox, new() { Name = "ABC Company" }).PressAsync("CapsLock");
    //     await Page.GetByRole(AriaRole.Textbox, new() { Name = "ABC Company" }).FillAsync("ABC");
    //     await Page.GetByRole(AriaRole.Textbox, new() { Name = "ABC Company" }).PressAsync("CapsLock");
    //     await Page.GetByRole(AriaRole.Textbox, new() { Name = "ABC Company" }).FillAsync("ABC Company");
    //     await Page.GetByRole(AriaRole.Textbox, new() { Name = "ABC Company" }).PressAsync("Tab");
    //     await Page.GetByRole(AriaRole.Textbox, new() { Name = "Software Engineer" }).FillAsync("Software Engineer");
    //     await Page.GetByRole(AriaRole.Textbox, new() { Name = "Software Engineer" }).PressAsync("Tab");
    //     await Page.Locator("input[name=\"applicationModel.MonthlyIncome\"]").FillAsync("9600");
    //     await Page.Locator("input[name=\"applicationModel.MonthlyIncome\"]").PressAsync("Tab");
    //     await Page.Locator("input[name=\"applicationModel.EmploymentLengthMonths\"]").FillAsync("15");
    //     await Page.Locator("input[name=\"applicationModel.EmploymentLengthMonths\"]").PressAsync("Tab");
    //     await Page.Locator("input[name=\"applicationModel.Reference1Name\"]").FillAsync("Richard");
    //     await Page.Locator("input[name=\"applicationModel.Reference1Name\"]").PressAsync("Tab");
    //     await Page.Locator("input[name=\"applicationModel.Reference1Phone\"]").FillAsync("Zachary");
    //     await Page.Locator("input[name=\"applicationModel.Reference1Phone\"]").PressAsync("Tab");
    //     await Page.GetByRole(AriaRole.Textbox, new() { Name = "Friend, Coworker, etc." }).FillAsync("Spouse");
    //     await Page.GetByRole(AriaRole.Button, new() { Name = "Submit Application" }).ClickAsync();

    //     // Verify property was created successfully
    //     await Expect(Page.GetByText("Application submitted successfully")).ToBeVisibleAsync();

    //     await Page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);

    //     await Page.GetByRole(AriaRole.Link, new() { Name = "Prospects" }).ClickAsync();

    //     await Page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);

    //     await Page.WaitForSelectorAsync("h1:has-text('Prospective Tenants')");

    //     await Page.GetByRole(AriaRole.Link, new() { Name = "Dashboard" }).ClickAsync();

    //     await Page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);

    //     await Page.GetByRole(AriaRole.Button, new() { Name = "Logout" }).ClickAsync();
    // }

    // [Test, Order(6)]
    // public async Task ApproveApplication()
    // {
    //     await Page.GotoAsync("http://localhost:5197/");
    //     await Page.GetByRole(AriaRole.Button, new() { Name = "Sign In" }).ClickAsync();
    //     await Page.GetByRole(AriaRole.Textbox, new() { Name = "Email" }).ClickAsync();
    //     await Page.GetByRole(AriaRole.Textbox, new() { Name = "Email" }).FillAsync("owner1@aquiis.com");
    //     await Page.GetByRole(AriaRole.Textbox, new() { Name = "Email" }).PressAsync("Tab");
    //     await Page.GetByRole(AriaRole.Textbox, new() { Name = "Password" }).FillAsync("Today123");
    //     await Page.GetByRole(AriaRole.Button, new() { Name = "Log in" }).ClickAsync();
       
    //     await Page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);

    //     await Page.GetByRole(AriaRole.Link, new() { Name = "Prospects" }).ClickAsync();

    //     await Page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);

    //     await Page.GetByTitle("View Details").ClickAsync();

    //     await Page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);

    //     await Page.GetByRole(AriaRole.Button, new() { Name = " View Application" }).ClickAsync();

    //     await Page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);

    //     await Page.GetByRole(AriaRole.Button, new() { Name = " Collect Application Fee" }).ClickAsync();
    //     await Page.GetByRole(AriaRole.Combobox).SelectOptionAsync(new[] { "Online Payment" });
    //     await Page.GetByRole(AriaRole.Button, new() { Name = " Confirm Payment" }).ClickAsync();

    //     await Page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);

    //     await Page.GetByRole(AriaRole.Button, new() { Name = " Initiate Screening" }).ClickAsync();
    //     await Page.GetByRole(AriaRole.Button, new() { Name = " Pass" }).First.ClickAsync();
    //     await Page.GetByRole(AriaRole.Button, new() { Name = " Confirm Pass" }).ClickAsync();
    //     await Page.GetByRole(AriaRole.Button, new() { Name = " Pass" }).Nth(1).ClickAsync();
    //     await Page.GetByRole(AriaRole.Button, new() { Name = " Confirm Pass" }).ClickAsync();

    //     await Page.GetByRole(AriaRole.Button, new() { Name = " Approve Application" }).ClickAsync();

    //     await Page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);

    //     await Page.GetByRole(AriaRole.Link, new() { Name = "Dashboard" }).ClickAsync();
    //     await Page.GetByRole(AriaRole.Button, new() { Name = "Logout" }).ClickAsync();
    // }

    // [Test]
    // public async Task GenerateLeaseOfferAndConvertToLease()
    // {
    //     await Page.GotoAsync("http://localhost:5197/");
    //     await Page.GetByRole(AriaRole.Button, new() { Name = "Sign In" }).ClickAsync();
    //     await Page.GetByRole(AriaRole.Textbox, new() { Name = "Email" }).ClickAsync();
    //     await Page.GetByRole(AriaRole.Textbox, new() { Name = "Email" }).FillAsync("owner1@aquiis.com");
    //     await Page.GetByRole(AriaRole.Textbox, new() { Name = "Email" }).PressAsync("Tab");
    //     await Page.GetByRole(AriaRole.Textbox, new() { Name = "Password" }).FillAsync("Today123");
    //     await Page.GetByRole(AriaRole.Button, new() { Name = "Log in" }).ClickAsync();

    //     await Page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);

    //     await Page.GetByRole(AriaRole.Link, new() { Name = "Prospects" }).ClickAsync();

    //     await Page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);

    //     await Page.GetByTitle("View Details").ClickAsync();

    //     await Page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);

    //     await Page.GetByRole(AriaRole.Button, new() { Name = " View Application" }).ClickAsync();

    //     await Page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);

    //     await Page.GetByRole(AriaRole.Button, new() { Name = " Generate Lease Offer" }).ClickAsync();

    //     await Page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);

    //     await Page.GetByRole(AriaRole.Button, new() { Name = " Generate Lease Offer" }).ClickAsync();

    //     await Page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);

    //     await Page.GetByRole(AriaRole.Button, new() { Name = " Accept Offer (Convert to Lease" }).ClickAsync();

    //     await Page.GetByRole(AriaRole.Combobox).SelectOptionAsync(new[] { "Online Payment" });
    //     await Page.GetByRole(AriaRole.Button, new() { Name = " Accept & Create Lease" }).ClickAsync();

    //     await Page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);

    //     await Page.GetByRole(AriaRole.Link, new() { Name = "Dashboard" }).ClickAsync();

    //     await Page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);

    //     await Page.GetByRole(AriaRole.Button, new() { Name = "Logout" }).ClickAsync();
    // }

}