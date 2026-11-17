namespace Nine.Application.Models;

/// <summary>
/// Describes related records that will be affected by an archive or delete operation.
/// </summary>
public class CascadeSummary
{
    public string EntityName { get; init; } = string.Empty;

    /// <summary>Key = human-readable label, Value = record count.</summary>
    public Dictionary<string, int> Counts { get; init; } = new();

    public bool HasRelatedRecords => Counts.Values.Any(c => c > 0);

    public string Summary =>
        HasRelatedRecords
            ? string.Join(", ", Counts.Where(c => c.Value > 0).Select(c => $"{c.Value} {c.Key}"))
            : "No related records.";
}
