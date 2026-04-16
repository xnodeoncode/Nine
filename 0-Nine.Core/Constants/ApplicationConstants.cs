using System.Security.Cryptography.X509Certificates;

namespace Nine.Core.Constants
{
    public static class ApplicationConstants
    {
        /// <summary>
        /// System service account for background jobs and automated processes
        /// </summary>
        public static class SystemUser
        {
            /// <summary>
            /// Well-known GUID for system service account.
            /// Used by background jobs, scheduled tasks, and automated processes.
            /// </summary>
            public static readonly string Id = "00000000-0000-0000-0000-000000000001";

            public const string Email = "system@aquiis.local";
            public const string UserName = "system@aquiis.local"; // UserName = Email in this system
            public const string DisplayName = "System";

            // Service account details
            public const string FirstName = "System User";
            public const string LastName = "Account";
        }

        // DEPRECATED: Legacy Identity roles - kept for backward compatibility but not used for authorization
        public static string DefaultSuperAdminRole { get; } = "SuperAdministrator";
        public static string DefaultAdminRole { get; } = "Administrator";
        public static string DefaultPropertyManagerRole { get; } = "PropertyManager";
        public static string DefaultTenantRole { get; } = "Tenant";
        public static string DefaultUserRole { get; } = "User";
        public static string DefaultGuestRole { get; } = "Guest";

        /// <summary>
        /// Organization-scoped roles for multi-organization support
        /// </summary>
        public static class OrganizationRoles
        {
            /// <summary>
            /// Owner - Full data sovereignty (create/delete orgs, backup/delete data, all features)
            /// </summary>
            public const string Owner = "Owner";
            
            /// <summary>
            /// Administrator - Delegated owner access (all features except org creation/deletion/data management)
            /// </summary>
            public const string Administrator = "Administrator";
            
            /// <summary>
            /// PropertyManager - Full property management features (no admin/settings access)
            /// </summary>
            public const string PropertyManager = "PropertyManager";

            /// <summary>
            /// Maintenance - Maintenance requests, work orders, and vendors
            /// </summary>
            public const string Maintenance = "Maintenance";
            
            /// <summary>
            /// User - Limited feature access (view-only or basic operations)
            /// </summary>
            public const string User = "User";

            public static readonly string[] AllRoles = { Owner, Administrator, PropertyManager, User };

            public static bool IsValid(string role) => AllRoles.Contains(role);

            public static bool CanManageUsers(string role) => role == Owner || role == Administrator;

            public static bool CanEditSettings(string role) => role == Owner || role == Administrator;

            public static bool CanManageOrganizations(string role) => role == Owner;

            public static bool CanManageProperties(string role) => role == Owner || role == Administrator || role == PropertyManager;

            public static bool CanManageTenants(string role) => role == Owner || role == Administrator || role == PropertyManager;

            public static bool CanManageLeases(string role) => role == Owner || role == Administrator || role == PropertyManager;

            public static bool CanManageInvoices(string role) => role == Owner || role == Administrator || role == PropertyManager;

            public static bool CanManagePayments(string role) => role == Owner || role == Administrator || role == PropertyManager;

            public static bool CanManageSecurityDeposits(string role) => role == Owner || role == Administrator || role == PropertyManager;

            public static bool CanManageDocuments(string role) => role == Owner || role == Administrator || role == PropertyManager;

            public static bool CanManageMaintenanceRequests(string role) => role == Owner || role == Administrator || role == PropertyManager;

            public static bool CanManageInspections(string role) => role == Owner || role == Administrator || role == PropertyManager;

            public static bool CanManageProspectiveTenants(string role) => role == Owner || role == Administrator || role == PropertyManager;

            public static bool CanManageApplications(string role) => role == Owner || role == Administrator || role == PropertyManager;

            public static bool CanManageTours(string role) => role == Owner || role == Administrator || role == PropertyManager;

            public static bool CanManageChecklists(string role) => role == Owner || role == Administrator || role == PropertyManager;



            public static bool CanViewRecords(string role) => AllRoles.Contains(role);

            public static bool CanEditRecords(string role) => role == Owner || role == Administrator || role == PropertyManager;
        }

