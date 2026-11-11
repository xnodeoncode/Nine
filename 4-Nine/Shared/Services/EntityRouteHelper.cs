using Nine.Core.Entities;

namespace Nine.Shared.Services;

/// <summary>
/// Provides centralized mapping between entity types and their navigation routes.
/// Follows RESTful routing conventions: /resource/{id} for details, /resource/{id}/action for specific actions.
/// This ensures consistent URL generation across the application when navigating to entity details.
/// </summary>
public static class EntityRouteHelper
{
    /// <summary>
    /// RESTful route mapping: entity type to base resource path.
    /// Detail view: {basePath}/{id}
    /// Actions: {basePath}/{id}/{action}
    /// </summary>
    private static readonly Dictionary<string, string> RouteMap = new()
    {
        { "Lease", "/propertymanagement/leases" },
        { "Payment", "/propertymanagement/payments" },
        { "Invoice", "/propertymanagement/invoices" },
        { "Maintenance", "/propertymanagement/maintenance" },
        { "Property", "/propertymanagement/properties" },
        { "Tenant", "/propertymanagement/tenants" }
    };

    /// <summary>
    /// Gets the detail view route for a given entity (RESTful: /resource/{id}).
    /// </summary>
    /// <param name="entityType">The type of entity (e.g., "Lease", "Payment", "Maintenance")</param>
    /// <param name="entityId">The unique identifier of the entity</param>
    /// <returns>The full route path including the entity ID, or "/" if the entity type is not mapped</returns>
    public static string GetEntityRoute(string? entityType, Guid entityId)
    {
        if (string.IsNullOrWhiteSpace(entityType))
        {
            return "/";
        }

        if (RouteMap.TryGetValue(entityType, out var route))
        {
            return $"{route}/{entityId}";
        }
        
        // Fallback to home if entity type not found
        return "/"; 
    }

    /// <summary>
    /// Gets the route for a specific action on an entity (RESTful: /resource/{id}/{action}).
    /// </summary>
    /// <param name="entityType">The type of entity</param>
    /// <param name="entityId">The unique identifier of the entity</param>
    /// <param name="action">The action to perform (e.g., "edit", "accept", "approve", "submit-application")</param>
    /// <returns>The full route path including the entity ID and action</returns>
    public static string GetEntityActionRoute(string? entityType, Guid entityId, string action)
    {
        if (string.IsNullOrWhiteSpace(entityType) || string.IsNullOrWhiteSpace(action))
        {
            return "/";
        }

        if (RouteMap.TryGetValue(entityType, out var route))
        {
            return $"{route}/{entityId}/{action}";
        }
        
        return "/"; 
    }

    /// <summary>
    /// Gets the list view route for a given entity type (RESTful: /resource).
    /// </summary>
    /// <param name="entityType">The type of entity</param>
    /// <returns>The list view route path</returns>
    public static string GetListRoute(string? entityType)
    {
        if (string.IsNullOrWhiteSpace(entityType))
        {
            return "/";
        }

        if (RouteMap.TryGetValue(entityType, out var route))
        {
            return route;
        }
        
        return "/"; 
    }

    /// <summary>
    /// Gets the create route for a given entity type (RESTful: /resource/create).
    /// </summary>
    /// <param name="entityType">The type of entity</param>
    /// <returns>The create route path</returns>
    public static string GetCreateRoute(string? entityType)
    {
        if (string.IsNullOrWhiteSpace(entityType))
        {
            return "/";
        }

        if (RouteMap.TryGetValue(entityType, out var route))
        {
            return $"{route}/create";
        }
        
        return "/"; 
    }

    /// <summary>
    /// Checks if a route mapping exists for the given entity type.
    /// </summary>
    /// <param name="entityType">The type of entity to check</param>
    /// <returns>True if a route mapping exists, false otherwise</returns>
    public static bool HasRoute(string? entityType)
    {
        return !string.IsNullOrWhiteSpace(entityType) && RouteMap.ContainsKey(entityType);
    }

    /// <summary>
    /// Gets all supported entity types that have route mappings.
    /// </summary>
    /// <returns>A collection of supported entity type names</returns>
    public static IEnumerable<string> GetSupportedEntityTypes()
    {
        return RouteMap.Keys;
    }
}
