using Microsoft.AspNetCore.Identity;

namespace Nine.Entities;

/// <summary>
/// Nine user entity for authentication and authorization.
/// Extends IdentityUser with Nine-specific properties.
/// </summary>
public class ApplicationUser : IdentityUser
{
    /// <summary>
    /// The currently active organization ID for this user session.
    /// </summary>
    public Guid ActiveOrganizationId { get; set; } = Guid.Empty;

    /// <summary>
    /// The primary organization ID this user belongs to.
    /// DEPRECATED in multi-org scenarios - use ActiveOrganizationId instead.
    /// </summary>
    public Guid OrganizationId { get; set; } = Guid.Empty;

    /// <summary>
    /// User's first name.
    /// </summary>
    public string FirstName { get; set; } = string.Empty;

    /// <summary>
    /// User's last name.
    /// </summary>
    public string LastName { get; set; } = string.Empty;

    /// <summary>
    /// The timestamp of the user's most recent login.
    /// </summary>
    public DateTime? LastLoginDate { get; set; }

    /// <summary>
    /// The timestamp of the user's previous login (before LastLoginDate).
    /// </summary>
    public DateTime? PreviousLoginDate { get; set; }

    /// <summary>
    /// Total number of times the user has logged in.
    /// </summary>
    public int LoginCount { get; set; } = 0;

    /// <summary>
    /// The IP address from the user's last login.
    /// </summary>
    public string? LastLoginIP { get; set; }
}