        public static string DefaultSuperAdminPassword { get; } = "SuperAdmin@123!";
        public static string DefaultAdminPassword { get; } = "Admin@123!";
        public static string DefaultPropertyManagerPassword { get; } = "PropertyManager@123!";
        public static string DefaultTenantPassword { get; } = "Tenant@123!";
        public static string DefaultUserPassword { get; } = "User@123!";
        public static string DefaultGuestPassword { get; } = "Guest@123!";

        public static string AdministrationPath { get; } = "/Administration";
        public static string PropertyManagementPath { get; } = "/PropertyManagement";
        public static string TenantPortalPath { get; } = "/TenantPortal";


        public static string SuperAdminUserName { get; } = "superadmin";
        public static string SuperAdminEmail { get; } = "superadmin@example.local";

        public static IReadOnlyList<string> DefaultRoles { get; } = new List<string>
        {
            DefaultSuperAdminRole,
            DefaultAdminRole,
            DefaultPropertyManagerRole,
            DefaultTenantRole,
            DefaultUserRole,
            DefaultGuestRole
        };

        public static IReadOnlyList<string> DefaultPasswords { get; } = new List<string>
        {
            DefaultSuperAdminPassword,
            DefaultAdminPassword,
            DefaultPropertyManagerPassword,
            DefaultTenantPassword,
            DefaultUserPassword,
            DefaultGuestPassword
        };

        public static string[] USStateAbbreviations { get; } = States.Abbreviations();
        public static string[] USStateNames { get; } = States.Names();

        public static State[] USStates { get; } = States.StatesArray();

        public static class PaymentMethods
        { 
            public const string OnlinePayment = "Online Payment";
            public const string DebitCard = "Debit Card";
            public const string CreditCard = "Credit Card";
            public const string BankTransfer = "Bank Transfer";
            public const string CryptoCurrency = "Crypto Currency";
            public const string Cash = "Cash";
            public const string Check = "Check";
            public const string Other = "Other";

            public static IReadOnlyList<string> AllPaymentMethods { get; } = new List<string>
            {
                OnlinePayment,
                DebitCard,
                CreditCard,
                BankTransfer,
                CryptoCurrency,
                Cash,
                Check,
                Other
            };
        }

        public static class InvoiceStatuses
        {
            public const string Pending = "Pending";
            public const string PaidPartial = "Paid Partial";
            public const string Paid = "Paid";
            public const string Overdue = "Overdue";
            public const string Cancelled = "Cancelled";
            public const string Voided = "Voided";

            public static IReadOnlyList<string> AllInvoiceStatuses { get; } = new List<string>
            {
                Pending,
                PaidPartial,
                Paid,
                Overdue,
                Cancelled,
                Voided
            };
        }

        public static class PaymentStatuses
        {
            public const string Completed = "Completed";
            public const string Pending = "Pending";
            public const string Failed = "Failed";
            public const string Refunded = "Refunded";
            public const string Voided = "Voided";

            public static IReadOnlyList<string> AllPaymentStatuses { get; } = new List<string>
            {
                Completed,
                Pending,
                Failed,
                Refunded,
                Voided
            };
        }
        public static class InspectionTypes
        { 
            public const string MoveIn = "Move-In";
            public const string MoveOut = "Move-Out";
            public const string Routine = "Routine";
            public const string Maintenance = "Maintenance";
            public const string Other = "Other";

            public static IReadOnlyList<string> AllInspectionTypes { get; } = new List<string>
            {
                MoveIn,
                MoveOut,
                Routine,
                Maintenance,
                Other
            };
        }
       
        public static class LeaseTypes { 
            public const string FixedTerm = "Fixed-Term";
            public const string MonthToMonth = "Month-to-Month";
            public const string Sublease = "Sublease";
            public const string Other = "Other";

            public static IReadOnlyList<string> AllLeaseTypes { get; } = new List<string>
            {
                FixedTerm,
                MonthToMonth,
                Sublease,
                Other
            };

        }

