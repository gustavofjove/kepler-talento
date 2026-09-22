using KeplerTalento.Domain.Catalogs;

namespace KeplerTalento.Domain.Positions;

public static class PositionStatuses
{
    public const string Open = "open";
    public const string Closed = "closed";
    public static IReadOnlyList<string> All { get; } = [Open, Closed];
    public static bool IsKnown(string value) => All.Contains(value, StringComparer.Ordinal);
}

public static class PositionText
{
    public const int MaximumTitleLength = 200;
    public const int MaximumLocationLength = 200;
    public const int MaximumDescriptionLength = 20_000;
    public static string Normalize(string? value) => CatalogName.Normalize(value);
}

public sealed class Position
{
    private Position() { }

    public Position(
        Guid id,
        string title,
        string description,
        string location,
        string requirements,
        int filterSchemaVersion,
        DateTimeOffset createdAtUtc)
    {
        Id = id;
        Status = PositionStatuses.Open;
        CreatedAtUtc = createdAtUtc;
        Update(title, description, location, Status, requirements, filterSchemaVersion, createdAtUtc);
    }

    public Guid Id { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string NormalizedTitle { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public string Location { get; private set; } = string.Empty;
    public string NormalizedLocation { get; private set; } = string.Empty;
    public string Status { get; private set; } = PositionStatuses.Open;
    public string Requirements { get; private set; } = "{}";
    public int FilterSchemaVersion { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public uint Version { get; private set; }

    public void Update(
        string title,
        string description,
        string location,
        string status,
        string requirements,
        int filterSchemaVersion,
        DateTimeOffset updatedAtUtc)
    {
        var trimmedTitle = title.Trim();
        var trimmedLocation = location.Trim();
        if (trimmedTitle.Length is 0 or > PositionText.MaximumTitleLength)
            throw new ArgumentOutOfRangeException(nameof(title));
        if (trimmedLocation.Length > PositionText.MaximumLocationLength)
            throw new ArgumentOutOfRangeException(nameof(location));
        if (description.Length > PositionText.MaximumDescriptionLength)
            throw new ArgumentOutOfRangeException(nameof(description));
        if (!PositionStatuses.IsKnown(status))
            throw new ArgumentOutOfRangeException(nameof(status));
        if (filterSchemaVersion < 1)
            throw new ArgumentOutOfRangeException(nameof(filterSchemaVersion));

        Title = trimmedTitle;
        NormalizedTitle = PositionText.Normalize(trimmedTitle);
        Description = description;
        Location = trimmedLocation;
        NormalizedLocation = PositionText.Normalize(trimmedLocation);
        Status = status;
        Requirements = requirements;
        FilterSchemaVersion = filterSchemaVersion;
        UpdatedAtUtc = updatedAtUtc;
    }
}