        public static class LeaseStatuses { 
            public const string Offered = "Offered";
            public const string Pending = "Pending";
            public const string Accepted = "Accepted";
            public const string Declined = "Declined";
            public const string Renewed = "Renewed";
            public const string MonthToMonth = "Month-to-Month";
            public const string NoticeGiven = "Notice Given";
            public const string Interrupted = "Interrupted";
            public const string Terminated = "Terminated";
            public const string Expired = "Expired";

            public static IReadOnlyList<string> RenewalStatuses { get; } = new List<string>
            {
                "NotRequired",
                "Pending",
                "Offered",
                "Accepted",
                "Declined",
                "Expired"
            };

            /// <summary>Active leases: IsActive = true</summary>
            public static IReadOnlyList<string> ActiveStatuses { get; } = new List<string>
            {
                Offered,
                Pending,
                Accepted,
                MonthToMonth,
                NoticeGiven,
            };

            /// <summary>Inactive leases: IsActive = false</summary>
            public static IReadOnlyList<string> InactiveStatuses { get; } = new List<string>
            {
                Renewed,
                Declined,
                Interrupted,
                Terminated,
                Expired,
            };

            public static IReadOnlyList<string> AllLeaseStatuses { get; } = new List<string>
            {
                Offered,
                Pending,
                Accepted,
                MonthToMonth,
                NoticeGiven,
                Renewed,
                Declined,
                Interrupted,
                Terminated,
                Expired,
            };
        }

        
      
        public static class PropertyTypes
        {
            public const string House = "House";
            public const string Apartment = "Apartment";
            public const string Condo = "Condo";
            public const string Townhouse = "Townhouse";
            public const string Duplex = "Duplex";
            public const string Studio = "Studio";
            public const string Loft = "Loft";
            public const string Other = "Other";

            public static IReadOnlyList<string> AllPropertyTypes { get; } = new List<string>
            {
                House,
                Apartment,
                Condo,
                Townhouse,
                Duplex,
                Studio,
                Loft,
                Other
            };

        }
        
        public static class PropertyStatuses
        {
            public const string Available = "Available";
            public const string ApplicationPending = "Application Pending";
            public const string LeasePending = "Lease Pending";
            public const string MoveInPending = "Accepted - Move-In Pending";
            public const string Occupied = "Occupied";
            public const string MoveOutPending = "Move-Out Pending";
            public const string UnderRenovation = "Under Renovation";
            public const string OffMarket = "Off Market";
            
            public static IReadOnlyList<string> OccupiedStatuses { get; } = new List<string>
            {
                MoveInPending,
                Occupied,
                MoveOutPending
            };

            /// <summary>
            /// Statuses that classify a property as Inactive (off-market). User-driven only.
            /// All other statuses are considered Active.
            /// </summary>
            public static IReadOnlyList<string> InactiveStatuses { get; } = new List<string>
            {
                OffMarket,
                UnderRenovation
            };

            public static IReadOnlyList<string> AllPropertyStatuses { get; } = new List<string>
            {
                Available,
                ApplicationPending,
                LeasePending,
                Occupied,
                UnderRenovation,
                OffMarket
            };
        }

       

        public static class MaintenanceRequestTypes
        {

            public const string Plumbing = "Plumbing";
            public const string Electrical = "Electrical";
            public const string HeatingCooling = "Heating/Cooling";
            public const string Appliance = "Appliance";
            public const string Structural = "Structural";
            public const string Landscaping = "Landscaping";
            public const string PestControl = "Pest Control";
            public const string Other = "Other";

            public static IReadOnlyList<string> AllMaintenanceRequestTypes { get; } = new List<string>
            {
                Plumbing,
                Electrical,
                HeatingCooling,
                Appliance,
                Structural,
                Landscaping,
                PestControl,
                Other
            };
        }

        /// <summary>
        /// Repair type categories for logging work performed on properties.
        /// Used by the Repair entity (work WITHOUT workflow).
        /// </summary>
        public static class RepairTypes
        {
            public const string Plumbing = "Plumbing";
            public const string Electrical = "Electrical";
            public const string HeatingCooling = "Heating/Cooling";
            public const string Appliance = "Appliance";
            public const string Structural = "Structural";
            public const string Landscaping = "Landscaping";
            public const string PestControl = "Pest Control";
            public const string Painting = "Painting";
            public const string Carpentry = "Carpentry";
            public const string Flooring = "Flooring";
            public const string Roofing = "Roofing";
            public const string Windows = "Windows/Doors";
            public const string Other = "Other";

            public static IReadOnlyList<string> AllRepairTypes { get; } = new List<string>
            {
                Plumbing,
                Electrical,
                HeatingCooling,
                Appliance,
                Structural,
                Landscaping,
                PestControl,
                Painting,
                Carpentry,
                Flooring,
                Roofing,
                Windows,
                Other
            };
        }

        public static class MaintenanceRequestPriorities
        {
            public const string Low = "Low";
            public const string Medium = "Medium";
            public const string High = "High";
            public const string Urgent = "Urgent";

            public static IReadOnlyList<string> AllMaintenanceRequestPriorities { get; } = new List<string>
            {
                Low,
                Medium,
                High,
                Urgent
            };
        }

        public static class MaintenanceRequestStatuses
        {
            public const string Submitted = "Submitted";
            public const string InProgress = "In Progress";
            public const string Completed = "Completed";
            public const string Cancelled = "Cancelled";

            public static IReadOnlyList<string> AllMaintenanceRequestStatuses { get; } = new List<string>
            {
                Submitted,
                InProgress,
                Completed,
                Cancelled
            };
        }

        public static class TenantStatuses
        {
            public const string Prospective = "Prospective";
            public const string Pending = "Pending";
            public const string MoveInPending = "Move-In Pending";
            public const string Active = "Active";
            public const string MoveOutPending = "Move-Out Pending";
            public const string Inactive = "Inactive";
            public const string Evicted = "Evicted";

            public static IReadOnlyList<string> AllTenantStatuses { get; } = new List<string>
            {
                Prospective,
                Pending,       
                MoveInPending,
                Active,
                MoveOutPending,
                Inactive,
                Evicted
            };

        }

        public static class DocumentTypes
        {
            public const string LeaseApplication = "Lease Application";
            public const string LeaseAgreement = "Lease Agreement";
            public const string InspectionReport = "Inspection Report";
            public const string MaintenanceRecord = "Maintenance Record";
            public const string Invoice = "Invoice";
            public const string PaymentReceipt = "Payment Receipt";
            public const string Other = "Other";

            public static IReadOnlyList<string> AllDocumentTypes { get; } = new List<string>
            {
                LeaseApplication,
                LeaseAgreement,
                InspectionReport,
                MaintenanceRecord,
                Invoice,
                PaymentReceipt,
                Other
            };

        }

        public static class ChecklistTypes
        {
            public const string MoveIn = "Move-In";
            public const string MoveOut = "Move-Out";
            public const string OpenHouse = "Open House";
            public const string Tour = "Tour";
            public const string Custom = "Custom";

            public static IReadOnlyList<string> AllChecklistTypes { get; } = new List<string>
            {
                MoveIn,
                MoveOut,
                OpenHouse,
                Tour,
                Custom
            };
        }

        public static class ChecklistStatuses
        {
            public const string Draft = "Draft";
            public const string InProgress = "In Progress";
            public const string Completed = "Completed";

            public static IReadOnlyList<string> AllChecklistStatuses { get; } = new List<string>
            {
                Draft,
                InProgress,
                Completed
            };
        }

        public static class ProspectiveStatuses
        {
            public const string Lead = "Lead";
            public const string TourScheduled = "Tour Scheduled";
            public const string Applied = "Applied";
            public const string Screening = "Screening";
            public const string Approved = "Approved";
            public const string Denied = "Denied";
            public const string Withdrawn = "Withdrawn";
            public const string LeaseOffered = "Lease Offered";
            public const string LeaseDeclined = "Lease Declined";
            public const string ConvertedToTenant = "Converted To Tenant";

            public static IReadOnlyList<string> AllProspectiveStatuses { get; } = new List<string>
            {
                Lead,
                TourScheduled,
                Applied,
                Screening,
                Approved,
                Denied,
                Withdrawn,
                LeaseOffered,
                LeaseDeclined,
                ConvertedToTenant
            };
        }

        public static class ProspectiveSources
        {
            public const string Website = "Website";
            public const string Referral = "Referral";
            public const string WalkIn = "Walk-in";
            public const string Zillow = "Zillow";
            public const string Apartments = "Apartments.com";
            public const string SignCall = "Sign Call";
            public const string SocialMedia = "Social Media";
            public const string Other = "Other";

            public static IReadOnlyList<string> AllProspectiveSources { get; } = new List<string>
            {
                Website,
                Referral,
                WalkIn,
                Zillow,
                Apartments,
                SignCall,
                SocialMedia,
                Other
            };
        }

        public static class TourStatuses
        {
            public const string Scheduled = "Scheduled";
            public const string Completed = "Completed";
            public const string Cancelled = "Cancelled";
            public const string NoShow = "NoShow";

            public static IReadOnlyList<string> AllTourStatuses { get; } = new List<string>
            {
                Scheduled,
                Completed,
                Cancelled,
                NoShow
            };
        }

        public static class TourInterestLevels
        {
            public const string VeryInterested = "Very Interested";
            public const string Interested = "Interested";
            public const string Neutral = "Neutral";
            public const string NotInterested = "Not Interested";

            public static IReadOnlyList<string> AllTourInterestLevels { get; } = new List<string>
            {
                VeryInterested,
                Interested,
                Neutral,
                NotInterested
            };
        }

        public static class ApplicationStatuses
        {
            public const string Submitted = "Submitted";
            public const string UnderReview = "Under Review";
            public const string Screening = "Screening";
            public const string Approved = "Approved";
            public const string Denied = "Denied";
            public const string Expired = "Expired";
            public const string Withdrawn = "Withdrawn";
            public const string LeaseOffered = "Lease Offered";
            public const string LeaseAccepted = "Lease Accepted";
            public const string LeaseDeclined = "Lease Declined";

            public static IReadOnlyList<string> AllApplicationStatuses { get; } = new List<string>
            {
                Submitted,
                UnderReview,
                Screening,
                Approved,
                Denied,
                Expired,
                Withdrawn,
                LeaseOffered,
                LeaseAccepted,
                LeaseDeclined
            };
        }

        public static class ScreeningResults
        {
            public const string Pending = "Pending";
            public const string Passed = "Passed";
            public const string Failed = "Failed";
            public const string ConditionalPass = "Conditional Pass";

            public static IReadOnlyList<string> AllScreeningResults { get; } = new List<string>
            {
                Pending,
                Passed,
                Failed,
                ConditionalPass
            };
        }

        public static class SecurityDepositStatuses
        {
            public const string Held = "Held";
            public const string Released = "Released";
            public const string Refunded = "Refunded";
            public const string Forfeited = "Forfeited";
            public const string PartiallyRefunded = "Partially Refunded";

            public static IReadOnlyList<string> AllSecurityDepositStatuses { get; } = new List<string>
            {
                Held,
                Released,
                Refunded,
                Forfeited,
                PartiallyRefunded
            };
        }

        public static class InvestmentPoolStatuses
        {
            public const string Open = "Open";
            public const string Calculated = "Calculated";
            public const string Distributed = "Distributed";
            public const string Closed = "Closed";

            public static IReadOnlyList<string> AllInvestmentPoolStatuses { get; } = new List<string>
            {
                Open,
                Calculated,
                Distributed,
                Closed
            };
        }

        public static class DividendPaymentMethods
        {
            public const string Pending = "Pending";
            public const string LeaseCredit = "Lease Credit";
            public const string Check = "Check";

            public static IReadOnlyList<string> AllDividendPaymentMethods { get; } = new List<string>
            {
                Pending,
                LeaseCredit,
                Check
            };
        }

        public static class DividendStatuses
        {
            public const string Pending = "Pending";
            public const string ChoiceMade = "Choice Made";
            public const string Applied = "Applied";
            public const string Paid = "Paid";

            public static IReadOnlyList<string> AllDividendStatuses { get; } = new List<string>
            {
                Pending,
                ChoiceMade,
                Applied,
                Paid
            };
        }

        public static class EntityTypes
        {
            public const string Property = "Property";
            public const string Tenant = "Tenant";
            public const string Lease = "Lease";
            public const string Invoice = "Invoice";
            public const string Payment = "Payment";
            public const string MaintenanceRequest = "MaintenanceRequest";
            public const string Document = "Document";
            public const string Inspection = "Inspection";
            public const string ProspectiveTenant = "ProspectiveTenant";
            public const string Application = "Application";
            public const string Tour = "Tour";
            public const string Checklist = "Checklist";
            public const string Note = "Note";
        }

        

    }
    public static class States
    {

        static List<State> _states = new List<State>(50);

        static States()
        {
        _states.Add(new State("AL", "Alabama"));
        _states.Add(new State("AK", "Alaska"));
        _states.Add(new State("AZ", "Arizona"));
        _states.Add(new State("AR", "Arkansas"));
        _states.Add(new State("CA", "California"));
        _states.Add(new State("CO", "Colorado"));
        _states.Add(new State("CT", "Connecticut"));
        _states.Add(new State("DE", "Delaware"));
        _states.Add(new State("DC", "District Of Columbia"));
        _states.Add(new State("FL", "Florida"));
        _states.Add(new State("GA", "Georgia"));
        _states.Add(new State("HI", "Hawaii"));
        _states.Add(new State("ID", "Idaho"));
        _states.Add(new State("IL", "Illinois"));
        _states.Add(new State("IN", "Indiana"));
        _states.Add(new State("IA", "Iowa"));
        _states.Add(new State("KS", "Kansas"));
        _states.Add(new State("KY", "Kentucky"));
        _states.Add(new State("LA", "Louisiana"));
        _states.Add(new State("ME", "Maine"));
        _states.Add(new State("MD", "Maryland"));
        _states.Add(new State("MA", "Massachusetts"));
        _states.Add(new State("MI", "Michigan"));
        _states.Add(new State("MN", "Minnesota"));
        _states.Add(new State("MS", "Mississippi"));
        _states.Add(new State("MO", "Missouri"));
        _states.Add(new State("MT", "Montana"));
        _states.Add(new State("NE", "Nebraska"));
        _states.Add(new State("NV", "Nevada"));
        _states.Add(new State("NH", "New Hampshire"));
        _states.Add(new State("NJ", "New Jersey"));
        _states.Add(new State("NM", "New Mexico"));
        _states.Add(new State("NY", "New York"));
        _states.Add(new State("NC", "North Carolina"));
        _states.Add(new State("ND", "North Dakota"));
        _states.Add(new State("OH", "Ohio"));
        _states.Add(new State("OK", "Oklahoma"));
        _states.Add(new State("OR", "Oregon"));
        _states.Add(new State("PA", "Pennsylvania"));
        _states.Add(new State("RI", "Rhode Island"));
        _states.Add(new State("SC", "South Carolina"));
        _states.Add(new State("SD", "South Dakota"));
        _states.Add(new State("TN", "Tennessee"));
        _states.Add(new State("TX", "Texas"));
        _states.Add(new State("UT", "Utah"));
        _states.Add(new State("VT", "Vermont"));
        _states.Add(new State("VA", "Virginia"));
        _states.Add(new State("WA", "Washington"));
        _states.Add(new State("WV", "West Virginia"));
        _states.Add(new State("WI", "Wisconsin"));
        _states.Add(new State("WY", "Wyoming"));
        }

        public static string[] Abbreviations()
        {
        List<string> abbrevList = new List<string>(_states.Count);
        foreach (var state in _states)
        {
            abbrevList.Add(state.Abbreviation);
        }
        return abbrevList.ToArray();
        }

        public static string[] Names()
        {
        List<string> nameList = new List<string>(_states.Count);
        foreach (var state in _states)
        {
            nameList.Add(state.Name);
        }
        return nameList.ToArray();
        }

        public static State[] StatesArray()
        {
        return _states.ToArray();
        }

    }

    public class State
    {
        public State(string ab, string name)
        {
        Name = name;
        Abbreviation = ab;
        }

        public string Name { get; set; }

        public string Abbreviation { get; set; }

        public override string ToString()
        {
        return string.Format("{0} - {1}", Abbreviation, Name);
        }

    }
}